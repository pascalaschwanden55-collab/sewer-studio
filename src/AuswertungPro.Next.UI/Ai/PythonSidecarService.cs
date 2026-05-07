using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AuswertungPro.Next.Application.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;

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

    /// <summary>
    /// Bearer-Token fuer Sidecar-Auth (Audit 2026-04-25, SEC-H5).
    /// Bei jedem App-Start neu generiert und an den Sidecar-Subprozess uebergeben.
    /// HTTP-Clients muessen den Header `X-Sidecar-Token: {Token}` senden, sonst 401.
    /// Public weil die Vision-Pipeline-Clients ihn beim Request-Bau lesen muessen.
    /// </summary>
    public string AuthToken { get; }

    /// <summary>
    /// Singleton-Slot: aktuell aktives Token, damit VisionPipelineClient-Stellen
    /// ohne explizite Token-Uebergabe automatisch authentifizieren. Pro Prozess
    /// gibt es genau einen Sidecar, daher static akzeptabel.
    /// </summary>
    public static string? CurrentAuthToken { get; private set; }

    /// <summary>
    /// Pfad zur Token-Datei, die App und Sidecar gemeinsam lesen
    /// (siehe sidecar/sidecar/main.py _resolve_sidecar_token).
    /// </summary>
    public static string TokenFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SewerStudio", ".sidecar_token");

    /// <summary>
    /// True wenn ENV SEWER_SIDECAR_AUTH=disabled gesetzt ist. Dann sendet
    /// VisionPipelineClient kein Token und der Sidecar prueft auch keins.
    /// </summary>
    public static bool AuthDisabled =>
        string.Equals(Environment.GetEnvironmentVariable("SEWER_SIDECAR_AUTH"),
                      "disabled", StringComparison.OrdinalIgnoreCase);

    public PythonSidecarService(ILogger logger, string sidecarDir, string host = "127.0.0.1", int port = 8100)
    {
        _log = logger;
        _sidecarDir = sidecarDir;
        _host = host;
        _port = port;

        if (AuthDisabled)
        {
            // Dev-Modus: kein Token verwenden, Sidecar muss auch ohne starten
            AuthToken = "";
            CurrentAuthToken = null;
            _log.LogWarning("[Sidecar-Auth] SEWER_SIDECAR_AUTH=disabled — kein Token, keine Auth.");
            return;
        }

        // Token-Datei lesen wenn vorhanden, sonst neues Token generieren+schreiben.
        // So koennen App und manuell gestarteter Sidecar dasselbe Token nutzen.
        try
        {
            if (File.Exists(TokenFilePath))
            {
                var existing = File.ReadAllText(TokenFilePath).Trim();
                if (!string.IsNullOrWhiteSpace(existing) && existing.Length >= 16)
                {
                    AuthToken = existing;
                    CurrentAuthToken = AuthToken;
                    _log.LogInformation("[Sidecar-Auth] Token aus {Path} geladen.", TokenFilePath);
                    return;
                }
            }

            // Datei fehlt oder ungueltig -> neu generieren und persistieren
            AuthToken = Guid.NewGuid().ToString("N");
            CurrentAuthToken = AuthToken;
            var dir = Path.GetDirectoryName(TokenFilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(TokenFilePath, AuthToken);
            _log.LogInformation("[Sidecar-Auth] Neues Token in {Path} geschrieben.", TokenFilePath);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "[Sidecar-Auth] Token-Datei nicht nutzbar — fallback auf In-Memory-Token.");
            AuthToken = Guid.NewGuid().ToString("N");
            CurrentAuthToken = AuthToken;
        }
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
        // Host/Port werden validiert um zusaetzliche uvicorn-Argumente
        // ueber Stringeinschleusung zu verhindern (Audit 2026-04-25 H4).
        if (!IsValidHost(_host))
        {
            _log.LogError("[Sidecar] Ungueltiger Host '{Host}' — Start abgebrochen", _host);
            return;
        }
        if (_port < 1 || _port > 65535)
        {
            _log.LogError("[Sidecar] Ungueltiger Port {Port} — Start abgebrochen", _port);
            return;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = venvPython,
                WorkingDirectory = _sidecarDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            // ArgumentList statt Arguments-String: keine Shell-Interpretation,
            // keine Argument-Injection ueber manipulierte Host/Port-Werte.
            psi.ArgumentList.Add("-m");
            psi.ArgumentList.Add("uvicorn");
            psi.ArgumentList.Add("sidecar.main:app");
            psi.ArgumentList.Add("--host");
            psi.ArgumentList.Add(_host);
            psi.ArgumentList.Add("--port");
            psi.ArgumentList.Add(_port.ToString(System.Globalization.CultureInfo.InvariantCulture));

            // Bearer-Token an den Sidecar-Prozess uebergeben (SEC-H5).
            psi.EnvironmentVariables["SEWER_SIDECAR_TOKEN"] = AuthToken;

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

    /// <summary>
    /// Validiert Host-String fuer uvicorn (IPv4/IPv6/Hostname).
    /// Verhindert Argument-Injection durch manipulierte Host-Werte.
    /// </summary>
    private static bool IsValidHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host)) return false;
        // Keine Whitespace, keine Argument-Trenner
        foreach (var ch in host)
        {
            if (char.IsWhiteSpace(ch)) return false;
            if (ch == '"' || ch == '\'' || ch == '`') return false;
        }
        // Try IP-Parse (IPv4/IPv6) oder DNS-Hostname-Pattern
        if (System.Net.IPAddress.TryParse(host, out _)) return true;
        // Hostname: nur a-z, 0-9, Punkt, Bindestrich
        foreach (var ch in host)
        {
            if (!(char.IsLetterOrDigit(ch) || ch == '.' || ch == '-')) return false;
        }
        return host.Length > 0 && host.Length <= 253;
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

    /// <summary>
    /// Audit STAB-H4 (2026-04-23): Vor dem Hard-Kill /shutdown POSTen damit
    /// uvicorn die FastAPI-Lifespan-Cleanup (GPU-Modelle entladen, etc.)
    /// sauber durchlaufen kann. Nach 3 s ohne Exit wird trotzdem gekillt.
    /// </summary>
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
                _log.LogInformation(
                    "[Sidecar] Beende eigenen Prozess (PID={Pid}) — versuche Graceful-Shutdown via /shutdown...",
                    process.Id);

                if (TryRequestGracefulShutdown(out var responded))
                {
                    // 3 s warten — uvicorn-Shutdown-Handler braucht ~1-2 s fuer
                    // GPU-Unload + Async-Tasks-Drain. Reicht der Sidecar nicht aus,
                    // greift Kill darunter.
                    if (process.WaitForExit(3000))
                    {
                        _log.LogInformation(
                            "[Sidecar] Graceful-Shutdown erfolgreich (HTTP responded={Responded}, ExitCode={Exit})",
                            responded, process.ExitCode);
                    }
                    else
                    {
                        _log.LogWarning(
                            "[Sidecar] /shutdown beantwortet aber Prozess noch aktiv nach 3s — fallback Hard-Kill");
                        process.Kill(entireProcessTree: true);
                        process.WaitForExit(5000);
                    }
                }
                else
                {
                    _log.LogWarning("[Sidecar] /shutdown nicht erreichbar — Hard-Kill");
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                }
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

    /// <summary>
    /// POSTet /shutdown am Sidecar (mit X-Sidecar-Token wenn aktiv).
    /// Synchron und kurzer Timeout: dieser Pfad laeuft bei App-Exit, da darf
    /// keine I/O ewig haengen. Returns true wenn HTTP-Antwort kam.
    /// </summary>
    private bool TryRequestGracefulShutdown(out bool responded)
    {
        responded = false;
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            using var req = new HttpRequestMessage(HttpMethod.Post,
                $"http://{_host}:{_port}/shutdown");
            if (!AuthDisabled && !string.IsNullOrEmpty(AuthToken))
                req.Headers.Add("X-Sidecar-Token", AuthToken);

            var resp = http.Send(req);
            responded = resp.IsSuccessStatusCode;
            return true;
        }
        catch
        {
            // Sidecar antwortet nicht — Caller faellt auf Kill zurueck.
            return false;
        }
    }
}
