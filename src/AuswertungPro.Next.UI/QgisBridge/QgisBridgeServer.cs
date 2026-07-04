using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.QgisBridge;

internal sealed class QgisBridgeServer : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly Window _mainWindow;
    private readonly Dispatcher _dispatcher;
    private readonly ILogger _logger;
    private readonly QgisBridgeSnapshotBuilder _builder;
    private readonly int _port;
    private readonly CancellationTokenSource _cts = new();
    private TcpListener? _listener;
    private Task? _loopTask;

    private QgisBridgeServer(Window mainWindow, ServiceProvider services, ILogger logger, int port)
    {
        _mainWindow = mainWindow;
        _dispatcher = mainWindow.Dispatcher;
        _logger = logger;
        _port = port;
        _builder = new QgisBridgeSnapshotBuilder(services.Settings);
    }

    public static QgisBridgeServer? TryStart(Window mainWindow, ServiceProvider services, ILogger logger)
    {
        if (string.Equals(Environment.GetEnvironmentVariable("SEWERSTUDIO_QGIS_BRIDGE"), "0", StringComparison.Ordinal))
            return null;

        var portText = Environment.GetEnvironmentVariable("SEWERSTUDIO_QGIS_BRIDGE_PORT");
        var port = int.TryParse(portText, out var parsed) && parsed is >= 1024 and <= 65535
            ? parsed
            : 8765;

        var server = new QgisBridgeServer(mainWindow, services, logger, port);
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
                _ = Task.Run(() => HandleClientAsync(client, cancellationToken), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "QGIS-Bridge Accept fehlgeschlagen.");
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using var _ = client;
        try
        {
            using var stream = client.GetStream();
            var request = await ReadRequestAsync(stream, cancellationToken).ConfigureAwait(false);
            if (request is null)
                return;

            var response = await DispatchAsync(request.Value).ConfigureAwait(false);
            await WriteJsonResponseAsync(stream, response, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "QGIS-Bridge Request fehlgeschlagen.");
        }
    }

    private static async Task<QgisHttpRequest?> ReadRequestAsync(NetworkStream stream, CancellationToken cancellationToken)
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

        return new QgisHttpRequest(parts[0].ToUpperInvariant(), path);
    }

    private async Task<QgisHttpResponse> DispatchAsync(QgisHttpRequest request)
    {
        if (request.Method is not ("GET" or "HEAD"))
        {
            return Json(405, new { ok = false, error = "Nur GET ist erlaubt." });
        }

        try
        {
            return await _dispatcher.InvokeAsync(() => DispatchOnUiThread(request.Path)).Task.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "QGIS-Bridge Payload konnte nicht erstellt werden.");
            return Json(500, new { ok = false, error = ex.Message });
        }
    }

    private QgisHttpResponse DispatchOnUiThread(string path)
    {
        var shell = _mainWindow.DataContext as ShellViewModel;
        var project = shell?.Project ?? new Project { Name = "" };
        var currentHolding = ResolveCurrentHolding(shell);

        return path switch
        {
            "/" or "/qgis" or "/qgis/status.json" => Json(200, _builder.BuildStatus(project, currentHolding)),
            "/qgis/current.geojson" => GeoJson(200, _builder.BuildCurrentGeoJson(project, currentHolding)),
            "/qgis/damages.geojson" => GeoJson(200, _builder.BuildDamagesGeoJson(project)),
            "/qgis/network.geojson" => GeoJson(200, _builder.BuildNetworkGeoJson(project)),
            _ => Json(404, new { ok = false, error = "Unbekannter QGIS-Bridge-Endpunkt." })
        };
    }

    private static string ResolveCurrentHolding(ShellViewModel? shell)
    {
        if (shell?.CurrentPage is DataPageViewModel dataPage)
            return NormalizeHolding(dataPage.Selected?.GetFieldValue("Haltungsname"));

        if (shell?.CurrentPage is KarteViewModel mapPage)
            return NormalizeHolding(mapPage.SelectedHaltungsname);

        return NormalizeHolding(TryReadHoldingFromPage(shell?.CurrentPage));
    }

    private static string? TryReadHoldingFromPage(object? page)
    {
        if (page is null)
            return null;

        foreach (var propertyName in new[] { "Selected", "SelectedRecord", "SelectedHaltung", "SelectedHolding", "SelectedHaltungsname" })
        {
            var property = page.GetType().GetProperty(propertyName);
            if (property is null)
                continue;

            var value = property.GetValue(page);
            if (value is HaltungRecord record)
                return record.GetFieldValue("Haltungsname");

            if (value is string text)
                return text;
        }

        return null;
    }

    private static string NormalizeHolding(string? value)
        => value?.Trim() ?? string.Empty;

    private static QgisHttpResponse Json(int statusCode, object payload)
        => new(statusCode, "application/json; charset=utf-8", payload);

    private static QgisHttpResponse GeoJson(int statusCode, object payload)
        => new(statusCode, "application/geo+json; charset=utf-8", payload);

    private static async Task WriteJsonResponseAsync(
        NetworkStream stream,
        QgisHttpResponse response,
        CancellationToken cancellationToken)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(response.Payload, JsonOptions);
        var header =
            $"HTTP/1.1 {response.StatusCode} {ReasonPhrase(response.StatusCode)}\r\n" +
            $"Content-Type: {response.ContentType}\r\n" +
            $"Content-Length: {body.Length}\r\n" +
            "Cache-Control: no-store\r\n" +
            "Access-Control-Allow-Origin: *\r\n" +
            "Connection: close\r\n\r\n";

        var headerBytes = Encoding.ASCII.GetBytes(header);
        await stream.WriteAsync(headerBytes, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(body, cancellationToken).ConfigureAwait(false);
    }

    private static string ReasonPhrase(int statusCode)
        => statusCode switch
        {
            200 => "OK",
            404 => "Not Found",
            405 => "Method Not Allowed",
            500 => "Internal Server Error",
            _ => "OK"
        };

    public void Dispose()
    {
        _cts.Cancel();
        try { _listener?.Stop(); } catch { }
        try { _loopTask?.Wait(TimeSpan.FromSeconds(1)); } catch { }
        _cts.Dispose();
    }

    private readonly record struct QgisHttpRequest(string Method, string Path);
    private sealed record QgisHttpResponse(int StatusCode, string ContentType, object Payload);
}
