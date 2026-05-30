using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Map;
using Mapsui;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Providers.Wms;
using Mapsui.Styles;
using NetTopologySuite.Geometries;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class KarteViewModel : ObservableObject
{
    private const double ViewportPaddingRatio = 0.50;

    private readonly ShellViewModel _shell;

    // Pfad zur Netz-XTF; kein Settings-Eintrag vorhanden → Konstante.
    // Korrigierte Fassung: vollstaendigerer Netzplan (110'224 Haltungen statt 94'109).
    private readonly string _xtfPath = @"D:\QGIS_V4\Export_Sewer_Studio\Abwasserkataster_Uri_korrigiert.xtf";

    // Lokale QGIS-Kacheln (von qgis_process erzeugt). Vorhanden = werden als Hintergrund
    // ueber dem WMS gezeigt; fehlt der Ordner, bleibt es beim WMS allein.
    private readonly string _qgisTilesPath = @"D:\QGIS_V4\Export_Sewer_Studio\tiles_test";

    // Skalierung: false = VSA-Skala (0=gut); true = EZ-Skala (0=schlecht/4=gut)
    private bool _invertiert = true;

    private IReadOnlyList<ProjectedHaltungGeometry> _projectedGeometrien = Array.Empty<ProjectedHaltungGeometry>();
    private IReadOnlyDictionary<string, int?> _kondition = new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase);
    private MemoryLayer? _netzLayer;
    private Map? _map;
    private MapBounds? _loadedBounds;

    [ObservableProperty] private string _statusText = "Karte wird geladen…";
    [ObservableProperty] private string? _selectedHaltungsname;

    public IRelayCommand OpenInspektionCommand { get; }

    public KarteViewModel(ShellViewModel shell)
    {
        _shell = shell;
        OpenInspektionCommand = new RelayCommand(OpenInspektion);
    }

    /// <summary>
    /// Baut die Mapsui-Karte (WMS + Netzlinien) asynchron auf.
    /// Wird aus dem Code-Behind nach dem Loaded-Event aufgerufen.
    /// </summary>
    public async Task<Map> BuildMapAsync()
    {
        var map = new Map();

        // ── WMS-Hintergrundlayer ──────────────────────────────────────────────
        try
        {
            var provider = await WmsProvider.CreateAsync("https://geo.ur.ch/wms");
            provider.ContinueOnError = true;
            provider.TimeOut = 20000;
            provider.CRS = "EPSG:3857";
            provider.AddLayer("basemaps:basemap_av_farbe");
            provider.SetImageFormat("image/png");
            var wmsLayer = new ImageLayer("WMS") { DataSource = provider, Style = new RasterStyle() };
            map.Layers.Add(wmsLayer);
        }
        catch (Exception ex)
        {
            // WMS nicht verfügbar → trotzdem Netzlinien anzeigen
            StatusText = $"WMS nicht verfügbar: {ex.Message}";
        }

        // ── QGIS-Kachel-Hintergrund (lokal, falls vorhanden) ─────────────────
        // QGIS-Optik als XYZ-Kacheln ueber dem WMS; ausserhalb des Exports leer.
        try
        {
            if (Directory.Exists(_qgisTilesPath))
            {
                var tileSource = new AuswertungPro.Next.UI.Mapping.LocalXyzTileSource(_qgisTilesPath, "QGIS");
                map.Layers.Add(new Mapsui.Tiling.Layers.TileLayer(tileSource) { Name = "QGIS" });
            }
        }
        catch (Exception ex)
        {
            StatusText = $"QGIS-Kacheln nicht ladbar: {ex.Message}";
        }

        // ── Netz-Geometrie laden ──────────────────────────────────────────────
        if (!File.Exists(_xtfPath))
        {
            StatusText = $"Netz-Datei nicht gefunden: {_xtfPath}";
        }
        else
        {
            try
            {
                // XTF-Parsing im Hintergrundthread (kann groß sein)
                var geometrien = await Task.Run(() => new NetworkGeometryCache().Load(_xtfPath));
                _projectedGeometrien = await Task.Run(() => NetworkViewportFilter.Project(geometrien));

                // Zustandsfarben aus dem aktuellen Projekt
                _kondition = HaltungConditionProvider.Build(_shell.Project.Data);

                _netzLayer = new MemoryLayer("Netz") { Features = Array.Empty<GeometryFeature>(), Style = null };
                map.Layers.Add(_netzLayer);

                StatusText = $"{_projectedGeometrien.Count} Haltungen im Cache geladen";
            }
            catch (Exception ex)
            {
                StatusText = $"Fehler beim Laden der Netz-Datei: {ex.Message}";
            }
        }

        // ── Klick-Handler: Haltungsname setzen ───────────────────────────────
        // Hinweis: CenterOnAndZoomTo wird NICHT hier aufgerufen, weil der
        // MapControl zu diesem Zeitpunkt noch keinen gültigen Viewport hat.
        // CenterOnUriAndRefresh() wird stattdessen aus dem Code-Behind aufgerufen,
        // sobald der MapControl eine echte Größe besitzt (SizeChanged-Einmal-Handler).
        _map = map;
        map.Navigator.FetchRequested += (_, _) => RefreshVisibleNetworkLayer(force: false);

        if (_netzLayer is not null)
        {
            var capturedLayer = _netzLayer;
            map.Tapped += (_, e) =>
            {
                var mi = e.GetMapInfo(new[] { capturedLayer });
                if (mi?.Feature is GeometryFeature gf && gf["Haltungsname"] is string name)
                    SelectedHaltungsname = name;
            };
        }

        return map;
    }

    /// <summary>
    /// Zentriert die Karte auf Uri/Altdorf und lädt die sichtbaren Netzlinien.
    /// Wird aus dem Code-Behind aufgerufen, sobald der MapControl eine gültige Größe hat.
    /// </summary>
    public void CenterOnUriAndRefresh()
    {
        // WebMercator-Koordinaten Uri/Altdorf; Zoom-Level 14 ≈ 9.55 m/px
        _map?.Navigator.CenterOnAndZoomTo(new MPoint(960296, 5925558), 9.55);
        RefreshVisibleNetworkLayer(force: true);
    }

    public void RefreshVisibleNetworkLayer(bool force)
    {
        if (_map is null || _netzLayer is null || _projectedGeometrien.Count == 0)
            return;

        var viewport = TryGetViewportBounds(_map);
        if (viewport is null)
            return;

        if (!force && _loadedBounds is { } loadedBounds && loadedBounds.Contains(viewport.Value))
            return;

        var paddedViewport = GrowByRatio(viewport.Value, ViewportPaddingRatio);
        var visibleGeometrien = NetworkViewportFilter.FilterByViewport(_projectedGeometrien, paddedViewport);
        var features = visibleGeometrien.Select(CreateFeature).ToList();

        _netzLayer.Features = features;
        _netzLayer.DataHasChanged();
        _loadedBounds = paddedViewport;

        StatusText = $"{features.Count} von {_projectedGeometrien.Count} Haltungen im sichtbaren Ausschnitt";
        _map.RefreshGraphics();
    }

    private GeometryFeature CreateFeature(ProjectedHaltungGeometry hg)
    {
        var coords = hg.Points.Select(p => new Coordinate(p.X, p.Y)).ToArray();

        var farbe = ZustandColorMapper.Map(
            _kondition.TryGetValue(hg.Haltungsname, out var k) ? k : null,
            _invertiert);

        var color = farbe switch
        {
            ZustandFarbe.Gut => Color.Green,
            ZustandFarbe.Mittel => Color.Orange,
            ZustandFarbe.Schlecht => Color.Red,
            _ => Color.Gray,
        };

        var feature = new GeometryFeature { Geometry = new LineString(coords) };
        feature["Haltungsname"] = hg.Haltungsname;
        feature.Styles.Add(new VectorStyle { Line = new Pen(color, 4) });
        return feature;
    }

    private static MapBounds? TryGetViewportBounds(Map map)
    {
        var viewport = map.Navigator.Viewport;
        if (viewport.Width <= 0 || viewport.Height <= 0 || viewport.Resolution <= 0)
            return null;

        var extent = viewport.ToExtent();
        if (!double.IsFinite(extent.MinX)
            || !double.IsFinite(extent.MinY)
            || !double.IsFinite(extent.MaxX)
            || !double.IsFinite(extent.MaxY))
        {
            return null;
        }

        return new MapBounds(extent.MinX, extent.MinY, extent.MaxX, extent.MaxY);
    }

    private static MapBounds GrowByRatio(MapBounds bounds, double ratio)
    {
        var marginX = (bounds.MaxX - bounds.MinX) * ratio;
        var marginY = (bounds.MaxY - bounds.MinY) * ratio;
        return bounds.Grow(marginX, marginY);
    }

    private void OpenInspektion()
    {
        if (string.IsNullOrWhiteSpace(SelectedHaltungsname))
        {
            StatusText = "Keine Haltung ausgewählt.";
            return;
        }

        var record = _shell.Project.Data
            .FirstOrDefault(r => string.Equals(
                r.GetFieldValue("Haltungsname"),
                SelectedHaltungsname,
                StringComparison.OrdinalIgnoreCase));

        if (record is null)
        {
            StatusText = $"Haltung '{SelectedHaltungsname}' nicht im Projekt gefunden.";
            return;
        }

        OpenInspektionForRecord(record);
    }

    private void OpenInspektionForRecord(HaltungRecord record)
    {
        var videoLink = record.GetFieldValue("Link");
        var resolved = ResolveExistingPath(videoLink);

        if (string.IsNullOrWhiteSpace(resolved))
        {
            StatusText = $"Kein Video für '{SelectedHaltungsname}' verknüpft.";
            return;
        }

        try
        {
            var sp = (ServiceProvider)App.Services;
            var options = new Views.Windows.PlayerWindowOptions(
                EnableHardwareDecoding: sp.Settings.VideoHwDecoding,
                DropLateFrames: sp.Settings.VideoDropLateFrames,
                SkipFrames: sp.Settings.VideoSkipFrames,
                FileCachingMs: sp.Settings.VideoFileCachingMs,
                NetworkCachingMs: sp.Settings.VideoNetworkCachingMs,
                CodecThreads: sp.Settings.VideoCodecThreads,
                VideoOutput: sp.Settings.VideoOutput);

            var window = new Views.Windows.PlayerWindow(
                resolved,
                options,
                serviceProvider: sp,
                haltungId: record.Id.ToString(),
                haltungRecord: record)
            {
                Owner = System.Windows.Application.Current?.MainWindow
            };
            window.Show();
        }
        catch (Exception ex)
        {
            StatusText = $"Video-Start fehlgeschlagen: {ex.Message}";
        }
    }

    /// <summary>Löst einen Pfad auf und gibt null zurück, wenn die Datei nicht existiert.</summary>
    private static string? ResolveExistingPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;
        var trimmed = path.Trim();
        return File.Exists(trimmed) ? trimmed : null;
    }
}
