using System.Diagnostics;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Ai;

public sealed class ProcessOutputReaderService : IProcessOutputReader
{
    public async Task<ProcessOutputResult?> ReadToExitAsync(
        ProcessStartInfo startInfo,
        CancellationToken cancellationToken,
        Action<int>? onStarted = null)
    {
        using var process = Process.Start(startInfo);
        if (process is null)
            return null;

        try
        {
            onStarted?.Invoke(process.Id);
            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);

            return new ProcessOutputResult(process.ExitCode, stdout, stderr);
        }
        catch
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
            // Best effort: Die urspruengliche Ausnahme muss erhalten bleiben.
        }
    }
}
