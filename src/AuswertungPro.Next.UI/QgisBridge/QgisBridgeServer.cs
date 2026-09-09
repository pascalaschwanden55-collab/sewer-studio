using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using AuswertungPro.Next.UI.Helpers;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.QgisBridge;

/// <summary>
/// Eigenstaendiger HTTP-Host der QGIS-Bridge (Loopback, Standardport 8765).
/// Wird nur gestartet, wenn Live-Control den Port nicht bereits haelt —
/// in dem Fall liefert der LiveControlServer die /qgis-Endpunkte selbst aus.
/// Die eigentliche Verarbeitung liegt im <see cref="QgisBridgeRequestProcessor"/>.
/// Bewusste Einzelplatz-Grenze: nur IPv4-Loopback, nur GET/HEAD und — seit dem
/// Rueckweg aus der Karte — POST auf genau einen Pfad (/qgis/seek). Zusaetzlich ist
/// seit dem Gesamtaudit 2026-08-14 ein Token Pflicht (<see cref="QgisBridgeToken"/>):
/// Loopback allein schuetzt nicht davor, dass ein anderes lokales Programm Projekt-
/// und Geodaten abruft.
/// </summary>
internal sealed class QgisBridgeServer : IDisposable
{
    private const int MaxConcurrentClients = 8;
    private readonly QgisBridgeRequestProcessor _processor;
    private readonly ILogger _logger;
    private readonly BoundedBackgroundTaskRunner _clientTasks;
    private readonly int _port;
    private readonly string _token;
    private readonly CancellationTokenSource _cts = new();
    private TcpListener? _listener;
    private Task? _loopTask;

    private QgisBridgeServer(QgisBridgeRequestProcessor processor, ILogger logger, int port, string token)
    {
        _processor = processor;
        _logger = logger;
        _clientTasks = new BoundedBackgroundTaskRunner(MaxConcurrentClients, logger);
        _port = port;
        _token = token;
    }

    public static QgisBridgeServer? TryStart(QgisBridgeRequestProcessor processor, ILogger logger)
    {
        if (string.Equals(Environment.GetEnvironmentVariable("SEWERSTUDIO_QGIS_BRIDGE"), "0", StringComparison.Ordinal))
            return null;

        var portText = Environment.GetEnvironmentVariable("SEWERSTUDIO_QGIS_BRIDGE_PORT");
        var port = int.TryParse(portText, out var parsed) && parsed is >= 1024 and <= 65535
            ? parsed
            : 8765;

        var server = new QgisBridgeServer(processor, logger, port, QgisBridgeToken.ResolveOrCreate(logger));
        try
        {
            server.Start();
            return server;
        }
        catch (SocketException ex)
        {
            logger.LogWarning(ex, "QGIS-Bridge konnte Port {Port} nicht oeffnen.", port);
            server.Dispose();
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "QGIS-Bridge konnte nicht gestartet werden.");
            server.Dispose();
            return null;
        }
    }

    private void Start()
    {
        _listener = new TcpListener(IPAddress.Loopback, _port);
        _listener.Start();
        _loopTask = Task.Run(() => AcceptLoopAsync(_cts.Token));
        _logger.LogInformation("SewerStudio QGIS-Bridge aktiv auf http://127.0.0.1:{Port}/qgis/status.json", _port);
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        if (_listener is null)
            return;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                if (!_clientTasks.TryRun(
                        () => HandleClientAsync(client, cancellationToken),
                        "QGIS-Bridge Request"))
                {
                    await LoopbackHttpServerSafety
                        .RejectBusyAsync(client, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                TryLogWarning(ex, "QGIS-Bridge Accept fehlgeschlagen.");
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using var _ = client;
        try
        {
            using var stream = client.GetStream();
            using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            requestTimeout.CancelAfter(LoopbackHttpServerSafety.RequestReadTimeout);
            var request = await ReadRequestAsync(stream, requestTimeout.Token).ConfigureAwait(false);
            if (request is null)
                return;

            var (method, path, token, body) = request.Value;
            QgisBridgeResponse response;
            if (method is not ("GET" or "HEAD" or "POST"))
            {
                response = new QgisBridgeResponse(
                    405,
                    "application/json; charset=utf-8",
                    JsonSerializer.SerializeToUtf8Bytes(new { ok = false, error = "Nur GET und POST sind erlaubt." }));
            }
            else if (!QgisBridgeToken.Matches(_token, token))
            {
                // Anmeldung ist Pflicht: sonst liest jedes lokale Programm Projekt- und Geodaten.
                response = new QgisBridgeResponse(
                    401,
                    "application/json; charset=utf-8",
                    JsonSerializer.SerializeToUtf8Bytes(new
                    {
                        ok = false,
                        error = "QGIS-Bridge-Token fehlt oder ist falsch.",
                        hinweis = $"Token aus der Datei {QgisBridgeToken.FileName} im SewerStudio-AppData-Ordner "
                                  + $"im Header {QgisBridgeToken.HeaderName} senden."
                    }));
            }
            else if (method == "POST")
            {
                response = await _processor.HandlePostAsync(path, body).ConfigureAwait(false);
            }
            else
            {
                response = await _processor.HandleAsync(path).ConfigureAwait(false);
            }

            await WriteResponseAsync(stream, response, includeBody: method != "HEAD", cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Normales Beenden des Servers.
        }
        catch (OperationCanceledException)
        {
            TryLogWarning(null, "QGIS-Bridge Request wegen Zeitueberschreitung beendet.");
        }
        catch (Exception ex)
        {
            TryLogWarning(ex, "QGIS-Bridge Request fehlgeschlagen.");
        }
    }

    /// <summary>
    /// Rumpfgrenze fuer POST. Ein Sprungauftrag ist ein Haltungsname und eine Zahl —
    /// mehr als 8 KiB kann kein ehrlicher Auftrag brauchen.
    /// </summary>
    private const int MaxBodyBytes = 8 * 1024;

    private static async Task<(string Method, string Path, string? Token, string? Body)?> ReadRequestAsync(
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        // Feste Grenzen fuer Anfragezeile und Kopfteil: Die Anmeldung wird erst danach
        // geprueft, also darf hier noch niemand beliebig viel Speicher binden.
        var begrenzt = new BoundedHttpRequestReader(reader);
        var requestLine = await begrenzt.ReadRequestLineAsync(cancellationToken).ConfigureAwait(false);
        if (requestLine is null)
            return null;

        var parts = requestLine.Split(' ', 3);
        if (parts.Length < 2)
            return null;

        var headerLines = await begrenzt.ReadHeaderLinesAsync(cancellationToken).ConfigureAwait(false);
        if (headerLines is null)
            return null;

        string? token = null;
        var contentLength = 0;
        foreach (var line in headerLines)
        {
            var separator = line.IndexOf(':');
            if (separator <= 0)
                continue;

            var name = line[..separator].Trim();
            if (string.Equals(name, QgisBridgeToken.HeaderName, StringComparison.OrdinalIgnoreCase))
                token = line[(separator + 1)..].Trim();
            else if (string.Equals(name, "Content-Length", StringComparison.OrdinalIgnoreCase)
                     && !int.TryParse(line[(separator + 1)..].Trim(), out contentLength))
                return null; // Angekuendigte Laenge unlesbar: nichts raten.
        }

        var method = parts[0].ToUpperInvariant();
        string? body = null;
        if (method == "POST")
        {
            body = await begrenzt.ReadBodyAsync(contentLength, MaxBodyBytes, cancellationToken).ConfigureAwait(false);
            if (body is null)
                return null; // Rumpf zu gross.
        }

        var path = parts[1];
        var queryIndex = path.IndexOf('?');
        if (queryIndex >= 0)
            path = path[..queryIndex];

        return (method, path, token, body);
    }

    private static async Task WriteResponseAsync(
        NetworkStream stream,
        QgisBridgeResponse response,
        bool includeBody,
        CancellationToken cancellationToken)
    {
        var header =
            $"HTTP/1.1 {response.StatusCode} {ReasonPhrase(response.StatusCode)}\r\n" +
            $"Content-Type: {response.ContentType}\r\n" +
            $"Content-Length: {response.Body.Length}\r\n" +
            "Cache-Control: no-store\r\n" +
            "Connection: close\r\n\r\n";

        await stream.WriteAsync(Encoding.ASCII.GetBytes(header), cancellationToken).ConfigureAwait(false);
        if (includeBody)
            await stream.WriteAsync(response.Body, cancellationToken).ConfigureAwait(false);
    }

    private static string ReasonPhrase(int statusCode)
        => statusCode switch
        {
            200 => "OK",
            401 => "Unauthorized",
            404 => "Not Found",
            405 => "Method Not Allowed",
            503 => "Service Unavailable",
            500 => "Internal Server Error",
            _ => "OK"
        };

    public void Dispose()
    {
        _cts.Cancel();
        try { _listener?.Stop(); }
        catch (Exception ex) { TryLogWarning(ex, "QGIS-Bridge-Listener konnte beim Beenden nicht gestoppt werden."); }
        try { _loopTask?.Wait(TimeSpan.FromSeconds(1)); }
        catch (Exception ex) { TryLogWarning(ex, "QGIS-Bridge-Serverloop konnte beim Beenden nicht abgewartet werden."); }
        try { _clientTasks.WaitForIdleAsync().Wait(TimeSpan.FromSeconds(1)); }
        catch (Exception ex) { TryLogWarning(ex, "QGIS-Bridge-Clients konnten beim Beenden nicht abgewartet werden."); }
        _cts.Dispose();
    }

    private void TryLogWarning(Exception? exception, string message)
    {
        try { _logger.LogWarning(exception, "{Message}", message); }
        catch
        {
            // Ein Logfehler darf weder Listener noch Client-Behandlung beenden.
        }
    }
}
