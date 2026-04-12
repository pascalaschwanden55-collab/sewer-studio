using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Ai;

/// <summary>
/// Startet den Python-Sidecar (YOLO/DINO/SAM) automatisch beim App-Start
/// und beendet ihn beim App-Exit. Wenn der Sidecar bereits laeuft oder
/// kein venv vorhanden ist, wird nichts getan.
/// </summary>
public sealed class PythonSidecarService : IDisposable
{
    private readonly ILogger _log;
    private readonly string _sidecarDir;
    private readonly string _host;
    private readonly int _port;
    private Process? _process;
    private bool _ownsProcess;
    private bool _disposed;

    public bool IsRunning => _process is { HasExited: false };

    /// <summary>True wenn der Sidecar erreichbar ist (egal ob selbst gestartet oder extern).</summary>
    public bool IsAvailable { get; private set; }

    public PythonSidecarService(ILogger logger, string sidecarDir, string host = "127.0.0.1", int port = 8100)
    {
        _log = logger;
        _sidecarDir = sidecarDir;
        _host = host;
        _port = port;
    }

    /// <summary>
    /// Startet den Sidecar-Prozess falls noetig.
    /// Blockiert nicht die UI — kehrt nach Health-Check oder Timeout zurueck.
    /// </summary>
    public async Task StartAsync(CancellationToken ct = default)
    {
        // 1. Pruefen ob bereits ein gesunder Sidecar erreichbar ist
        if (await IsHealthyAsync(ct))
        {
            _log.LogInformation("[Sidecar] Externer Sidecar auf Port {Port} ist erreichbar", _port);
            IsAvailable = true;
            return;
        }

        if (await IsPortInUseAsync())
        {
            _log.LogWarning("[Sidecar] Port {Port} ist belegt, aber /health antwortet nicht — Sidecar wird nicht als verfuegbar markiert", _port);
            IsAvailable = false;
            return;
        }

        // 2. Python venv pruefen
        var venvPython = Path.Combine(_sidecarDir, ".venv", "Scripts", "python.exe");
        if (!File.Exists(venvPython))
        {
            _log.LogWarning("[Sidecar] Python venv nicht gefunden: {Path} — Sidecar wird nicht gestartet (Qwen-only Fallback)", venvPython);
            return;
        }

        // 3. Prozess starten
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = venvPython,
                Arguments = "-m uvicorn sidecar.main:app --host " + _host + " --port " + _port,
                WorkingDirectory = _sidecarDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _ownsProcess = true;

            // stdout/stderr ins Log weiterleiten
            _process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    _log.LogDebug("[Sidecar] {Line}", e.Data);
            };
            _process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    _log.LogDebug("[Sidecar] {Line}", e.Data);
            };
            _process.Exited += (_, _) =>
            {
                _log.LogWarning("[Sidecar] Prozess beendet (ExitCode={Code})", _process?.ExitCode);
                _ownsProcess = false;
                IsAvailable = false;
            };

            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            _log.LogInformation("[Sidecar] Python-Prozess gestartet (PID={Pid})", _process.Id);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "[Sidecar] Prozess konnte nicht gestartet werden — Qwen-only Fallback");
            return;
        }

        // 4. Health-Poll: max 30s warten bis /health antwortet
        IsAvailable = await WaitForHealthAsync(TimeSpan.FromSeconds(30), ct);
        if (IsAvailable)
            _log.LogInformation("[Sidecar] Bereit auf http://{Host}:{Port}", _host, _port);
        else
        {
            _log.LogWarning("[Sidecar] Health-Check nach 30s nicht erfolgreich — Qwen-only Fallback");
            TryStopOwnedProcess();
        }
    }

    private async Task<bool> WaitForHealthAsync(TimeSpan timeout, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            if (await IsHealthyAsync(ct))
                return true;

            await Task.Delay(1000, ct);
        }

        return false;
    }

    private async Task<bool> IsHealthyAsync(CancellationToken ct)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        var healthUrl = $"http://{_host}:{_port}/health";

        try
        {
            var resp = await http.GetAsync(healthUrl, ct);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> IsPortInUseAsync()
    {
        try
        {
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(_host, _port);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        TryStopOwnedProcess();
    }

    private void TryStopOwnedProcess()
    {
        var process = _process;
        if (process is null)
        {
            IsAvailable = false;
            return;
        }

        try
        {
            if (_ownsProcess && !process.HasExited)
            {
                _log.LogInformation("[Sidecar] Beende eigenen Prozess (PID={Pid})...", process.Id);
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "[Sidecar] Fehler beim Beenden des Prozesses");
        }
        finally
        {
            _ownsProcess = false;
            process.Dispose();
            if (ReferenceEquals(_process, process))
                _process = null;
            IsAvailable = false;
        }
    }
}
