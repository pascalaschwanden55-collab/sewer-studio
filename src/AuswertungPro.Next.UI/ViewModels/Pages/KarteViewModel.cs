using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using System.Windows.Threading;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Map;
using AuswertungPro.Next.UI.Mapping;
using AuswertungPro.Next.UI.Player;
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
    private readonly ServiceProvider _services;

    private string XtfPath => KatasterXtfPathResolver.Resolve(_services.Settings);

    // Offline-Hintergrundkarten (Satellit/AV im Programmordner). Resolver toleriert einen
    // veralteten gespeicherten Pfad (z.B. "...\basemap_tiles\uri") und nimmt dann den
    // Elternordner, der die Unterordner satellit/av enthaelt.
    private string? OfflineBasemapPath => OfflineBasemapBaseResolver.Resolve(_services.Settings.OfflineBasemapPath);

    // Skalierung: false = VSA-Skala (0=gut); true = EZ-Skala (0=schlecht/4=gut)
    private bool _invertiert = true;

    // Zustandsfarben je Haltungsname aus dem aktuellen Projekt (fuers Feature-Bauen im Cache).
    private IReadOnlyDictionary<string, int?> _kondition = new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase);
    private MemoryLayer? _netzLayer;
    private MemoryLayer? _schachtLayer;
    private MemoryLayer? _highlightLayer;   // pulsierende Hervorhebung der gewaehlten Haltung
    private Map? _map;

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

        // ── Hintergrundkarte: Satellit + AV-Karte offline (lokal) + OpenStreetMap online ──
        // Aufbau in KarteBasemapLayerFactory ausgelagert; hier nur einhaengen + auswaehlen.
        _satellitBasemap = KarteBasemapLayerFactory.CreateOfflineSatellit(OfflineBasemapPath);
        _avBasemap = KarteBasemapLayerFactory.CreateOfflineAv(OfflineBasemapPath);
        _osmBasemap = KarteBasemapLayerFactory.CreateOsmOnline();
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
            await Task.Run(() => _services.NetworkFeatures.EnsureBuilt(XtfPath, _kondition, _invertiert));
            HasNetworkGeometry = _services.NetworkFeatures.HasGeometry;

            _netzLayer = new MemoryLayer("Netz") { Features = Array.Empty<GeometryFeature>(), Style = null };
            map.Layers.Add(_netzLayer);

            // Schaechte-Layer (Kreise) UEBER dem Netz, damit die Knoten auf den Linien sitzen.
            // Bleibt leer, bis weit genug reingezoomt wird (SchachtSichtbarkeitPolicy).
            _schachtLayer = new MemoryLayer("Schaechte") { Features = Array.Empty<GeometryFeature>(), Style = null };
            map.Layers.Add(_schachtLayer);

            // Hervorhebungs-Layer GANZ OBEN: die angeklickte Haltung blinkt hier pulsierend auf.
            _highlightLayer = new MemoryLayer("Auswahl-Puls") { Features = Array.Empty<GeometryFeature>(), Style = null };
            map.Layers.Add(_highlightLayer);

            StatusText = HasNetworkGeometry
                ? $"{_services.NetworkFeatures.Count} Haltungen geladen"
                : $"Netz-Datei nicht gefunden: {XtfPath}";
        }
        catch (Exception ex)
        {
            StatusText = $"Fehler beim Laden der Netz-Datei: {ex.Message}";
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
                    // Angeklickte Haltung pulsierend aufblinken lassen (Geometrie direkt vom Klick).
                    PulseGeometry(gf.Geometry);
                }
            };
        }

        // Schaechte-Index im Hintergrund bauen: der erste XTF-Parse ist langsam, das Netz ist
        // aber schon sichtbar. Ein Fehler hier darf Netz/Karte nicht stoeren. Wenn fertig (und
        // bereits reingezoomt), die Schaechte nachziehen.
        var xtfForManholes = XtfPath;
        _ = Task.Run(() =>
        {
            try { _services.NetworkFeatures.EnsureManholesBuilt(xtfForManholes); }
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
        if (_map is null || _services.NetworkFeatures.NetworkBounds is not { } nb)
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
        if (_map is null || !_services.NetworkFeatures.HasGeometry)
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

        if (netzGeaendert || schaechteGeaendert)
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

        var alle = _services.NetworkFeatures.QueryVisible(paddedViewport);
        var (features, ausgeduennt) = NetzLevelOfDetail.Thin(alle, MaxRenderFeatures);
        _netzLayer.Features = features;
        _netzLayer.DataHasChanged();
        _loadedBounds = paddedViewport;
        _loadedResolution = resolution;

        StatusText = ausgeduennt
            ? $"Übersicht vereinfacht: {features.Count} von {alle.Count} Haltungen gezeigt — zum Detail reinzoomen"
            : $"{features.Count} von {_services.NetworkFeatures.Count} Haltungen im sichtbaren Ausschnitt";
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

        var schaechte = _services.NetworkFeatures.QueryVisibleManholes(paddedViewport);
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

        if (_services.NetworkFeatures.TryGetBounds(name) is not { } b)
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
        _map.RefreshGraphics();
    }

    /// <summary>Sucht die Geometrie der Haltung im Cache und laesst sie pulsierend aufblinken.</summary>
    private void PulseHaltung(string? haltungsname)
        => PulseGeometry(_services.NetworkFeatures.TryGetGeometry(haltungsname));

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
    // Video-Pfad aufloesen: absolut ODER relativ zum Projektordner — wiederverwendeter
    // Resolver der Datenseite statt eigener File.Exists-Logik hier (behebt relative Links).
    private string? ResolveExistingPath(string? path)
        => DataPage.DataPageProtocolPathResolver.ResolveExistingPath(path, _services.Settings.LastProjectPath);
}
