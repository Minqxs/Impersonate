using Impersonate.Application.Ai;
using Impersonate.Domain.Ai;
using Impersonate.Domain.Pipelines;

namespace Impersonate.Application.Execution;

public sealed record RepositoryToolResult(bool Succeeded, string Output, string? FailureCode = null, string? FailureMessage = null, bool Truncated = false);
