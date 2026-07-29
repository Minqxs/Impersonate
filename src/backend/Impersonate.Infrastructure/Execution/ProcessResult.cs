using System.Diagnostics;
using System.Text;
using Impersonate.Application.Execution;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Impersonate.Infrastructure.Execution;

internal sealed record ProcessResult(bool Succeeded, bool TimedOut, string Output, bool StartFailure = false, int? ExitCode = null);
