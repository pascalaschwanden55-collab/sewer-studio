using System.Diagnostics;

namespace AuswertungPro.Next.Infrastructure.Ai;

internal sealed record ProcessOutput(int ExitCode, string StandardOutput, string StandardError);

internal static class ProcessOutputReader
{
    public static async Task<ProcessOutput?> ReadToExitAsync(
        ProcessStartInfo startInfo,
        CancellationToken ct,
        Action<int>? onStarted = null)
    {
        using var process = Process.Start(startInfo);
        if (process is null)
            return null;

        try
        {
            onStarted?.Invoke(process.Id);
            var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct).ConfigureAwait(false);

            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);

            return new ProcessOutput(process.ExitCode, stdout, stderr);
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
            // Best effort cleanup only; preserve the original exception.
        }
    }
}
