using Impersonate.Application.Ai;
using Impersonate.Domain.Ai;
using Impersonate.Domain.Pipelines;

namespace Impersonate.Application.Execution;

public sealed record RepositoryCommand(string Executable, IReadOnlyList<string> Arguments, string? WorkingDirectory = null, int TimeoutSeconds = 120);
