using Impersonate.Application.Ai;
using Impersonate.Domain.Pipelines;
using Impersonate.Domain.Projects;

namespace Impersonate.Application.Pipelines;

public sealed record TaskAttemptDto(Guid Id, int AttemptNumber, TaskAttemptType AttemptType, TaskAttemptStatus Status, string? Provider, string? Model, string? PromptVersion, int? InputTokenCount, int? OutputTokenCount, int ToolStepCount, string? Summary, string? FailureCode, string? FailureReason, IReadOnlyList<string> ChangedFiles, string? PatchArtifactReference, string? PatchSha256, IReadOnlyList<string> ValidationSummary, DateTimeOffset StartedAtUtc, DateTimeOffset? CompletedAtUtc, IReadOnlyList<ExecutionInvocationDto>? Invocations = null, string? SourceBaseCommitSha = null, int DependencyPatchCount = 0, IReadOnlyList<Guid>? DependencyTaskIds = null, string? ComposedTreeFingerprint = null, bool CurrentRevisionPatchApplied = false, int IncrementalPatchFileCount = 0, string? CompositionStatus = null);
