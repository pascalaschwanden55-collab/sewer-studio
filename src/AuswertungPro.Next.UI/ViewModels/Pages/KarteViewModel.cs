using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using System.Windows.Threading;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Application.Map;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Map;
using AuswertungPro.Next.UI.Mapping;
using AuswertungPro.Next.UI.Services;
using Mapsui;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Styles;
using NetTopologySuite.Geometries;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class KarteViewModel : ObservableObject
{
    private const double ViewportPaddingRatio = 0.50;

    // Obergrenze gleichzeitig gezeichneter Netzlinien. Darueber wird ausgeduennt (NetzLevelOfDetail),
    // damit Mapsui beim Zeichnen nicht den UI-Thread blockiert. Ganz Uri hat ~110'000 Haltungen.
    private const int MaxRenderFeatures = 8000;

    private readonly ShellViewModel _shell;
    private readonly AppSettings _settings;
    private readonly NetworkFeatureCache _networkFeatures;
    private readonly Action<string, HaltungRecord> _playVideo;
    private readonly IInspectionProtocolFileLocator _inspectionProtocolFiles;
    private readonly IKatasterXtfPathResolver _katasterXtfPaths;
    private readonly IOfflineBasemapPathResolver _offlineBasemapPaths;
    private readonly IKarteBasemapLayerFactory _basemapLayers;

    private string XtfPath => _katasterXtfPaths.Resolve(
        _settings.AbwasserkatasterXtfPath,
        _settings.KantonUriXtfDirectory);

    // Offline-Hintergrundkarten (Satellit/AV im Programmordner). Resolver toleriert einen
    // veralteten gespeicherten Pfad (z.B. "...\basemap_tiles\uri") und nimmt dann den
    // Elternordner, der die Unterordner satellit/av enthaelt.
    private string? OfflineBasemapPath => _offlineBasemapPaths.Resolve(_settings.OfflineBasemapPath);

    // Skalierung: false = VSA-Skala (0=gut); true = EZ-Skala (0=schlecht/4=gut)
    private bool _invertiert = true;

    // Zustandsfarben je Haltungsname aus dem aktuellen Projekt (fuers Feature-Bauen im Cache).
    private IReadOnlyDictionary<string, int?> _kondition = new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase);
    // Nennweiten je Haltungsname (Linienbreite nach DN).
    private IReadOnlyDictionary<string, int?> _dnByName = new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase);
    private MemoryLayer? _netzLayer;
    private MemoryLayer? _detailLayer;      // Labels + Fliessrichtungs-Pfeile (nur Detail-Zoom)
    private MemoryLayer? _schachtLayer;
    private MemoryLayer? _hoverLayer;       // Halo unter dem Mauszeiger
    private MemoryLayer? _schadenLayer;     // Schadenspunkte der gewaehlten Haltung
    private MemoryLayer? _highlightLayer;   // pulsierende Hervorhebung der gewaehlten Haltung
    private Map? _map;
    private KarteZoomSicht? _letzteDetailSicht;
    private string? _hoverName;

    // Puls-Animation (kurzes Aufblinken der angeklickten Haltung, wie ein QGIS-Flash).
    private DispatcherTimer? _pulseTimer;
    private Pen? _pulsePen;
    private int _pulseElapsedMs;
    private const int PulseDauerMs = 1600;
    private static readonly Color PulseFarbe = new(255, 0, 200); // kraeftiges Magenta, hebt sich ab
    private MapBounds? _loadedBounds;
    private double _loadedResolution = -1; // Zoomstufe des letzten Netz-Ladens (fuer Detailstufe)

    // Hintergrundkarten-Layer (Satellit + AV offline, OSM online), umschaltbar. Aufbau in
    // KarteBasemapLayerFactory; hier nur gehalten fuer die Umschaltung. null = Ordner fehlt.
    private ILayer? _satellitBasemap;
    private ILayer? _avBasemap;
    private ILayer? _osmBasemap;

    [ObservableProperty] private string _statusText = "Karte wird geladen…";
    [ObservableProperty] private string? _selectedHaltungsname;
    [ObservableProperty] private bool _hasNetworkGeometry;

    // Aktive Hintergrundkarte; per Knopf reihum umschaltbar (Satellit -> AV-Karte -> OSM).
    [ObservableProperty] private KarteBasemapAuswahl _basemapAuswahl = KarteBasemapAuswahl.Satellit;

    // Anzeigename der aktiven Hintergrundkarte (kleine Einblendung unten links auf der Karte).
    [ObservableProperty] private string _basemapAnzeige = KarteBasemapLayerFactory.SatellitLayerName;

    // Schaechte (Kreise) ein-/ausblenden. Standard an, werden aber erst beim Reinzoomen gezeigt.
    [ObservableProperty] private bool _showSchaechte = true;

    /// <summary>Infopanel-Daten der angeklickten Haltung; null = Panel zu.</summary>
    [ObservableProperty] private KarteHaltungInfo? _selectedInfo;

    public IRelayCommand OpenInspektionCommand { get; }
    public IRelayCommand OpenDetailCommand { get; }
    public IRelayCommand SchliesseInfoCommand { get; }

    public KarteViewModel(ShellViewModel shell, ServiceProvider services)
        : this(
            shell,
            settings: services.Settings,
            networkFeatures: services.NetworkFeatures,
            playVideo: KarteVideoLauncher.Create(services),
            inspectionProtocolFiles: services.InspectionProtocolFiles,
            katasterXtfPaths: services.KatasterXtfPaths,
            offlineBasemapPaths: services.OfflineBasemapPaths,
            basemapLayers: services.BasemapLayers)
    {
    }

    public KarteViewModel(
        ShellViewModel shell,
        AppSettings settings,
        NetworkFeatureCache networkFeatures,
        Action<string, HaltungRecord> playVideo,
        IInspectionProtocolFileLocator? inspectionProtocolFiles = null,
        IKatasterXtfPathResolver? katasterXtfPaths = null,
        IOfflineBasemapPathResolver? offlineBasemapPaths = null,
        IKarteBasemapLayerFactory? basemapLayers = null)
    {
        _shell = shell;
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _networkFeatures = networkFeatures ?? throw new ArgumentNullException(nameof(networkFeatures));
        _playVideo = playVideo ?? throw new ArgumentNullException(nameof(playVideo));
        _inspectionProtocolFiles = inspectionProtocolFiles
            ?? DataPage.DataPageProtocolPathResolver.CompatibilityService;
        _katasterXtfPaths = katasterXtfPaths ?? KatasterXtfPathResolver.CompatibilityService;
        _offlineBasemapPaths = offlineBasemapPaths ?? OfflineBasemapBaseResolver.CompatibilityService;
        _basemapLayers = basemapLayers ?? KarteBasemapLayerFactory.CompatibilityService;
        OpenInspektionCommand = new RelayCommand(OpenInspektion);
        OpenDetailCommand = new RelayCommand(OpenDetail);
        SchliesseInfoCommand = new RelayCommand(() =>
        {
            SelectedInfo = null;
            LeereSchadenLayer(); // Panel zu = Schadenspunkte wieder ausblenden
        });
    }

    /// <summary>
    /// Baut die Mapsui-Karte (WMS + Netzlinien) asynchron auf.
    /// Wird aus dem Code-Behind nach dem Loaded-Event aufgerufen.
    /// </summary>
    public async Task<Map> BuildMapAsync()
    {
        HasNetworkGeometry = false;
        var map = new Map();

        // ── Hintergrundkarte: Satellit + AV-Karte offline (lokal) + OpenStreetMap online ──
        // Aufbau in KarteBasemapLayerFactory ausgelagert; hier nur einhaengen + auswaehlen.
        _satellitBasemap = _basemapLayers.CreateOfflineSatellit(OfflineBasemapPath);
        _avBasemap = _basemapLayers.CreateOfflineAv(OfflineBasemapPath);
        _osmBasemap = _basemapLayers.CreateOsmOnline();
        // Reihenfolge egal — es ist immer genau eine aktiv (ApplyBasemapSelection).
        if (_satellitBasemap is not null) map.Layers.Add(_satellitBasemap);
        if (_avBasemap is not null) map.Layers.Add(_avBasemap);
        map.Layers.Add(_osmBasemap);

        // Startauswahl bevorzugt Satellit; fehlt der Ordner, auf die naechste verfuegbare wechseln.
        if (!KarteBasemapWahl.IstVerfuegbar(BasemapAuswahl, _satellitBasemap is not null, _avBasemap is not null))
            BasemapAuswahl = KarteBasemapWahl.Naechste(BasemapAuswahl, _satellitBasemap is not null, _avBasemap is not null);
        ApplyBasemapSelection();

        // ── Netz laden: EINMAL im gemeinsamen Cache bauen, ueber alle Kartenoeffnungen ──
        // wiederverwendet. Der Cache baut nur neu, wenn sich XTF/Zustandsdaten/Skala aendern.
        try
        {
            _kondition = HaltungConditionProvider.Build(_shell.Project.Data);
            _dnByName = HaltungDnProvider.Build(_shell.Project.Data);
            await Task.Run(() => _networkFeatures.EnsureBuilt(XtfPath, _kondition, _invertiert, _dnByName));
            HasNetworkGeometry = _networkFeatures.HasGeometry;

            _netzLayer = new MemoryLayer("Netz") { Features = Array.Empty<GeometryFeature>(), Style = null };
            map.Layers.Add(_netzLayer);

            // Hover-Halo UNTER allen Markierungen, direkt ueber dem Netz.
            _hoverLayer = new MemoryLayer("Hover") { Features = Array.Empty<GeometryFeature>(), Style = null };
            map.Layers.Add(_hoverLayer);

            // Detail-Layer: Haltungs-Labels + Fliessrichtungs-Pfeile, erst im Detail-Zoom befuellt.
            _detailLayer = new MemoryLayer("Details") { Features = Array.Empty<GeometryFeature>(), Style = null };
            map.Layers.Add(_detailLayer);

            // Schaechte-Layer (Kreise) UEBER dem Netz, damit die Knoten auf den Linien sitzen.
            // Bleibt leer, bis weit genug reingezoomt wird (SchachtSichtbarkeitPolicy).
            _schachtLayer = new MemoryLayer("Schaechte") { Features = Array.Empty<GeometryFeature>(), Style = null };
            map.Layers.Add(_schachtLayer);

            // Schadenspunkte der gewaehlten Haltung (Klick fuellt sie, Panel-Schliessen leert sie).
            _schadenLayer = new MemoryLayer("Schaeden") { Features = Array.Empty<GeometryFeature>(), Style = null };
            map.Layers.Add(_schadenLayer);

            // Hervorhebungs-Layer GANZ OBEN: die angeklickte Haltung blinkt hier pulsierend auf.
            _highlightLayer = new MemoryLayer("Auswahl-Puls") { Features = Array.Empty<GeometryFeature>(), Style = null };
            map.Layers.Add(_highlightLayer);

            StatusText = HasNetworkGeometry
                ? $"{_networkFeatures.Count} Haltungen geladen"
                : $"Netz-Datei nicht gefunden: {XtfPath}";
        }
        catch (Exception ex)
        {
            StatusText = "Fehler beim Laden der Netz-Datei: "
                         + UserError.DescribeAndReport(ex, "Karte laden");
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
            var capturedSchachtLayer = _schachtLayer;
            map.Tapped += (_, e) =>
            {
                // Zuerst Schaechte (Kreise liegen ueber den Linien): Klick auf einen Schacht
                // meldet ihn an die QGIS-Bridge -> QGIS zoomt auf den Punkt (analog Haltung).
                if (capturedSchachtLayer is not null)
                {
                    var si = e.GetMapInfo(new[] { capturedSchachtLayer });
                    if (si?.Feature is GeometryFeature sf
                        && sf["Schachtnummer"] is string schachtNr
                        && !string.IsNullOrWhiteSpace(schachtNr))
                    {
                        QgisBridge.QgisBridgeSelection.SetSchacht(schachtNr);
                        StatusText = $"Schacht {schachtNr} an QGIS gemeldet.";
                        return;
                    }
                }

                // Schadenspunkt angeklickt? Code + Meterstand in der Statuszeile zeigen.
                if (_schadenLayer is not null)
                {
                    var di = e.GetMapInfo(new[] { _schadenLayer });
                    if (di?.Feature is GeometryFeature df
                        && df["Code"] is string schadenCode
                        && df["Meter"] is double schadenMeter)
                    {
                        StatusText = $"Schaden {schadenCode} bei {schadenMeter:0.00} m";
                        return;
                    }
                }

                var mi = e.GetMapInfo(new[] { capturedLayer });
                if (mi?.Feature is GeometryFeature gf && gf["Haltungsname"] is string name)
                {
                    SelectedHaltungsname = name;
                    // Infopanel mit den Stammdaten der Haltung fuellen (null = nicht im Projekt).
                    SelectedInfo = KarteHaltungInfoBuilder.Build(FindeRecord(name));
                    // Angeklickte Haltung pulsierend aufblinken lassen (Geometrie direkt vom Klick).
                    PulseGeometry(gf.Geometry);
                    // Ihre Schadenspunkte entlang der Linie einblenden.
                    ZeigeSchaedenFuerHaltung(name, gf.Geometry);
                }
            };
        }

        // Schaechte-Index im Hintergrund bauen: der erste XTF-Parse ist langsam, das Netz ist
        // aber schon sichtbar. Ein Fehler hier darf Netz/Karte nicht stoeren. Wenn fertig (und
        // bereits reingezoomt), die Schaechte nachziehen.
        var xtfForManholes = XtfPath;
        _ = Task.Run(() =>
        {
            try { _networkFeatures.EnsureManholesBuilt(xtfForManholes); }
            catch { /* Schaechte optional — Karte laeuft unabhaengig weiter */ }
            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() => RefreshVisibleNetworkLayer(force: true));
        });

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

    /// <summary>Hintergrundkarte reihum umschalten: Satellit -&gt; AV-Karte -&gt; OpenStreetMap.</summary>
    public void ToggleBasemap()
        => BasemapAuswahl = KarteBasemapWahl.Naechste(BasemapAuswahl, _satellitBasemap is not null, _avBasemap is not null);

    partial void OnBasemapAuswahlChanged(KarteBasemapAuswahl value) => ApplyBasemapSelection();

    /// <summary>Schaechte (Kreise) ein-/ausblenden.</summary>
    public void ToggleSchaechte() => ShowSchaechte = !ShowSchaechte;

    partial void OnShowSchaechteChanged(bool value) => RefreshVisibleNetworkLayer(force: true);

    /// <summary>Aktiviert genau einen Hintergrund-Layer passend zur Auswahl; fehlt er, weicht auf OSM aus.</summary>
    private void ApplyBasemapSelection()
    {
        var wahl = BasemapAuswahl;
        if (!KarteBasemapWahl.IstVerfuegbar(wahl, _satellitBasemap is not null, _avBasemap is not null))
            wahl = KarteBasemapWahl.Naechste(wahl, _satellitBasemap is not null, _avBasemap is not null);

        if (_satellitBasemap is not null) _satellitBasemap.Enabled = wahl == KarteBasemapAuswahl.Satellit;
        if (_avBasemap is not null) _avBasemap.Enabled = wahl == KarteBasemapAuswahl.AvKarte;
        if (_osmBasemap is not null) _osmBasemap.Enabled = wahl == KarteBasemapAuswahl.OpenStreetMap;

        BasemapAnzeige = wahl switch
        {
            KarteBasemapAuswahl.Satellit => KarteBasemapLayerFactory.SatellitLayerName,
            KarteBasemapAuswahl.AvKarte => KarteBasemapLayerFactory.AvLayerName,
            _ => KarteBasemapLayerFactory.OsmLayerName,
        };

        _map?.RefreshGraphics();
    }

    public void ZoomToNetworkAndRefresh()
    {
        if (_map is null || _networkFeatures.NetworkBounds is not { } nb)
        {
            CenterOnUriAndRefresh();
            return;
        }

        var bounds = new MRect(nb.MinX, nb.MinY, nb.MaxX, nb.MaxY).Grow(120);
        _map.Navigator.ZoomToBox(bounds, MBoxFit.Fit, 300);
        RefreshVisibleNetworkLayer(force: true);
        _map.RefreshGraphics();
    }

    public void RefreshVisibleNetworkLayer(bool force)
    {
        if (_map is null || !_networkFeatures.HasGeometry)
            return;

        var viewport = TryGetViewportBounds(_map);
        if (viewport is null)
            return;

        var resolution = _map.Navigator.Viewport.Resolution;
        var paddedViewport = GrowByRatio(viewport.Value, ViewportPaddingRatio);

        // Netz und Schaechte getrennt aktualisieren; Schaechte haengen an der Zoomstufe, das Netz
        // am Ausschnitt+Zoom (Detailstufe). Nur bei echter Aenderung neu zeichnen (spart Redraws).
        var netzGeaendert = RefreshNetzLayer(force, viewport.Value, paddedViewport, resolution);
        var schaechteGeaendert = RefreshSchachtLayer(paddedViewport);
        var detailsGeaendert = RefreshDetailLayer(netzGeaendert || force, resolution);

        if (netzGeaendert || schaechteGeaendert || detailsGeaendert)
            _map.RefreshGraphics();
    }

    // Labels + Fliessrichtungs-Pfeile fuer die SICHTBAREN Netzlinien, nur im Detail-Zoom.
    // On-the-fly gebaut (nie im Netz-Cache) und gedeckelt (KarteDetailFeatureBuilder.MaxHaltungen).
    private bool RefreshDetailLayer(bool quelleGeaendert, double resolution)
    {
        if (_detailLayer is null || _map is null)
            return false;

        var sicht = KarteZoomStufenPolicy.Fuer(resolution, ShowSchaechte);
        var sichtGewechselt = _letzteDetailSicht is null
            || _letzteDetailSicht.LabelsSichtbar != sicht.LabelsSichtbar
            || _letzteDetailSicht.PfeileSichtbar != sicht.PfeileSichtbar;
        if (!quelleGeaendert && !sichtGewechselt)
            return false;
        _letzteDetailSicht = sicht;

        if (!sicht.LabelsSichtbar && !sicht.PfeileSichtbar)
        {
            if (!_detailLayer.Features.Any())
                return false;
            _detailLayer.Features = Array.Empty<GeometryFeature>();
            _detailLayer.DataHasChanged();
            return true;
        }

        var sichtbareLinien = _netzLayer?.Features?.OfType<GeometryFeature>()
            ?? Enumerable.Empty<GeometryFeature>();
        _detailLayer.Features = KarteDetailFeatureBuilder.Build(
            sichtbareLinien, resolution, sicht.LabelsSichtbar, sicht.PfeileSichtbar);
        _detailLayer.DataHasChanged();
        return true;
    }

    // Schadenspunkte der gewaehlten Haltung entlang der Linie einblenden.
    private void ZeigeSchaedenFuerHaltung(string? name, Geometry? geometry)
    {
        if (_schadenLayer is null)
            return;

        var record = FindeRecord(name);
        var entries = record?.Protocol?.Current?.Entries;
        var sollLaenge = ParseLaenge(record?.GetFieldValue("Haltungslaenge_m"));

        var features = KarteSchadenFeatureBuilder.Build(geometry, entries, sollLaenge);
        _schadenLayer.Features = features;
        _schadenLayer.DataHasChanged();
        if (features.Count > 0)
            StatusText = $"{features.Count} Schadenspunkte auf {name}";
    }

    private void LeereSchadenLayer()
    {
        if (_schadenLayer is null || !_schadenLayer.Features.Any())
            return;
        _schadenLayer.Features = Array.Empty<GeometryFeature>();
        _schadenLayer.DataHasChanged();
        _map?.RefreshGraphics();
    }

    // Haltungslaenge tolerant lesen ("45.3", "45,3"); null wenn unlesbar.
    private static double? ParseLaenge(string? roh)
    {
        if (string.IsNullOrWhiteSpace(roh))
            return null;
        var normalisiert = roh.Trim().Replace(',', '.');
        return double.TryParse(normalisiert, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var l) && l > 0 ? l : null;
    }

    /// <summary>Hover-Halo unter dem Mauszeiger (von der Page mit Drossel aufgerufen).
    /// Koordinaten in Screen-Einheiten des MapControls; ausserhalb = Halo entfernen.</summary>
    public void HoverAtScreen(double screenX, double screenY)
    {
        if (_map is null || _hoverLayer is null)
            return;

        var viewport = _map.Navigator.Viewport;
        if (viewport.Width <= 0 || viewport.Height <= 0 || viewport.Resolution <= 0)
            return;

        GeometryFeature? treffer = null;
        if (screenX >= 0 && screenY >= 0)
        {
            var welt = viewport.ScreenToWorld(screenX, screenY);
            var fangradius = viewport.Resolution * 8; // ~8 Pixel Fangdistanz
            var box = new MapBounds(welt.X - fangradius, welt.Y - fangradius,
                                    welt.X + fangradius, welt.Y + fangradius);

            var beste = double.PositiveInfinity;
            foreach (var kandidat in _networkFeatures.QueryVisible(box))
            {
                if (kandidat.Geometry is not LineString linie)
                    continue;
                var punkte = linie.Coordinates.Select(c => (c.X, c.Y)).ToArray();
                var distanz = PolylineMath.DistanzZuPunkt(punkte, (welt.X, welt.Y));
                if (distanz < beste)
                {
                    beste = distanz;
                    treffer = kandidat;
                }
            }

            if (beste > fangradius)
                treffer = null;
        }

        var name = treffer?["Haltungsname"] as string;
        if (string.Equals(name, _hoverName, StringComparison.Ordinal))
            return;
        _hoverName = name;

        if (treffer is null)
        {
            _hoverLayer.Features = Array.Empty<GeometryFeature>();
        }
        else
        {
            // Breiter halbtransparenter Halo in Akzentblau — hebt ohne zu schreien.
            var halo = new GeometryFeature { Geometry = treffer.Geometry };
            halo.Styles.Add(new VectorStyle { Line = new Pen(new Color(37, 99, 235, 90), 14) });
            _hoverLayer.Features = new[] { halo };
        }

        _hoverLayer.DataHasChanged();
        _map.RefreshGraphics();
    }

    // Sichtbare Netzlinien nachziehen. Ueberspringt, wenn Ausschnitt UND Zoom schon geladen sind
    // (bei Zoomaenderung neu, damit die Detailstufe sich anpasst). Bei sehr vielen Linien wird
    // ausgeduennt (NetzLevelOfDetail) — sonst friert Mapsui beim Zeichnen zehntausender Linien ein.
    private bool RefreshNetzLayer(bool force, MapBounds viewport, MapBounds paddedViewport, double resolution)
    {
        if (_netzLayer is null)
            return false;
        if (!force
            && _loadedBounds is { } loadedBounds && loadedBounds.Contains(viewport)
            && _loadedResolution == resolution)
            return false;

        var alle = _networkFeatures.QueryVisible(paddedViewport);
        var (features, ausgeduennt) = NetzLevelOfDetail.Thin(alle, MaxRenderFeatures);
        _netzLayer.Features = features;
        _netzLayer.DataHasChanged();
        _loadedBounds = paddedViewport;
        _loadedResolution = resolution;

        StatusText = ausgeduennt
            ? $"Übersicht vereinfacht: {features.Count} von {alle.Count} Haltungen gezeigt — zum Detail reinzoomen"
            : $"{features.Count} von {_networkFeatures.Count} Haltungen im sichtbaren Ausschnitt";
        return true;
    }

    // Schaechte (Kreise) nur beim Reinzoomen zeigen; sonst leeren. Gibt true zurueck, wenn sich
    // die Feature-Liste geaendert hat (dann muss neu gezeichnet werden).
    private bool RefreshSchachtLayer(MapBounds paddedViewport)
    {
        if (_schachtLayer is null || _map is null)
            return false;

        var resolution = _map.Navigator.Viewport.Resolution;
        if (!SchachtSichtbarkeitPolicy.ShouldShow(ShowSchaechte, resolution))
        {
            if (!_schachtLayer.Features.Any())
                return false;
            _schachtLayer.Features = Array.Empty<GeometryFeature>();
            _schachtLayer.DataHasChanged();
            return true;
        }

        var schaechte = _networkFeatures.QueryVisibleManholes(paddedViewport);
        _schachtLayer.Features = schaechte;
        _schachtLayer.DataHasChanged();
        return true;
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

    // Angeklickte Haltung an die QGIS-Bridge melden (gilt auch fuers separate KarteWindow).
    // Bei extern uebernommener Auswahl (ZoomToSelectedHaltung) NICHT zurueckmelden -> keine Schleife.
    private bool _suppressSelectionEcho;
    partial void OnSelectedHaltungsnameChanged(string? value)
    {
        if (!_suppressSelectionEcho)
            QgisBridge.QgisBridgeSelection.Set(value);
    }

    /// <summary>Zoomt die Karte auf die aktuell (auch anderswo) gewaehlte Haltung — wie QGIS.</summary>
    public void ZoomToSelectedHaltung()
    {
        var name = QgisBridge.QgisBridgeSelection.CurrentFor(_shell.Project.Id);
        if (_map is null || string.IsNullOrWhiteSpace(name))
            return;

        if (_networkFeatures.TryGetBounds(name) is not { } b)
            return;

        // Auswahl spiegeln (ohne Rueckmeldung -> keine Schleife) + Infopanel fuellen.
        _suppressSelectionEcho = true;
        try { SelectedHaltungsname = name; }
        finally { _suppressSelectionEcho = false; }
        SelectedInfo = KarteHaltungInfoBuilder.Build(FindeRecord(name));

        var rect = new MRect(b.MinX, b.MinY, b.MaxX, b.MaxY).Grow(60);
        _map.Navigator.ZoomToBox(rect, MBoxFit.Fit, 400);
        RefreshVisibleNetworkLayer(force: true);
        // Auch bei Auswahl anderswo (Liste/QGIS-Bridge): die Haltung auf der Karte aufblinken lassen.
        PulseHaltung(name);
        // Und ihre Schadenspunkte zeigen (Geometrie aus dem Cache).
        ZeigeSchaedenFuerHaltung(name, _networkFeatures.TryGetGeometry(name));
        _map.RefreshGraphics();
    }

    /// <summary>Sucht die Geometrie der Haltung im Cache und laesst sie pulsierend aufblinken.</summary>
    private void PulseHaltung(string? haltungsname)
        => PulseGeometry(_networkFeatures.TryGetGeometry(haltungsname));

    // Startet die Puls-Animation auf dem Hervorhebungs-Layer fuer die gegebene Geometrie.
    private void PulseGeometry(Geometry? geometry)
    {
        if (_map is null || _highlightLayer is null || geometry is null)
            return;

        _pulsePen = new Pen(PulseFarbe, 12) { PenStyle = PenStyle.Solid };
        var feature = new GeometryFeature { Geometry = geometry };
        feature.Styles.Add(new VectorStyle { Line = _pulsePen });
        _highlightLayer.Features = new[] { feature };
        _highlightLayer.DataHasChanged();

        _pulseElapsedMs = 0;
        _pulseTimer ??= CreatePulseTimer();
        _pulseTimer.Start();
        _map.RefreshGraphics();
    }

    private DispatcherTimer CreatePulseTimer()
    {
        var timer = new DispatcherTimer { Interval = System.TimeSpan.FromMilliseconds(50) };
        timer.Tick += (_, _) => PulseTick();
        return timer;
    }

    private void PulseTick()
    {
        if (_pulsePen is null || _highlightLayer is null || _map is null)
        {
            _pulseTimer?.Stop();
            return;
        }

        _pulseElapsedMs += 50;
        var t = _pulseElapsedMs / (double)PulseDauerMs;
        if (t >= 1.0)
        {
            // Nach dem Blinken eine ruhige, duennere Dauer-Markierung stehen lassen.
            _pulsePen.Width = 5;
            _pulsePen.Color = PulseFarbe;
            _highlightLayer.DataHasChanged();
            _map.RefreshGraphics();
            _pulseTimer?.Stop();
            return;
        }

        // 3 Pulse ueber die Dauer: Linienbreite und Deckkraft schwingen.
        var puls = 0.5 + 0.5 * System.Math.Sin(2 * System.Math.PI * 3 * t);
        _pulsePen.Width = 4 + 12 * puls;
        _pulsePen.Color = new Color(PulseFarbe.R, PulseFarbe.G, PulseFarbe.B, (int)(120 + 135 * puls));
        _highlightLayer.DataHasChanged();
        _map.RefreshGraphics();
    }

    // Exakt zuerst (schnell, eindeutig), dann tolerant (umgekehrte Schacht-Reihenfolge /
    // Teilstrecken-Suffix) via KarteHaltungNameMatcher — gleiche Regel wie der QGIS-Bridge,
    // damit ein Kartenklick dieselbe Haltung findet. Logik liegt bewusst im Matcher, nicht hier.
    private HaltungRecord? FindeRecord(string? haltungsname)
    {
        if (string.IsNullOrWhiteSpace(haltungsname))
            return null;

        return _shell.Project.Data.FirstOrDefault(r => string.Equals(
                   r.GetFieldValue("Haltungsname"), haltungsname, StringComparison.OrdinalIgnoreCase))
            ?? _shell.Project.Data.FirstOrDefault(r =>
                   KarteHaltungNameMatcher.Matches(haltungsname, r.GetFieldValue("Haltungsname")));
    }

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
            _playVideo(resolved, record);
        }
        catch (Exception ex)
        {
            StatusText = "Video-Start fehlgeschlagen: "
                         + UserError.DescribeAndReport(ex, "Karten-Video starten");
        }
    }

    /// <summary>Löst einen Pfad auf und gibt null zurück, wenn die Datei nicht existiert.</summary>
    // Video-Pfad aufloesen: absolut ODER relativ zum Projektordner — wiederverwendeter
    // Resolver der Datenseite statt eigener File.Exists-Logik hier (behebt relative Links).
    private string? ResolveExistingPath(string? path)
        => _inspectionProtocolFiles.ResolveExistingPath(path, _settings.LastProjectPath);
}
