using Impersonate.Application.Ai;
using Impersonate.Domain.Ai;
using Impersonate.Domain.Pipelines;

namespace Impersonate.Application.Execution;

public sealed record ReviewFinding(string Severity, string Message, string? Path = null, int? Line = null);
