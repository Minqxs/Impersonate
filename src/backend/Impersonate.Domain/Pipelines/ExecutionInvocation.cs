namespace Impersonate.Domain.Pipelines;

public enum ExecutionInvocationStatus { Succeeded, Failed }

public sealed class ExecutionInvocation
{
    private ExecutionInvocation() { }
    public Guid Id { get; private set; }
    public Guid TaskAttemptId { get; private set; }
    public int Sequence { get; private set; }
    public string AgentRole { get; private set; } = string.Empty;
    public string Provider { get; private set; } = string.Empty;
    public string Model { get; private set; } = string.Empty;
    public Guid? SelectionDecisionId { get; private set; }
    public string PromptVersion { get; private set; } = string.Empty;
    public string? ProviderRequestId { get; private set; }
    public int? InputTokenCount { get; private set; }
    public int? OutputTokenCount { get; private set; }
    public string? ResponseType { get; private set; }
    public int ToolStepCount { get; private set; }
    public int SuccessfulReadCount { get; private set; }
    public int SuccessfulSearchCount { get; private set; }
    public int SuccessfulPatchCount { get; private set; }
    public int FallbackSequence { get; private set; }
    public ExecutionInvocationStatus Status { get; private set; }
    public string? FailureCode { get; private set; }
    public string? FailureReason { get; private set; }
    public DateTimeOffset StartedAtUtc { get; private set; }
    public DateTimeOffset CompletedAtUtc { get; private set; }

    public static ExecutionInvocation Record(Guid taskAttemptId,int sequence,string role,string provider,string model,Guid? selectionDecisionId,string promptVersion,string? requestId,int? input,int? output,string? responseType,int toolSteps,int reads,int searches,int patches,int fallbackSequence,bool succeeded,string? failureCode,string? failureReason,DateTimeOffset startedAt,DateTimeOffset completedAt) => new()
    {
        Id=Guid.NewGuid(),TaskAttemptId=taskAttemptId,Sequence=sequence,AgentRole=role,Provider=provider,Model=model,SelectionDecisionId=selectionDecisionId,PromptVersion=promptVersion,ProviderRequestId=requestId,InputTokenCount=input,OutputTokenCount=output,ResponseType=responseType,ToolStepCount=toolSteps,SuccessfulReadCount=reads,SuccessfulSearchCount=searches,SuccessfulPatchCount=patches,FallbackSequence=fallbackSequence,Status=succeeded?ExecutionInvocationStatus.Succeeded:ExecutionInvocationStatus.Failed,FailureCode=failureCode,FailureReason=failureReason,StartedAtUtc=startedAt,CompletedAtUtc=completedAt
    };
}
