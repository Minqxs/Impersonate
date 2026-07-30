using Impersonate.Application.Ai;
using Impersonate.Domain.Pipelines;
using Impersonate.Domain.Projects;

namespace Impersonate.Application.Pipelines;

public sealed record PipelineOperationResult<T>(T? Value, string? Error, string? Code)
{
    public bool Succeeded => Error is null;

    public static PipelineOperationResult<T> Ok(T value) => new(value, null, null);
    public static PipelineOperationResult<T> Fail(string code, string error) => new(default, error, code);
}
