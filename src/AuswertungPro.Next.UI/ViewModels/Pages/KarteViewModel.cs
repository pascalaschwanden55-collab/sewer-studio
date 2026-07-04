using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Map;
using AuswertungPro.Next.UI.Player;
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
    private readonly ServiceProvider _services;

    private string XtfPath => _services.Settings.AbwasserkatasterXtfPath;

    // Lokale QGIS-Kacheln (von qgis_process erzeugt). Vorhanden = werden als Hintergrund
    // ueber dem WMS gezeigt; fehlt der Ordner, bleibt es beim WMS allein.
    private string QgisTilesPath => _services.Settings.QgisTilesPath;

    // Skalierung: false = VSA-Skala (0=gut); true = EZ-Skala (0=schlecht/4=gut)
    private bool _invertiert = true;

    private IReadOnlyList<ProjectedHaltungGeometry> _projectedGeometrien = Array.Empty<ProjectedHaltungGeometry>();
    private IReadOnlyDictionary<string, int?> _kondition = new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase);
    private MemoryLayer? _netzLayer;
    private Map? _map;
    private MapBounds? _loadedBounds;

    [ObservableProperty] private string _statusText = "Karte wird geladen…";
    [ObservableProperty] private string? _selectedHaltungsname;
    [ObservableProperty] private bool _hasNetworkGeometry;

    /// <summary>Infopanel-Daten der angeklickten Haltung; null = Panel zu.</summary>
    [ObservableProperty] private KarteHaltungInfo? _selectedInfo;

    public IRelayCommand OpenInspektionCommand { get; }
    public IRelayCommand OpenDetailCommand { get; }
    public IRelayCommand SchliesseInfoCommand { get; }

    public KarteViewModel(ShellViewModel shell, ServiceProvider services)
    {
        _shell = shell;
        _services = services;
        OpenInspektionCommand = new RelayCommand(OpenInspektion);
        OpenDetailCommand = new RelayCommand(OpenDetail);
        SchliesseInfoCommand = new RelayCommand(() => SelectedInfo = null);
    }

    /// <summary>
    /// Baut die Mapsui-Karte (WMS + Netzlinien) asynchron auf.
    /// Wird aus dem Code-Behind nach dem Loaded-Event aufgerufen.
    /// </summary>
    public async Task<Map> BuildMapAsync()
    {
        HasNetworkGeometry = false;
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
            if (!string.IsNullOrWhiteSpace(QgisTilesPath) && Directory.Exists(QgisTilesPath))
            {
                var tileSource = new AuswertungPro.Next.UI.Mapping.LocalXyzTileSource(QgisTilesPath, "QGIS");
                map.Layers.Add(new Mapsui.Tiling.Layers.TileLayer(tileSource) { Name = "QGIS" });
            }
        }
        catch (Exception ex)
        {
            StatusText = $"QGIS-Kacheln nicht ladbar: {ex.Message}";
        }

        // ── Netz-Geometrie laden ──────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(XtfPath) || !File.Exists(XtfPath))
        {
            StatusText = $"Netz-Datei nicht gefunden: {XtfPath}";
        }
        else
        {
            try
            {
                // XTF-Parsing im Hintergrundthread (kann groß sein)
                var geometrien = await Task.Run(() => new NetworkGeometryCache().Load(XtfPath));
                _projectedGeometrien = await Task.Run(() => NetworkViewportFilter.Project(geometrien));
                HasNetworkGeometry = _projectedGeometrien.Count > 0;

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
                {
                    SelectedHaltungsname = name;
                    // Infopanel mit den Stammdaten der Haltung fuellen (null = nicht im Projekt).
                    SelectedInfo = KarteHaltungInfoBuilder.Build(FindeRecord(name));
                }
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

    public void ZoomToNetworkAndRefresh()
    {
        if (_map is null || _projectedGeometrien.Count == 0)
        {
            CenterOnUriAndRefresh();
            return;
        }

        var points = _projectedGeometrien.SelectMany(g => g.Points).ToList();
        if (points.Count == 0)
        {
            CenterOnUriAndRefresh();
            return;
        }

        var minX = points.Min(p => p.X);
        var minY = points.Min(p => p.Y);
        var maxX = points.Max(p => p.X);
        var maxY = points.Max(p => p.Y);
        var bounds = new MRect(minX, minY, maxX, maxY).Grow(120);

        _map.Navigator.ZoomToBox(bounds, MBoxFit.Fit, 300);
        RefreshVisibleNetworkLayer(force: true);
        _map.RefreshGraphics();
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

        // Farben spiegeln die Theme-Severity-Brushes (Severity1/3/5, Muted),
        // damit Netzlinien und Kartenlegende (KartePage.xaml) dieselbe Farbsprache
        // nutzen. Feste Hex-Werte, weil Mapsui nicht theme-abhaengig ist.
        var color = farbe switch
        {
            ZustandFarbe.Gut => new Color(22, 163, 74),      // Severity1 #16A34A
            ZustandFarbe.Mittel => new Color(245, 158, 11),  // Severity3 #F59E0B
            ZustandFarbe.Schlecht => new Color(220, 38, 38), // Severity5 #DC2626
            _ => new Color(61, 77, 99),                      // MutedBrush #3D4D63
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

    private HaltungRecord? FindeRecord(string? haltungsname)
        => string.IsNullOrWhiteSpace(haltungsname)
            ? null
            : _shell.Project.Data.FirstOrDefault(r => string.Equals(
                r.GetFieldValue("Haltungsname"),
                haltungsname,
                StringComparison.OrdinalIgnoreCase));

    private void OpenInspektion()
    {
        if (string.IsNullOrWhiteSpace(SelectedHaltungsname))
        {
            StatusText = "Keine Haltung ausgewählt.";
            return;
        }

        var record = FindeRecord(SelectedHaltungsname);
        if (record is null)
        {
            StatusText = $"Haltung '{SelectedHaltungsname}' nicht im Projekt gefunden.";
            return;
        }

        OpenInspektionForRecord(record);
    }

    /// <summary>Springt zur Haltungen-Seite und selektiert die angeklickte Haltung.</summary>
    private void OpenDetail()
    {
        var record = FindeRecord(SelectedHaltungsname);
        if (record is null)
        {
            StatusText = $"Haltung '{SelectedHaltungsname}' nicht im Projekt gefunden.";
            return;
        }

        _shell.NavigateTo("Haltungen");
        if (_shell.CurrentPage is DataPageViewModel dataPage)
            dataPage.Selected = record;
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
            var options = PlayerWindowOptions.FromSettings(_services.Settings);

            var window = new Views.Windows.PlayerWindow(
                resolved,
                options,
                serviceProvider: _services,
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
