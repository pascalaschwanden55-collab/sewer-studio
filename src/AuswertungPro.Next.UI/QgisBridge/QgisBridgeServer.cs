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
/// Bewusste Einzelplatz-Grenze: nur IPv4-Loopback und nur GET/HEAD. Die reine
/// Lese-Bridge verwendet kein Token; fuer Mehrbenutzer-Systeme muss sie deaktiviert
/// oder vor dem Einsatz um eine gemeinsame Authentifizierung erweitert werden.
/// </summary>
internal sealed class QgisBridgeServer : IDisposable
{
    private const int MaxConcurrentClients = 8;
    private readonly QgisBridgeRequestProcessor _processor;
    private readonly ILogger _logger;
    private readonly BoundedBackgroundTaskRunner _clientTasks;
    private readonly int _port;
    private readonly CancellationTokenSource _cts = new();
    private TcpListener? _listener;
    private Task? _loopTask;

    private QgisBridgeServer(QgisBridgeRequestProcessor processor, ILogger logger, int port)
    {
        _processor = processor;
        _logger = logger;
        _clientTasks = new BoundedBackgroundTaskRunner(MaxConcurrentClients, logger);
        _port = port;
    }

    public static QgisBridgeServer? TryStart(QgisBridgeRequestProcessor processor, ILogger logger)
    {
        if (string.Equals(Environment.GetEnvironmentVariable("SEWERSTUDIO_QGIS_BRIDGE"), "0", StringComparison.Ordinal))
            return null;

        var portText = Environment.GetEnvironmentVariable("SEWERSTUDIO_QGIS_BRIDGE_PORT");
        var port = int.TryParse(portText, out var parsed) && parsed is >= 1024 and <= 65535
            ? parsed
            : 8765;

        var server = new QgisBridgeServer(processor, logger, port);
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

            var (method, path) = request.Value;
            QgisBridgeResponse response;
            if (method is not ("GET" or "HEAD"))
            {
                response = new QgisBridgeResponse(
                    405,
                    "application/json; charset=utf-8",
                    JsonSerializer.SerializeToUtf8Bytes(new { ok = false, error = "Nur GET ist erlaubt." }));
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

    private static async Task<(string Method, string Path)?> ReadRequestAsync(
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var requestLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(requestLine))
            return null;

        var parts = requestLine.Split(' ', 3);
        if (parts.Length < 2)
            return null;

        string? line;
        while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)))
        {
            // Header werden fuer die reine Lese-Bridge nicht benoetigt.
        }

        var path = parts[1];
        var queryIndex = path.IndexOf('?');
        if (queryIndex >= 0)
            path = path[..queryIndex];

        return (parts[0].ToUpperInvariant(), path);
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
            404 => "Not Found",
            405 => "Method Not Allowed",
            503 => "Service Unavailable",
            500 => "Internal Server Error",
            _ => "OK"
        };

    public void Dispose()
    {
        _cts.Cancel();
        try { _listener?.Stop(); } catch { }
        try { _loopTask?.Wait(TimeSpan.FromSeconds(1)); } catch { }
        try { _clientTasks.WaitForIdleAsync().Wait(TimeSpan.FromSeconds(1)); } catch { }
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
