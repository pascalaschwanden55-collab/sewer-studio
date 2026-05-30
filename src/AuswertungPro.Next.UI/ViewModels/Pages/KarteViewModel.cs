using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Map;
using Mapsui;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Providers.Wms;
using Mapsui.Styles;
using NetTopologySuite.Geometries;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class KarteViewModel : ObservableObject
{
    private readonly ShellViewModel _shell;

    // Pfad zur Netz-XTF; kein Settings-Eintrag vorhanden → Konstante
    private readonly string _xtfPath = @"D:\QGIS_V4\Export_Sewer_Studio\Abwasserkataster_Uri.xtf";

    // Skalierung: false = VSA-Skala (0=gut); true = EZ-Skala (0=schlecht/4=gut)
    private bool _invertiert = false;

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

        // ── Netz-Geometrie laden ──────────────────────────────────────────────
        MemoryLayer? netzLayer = null;
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

                // Zustandsfarben aus dem aktuellen Projekt
                var kondition = HaltungConditionProvider.Build(_shell.Project.Data);

                var features = new List<GeometryFeature>(geometrien.Count);
                foreach (var hg in geometrien)
                {
                    if (hg.Points.Count < 2)
                        continue;

                    // LV95 → WebMercator für jeden Punkt
                    var coords = hg.Points
                        .Select(p =>
                        {
                            var (mx, my) = CoordinateTransform.Lv95ToWebMercator(p.X, p.Y);
                            return new Coordinate(mx, my);
                        })
                        .ToArray();

                    var farbe = ZustandColorMapper.Map(
                        kondition.TryGetValue(hg.Haltungsname, out var k) ? k : null,
                        _invertiert);

                    var color = farbe switch
                    {
                        ZustandFarbe.Gut      => Color.Green,
                        ZustandFarbe.Mittel   => Color.Orange,
                        ZustandFarbe.Schlecht => Color.Red,
                        _                     => Color.Gray,
                    };

                    var feature = new GeometryFeature { Geometry = new LineString(coords) };
                    feature["Haltungsname"] = hg.Haltungsname;
                    feature.Styles.Add(new VectorStyle { Line = new Pen(color, 4) });
                    features.Add(feature);
                }

                netzLayer = new MemoryLayer("Netz") { Features = features, Style = null };
                map.Layers.Add(netzLayer);

                StatusText = $"{features.Count} Haltungen geladen";
            }
            catch (Exception ex)
            {
                StatusText = $"Fehler beim Laden der Netz-Datei: {ex.Message}";
            }
        }

        // ── Karten-Mittelpunkt Uri/Altdorf (EPSG:3857) ───────────────────────
        // WebMercator Zoom-Level 14 ≈ 9.55 m/px
        map.Navigator.CenterOnAndZoomTo(new MPoint(960296, 5925558), 9.55);

        // ── Klick-Handler: Haltungsname setzen ───────────────────────────────
        if (netzLayer is not null)
        {
            var capturedLayer = netzLayer;
            map.Tapped += (_, e) =>
            {
                var mi = e.GetMapInfo(new[] { capturedLayer });
                if (mi?.Feature is GeometryFeature gf && gf["Haltungsname"] is string name)
                    SelectedHaltungsname = name;
            };
        }

        return map;
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
