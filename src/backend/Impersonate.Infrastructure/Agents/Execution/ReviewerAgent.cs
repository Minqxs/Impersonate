using System.Reflection;
using System.Text.Json;
using Impersonate.Application.Ai;
using Impersonate.Application.Execution;
using Impersonate.Domain.Pipelines;
using Microsoft.Extensions.Options;

namespace Impersonate.Infrastructure.Agents.Execution;

internal sealed class ReviewerAgent(IEnumerable<IAiProviderAdapter> adapters, IProviderCredentialStore credentials, IOptions<ExecutionOptions> options) : IReviewerAgent
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true
    };
    public async Task<ReviewerResult> ReviewAsync(ReviewerContext context, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(context.Patch))
            return Failure("invalid_reviewer_output", "Reviewer approval requires an actual patch.");
        if (context.Model.ConnectionId is not { } connectionId)
            return Failure("reviewer_provider_failed", "The selected Reviewer model has no provider connection.");
        var adapter = adapters.SingleOrDefault(x => x.ProviderType == context.Model.ProviderType);
        var credential = await credentials.RetrieveAsync(connectionId, ct);
        if (adapter is null || credential.Status != ProviderCredentialReadStatus.Found)
            return Failure("reviewer_provider_failed", "Reviewer provider access is unavailable.");
        var input = JsonSerializer.Serialize(new
        {
            context.FeatureRequest,
            task = new
            {
                context.TaskTitle,
                context.TaskDescription,
                context.AcceptanceCriteria
            },
            context.AttemptNumber,
            actualPatch = context.Patch,
            context.PatchSha256,
            context.ChangedFiles,
            context.ValidationResults,
            context.CoderSummary,
            context.PriorFeedback
        });
        var contextWindow = context.Model.ContextWindowSize ?? options.Value.DefaultModelContextWindowTokens;
        var maximumOutput = Math.Min(Math.Max(1, contextWindow / 2), Math.Max(1, context.Model.MaximumOutputSize ?? options.Value.DefaultReviewerMaximumOutputTokens));
        if ((input.Length + 2) / 3 + maximumOutput >= contextWindow)
            return Failure("provider_context_limit_exceeded", "The complete Reviewer request cannot fit the selected model's advertised context window.");
        LanguageModelResponse response;
        try
        {
            response = await adapter.CompleteAsync(new(connectionId, context.Model.ProviderType, credential.Credential!), new(context.Model.DiscoveredModelId, context.Model.ProviderModelId), new(context.Model.ProviderModelId, PromptLoader.Load("reviewer-v1"), input, StructuredOutputSchema, maximumOutput), ct);
        }
        catch (ProviderRequestException ex)
        {
            return Failure(ex.Code, ex.Message);
        }
        catch (HttpRequestException)
        {
            return Failure("provider_unavailable", "The Reviewer provider could not be reached.");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return Failure("provider_timeout", "The Reviewer provider timed out.");
        }

        ReviewEnvelope value;
        try
        {
            value = JsonSerializer.Deserialize<ReviewEnvelope>(response.Content, Json) ?? throw new JsonException();
        }
        catch (JsonException)
        {
            return Failure("invalid_reviewer_output", "The Reviewer returned invalid structured output.");
        }

        if (!Enum.TryParse<ReviewDecisionType>(value.Decision, true, out var decision))
            return Failure("invalid_reviewer_output", "Reviewer decision is invalid.");
        var findings = value.Findings ?? [];
        if (decision == ReviewDecisionType.ChangesRequested && string.IsNullOrWhiteSpace(value.Feedback))
            return Failure("invalid_reviewer_output", "ChangesRequested requires actionable feedback.");
        if (decision == ReviewDecisionType.Approved && findings.Any(x => x.Severity.Equals("Blocking", StringComparison.OrdinalIgnoreCase)))
            return Failure("invalid_reviewer_output", "Approved output cannot contain blocking findings.");
        return new(true, decision, value.Summary?.Trim() ?? string.Empty, value.Feedback?.Trim(), findings, response.ProviderRequestId, response.InputTokenCount, response.OutputTokenCount);
    }

    private static ReviewerResult Failure(string code, string message) => new(false, null, string.Empty, null, [], null, null, null, code, message);
    internal const string StructuredOutputSchema = """{"type":"object","additionalProperties":false,"required":["decision","summary","feedback","findings"],"properties":{"decision":{"enum":["Approved","ChangesRequested"]},"summary":{"type":"string"},"feedback":{"type":["string","null"]},"findings":{"type":"array","items":{"type":"object","additionalProperties":false,"required":["severity","message","path","line"],"properties":{"severity":{"type":"string"},"message":{"type":"string"},"path":{"type":["string","null"]},"line":{"type":["integer","null"]}}}}}}""";
    private sealed record ReviewEnvelope(string Decision, string Summary, string? Feedback, List<ReviewFinding>? Findings);
}
