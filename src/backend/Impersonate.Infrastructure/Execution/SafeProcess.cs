using System.Diagnostics;
using System.Text;
using Impersonate.Application.Execution;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Impersonate.Infrastructure.Execution;

internal sealed class SafeProcess(IChildProcessEnvironmentBuilder environments, Microsoft.Extensions.Logging.ILogger<SafeProcess> logger)
{
    public async Task<ProcessResult> RunAsync(string executable, IReadOnlyList<string> arguments, string cwd, int timeoutSeconds, int outputLimit, string? standardInput, CancellationToken ct)
    {
        var supplied = environments.Build();
        logger.LogDebug("Launching sanitized child process on {OperatingSystem}: {Executable}; variables {VariableNames}; working directory reference supplied.", System.Runtime.InteropServices.RuntimeInformation.OSDescription, executable, supplied.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
        var start = new ProcessStartInfo(executable)
        {
            WorkingDirectory = cwd,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = standardInput is not null,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        start.Environment.Clear();
        foreach (var item in supplied)
            start.Environment[item.Key] = item.Value;
        using var process = new Process
        {
            StartInfo = start
        };
        var output = new StringBuilder();
        var outputGate = new object();
        process.OutputDataReceived += (_, e) => Append(e.Data);
        process.ErrorDataReceived += (_, e) => Append(e.Data);
        try
        {
            process.Start();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            logger.LogWarning("Sanitized child process failed to start on {OperatingSystem}: {Executable}.", System.Runtime.InteropServices.RuntimeInformation.OSDescription, executable);
            return new(false, false, "Process could not be started.", true);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        if (standardInput is not null)
        {
            await process.StandardInput.WriteAsync(standardInput.AsMemory(), ct);
            process.StandardInput.Close();
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
        try
        {
            await process.WaitForExitAsync(linked.Token);
            process.WaitForExit();
            logger.LogDebug("Sanitized child process exited: {Executable}; exit code {ExitCode}; timeout false.", executable, process.ExitCode);
            lock (outputGate)
                return new(process.ExitCode == 0, false, output.ToString(), false, process.ExitCode);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            try
            {
                process.Kill(true);
            }
            catch
            {
            }

            logger.LogWarning("Sanitized child process timed out: {Executable}; timeout true.", executable);
            return new(false, true, output.ToString());
        }

        void Append(string? line)
        {
            if (line is null) return;
            lock (outputGate)
            {
                if (output.Length >= outputLimit) return;
                var remaining = outputLimit - output.Length;
                var take = Math.Min(line.Length, remaining);
                if (take > 0) output.Append(line.AsSpan(0, take));
                if (output.Length < outputLimit) output.AppendLine();
            }
        }
    }
}
