using System.Text.Json;
using AuswertungPro.Next.UI.ViewModels;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.QgisBridge;

/// <summary>
/// Beantwortet QGIS-Bridge-Anfragen (/qgis/*) unabhaengig vom HTTP-Host.
/// Ablauf pro Anfrage: billiger Projekt-Snapshot auf dem UI-Thread, danach
/// XTF-Laden und GeoJSON-Bau im Hintergrund — so friert die App nie ein,
/// auch nicht beim ersten Poll (XTF-Parse kann Sekunden dauern).
/// Wird sowohl vom eigenstaendigen <see cref="QgisBridgeServer"/> als auch vom
/// LiveControlServer genutzt, damit die Endpunkte auf Port 8765 auch dann
/// funktionieren, wenn Live-Control den Port bereits haelt.
/// </summary>
internal sealed class QgisBridgeRequestProcessor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly System.Windows.Application _app;
    private readonly ILogger _logger;
    private readonly QgisBridgeSnapshotBuilder _builder;

    public QgisBridgeRequestProcessor(System.Windows.Application app, AppSettings settings, ILogger logger)
    {
        _app = app;
        _logger = logger;
        _builder = new QgisBridgeSnapshotBuilder(settings);
    }

    /// <summary>Pfade, die die QGIS-Bridge beantwortet (inkl. Status unter "/").</summary>
    public static bool IsBridgePath(string path)
        => path is "/" or "/qgis" or "/qgis/"
           || path.StartsWith("/qgis/", StringComparison.Ordinal);

    public async Task<QgisBridgeResponse> HandleAsync(string path)
    {
        // Query-String abschneiden (der Live-Control-Host reicht Pfade ungefiltert durch).
        var queryIndex = path.IndexOf('?');
        if (queryIndex >= 0)
            path = path[..queryIndex];

        try
        {
            var snapshot = await CaptureSnapshotAsync().ConfigureAwait(false);
            return await Task.Run(() => BuildResponse(path, snapshot)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "QGIS-Bridge Payload fehlgeschlagen fuer {Path}.", path);
            return Error(500, ex.Message);
        }
    }

    private Task<QgisProjectSnapshot> CaptureSnapshotAsync()
        => _app.Dispatcher.InvokeAsync(() =>
        {
            var shell = _app.MainWindow?.DataContext as ShellViewModel;
            var project = shell?.Project;
            var current = project is null ? "" : QgisBridgeSelection.CurrentFor(project.Id);
            return QgisProjectSnapshot.Capture(project, current);
        }).Task;

    private QgisBridgeResponse BuildResponse(string path, QgisProjectSnapshot snapshot)
        => path switch
        {
            "/" or "/qgis" or "/qgis/" or "/qgis/status.json"
                => Json(200, _builder.BuildStatus(snapshot)),
            "/qgis/current.geojson" => GeoJson(_builder.BuildCurrentGeoJson(snapshot)),
            "/qgis/damages.geojson" => GeoJson(_builder.BuildDamagesGeoJson(snapshot)),
            "/qgis/network.geojson" => GeoJson(_builder.BuildNetworkGeoJson(snapshot)),
            _ => Error(404, "Unbekannter QGIS-Bridge-Endpunkt.")
        };

    private static QgisBridgeResponse Json(int statusCode, object payload)
        => new(statusCode, "application/json; charset=utf-8",
            JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions));

    private static QgisBridgeResponse GeoJson(object payload)
        => new(200, "application/geo+json; charset=utf-8",
            JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions));

    private static QgisBridgeResponse Error(int statusCode, string message)
        => Json(statusCode, new { ok = false, error = message });
}

/// <summary>Fertige HTTP-Antwort der QGIS-Bridge (Body bereits serialisiert).</summary>
internal sealed record QgisBridgeResponse(int StatusCode, string ContentType, byte[] Body);
