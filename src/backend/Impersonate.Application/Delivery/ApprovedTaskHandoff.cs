namespace Impersonate.Application.Delivery;

public sealed record ApprovedTaskHandoff(
    Guid ProjectId, Guid PipelineRunId, Guid PlannedTaskId, int TaskSequence,
    string Title, string Description, IReadOnlyList<string> AcceptanceCriteria,
    IReadOnlyList<Guid> DependencyTaskIds, string SourceBaseCommitSha,
    string ApprovedPatchArtifactReference, string ApprovedPatchSha256,
    IReadOnlyList<string> ChangedFiles, IReadOnlyList<string> ValidationNotes,
    Guid ApprovedReviewDecisionId, string ReviewerProvider, string ReviewerModel,
    string ReviewSummary, string CoderProvider, string CoderModel,
    ModelSelectionEvidence CoderSelection, ModelSelectionEvidence ReviewerSelection,
    Guid TaskAttemptId, int AttemptNumber, int RevisionNumber);
