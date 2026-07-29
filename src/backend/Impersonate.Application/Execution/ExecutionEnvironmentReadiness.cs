using Impersonate.Application.Ai;
using Impersonate.Domain.Ai;
using Impersonate.Domain.Pipelines;

namespace Impersonate.Application.Execution;

public sealed record ExecutionEnvironmentReadiness(bool Ready, string OperatingSystem, bool GitAvailable, bool GitVersionSucceeded, bool WorkspaceRootWritable, bool CoreEnvironmentValid, bool SanitizedProcessSucceeded, IReadOnlyList<string> SuppliedVariableNames, IReadOnlyList<string> Blockers);
