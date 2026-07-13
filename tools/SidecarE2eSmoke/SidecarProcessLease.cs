using System.Diagnostics;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Startup;

namespace SidecarE2eSmoke;

/// <summary>
/// Startet den lokalen Sidecar nur dann, wenn er noch nicht laeuft.
/// Ein bereits laufender Sidecar wird beim Beenden nie angefasst.
/// </summary>
public sealed class SidecarProcessLease : IAsyncDisposable
{
    private readonly Process? _process;
    private readonly Task<string>? _standardOutput;
    private readonly Task<string>? _standardError;
    private readonly bool _keepRunning;

    private SidecarProcessLease(
        Process? process,
        Task<string>? standardOutput,
        Task<string>? standardError,
        bool keepRunning)
    {
        _process = process;
        _standardOutput = standardOutput;
        _standardError = standardError;
        _keepRunning = keepRunning;
    }

    public bool StartedByTool => _process is not null;

    public static async Task<SidecarProcessLease> EnsureReadyAsync(
        SidecarSmokeOptions options,
        IVisionPipelineClient client,
        CancellationToken ct)
    {
        var initial = await client.CheckHealthDetailedAsync(ct);
        if (initial.IsReachable)
        {
            if (!initial.IsAuthorized)
                throw new InvalidOperationException("Der Sidecar laeuft, aber das Zugriffstoken ist falsch oder fehlt.");
            return new SidecarProcessLease(null, null, null, keepRunning: true);
        }

        if (!options.StartSidecar)
        {
            throw new InvalidOperationException(
                "Der Sidecar ist nicht erreichbar. Starte Sewer Studio oder verwende --start-sidecar.");
        }

        var script = SidecarScriptLocator.FindDefaultSidecarScript();
        if (script is null)
            throw new FileNotFoundException("sidecar/start_sidecar.ps1 wurde nicht gefunden.");

        var startInfo = new ProcessStartInfo
        {
            FileName = SidecarScriptLocator.ResolvePowerShellExe(),
            UseShellExecute = false,
            // Bei --keep-sidecar duerfen keine Pipes an den kurzlebigen Tester gebunden
            // bleiben. Sonst kann der weiterlaufende Sidecar spaeter am geschlossenen
            // Ausgabekanal scheitern.
            RedirectStandardOutput = !options.KeepStartedSidecar,
            RedirectStandardError = !options.KeepStartedSidecar,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(script)!,
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(script);

        var process = Process.Start(startInfo)
                      ?? throw new InvalidOperationException("Der Sidecar-Startprozess konnte nicht gestartet werden.");
        var stdout = startInfo.RedirectStandardOutput
            ? process.StandardOutput.ReadToEndAsync()
            : null;
        var stderr = startInfo.RedirectStandardError
            ? process.StandardError.ReadToEndAsync()
            : null;
        var lease = new SidecarProcessLease(process, stdout, stderr, options.KeepStartedSidecar);

        try
        {
            var started = Stopwatch.StartNew();
            while (started.Elapsed < TimeSpan.FromSeconds(options.StartupTimeoutSec))
            {
                ct.ThrowIfCancellationRequested();
                if (process.HasExited)
                {
                    var detail = await lease.ReadExitedOutputAsync();
                    throw new InvalidOperationException(
                        $"Der Sidecar wurde beim Start beendet (ExitCode {process.ExitCode}). {detail}");
                }

                await Task.Delay(TimeSpan.FromSeconds(2), ct);
                var health = await client.CheckHealthDetailedAsync(ct);
                if (health.IsReachable && health.IsAuthorized)
                    return lease;
                if (health.IsReachable && !health.IsAuthorized)
                    throw new InvalidOperationException("Der gestartete Sidecar lehnt das Zugriffstoken ab.");
            }

            throw new TimeoutException(
                $"Der Sidecar war nach {options.StartupTimeoutSec} Sekunden noch nicht bereit.");
        }
        catch
        {
            await lease.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_process is null)
            return;

        try
        {
            if (!_keepRunning && !_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
            }
        }
        catch
        {
            // Der Tester ist bereits fertig; Aufraeumfehler sind nur nachrangig.
        }
        finally
        {
            _process.Dispose();
        }
    }

    private async Task<string> ReadExitedOutputAsync()
    {
        var stdout = _standardOutput is null ? string.Empty : await _standardOutput;
        var stderr = _standardError is null ? string.Empty : await _standardError;
        var combined = $"{stdout}\n{stderr}".Trim();
        return combined.Length <= 2000 ? combined : combined[^2000..];
    }
}
