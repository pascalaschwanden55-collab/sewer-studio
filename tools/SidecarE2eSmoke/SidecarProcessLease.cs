using System.Diagnostics;
using System.Text;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Startup;

namespace SidecarE2eSmoke;

/// <summary>
/// Startet den lokalen Sidecar nur dann, wenn er noch nicht laeuft.
/// Ein bereits laufender Sidecar wird beim Beenden nie angefasst.
/// </summary>
public sealed class SidecarProcessLease : IAsyncDisposable
{
    private const int MaxCapturedOutputChars = 2000;
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
            ? CaptureOutputTailAsync(process.StandardOutput)
            : null;
        var stderr = startInfo.RedirectStandardError
            ? CaptureOutputTailAsync(process.StandardError)
            : null;
        var lease = new SidecarProcessLease(process, stdout, stderr, options.KeepStartedSidecar);

        using var startupTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        startupTimeout.CancelAfter(TimeSpan.FromSeconds(options.StartupTimeoutSec));
        try
        {
            while (true)
            {
                startupTimeout.Token.ThrowIfCancellationRequested();
                if (process.HasExited)
                {
                    var detail = await lease.ReadExitedOutputAsync(startupTimeout.Token);
                    throw new InvalidOperationException(
                        $"Der Sidecar wurde beim Start beendet (ExitCode {process.ExitCode}). {detail}");
                }

                await Task.Delay(TimeSpan.FromSeconds(2), startupTimeout.Token);
                var health = await client.CheckHealthDetailedAsync(startupTimeout.Token);
                if (health.IsReachable && health.IsAuthorized)
                    return lease;
                if (health.IsReachable && !health.IsAuthorized)
                    throw new InvalidOperationException("Der gestartete Sidecar lehnt das Zugriffstoken ab.");
            }
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            var timeout = new TimeoutException(
                $"Der Sidecar war nach {options.StartupTimeoutSec} Sekunden noch nicht bereit.", ex);
            try
            {
                await lease.DisposeAsync();
            }
            catch (Exception cleanupError)
            {
                throw new AggregateException("Sidecar-Start und Aufräumen fehlgeschlagen.", timeout, cleanupError);
            }
            throw timeout;
        }
        catch (Exception startupError)
        {
            try
            {
                await lease.DisposeAsync();
            }
            catch (Exception cleanupError)
            {
                throw new AggregateException("Sidecar-Start und Aufräumen fehlgeschlagen.", startupError, cleanupError);
            }
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_process is null)
            return;

        try
        {
            if (!_keepRunning)
            {
                if (!_process.HasExited)
                {
                    try
                    {
                        _process.Kill(entireProcessTree: true);
                    }
                    catch (InvalidOperationException) when (_process.HasExited)
                    {
                        // Der Prozess ist zwischen HasExited und Kill selbst beendet worden.
                    }
                }
                await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
                var outputTasks = new[] { _standardOutput, _standardError }
                    .Where(task => task is not null)
                    .Cast<Task<string>>();
                await Task.WhenAll(outputTasks).WaitAsync(TimeSpan.FromSeconds(5));
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Der gestartete Sidecar konnte nicht sicher beendet werden.", ex);
        }
        finally
        {
            _process.Dispose();
        }
    }

    private async Task<string> ReadExitedOutputAsync(CancellationToken ct)
    {
        var stdout = _standardOutput is null ? string.Empty : await _standardOutput.WaitAsync(ct);
        var stderr = _standardError is null ? string.Empty : await _standardError.WaitAsync(ct);
        var combined = $"{stdout}\n{stderr}".Trim();
        return combined.Length <= MaxCapturedOutputChars
            ? combined
            : combined[^MaxCapturedOutputChars..];
    }

    private static async Task<string> CaptureOutputTailAsync(StreamReader reader)
    {
        var tail = new StringBuilder();
        var buffer = new char[1024];
        int length;
        while ((length = await reader.ReadAsync(buffer)) > 0)
        {
            tail.Append(buffer, 0, length);
            if (tail.Length > MaxCapturedOutputChars)
                tail.Remove(0, tail.Length - MaxCapturedOutputChars);
        }

        return tail.ToString();
    }
}
