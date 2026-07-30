using System.Reflection;
using System.Text.Json;
using Impersonate.Application.Ai;
using Impersonate.Application.Execution;
using Impersonate.Domain.Pipelines;
using Microsoft.Extensions.Options;

namespace Impersonate.Infrastructure.Agents.Execution;

internal sealed class CoderAgent(IEnumerable<IAiProviderAdapter> adapters, IProviderCredentialStore credentials, IRepositoryTools tools, IOptions<ExecutionOptions> options) : ICoderAgent
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true
    };
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
        var prompt = Load("coder-v1");
        var phase = "Discovery";
        var transcript = new List<object>();
        var toolCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var totalInput = 0;
        var totalOutput = 0;
        var toolSteps = 0;
        var providerRounds = 0;
        var paidRequests = 0;
        var consecutiveReadOnlyRounds = 0;
        var noProgressCorrections = 0;
        var repairs = 0;
        var maximumSingleRequestInput = 0;
        var prematureCompletions = 0;
        var successfulReads = evidence.Excerpts.Count;
        var successfulSearches = 0;
        var patchAttempts = 0;
        var successfulPatches = 0;
        var failedPatches = 0;
        string? lastPatchFailureCode = null;
        var repositoryInspected = successfulReads > 0;
        var currentDiffExists = false;
        string? requestId = null;
        string? responseType = null;
        string? providerStatus = null;
        string? incompleteReason = null;
        var task = new
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
            tools = new[]
            {
                "list_files",
                "read_file",
                "search_text",
                "apply_patch",
                "get_diff",
                "run_command"
            },
            protocol = new
            {
                completionRequirements = new
                {
                    repositoryInspected = true,
                    successfulPatchCount = "at least 1",
                    currentDiffExists = true
                },
                toolCalls = new
                {
                    type = "tool_calls",
                    calls = new[]
                    {
                        new
                        {
                            id = "call-1",
                            tool = "read_file",
                            arguments = new
                            {
                                path = "README.md",
                                query = (string? )null,
                                patch = (string? )null,
                                executable = (string? )null,
                                arguments = (string[]? )null,
                                workingDirectory = (string? )null,
                                timeoutSeconds = (int? )null
                            }
                        }
                    },
                    summary = (string?)null,
                    validationNotes = (string[]?)null,
                    knownLimitations = (string[]?)null
                },
                complete = new
                {
                    type = "complete",
                    calls = (object?)null,
                    summary = "Implemented the task.",
                    validationNotes = Array.Empty<string>(),
                    knownLimitations = Array.Empty<string>()
                }
            }
        };
        transcript.Add(new
        {
            role = "user",
            content = JsonSerializer.Serialize(task)
        });
        while (toolSteps < options.Value.MaximumCoderToolExecutions && providerRounds < options.Value.MaximumCoderProviderRounds)
        {
            ct.ThrowIfCancellationRequested();
            var contextWindow = context.Model.ContextWindowSize ?? options.Value.DefaultModelContextWindowTokens;
            var maximumOutput = Math.Min(Math.Max(1, contextWindow / 2), Math.Max(1, context.Model.MaximumOutputSize ?? options.Value.DefaultCoderMaximumOutputTokens));
            var availableInputTokens = Math.Max(1, contextWindow - maximumOutput);
            var input = SerializeTranscript(transcript, availableInputTokens);
            if (input is null)
                return Failure("provider_context_limit_exceeded", "The complete Coder transcript cannot fit the selected model's advertised context window.", toolSteps, requestId, totalInput, totalOutput, responseType, successfulReads, successfulSearches, successfulPatches, repositoryInspected, currentDiffExists, prematureCompletions, providerRounds, consecutiveReadOnlyRounds, maximumSingleRequestInput, providerStatus, incompleteReason, repairs, 0, paidRequests, phase);
            var estimatedInput = EstimateTokens(input);
            maximumSingleRequestInput = Math.Max(maximumSingleRequestInput, estimatedInput);
            LanguageModelResponse response;
            try
            {
                response = await adapter.CompleteAsync(new(connectionId, context.Model.ProviderType, credential.Credential!), new(context.Model.DiscoveredModelId, context.Model.ProviderModelId), new(context.Model.ProviderModelId, prompt, input, StructuredOutputSchema, maximumOutput, Reasoning(context), "low"), ct);
            }
            catch (ProviderRequestException ex)
            {
                return Failure(ex.Code, ex.Message);
            }
            catch (HttpRequestException)
            {
                return Failure("provider_unavailable", "The Coder provider could not be reached.");
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                return Failure("provider_timeout", "The Coder provider timed out.");
            }

            providerRounds++;
            paidRequests += response.SameModelRequestAttemptCount;
            requestId = response.ProviderRequestId ?? requestId;
            providerStatus = response.ResponseStatus;
            incompleteReason = response.IncompleteReason;
            totalInput += response.InputTokenCount ?? 0;
            totalOutput += response.OutputTokenCount ?? 0;
            if (response.SafeFailureCode is { } safeCode)
                return Failure(safeCode, SafeProviderMessage(safeCode, response), toolSteps, requestId, totalInput, totalOutput, responseType, successfulReads, successfulSearches, successfulPatches, repositoryInspected, currentDiffExists, prematureCompletions, providerRounds, consecutiveReadOnlyRounds, maximumSingleRequestInput, providerStatus, incompleteReason, repairs, noProgressCorrections, paidRequests);
            CoderEnvelope envelope;
            try
            {
                envelope = JsonSerializer.Deserialize<CoderEnvelope>(response.Content, Json) ?? throw new JsonException();
            }
            catch (JsonException ex)
            {
                if (repairs >= options.Value.MaximumStructuredOutputRepairAttempts || string.IsNullOrWhiteSpace(response.Content))
                    return Failure("coder_invalid_structured_output", "The Coder returned invalid structured output.", toolSteps, requestId, totalInput, totalOutput, responseType, successfulReads, successfulSearches, successfulPatches, repositoryInspected, currentDiffExists, prematureCompletions, providerRounds, consecutiveReadOnlyRounds, maximumSingleRequestInput, providerStatus, incompleteReason, repairs, noProgressCorrections, paidRequests);
                repairs++;
                var repairInput = JsonSerializer.Serialize(new
                {
                    type = "structured_output_repair",
                    schema = StructuredOutputSchema,
                    malformedOutput = Limit(response.Content, 6000),
                    validationError = Limit(ex.Message, 300)
                });
                maximumSingleRequestInput = Math.Max(maximumSingleRequestInput, EstimateTokens(repairInput));
                try
                {
                    response = await adapter.CompleteAsync(new(connectionId, context.Model.ProviderType, credential.Credential!), new(context.Model.DiscoveredModelId, context.Model.ProviderModelId), new(context.Model.ProviderModelId, "Repair the supplied JSON to match the schema exactly.", repairInput, StructuredOutputSchema, 1600, Reasoning(context), "low"), ct);
                }
                catch (ProviderRequestException repairFailure)
                {
                    return Failure(repairFailure.Code, repairFailure.Message, toolSteps, requestId, totalInput, totalOutput, responseType, successfulReads, successfulSearches, successfulPatches, repositoryInspected, currentDiffExists, prematureCompletions, providerRounds, consecutiveReadOnlyRounds, maximumSingleRequestInput, providerStatus, incompleteReason, repairs, noProgressCorrections, paidRequests);
                }

                providerRounds++;
                paidRequests += response.SameModelRequestAttemptCount;
                totalInput += response.InputTokenCount ?? 0;
                totalOutput += response.OutputTokenCount ?? 0;
                requestId = response.ProviderRequestId ?? requestId;
                providerStatus = response.ResponseStatus;
                incompleteReason = response.IncompleteReason;
                try
                {
                    envelope = JsonSerializer.Deserialize<CoderEnvelope>(response.Content, Json) ?? throw new JsonException();
                }
                catch (JsonException)
                {
                    return Failure("coder_invalid_structured_output", "The bounded Coder structured-output repair failed.", toolSteps, requestId, totalInput, totalOutput, responseType, successfulReads, successfulSearches, successfulPatches, repositoryInspected, currentDiffExists, prematureCompletions, providerRounds, consecutiveReadOnlyRounds, maximumSingleRequestInput, providerStatus, incompleteReason, repairs, noProgressCorrections, paidRequests);
                }
            }

            responseType = envelope.Type;
            if (envelope.Type.Equals("blocked", StringComparison.OrdinalIgnoreCase))
                return Blocked(envelope, toolSteps, requestId, totalInput, totalOutput, responseType, successfulReads, successfulSearches, successfulPatches, repositoryInspected, currentDiffExists, prematureCompletions, providerRounds, consecutiveReadOnlyRounds, maximumSingleRequestInput, providerStatus, incompleteReason, repairs, noProgressCorrections, paidRequests, phase);
            if (envelope.Type.Equals("complete", StringComparison.OrdinalIgnoreCase))
            {
                var diff = await tools.GetDiffAsync(context.Workspace, ct);
                currentDiffExists = diff.Succeeded && !string.IsNullOrWhiteSpace(diff.Output);
                if (!repositoryInspected || successfulPatches == 0 || !currentDiffExists)
                {
                    prematureCompletions++;
                    if (prematureCompletions >= 2)
                        return Failure("coder_protocol_failed", "The selected Coder model completed before satisfying the repository inspection and patch protocol.", toolSteps, requestId, totalInput, totalOutput, responseType, successfulReads, successfulSearches, successfulPatches, repositoryInspected, currentDiffExists, prematureCompletions, providerRounds, consecutiveReadOnlyRounds, maximumSingleRequestInput, providerStatus, incompleteReason, repairs, noProgressCorrections, paidRequests, phase, null, patchAttempts, failedPatches, lastPatchFailureCode);
                    transcript.Add(new
                    {
                        role = "assistant",
                        content = response.Content
                    });
                    transcript.Add(new
                    {
                        role = "user",
                        content = JsonSerializer.Serialize(new
                        {
                            type = "completion_rejected",
                            originalResponseType = envelope.Type,
                            reason = "Completion requires a successful repository inspection, a successful apply_patch call, and a non-empty diff.",
                            evidencePaths = evidence.Excerpts.Select(x => x.Path),
                            state = new
                            {
                                repositoryInspected,
                                successfulReads,
                                successfulSearches,
                                patchAttempts,
                                successfulPatches,
                                failedPatches,
                                lastPatchFailureCode,
                                currentDiffExists
                            },
                            instruction = "Inspect as needed, apply a real patch, verify the diff, and run focused validation before returning complete."
                        })
                    });
                    continue;
                }

                var changed = await tools.RunCommandAsync(context.Workspace, new("git", ["diff", "--name-only", "--"]), ct);
                var files = changed.Succeeded ? changed.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) : [];
                return new(true, Required(envelope.Summary, "Coder summary is required."), files, envelope.ValidationNotes ?? [], toolSteps, requestId, totalInput, totalOutput, ResponseType: responseType, SuccessfulReadCount: successfulReads, SuccessfulSearchCount: successfulSearches, SuccessfulPatchCount: successfulPatches, RepositoryInspected: repositoryInspected, CurrentDiffExists: currentDiffExists, PrematureCompletionCount: prematureCompletions, ProviderRoundTripCount: providerRounds, ConsecutiveReadOnlyRounds: consecutiveReadOnlyRounds, MaximumSingleRequestInput: maximumSingleRequestInput, ProviderResponseStatus: providerStatus, ProviderIncompleteReason: incompleteReason, StructuredOutputRepairCount: repairs, NoProgressCorrectionCount: noProgressCorrections, PaidProviderRequestCount: paidRequests, CurrentPhase: "Completion", PatchAttemptCount: patchAttempts, FailedPatchCount: failedPatches, LastPatchFailureCode: lastPatchFailureCode);
            }

            if (!envelope.Type.Equals("tool_calls", StringComparison.OrdinalIgnoreCase) || envelope.Calls is null || envelope.Calls.Count == 0)
                return Failure("coder_protocol_failed", "The Coder must return tool calls, complete, or blocked.", toolSteps, requestId, totalInput, totalOutput, responseType, successfulReads, successfulSearches, successfulPatches, repositoryInspected, currentDiffExists, prematureCompletions);
            var patchCountBefore = successfulPatches;
            var results = new List<object>();
            foreach (var call in envelope.Calls.Take(Math.Min(8, options.Value.MaximumCoderToolExecutions - toolSteps)))
            {
                ct.ThrowIfCancellationRequested();
                var result = await Execute(call, context.Workspace, ct);
                toolSteps++;
                toolCounts[call.Tool] = toolCounts.GetValueOrDefault(call.Tool) + 1;
                if (result.Succeeded && call.Tool.Equals("read_file", StringComparison.OrdinalIgnoreCase))
                {
                    successfulReads++;
                    repositoryInspected = true;
                }

                if (result.Succeeded && call.Tool.Equals("search_text", StringComparison.OrdinalIgnoreCase))
                {
                    successfulSearches++;
                    repositoryInspected = true;
                }

                if (call.Tool.Equals("apply_patch", StringComparison.OrdinalIgnoreCase))
                {
                    patchAttempts++;
                    phase = "Implementation";
                    if (result.Succeeded)
                    {
                        successfulPatches++;
                        lastPatchFailureCode = null;
                    }
                    else
                    {
                        failedPatches++;
                        lastPatchFailureCode = SafePatchFailureCode(result.FailureCode);
                    }
                }

                if (call.Tool.Equals("get_diff", StringComparison.OrdinalIgnoreCase))
                    currentDiffExists = result.Succeeded && !string.IsNullOrWhiteSpace(result.Output);
                if (call.Tool.Equals("run_command", StringComparison.OrdinalIgnoreCase) && successfulPatches > 0)
                    phase = "Validation";
                results.Add(new
                {
                    call.Id,
                    call.Tool,
                    result.Succeeded,
                    Output = Limit(result.Output, Math.Min(options.Value.MaximumToolOutputCharacters, 24_000)),
                    result.FailureCode,
                    result.FailureMessage,
                    result.Truncated
                });
            }

            consecutiveReadOnlyRounds = successfulPatches > patchCountBefore ? 0 : consecutiveReadOnlyRounds + 1;
            transcript.Add(new
            {
                role = "assistant",
                content = response.Content
            });
            transcript.Add(new
            {
                role = "user",
                content = JsonSerializer.Serialize(new
                {
                    type = "tool_results",
                    results,
                    state = new
                    {
                        repositoryInspected,
                        successfulReads,
                        successfulSearches,
                        patchAttempts,
                        successfulPatches,
                        failedPatches,
                        lastPatchFailureCode,
                        currentDiffExists,
                        toolSteps,
                        providerRounds,
                        phase
                    }
                })
            });
        }

        return Failure("coder_emergency_circuit_breaker_triggered", $"The Coder reached the emergency circuit breaker after {providerRounds} provider rounds and {toolSteps} repository tool executions. {ToolSummary(toolCounts)}", toolSteps, requestId, totalInput, totalOutput, responseType, successfulReads, successfulSearches, successfulPatches, repositoryInspected, currentDiffExists, prematureCompletions, providerRounds, consecutiveReadOnlyRounds, maximumSingleRequestInput, providerStatus, incompleteReason, repairs, 0, paidRequests, phase, null, patchAttempts, failedPatches, lastPatchFailureCode);
    }

    private async Task<RepositoryToolResult> Execute(ToolCall call, WorkspaceReference workspace, CancellationToken ct)
    {
        try
        {
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

    private static string Arg(ToolCall call, string name, string? fallback = null) => call.Arguments.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : fallback ?? throw new ArgumentException($"{name} is required.");
    private static int IntArg(ToolCall call, string name, int fallback) => call.Arguments.TryGetProperty(name, out var value) && value.TryGetInt32(out var result) ? result : fallback;
    private static string[] Args(ToolCall call) => call.Arguments.TryGetProperty("arguments", out var value) && value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToArray() : [];
    private static string? SerializeTranscript(List<object> transcript, int contextWindowTokens)
    {
        var value = JsonSerializer.Serialize(transcript);
        return EstimateTokens(value) < contextWindowTokens ? value : null;
    }

    private static string Limit(string value, int maximum) => value.Length <= maximum ? value : value[..maximum] + "\n[tool output truncated by the repository-tool safety limit]";
    private static int EstimateTokens(string value) => (value.Length + 2) / 3;
    private static string Reasoning(CoderContext context) => context.AcceptanceCriteria.Count <= 3 && context.RepositoryEvidence?.Count <= 2 ? "low" : "medium";
    private static string? SafePatchFailureCode(string? code) => string.IsNullOrWhiteSpace(code) ? "patch_failed" : Limit(code, 100);
    private static string SafeProviderMessage(string code, LanguageModelResponse response) => code switch
    {
        "provider_output_truncated" => $"The provider output was truncated ({response.IncompleteReason ?? "unknown reason"}).",
        "provider_refused" => "The provider refused the Coder request.",
        "provider_response_failed" => "The provider failed while producing the Coder response.",
        "provider_missing_output" => "The provider completed without output content.",
        _ => "The provider response could not be used."
    };
    private static CoderResult Failure(string code, string message) => new(false, string.Empty, [], [], 0, null, null, null, code, message);
    private static string Required(string? value, string message) => string.IsNullOrWhiteSpace(value) ? throw new InvalidDataException(message) : value.Trim();
    internal const string StructuredOutputSchema = """{"type":"object","additionalProperties":false,"required":["type","calls","summary","validationNotes","knownLimitations","blockerCode","blockerMessage","missingEvidencePaths"],"properties":{"type":{"enum":["tool_calls","complete","blocked"]},"calls":{"type":["array","null"],"items":{"type":"object","additionalProperties":false,"required":["id","tool","arguments"],"properties":{"id":{"type":"string"},"tool":{"type":"string"},"arguments":{"type":"object","additionalProperties":false,"required":["path","query","patch","executable","arguments","workingDirectory","timeoutSeconds"],"properties":{"path":{"type":["string","null"]},"query":{"type":["string","null"]},"patch":{"type":["string","null"]},"executable":{"type":["string","null"]},"arguments":{"type":["array","null"],"items":{"type":"string"}},"workingDirectory":{"type":["string","null"]},"timeoutSeconds":{"type":["integer","null"]}}}}}},"summary":{"type":["string","null"]},"validationNotes":{"type":["array","null"],"items":{"type":"string"}},"knownLimitations":{"type":["array","null"],"items":{"type":"string"}},"blockerCode":{"type":["string","null"]},"blockerMessage":{"type":["string","null"]},"missingEvidencePaths":{"type":["array","null"],"items":{"type":"string"}}}}""";
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
    private static CoderResult Blocked(CoderEnvelope envelope, int toolSteps, string? requestId, int? input, int? output, string? responseType, int reads, int searches, int patches, bool inspected, bool diff, int premature, int rounds, int readOnly, int maxInput, string? status, string? incomplete, int repairs, int corrections, int paid, string phase)
    {
        var code = envelope.BlockerCode switch
        {
            "missing_repository_evidence" when envelope.MissingEvidencePaths?.Count > 0 => "coder_missing_repository_evidence",
            "repository_contract_mismatch" => "coder_repository_contract_mismatch",
            "safe_implementation_blocked" => "coder_safe_implementation_blocked",
            _ => "coder_protocol_failed"
        };
        var message = string.IsNullOrWhiteSpace(envelope.BlockerMessage) ? "The Coder reported a safe implementation blocker." : Limit(envelope.BlockerMessage, 500);
        return Failure(code, message, toolSteps, requestId, input, output, responseType, reads, searches, patches, inspected, diff, premature, rounds, readOnly, maxInput, status, incomplete, repairs, corrections, paid, "Blocked");
    }

    private sealed record EvidencePreloadResult(bool Succeeded, IReadOnlyList<RepositoryEvidenceExcerpt> Excerpts, string? FailureCode, string? FailureMessage);
    private sealed record CoderEnvelope(string Type, List<ToolCall>? Calls, string? Summary, List<string>? ValidationNotes, List<string>? KnownLimitations, string? BlockerCode, List<string>? MissingEvidencePaths, string? BlockerMessage);
    private sealed record ToolCall(string Id, string Tool, JsonElement Arguments);
}
