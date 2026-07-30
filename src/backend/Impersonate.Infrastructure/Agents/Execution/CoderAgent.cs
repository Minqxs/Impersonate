using System.Text.Json;
using Impersonate.Application.Ai;
using Impersonate.Application.Execution;
using Impersonate.Domain.Pipelines;
using Microsoft.Extensions.Options;

namespace Impersonate.Infrastructure.Agents.Execution;

internal sealed class CoderAgent(IEnumerable<IAiProviderAdapter> adapters, IProviderCredentialStore credentials, IRepositoryTools tools, IOptions<ExecutionOptions> options) : ICoderAgent
{
    public async Task<CoderResult> ExecuteAsync(CoderContext context, CancellationToken ct)
    {
        if (context.Model.ConnectionId is not { } connectionId)
            return Failure("coder_provider_failed", "The selected Coder model has no provider connection.");
        var adapter = adapters.SingleOrDefault(x => x.ProviderType == context.Model.ProviderType);
        if (adapter is null)
            return Failure("coder_provider_failed", "The selected Coder provider is unavailable.");
        var credential = await credentials.RetrieveAsync(connectionId, ct);
        if (credential.Status != ProviderCredentialReadStatus.Found)
            return Failure("coder_provider_failed", credential.SafeFailureMessage ?? "Coder credentials are unavailable.");
        var evidence = await PreloadEvidence(context.Workspace, context.RepositoryEvidence ?? [], ct);
        if (!evidence.Succeeded)
            return Failure(evidence.FailureCode!, evidence.FailureMessage!);

        var definitions = NativeTools();
        var initialInput = JsonSerializer.Serialize(new
        {
            context.FeatureRequest,
            task = new
            {
                context.TaskTitle,
                context.TaskDescription,
                context.AcceptanceCriteria,
                repositoryEvidence = evidence.Excerpts
            },
            context.AttemptNumber,
            context.RevisionNumber,
            context.ReviewerFeedback,
            context.EarlierApprovedSummaries,
            context.PriorProtocolSummary,
            instruction = "Use repository functions to inspect, implement, validate, then call complete_task. Call report_blocker only for a precise safe blocker."
        });
        var contextWindow = context.Model.ContextWindowSize ?? options.Value.DefaultModelContextWindowTokens;
        var modelMaximumOutput = Math.Min(Math.Max(1, contextWindow / 2), Math.Max(1, context.Model.MaximumOutputSize ?? options.Value.DefaultCoderMaximumOutputTokens));
        if (EstimateTokens(initialInput) >= Math.Max(1, contextWindow - modelMaximumOutput))
            return Failure("provider_context_limit_exceeded", "The Coder request cannot fit the selected model's advertised context window.");
        AgentConversationReference? conversation = null;
        IReadOnlyList<AgentToolResult> pendingResults = [];
        var completedCalls = new Dictionary<string, AgentToolResult>(StringComparer.Ordinal);
        var toolCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var toolSteps = 0;
        var rounds = 0;
        var paidRequests = 0;
        var totalInput = 0;
        var totalOutput = 0;
        var successfulReads = evidence.Excerpts.Count;
        var successfulSearches = 0;
        var patchAttempts = 0;
        var successfulPatches = 0;
        var successfulValidations = 0;
        var failedPatches = 0;
        var repositoryInspected = successfulReads > 0;
        var currentDiffExists = false;
        var prematureCompletions = 0;
        string? lastPatchFailureCode = null;
        string? requestId = null;
        string? providerStatus = null;
        string? incompleteReason = null;
        var phase = "Discovery";
        var reservationReasons = new List<string>();
        var maximumReservation = 0;
        int? previousReservation = null;
        var priorOutputTruncated = false;
        var providerResetUsed = false;
        RateLimitScope? lastRateLimitScope = null;
        long capacityWait = 0;
        long? providerRemainingTokens = null;
        int? previousOutputTokens = null;
        var endpoint = Enum.TryParse<ProviderEndpoint>(context.Model.Endpoint, out var parsedEndpoint) ? parsedEndpoint : ProviderEndpoint.Responses;
        var estimatedDiff = context.ExpectedDiffTokens ?? Math.Clamp(800 + context.AcceptanceCriteria.Count * 500 + context.TaskDescription.Length / 3, 1_000, 12_000);

        while (toolSteps < options.Value.MaximumCoderToolExecutions && rounds < options.Value.MaximumCoderProviderRounds)
        {
            var pendingPayloadTokens = pendingResults.Sum(x => EstimateTokens(x.Output));
            var reservation = AdaptiveOutputReservationPolicy.Reserve(new(endpoint, modelMaximumOutput, estimatedDiff, phase, currentDiffExists, pendingPayloadTokens, previousOutputTokens, previousReservation, priorOutputTruncated, providerResetUsed, lastRateLimitScope, providerRemainingTokens));
            previousReservation = reservation.Tokens;
            maximumReservation = Math.Max(maximumReservation, reservation.Tokens);
            reservationReasons.Add($"Round {rounds + 1}: {reservation.Tokens} tokens — {reservation.Reason}.");
            AgentTurnResponse turn;
            try
            {
                turn = await adapter.CompleteAgentTurnAsync(
                    new(connectionId, context.Model.ProviderType, credential.Credential!),
                    new(context.Model.DiscoveredModelId, context.Model.ProviderModelId),
                    new(context.Model.ProviderModelId, Load("coder-v1"), conversation is null ? initialInput : null, definitions, pendingResults, conversation, reservation.Tokens, Reasoning(context), "low"), ct);
            }
            catch (ProviderRequestException ex)
            {
                return WithTelemetry(Failure(ex.Code, ex.Message, toolSteps, requestId, totalInput, totalOutput, null, successfulReads, successfulSearches, successfulPatches, repositoryInspected, currentDiffExists, prematureCompletions, rounds, 0, EstimateTokens(initialInput), providerStatus, incompleteReason, 0, 0, paidRequests, phase, null, patchAttempts, failedPatches, lastPatchFailureCode), ex.Capacity);
            }
            catch (NotSupportedException)
            {
                return Failure("coder_native_tools_unsupported", "The selected Coder model does not support provider-native repository tools.");
            }
            catch (HttpRequestException)
            {
                return Failure("provider_unavailable", "The Coder provider could not be reached.");
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                return Failure("provider_timeout", "The Coder provider timed out.");
            }

            rounds++;
            paidRequests += turn.SameModelRequestAttemptCount;
            totalInput += turn.InputTokenCount ?? 0;
            totalOutput += turn.OutputTokenCount ?? 0;
            previousOutputTokens = turn.OutputTokenCount;
            requestId = turn.ProviderRequestId ?? requestId;
            providerStatus = turn.ResponseStatus;
            incompleteReason = turn.IncompleteReason;
            capacityWait += turn.CumulativeRateLimitWaitMilliseconds;
            providerResetUsed |= turn.ProviderResetUsed;
            lastRateLimitScope = turn.LastRateLimitScope ?? lastRateLimitScope;
            providerRemainingTokens = turn.ProviderRemainingTokens ?? providerRemainingTokens;
            conversation = turn.Conversation;
            if (turn.SafeFailureCode == "provider_output_truncated" && reservation.Tokens < modelMaximumOutput)
            {
                priorOutputTruncated = true;
                pendingResults = [];
                continue;
            }
            if (turn.SafeFailureCode is { } safeCode)
                return WithTelemetry(Failure(safeCode, SafeAgentProviderMessage(safeCode, turn), toolSteps, requestId, totalInput, totalOutput, null, successfulReads, successfulSearches, successfulPatches, repositoryInspected, currentDiffExists, prematureCompletions, rounds, 0, EstimateTokens(initialInput), providerStatus, incompleteReason, 0, 0, paidRequests, phase, null, patchAttempts, failedPatches, lastPatchFailureCode));

            priorOutputTruncated = false;
            var results = new List<AgentToolResult>();
            var remainingToolSteps = options.Value.MaximumCoderToolExecutions - toolSteps;
            var unseenRepositoryCalls = turn.ToolCalls
                .Where(call => call.Name is not ("complete_task" or "report_blocker") && !completedCalls.ContainsKey(call.CallId))
                .Select(call => call.CallId)
                .Distinct(StringComparer.Ordinal)
                .Count();
            if (unseenRepositoryCalls > remainingToolSteps)
                return WithTelemetry(Failure("coder_emergency_circuit_breaker_triggered", $"The Coder returned {unseenRepositoryCalls} new repository tool calls with only {remainingToolSteps} executions remaining. No calls from the turn were executed.", toolSteps, requestId, totalInput, totalOutput, null, successfulReads, successfulSearches, successfulPatches, repositoryInspected, currentDiffExists, prematureCompletions, rounds, 0, EstimateTokens(initialInput), providerStatus, incompleteReason, 0, 0, paidRequests, phase, null, patchAttempts, failedPatches, lastPatchFailureCode));

            foreach (var call in turn.ToolCalls)
            {
                if (completedCalls.TryGetValue(call.CallId, out var cached))
                {
                    results.Add(cached);
                    continue;
                }

                AgentToolResult nativeResult;
                JsonElement arguments;
                try
                {
                    using var parsed = JsonDocument.Parse(call.ArgumentsJson);
                    arguments = parsed.RootElement.Clone();
                    if (arguments.ValueKind != JsonValueKind.Object)
                        throw new JsonException("Arguments must be a JSON object.");
                }
                catch (JsonException ex)
                {
                    nativeResult = ToolOutput(call.CallId, new(false, string.Empty, "tool_arguments_invalid", Limit(ex.Message, 300)));
                    completedCalls[call.CallId] = nativeResult;
                    results.Add(nativeResult);
                    continue;
                }

                if (call.Name is "complete_task" or "report_blocker")
                {
                    nativeResult = await Terminal(call, arguments, context, repositoryInspected, successfulPatches, successfulValidations, currentDiffExists, ct);
                    var accepted = Accepted(nativeResult);
                    if (call.Name == "complete_task" && accepted)
                    {
                        var changed = await tools.RunCommandAsync(context.Workspace, new("git", ["diff", "--name-only", "--"]), ct);
                        var files = changed.Succeeded ? changed.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) : [];
                        return new(true, RequiredString(arguments, "summary"), files, StringArray(arguments, "validationNotes"), toolSteps, requestId, totalInput, totalOutput, ResponseType: "complete_task", SuccessfulReadCount: successfulReads, SuccessfulSearchCount: successfulSearches, SuccessfulPatchCount: successfulPatches, RepositoryInspected: repositoryInspected, CurrentDiffExists: true, PrematureCompletionCount: prematureCompletions, ProviderRoundTripCount: rounds, MaximumSingleRequestInput: EstimateTokens(initialInput), ProviderResponseStatus: providerStatus, ProviderIncompleteReason: incompleteReason, PaidProviderRequestCount: paidRequests, CurrentPhase: "Completion", PatchAttemptCount: patchAttempts, FailedPatchCount: failedPatches, LastPatchFailureCode: lastPatchFailureCode, MaximumRequestedOutputReservation: maximumReservation, OutputReservationReasons: reservationReasons, ProviderCapacityWaitMilliseconds: capacityWait, ProviderResetUsed: providerResetUsed, LastRateLimitScope: lastRateLimitScope?.ToString());
                    }
                    if (call.Name == "complete_task")
                    {
                        prematureCompletions++;
                        if (prematureCompletions >= 2)
                            return WithTelemetry(Failure("coder_protocol_failed", "The selected Coder model completed before satisfying the repository inspection and patch protocol.", toolSteps, requestId, totalInput, totalOutput, "complete_task", successfulReads, successfulSearches, successfulPatches, repositoryInspected, currentDiffExists, prematureCompletions, rounds, 0, EstimateTokens(initialInput), providerStatus, incompleteReason, 0, 0, paidRequests, phase, null, patchAttempts, failedPatches, lastPatchFailureCode));
                    }
                    if (call.Name == "report_blocker" && accepted)
                        return WithTelemetry(BlockedNative(arguments, toolSteps, requestId, totalInput, totalOutput, successfulReads, successfulSearches, successfulPatches, repositoryInspected, currentDiffExists, rounds, paidRequests, patchAttempts, failedPatches, lastPatchFailureCode));
                }
                else
                {
                    var repositoryResult = await Execute(new(call.CallId, call.Name, arguments), context.Workspace, ct);
                    toolSteps++;
                    toolCounts[call.Name] = toolCounts.GetValueOrDefault(call.Name) + 1;
                    if (repositoryResult.Succeeded && call.Name == "read_file")
                    {
                        successfulReads++;
                        repositoryInspected = true;
                    }
                    if (repositoryResult.Succeeded && call.Name == "search_text")
                    {
                        successfulSearches++;
                        repositoryInspected = true;
                    }
                    if (repositoryInspected && successfulPatches == 0)
                        phase = "Implementation";
                    if (call.Name == "apply_patch")
                    {
                        patchAttempts++;
                        if (repositoryResult.Succeeded)
                        {
                            successfulPatches++;
                            successfulValidations = 0;
                            lastPatchFailureCode = null;
                            phase = "Validation";
                        }
                        else
                        {
                            failedPatches++;
                            lastPatchFailureCode = SafePatchFailureCode(repositoryResult.FailureCode);
                        }
                    }
                    if (call.Name == "get_diff")
                        currentDiffExists = repositoryResult.Succeeded && !string.IsNullOrWhiteSpace(repositoryResult.Output);
                    if (call.Name == "run_command" && successfulPatches > 0 && repositoryResult.Succeeded)
                    {
                        successfulValidations++;
                        phase = "Completion";
                    }
                    nativeResult = ToolOutput(call.CallId, repositoryResult);
                }

                completedCalls[call.CallId] = nativeResult;
                results.Add(nativeResult);
            }
            pendingResults = results;
        }

        return WithTelemetry(Failure("coder_emergency_circuit_breaker_triggered", $"The Coder reached the emergency circuit breaker after {rounds} provider rounds and {toolSteps} repository tool executions. {ToolSummary(toolCounts)}", toolSteps, requestId, totalInput, totalOutput, null, successfulReads, successfulSearches, successfulPatches, repositoryInspected, currentDiffExists, prematureCompletions, rounds, 0, EstimateTokens(initialInput), providerStatus, incompleteReason, 0, 0, paidRequests, phase, null, patchAttempts, failedPatches, lastPatchFailureCode));

        CoderResult WithTelemetry(CoderResult result, ProviderCapacityMetadata? terminalCapacity = null)
        {
            if (terminalCapacity is not null)
            {
                capacityWait += terminalCapacity.CumulativeWaitMilliseconds;
                providerResetUsed |= terminalCapacity.RetryAfter is not null || terminalCapacity.TokenReset is not null || terminalCapacity.RequestReset is not null;
                lastRateLimitScope = terminalCapacity.Scope;
                providerRemainingTokens = terminalCapacity.RemainingTokens;
                reservationReasons.Add($"Terminal provider capacity: scope {terminalCapacity.Scope}, remaining tokens {terminalCapacity.RemainingTokens?.ToString() ?? "unknown"}, reset metadata {(providerResetUsed ? "present" : "absent")}.");
            }
            return result with { MaximumRequestedOutputReservation = maximumReservation, OutputReservationReasons = reservationReasons, ProviderCapacityWaitMilliseconds = capacityWait, ProviderResetUsed = providerResetUsed, LastRateLimitScope = lastRateLimitScope?.ToString() };
        }
    }

    internal static IReadOnlyList<AgentToolDefinition> NativeTools() =>
    [
        Definition("list_files", "List safe repository-relative files under a directory.", """{"type":"object","additionalProperties":false,"required":["path"],"properties":{"path":{"type":"string"}}}"""),
        Definition("read_file", "Read a safe repository-relative text file.", """{"type":"object","additionalProperties":false,"required":["path"],"properties":{"path":{"type":"string"}}}"""),
        Definition("search_text", "Search repository text under a safe relative path.", """{"type":"object","additionalProperties":false,"required":["query","path"],"properties":{"query":{"type":"string"},"path":{"type":"string"}}}"""),
        Definition("apply_patch", "Apply one standard Git unified diff to the task workspace. The patch must begin with 'diff --git' and include ---/+++ file headers and @@ hunks. Do not use '*** Begin Patch' markers.", """{"type":"object","additionalProperties":false,"required":["patch"],"properties":{"patch":{"type":"string"}}}"""),
        Definition("get_diff", "Get the current incremental Git diff.", """{"type":"object","additionalProperties":false,"required":[],"properties":{}}"""),
        Definition("run_command", "Run an allow-listed focused validation command in the workspace.", """{"type":"object","additionalProperties":false,"required":["executable","arguments","workingDirectory","timeoutSeconds"],"properties":{"executable":{"type":"string"},"arguments":{"type":"array","items":{"type":"string"}},"workingDirectory":{"type":"string"},"timeoutSeconds":{"type":"integer","minimum":1,"maximum":600}}}"""),
        Definition("complete_task", "Finish only after inspection, a successful patch, a non-empty verified diff, and focused validation.", """{"type":"object","additionalProperties":false,"required":["summary","validationNotes","knownLimitations"],"properties":{"summary":{"type":"string"},"validationNotes":{"type":"array","items":{"type":"string"}},"knownLimitations":{"type":"array","items":{"type":"string"}}}}"""),
        Definition("report_blocker", "Report a precise repository blocker that prevents a safe implementation.", """{"type":"object","additionalProperties":false,"required":["blockerCode","blockerMessage","missingEvidencePaths"],"properties":{"blockerCode":{"type":"string","enum":["missing_repository_evidence","repository_contract_mismatch","safe_implementation_blocked"]},"blockerMessage":{"type":"string"},"missingEvidencePaths":{"type":"array","items":{"type":"string"}}}}""")
    ];

    private static AgentToolDefinition Definition(string name, string description, string schema)
    {
        using var document = JsonDocument.Parse(schema);
        return new(name, description, document.RootElement.Clone());
    }

    private async Task<AgentToolResult> Terminal(AgentToolCall call, JsonElement arguments, CoderContext context, bool inspected, int patches, int validations, bool knownDiff, CancellationToken ct)
    {
        try
        {
            ValidateArguments(call.Name, arguments);
            if (call.Name == "report_blocker")
            {
                var blockerCode = RequiredString(arguments, "blockerCode");
                if (blockerCode is not ("missing_repository_evidence" or "repository_contract_mismatch" or "safe_implementation_blocked"))
                    throw new ArgumentException("blockerCode is not supported.");
                _ = RequiredString(arguments, "blockerMessage");
                _ = StringArray(arguments, "missingEvidencePaths");
                return new(call.CallId, JsonSerializer.Serialize(new
                {
                    accepted = true
                }));
            }
            var diff = await tools.GetDiffAsync(context.Workspace, ct);
            var currentDiff = diff.Succeeded && !string.IsNullOrWhiteSpace(diff.Output);
            if (!inspected || patches == 0 || validations == 0 || !currentDiff)
                return new(call.CallId, JsonSerializer.Serialize(new
                {
                    accepted = false,
                    failureCode = "completion_rejected",
                    failureMessage = "Completion requires repository inspection, a successful patch, successful focused validation after patching, and a current non-empty Git diff.",
                    state = new
                    {
                        inspected,
                        successfulPatches = patches,
                        successfulValidations = validations,
                        currentDiff,
                        previouslyObservedDiff = knownDiff
                    }
                }));
            return new(call.CallId, JsonSerializer.Serialize(new
            {
                accepted = true
            }));
        }
        catch (Exception ex) when (ex is ArgumentException or JsonException)
        {
            return new(call.CallId, JsonSerializer.Serialize(new
            {
                accepted = false,
                failureCode = "tool_arguments_invalid",
                failureMessage = Limit(ex.Message, 300)
            }));
        }
    }

    private static AgentToolResult ToolOutput(string callId, RepositoryToolResult result) => new(callId, JsonSerializer.Serialize(new
    {
        result.Succeeded,
        Output = Limit(result.Output, 24_000),
        result.FailureCode,
        result.FailureMessage,
        result.Truncated
    }));

    private static bool Accepted(AgentToolResult result)
    {
        try
        {
            using var document = JsonDocument.Parse(result.Output);
            return document.RootElement.TryGetProperty("accepted", out var accepted) && accepted.ValueKind == JsonValueKind.True;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static CoderResult BlockedNative(JsonElement arguments, int steps, string? requestId, int input, int output, int reads, int searches, int patches, bool inspected, bool diff, int rounds, int paid, int patchAttempts, int failedPatches, string? lastPatchFailureCode)
    {
        var blocker = RequiredString(arguments, "blockerCode");
        var code = blocker switch
        {
            "missing_repository_evidence" when StringArray(arguments, "missingEvidencePaths").Count > 0 => "coder_missing_repository_evidence",
            "repository_contract_mismatch" => "coder_repository_contract_mismatch",
            "safe_implementation_blocked" => "coder_safe_implementation_blocked",
            _ => "coder_protocol_failed"
        };
        return Failure(code, Limit(RequiredString(arguments, "blockerMessage"), 500), steps, requestId, input, output, "report_blocker", reads, searches, patches, inspected, diff, 0, rounds, 0, 0, null, null, 0, 0, paid, "Blocked", null, patchAttempts, failedPatches, lastPatchFailureCode);
    }

    private async Task<RepositoryToolResult> Execute(ToolCall call, WorkspaceReference workspace, CancellationToken ct)
    {
        try
        {
            ValidateArguments(call.Tool, call.Arguments);
            return call.Tool switch
            {
                "list_files" => await tools.ListFilesAsync(workspace, Arg(call, "path", "."), ct),
                "read_file" => await tools.ReadFileAsync(workspace, Arg(call, "path"), ct),
                "search_text" => await tools.SearchTextAsync(workspace, Arg(call, "query"), Arg(call, "path", "."), ct),
                "apply_patch" => await tools.ApplyPatchAsync(workspace, Arg(call, "patch"), ct),
                "get_diff" => await tools.GetDiffAsync(workspace, ct),
                "run_command" => await tools.RunCommandAsync(workspace, new(Arg(call, "executable"), Args(call), Arg(call, "workingDirectory", "."), IntArg(call, "timeoutSeconds", 120)), ct),
                _ => new(false, string.Empty, "tool_rejected", "Unknown tool.")
            };
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return new(false, string.Empty, "tool_rejected", ex.Message);
        }
    }

    private static void ValidateArguments(string tool, JsonElement arguments)
    {
        var required = tool switch
        {
            "list_files" or "read_file" => new[] { "path" },
            "search_text" => ["query", "path"],
            "apply_patch" => ["patch"],
            "get_diff" => [],
            "run_command" => ["executable", "arguments", "workingDirectory", "timeoutSeconds"],
            "complete_task" => ["summary", "validationNotes", "knownLimitations"],
            "report_blocker" => ["blockerCode", "blockerMessage", "missingEvidencePaths"],
            _ => throw new ArgumentException("Unknown tool.")
        };
        var allowed = required.ToHashSet(StringComparer.Ordinal);
        if (arguments.EnumerateObject().Any(x => !allowed.Contains(x.Name)))
            throw new ArgumentException("Tool arguments contain an unsupported property.");
        if (required.Any(name => !arguments.TryGetProperty(name, out _)))
            throw new ArgumentException("Tool arguments are missing a required property.");
    }

    private static string RequiredString(JsonElement arguments, string name) => arguments.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString()!.Trim() : throw new ArgumentException($"{name} is required.");
    private static IReadOnlyList<string> StringArray(JsonElement arguments, string name) => arguments.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array && value.EnumerateArray().All(x => x.ValueKind == JsonValueKind.String) ? value.EnumerateArray().Select(x => x.GetString()!).ToList() : throw new ArgumentException($"{name} must be a string array.");

    private static string Arg(ToolCall call, string name, string? fallback = null) => call.Arguments.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : fallback ?? throw new ArgumentException($"{name} is required.");
    private static int IntArg(ToolCall call, string name, int fallback) => call.Arguments.TryGetProperty(name, out var value) && value.TryGetInt32(out var result) ? result : fallback;
    private static string[] Args(ToolCall call) => call.Arguments.TryGetProperty("arguments", out var value) && value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToArray() : [];
    private static string Limit(string value, int maximum) => value.Length <= maximum ? value : value[..maximum] + "\n[tool output truncated by the repository-tool safety limit]";
    private static int EstimateTokens(string value) => (value.Length + 2) / 3;
    private static string Reasoning(CoderContext context) => context.AcceptanceCriteria.Count <= 3 && context.RepositoryEvidence?.Count <= 2 ? "low" : "medium";
    private static string? SafePatchFailureCode(string? code) => string.IsNullOrWhiteSpace(code) ? "patch_failed" : Limit(code, 100);
    private static string SafeAgentProviderMessage(string code, AgentTurnResponse response) => code switch
    {
        "provider_output_truncated" => $"The provider output was truncated ({response.IncompleteReason ?? "unknown reason"}).",
        "provider_refused" => "The provider refused the Coder request.",
        "provider_response_failed" => "The provider failed while producing the Coder response.",
        "provider_missing_tool_call" => "The provider completed without a native Coder tool call.",
        _ => "The provider response could not be used."
    };
    private static CoderResult Failure(string code, string message) => new(false, string.Empty, [], [], 0, null, null, null, code, message);
    private static string Required(string? value, string message) => string.IsNullOrWhiteSpace(value) ? throw new InvalidDataException(message) : value.Trim();
    private static string Load(string version) => PromptLoader.Load(version);
    private async Task<EvidencePreloadResult> PreloadEvidence(WorkspaceReference workspace, IReadOnlyList<string> paths, CancellationToken ct)
    {
        const int perFile = 8_000, totalLimit = 32_000;
        var excerpts = new List<RepositoryEvidenceExcerpt>();
        var total = 0;
        foreach (var raw in paths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var path = raw.Trim().Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(path) || IsAbsoluteEvidencePath(path) || path.Split('/').Contains("..") || IsSensitiveEvidence(path))
                return new(false, [], "coder_evidence_rejected", $"Repository evidence path '{path}' is not permitted.");
            RepositoryToolResult read;
            try
            {
                read = await tools.ReadFileAsync(workspace, path, ct);
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
            {
                return new(false, [], "coder_evidence_rejected", $"Repository evidence path '{path}' could not be safely read: {ex.Message}");
            }

            if (!read.Succeeded)
                return new(false, [], "coder_evidence_rejected", $"Repository evidence path '{path}' could not be safely read: {read.FailureMessage}");
            var remaining = totalLimit - total;
            if (remaining <= 0)
                break;
            var take = Math.Min(Math.Min(perFile, remaining), read.Output.Length);
            var content = read.Output[..take];
            excerpts.Add(new(path, content, read.Truncated || take < read.Output.Length));
            total += content.Length;
        }

        return new(true, excerpts, null, null);
    }

    private static bool IsAbsoluteEvidencePath(string path) => Path.IsPathRooted(path) || path.StartsWith('/') || path.StartsWith("//", StringComparison.Ordinal) || (path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' && path[2] == '/');
    private static bool IsSensitiveEvidence(string path)
    {
        var name = Path.GetFileName(path);
        return path.StartsWith(".git/", StringComparison.OrdinalIgnoreCase) || name.Equals(".env", StringComparison.OrdinalIgnoreCase) || name.Contains("secret", StringComparison.OrdinalIgnoreCase) || name.Contains("credential", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".pfx", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".pem", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".key", StringComparison.OrdinalIgnoreCase);
    }

    private static string ToolSummary(IReadOnlyDictionary<string, int> counts) => counts.Count == 0 ? "No repository tools were used." : "Tool usage: " + string.Join(", ", counts.OrderBy(x => x.Key).Select(x => $"{x.Key}={x.Value}")) + ".";
    private static CoderResult Failure(string code, string message, int toolSteps, string? requestId, int? input, int? output, string? responseType = null, int reads = 0, int searches = 0, int patches = 0, bool inspected = false, bool diff = false, int premature = 0, int providerRounds = 0, int readOnlyRounds = 0, int maximumSingleRequestInput = 0, string? providerStatus = null, string? incompleteReason = null, int repairs = 0, int corrections = 0, int paidRequests = 0, string phase = "Discovery", string? prohibitedTool = null, int patchAttempts = 0, int failedPatches = 0, string? lastPatchFailureCode = null) => new(false, string.Empty, [], [], toolSteps, requestId, input, output, code, message, responseType, reads, searches, patches, inspected, diff, premature, providerRounds, readOnlyRounds, maximumSingleRequestInput, providerStatus, incompleteReason, repairs, corrections, paidRequests, phase, prohibitedTool, patchAttempts, failedPatches, lastPatchFailureCode);
    private sealed record EvidencePreloadResult(bool Succeeded, IReadOnlyList<RepositoryEvidenceExcerpt> Excerpts, string? FailureCode, string? FailureMessage);
    private sealed record ToolCall(string Id, string Tool, JsonElement Arguments);
}
