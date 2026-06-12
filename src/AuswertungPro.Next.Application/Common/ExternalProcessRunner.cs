using System.Diagnostics;
using System.Text;

namespace AuswertungPro.Next.Application.Common;

public sealed record ExternalProcessRunResult(
    bool Success,
    int? ExitCode,
    bool TimedOut,
    string StdOut,
    string StdErr,
    string? Message);

public static class ExternalProcessRunner
{
    private static readonly TimeSpan KillDrainTimeout = TimeSpan.FromSeconds(2);

    public static async Task<ExternalProcessRunResult> RunAsync(
        string fileName,
        IEnumerable<string> arguments,
        TimeSpan timeout,
        Encoding? standardOutputEncoding = null,
        Encoding? standardErrorEncoding = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("Prozesspfad fehlt.", nameof(fileName));

        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        if (standardOutputEncoding is not null)
            startInfo.StandardOutputEncoding = standardOutputEncoding;
        if (standardErrorEncoding is not null)
            startInfo.StandardErrorEncoding = standardErrorEncoding;

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        return await RunAsync(startInfo, timeout, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<ExternalProcessRunResult> RunAsync(
        ProcessStartInfo startInfo,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(startInfo);
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout muss positiv sein.");

        startInfo.UseShellExecute = false;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.CreateNoWindow = true;

        using var process = Process.Start(startInfo);
        if (process is null)
            return new ExternalProcessRunResult(false, null, false, string.Empty, string.Empty, "Prozess konnte nicht gestartet werden.");

        var stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        var waitTask = process.WaitForExitAsync(cancellationToken);
        var timeoutTask = Task.Delay(timeout, CancellationToken.None);

        try
        {
            var completed = await Task.WhenAny(waitTask, timeoutTask).ConfigureAwait(false);
            if (completed == timeoutTask)
            {
                TryKill(process);
                var timedOutStdOut = await TryReadCompletedTextAsync(stdOutTask).ConfigureAwait(false);
                var timedOutStdErr = await TryReadCompletedTextAsync(stdErrTask).ConfigureAwait(false);
                return new ExternalProcessRunResult(false, null, true, timedOutStdOut, timedOutStdErr, $"Timeout nach {(int)timeout.TotalMilliseconds} ms.");
            }

            await waitTask.ConfigureAwait(false);
            var stdOut = await stdOutTask.ConfigureAwait(false);
            var stdErr = await stdErrTask.ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                var message = string.IsNullOrWhiteSpace(stdErr)
                    ? $"ExitCode {process.ExitCode}"
                    : $"ExitCode {process.ExitCode}: {stdErr.Trim()}";
                return new ExternalProcessRunResult(false, process.ExitCode, false, stdOut, stdErr, message);
            }

            return new ExternalProcessRunResult(true, process.ExitCode, false, stdOut, stdErr, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            TryKill(process);
            throw;
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Best effort: callers get a timeout result even if the OS refuses termination.
        }
    }

    private static async Task<string> TryReadCompletedTextAsync(Task<string> textTask)
    {
        try
        {
            var completed = await Task.WhenAny(textTask, Task.Delay(KillDrainTimeout)).ConfigureAwait(false);
            return completed == textTask
                ? await textTask.ConfigureAwait(false)
                : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
}
