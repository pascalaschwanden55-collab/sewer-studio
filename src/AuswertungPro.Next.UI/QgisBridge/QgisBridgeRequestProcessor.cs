using AuswertungPro.Next.Application.Map;
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
    private readonly System.Windows.Application _app;
    private readonly ILogger _logger;
    private readonly QgisBridgeEndpointRouter _router;

    public QgisBridgeRequestProcessor(
        System.Windows.Application app,
        AppSettings settings,
        ILogger logger,
        IKatasterXtfPathResolver? katasterXtfPaths = null)
    {
        _app = app;
        _logger = logger;
        _router = new QgisBridgeEndpointRouter(
            new QgisBridgeSnapshotBuilder(settings, katasterXtfPaths: katasterXtfPaths));
    }

    /// <summary>Pfade, die die QGIS-Bridge beantwortet (inkl. Status unter "/").</summary>
    public static bool IsBridgePath(string path)
        => path is "/" or "/qgis" or "/qgis/"
           || path.StartsWith("/qgis/", StringComparison.Ordinal);

    public async Task<QgisBridgeResponse> HandleAsync(string path)
    {
        try
        {
            var snapshot = await CaptureSnapshotAsync().ConfigureAwait(false);
            return await Task.Run(() => _router.Route(path, snapshot)).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "QGIS-Bridge Payload fehlgeschlagen fuer {Path}.", path);
            return QgisBridgeEndpointRouter.Error(500, ex.Message);
        }
    }

    private Task<QgisProjectSnapshot> CaptureSnapshotAsync()
        => _app.Dispatcher.InvokeAsync(() =>
        {
            var shell = _app.MainWindow?.DataContext as ShellViewModel;
            var project = shell?.Project;
            var current = project is null ? "" : QgisBridgeSelection.CurrentFor(project.Id);
            var currentSchacht = project is null ? "" : QgisBridgeSelection.CurrentSchachtFor(project.Id);
            return QgisProjectSnapshot.Capture(
                project, current, QgisBridgeSelection.Stamp,
                currentSchacht, QgisBridgeSelection.SchachtStamp);
        }).Task;

}

/// <summary>Fertige HTTP-Antwort der QGIS-Bridge (Body bereits serialisiert).</summary>
internal sealed record QgisBridgeResponse(int StatusCode, string ContentType, byte[] Body);
