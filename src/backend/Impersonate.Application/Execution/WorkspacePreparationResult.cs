using Impersonate.Application.Ai;
using Impersonate.Domain.Ai;
using Impersonate.Domain.Pipelines;

namespace Impersonate.Application.Execution;

public sealed record WorkspacePreparationResult(bool Succeeded, WorkspaceReference? Workspace, string? FailureCode, string? FailureMessage, string? SourceBaseCommitSha = null, string? ComposedTreeFingerprint = null, IReadOnlyList<Guid>? DependencyTaskIds = null, bool CurrentRevisionPatchApplied = false, int? FailingDependencySequence = null);
