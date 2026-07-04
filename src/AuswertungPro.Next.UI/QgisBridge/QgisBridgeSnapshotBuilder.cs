using System.IO;
using AuswertungPro.Next.Infrastructure.Map;
using AuswertungPro.Next.UI.Mapping;

namespace AuswertungPro.Next.UI.QgisBridge;

/// <summary>
/// Baut Status- und GeoJSON-Payloads fuer die QGIS-Bridge aus einem
/// <see cref="QgisProjectSnapshot"/> (UI-frei, darf im Hintergrund laufen).
/// Die Netz-Geometrie kommt aus dem Kataster-XTF und wird gecacht, bis sich
/// Pfad oder Aenderungszeit der Datei aendern.
/// </summary>
internal sealed class QgisBridgeSnapshotBuilder
{
    private readonly AppSettings _settings;
    private readonly string? _networkCacheFilePath;
    private readonly object _cacheSync = new();
    private string? _cachedXtfPath;
    private long _cachedXtfTicks;
    private IReadOnlyList<HaltungGeometry> _cachedGeometries = Array.Empty<HaltungGeometry>();
    private IReadOnlyDictionary<string, HaltungGeometry> _cachedGeometryByHolding =
        new Dictionary<string, HaltungGeometry>(StringComparer.OrdinalIgnoreCase);
    // Umgekehrter Name ("B-A") -> amtliche Kataster-Bezeichnung ("A-B"). Noetig, weil
    // Haltungen nach Aufnahmerichtung benannt werden: bei Inspektion gegen die
    // Fliessrichtung ist der Projektname gegenlaeufig zum Kataster.
    private IReadOnlyDictionary<string, string> _cachedReversedNames =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public QgisBridgeSnapshotBuilder(AppSettings settings, string? networkCacheFilePath = null)
    {
        _settings = settings;
        _networkCacheFilePath = networkCacheFilePath;
    }

    public QgisStatusPayload BuildStatus(QgisProjectSnapshot snapshot)
    {
        var network = LoadNetwork();
        var damageStats = CountExportableDamages(snapshot, network);

        return new QgisStatusPayload(
            Ok: true,
            App: "SewerStudio",
            CurrentHolding: snapshot.CurrentHolding,
            SelectionStamp: snapshot.SelectionStamp,
            ProjectName: snapshot.ProjectName,
            ProjectHoldingCount: snapshot.Haltungen.Count,
            NetworkFeatureCount: network.Geometries.Count,
            DamageFeatureCount: damageStats.Exportable,
            SkippedDamageFeatureCount: damageStats.Skipped,
            XtfPath: network.XtfPath,
            XtfFound: network.XtfFound);
    }

    public GeoJsonFeatureCollection BuildNetworkGeoJson(QgisProjectSnapshot snapshot)
    {
        var network = LoadNetwork();
        var conditions = BuildConditionIndex(snapshot);
        var features = new List<GeoJsonFeature>(network.Geometries.Count);

        foreach (var geometry in network.Geometries)
        {
            if (geometry.Points.Count < 2)
                continue;

            var condition = conditions.TryGetValue(geometry.Haltungsname, out var value) ? value : null;
            features.Add(CreateLineFeature(
                geometry,
                new Dictionary<string, object?>
                {
                    ["haltung"] = geometry.Haltungsname,
                    ["zustandsklasse"] = condition,
                    ["zustand_farbe"] = MapConditionColor(condition),
                    ["source"] = "kataster_xtf"
                }));
        }

        return new GeoJsonFeatureCollection(features);
    }

    public GeoJsonFeatureCollection BuildCurrentGeoJson(QgisProjectSnapshot snapshot)
    {
        var holding = snapshot.CurrentHolding;
        if (string.IsNullOrWhiteSpace(holding))
            return GeoJsonFeatureCollection.Empty;

        var network = LoadNetwork();
        if (!TryResolveGeometry(network, holding, out var geometry, out var katasterName, out var reversedName))
            return GeoJsonFeatureCollection.Empty;

        var record = FindHaltung(snapshot, holding);
        var fromEnd = reversedName || (record?.GegenFliessrichtung ?? false);
        var feature = CreateLineFeature(
            geometry,
            new Dictionary<string, object?>
            {
                ["haltung"] = holding,
                ["haltung_kataster"] = katasterName,
                ["richtung"] = fromEnd ? "gegen_fliessrichtung" : "in_fliessrichtung",
                ["current"] = true,
                ["zustandsklasse"] = record?.Zustandsklasse,
                ["zustand_farbe"] = MapConditionColor(record?.Zustandsklasse),
                ["schaden_count"] = record?.Schaeden.Count ?? 0,
                ["source"] = "sewerstudio_selection"
            });

        return new GeoJsonFeatureCollection(new[] { feature });
    }

    public GeoJsonFeatureCollection BuildDamagesGeoJson(QgisProjectSnapshot snapshot)
    {
        var network = LoadNetwork();
        if (network.GeometryByHolding.Count == 0 || snapshot.Haltungen.Count == 0)
            return GeoJsonFeatureCollection.Empty;

        var features = new List<GeoJsonFeature>();
        foreach (var haltung in snapshot.Haltungen)
        {
            if (!TryResolveGeometry(network, haltung.Haltungsname, out var geometry, out var katasterName, out var reversedName))
                continue;

            // Meterstand zaehlt ab dem Start-Schacht der AUFNAHME. Die Kataster-Polyline
            // laeuft in Fliessrichtung — bei gegenlaeufigem Namen oder Inspektion gegen
            // die Fliessrichtung wird darum vom Polyline-Ende her gemessen.
            var fromEnd = reversedName || haltung.GegenFliessrichtung;

            foreach (var damage in haltung.Schaeden)
            {
                var meter = GetDamageMeterForMap(damage);
                if (meter is null)
                    continue;

                var point = InterpolateLv95(geometry, meter.Value, fromEnd);
                if (point is null)
                    continue;

                features.Add(new GeoJsonFeature(
                    new GeoJsonPoint(new[] { point.Value.X, point.Value.Y }),
                    new Dictionary<string, object?>
                    {
                        ["haltung"] = haltung.Haltungsname,
                        ["haltung_kataster"] = katasterName,
                        ["richtung"] = fromEnd ? "gegen_fliessrichtung" : "in_fliessrichtung",
                        ["code"] = damage.Code,
                        ["beschreibung"] = damage.Beschreibung,
                        ["meter_start"] = damage.MeterStart,
                        ["meter_end"] = damage.MeterEnd,
                        ["streckenschaden"] = damage.IsStreckenschaden,
                        ["severity"] = damage.Severity,
                        ["mpeg"] = damage.Mpeg,
                        ["raw"] = damage.Raw,
                        ["quantifizierung1"] = damage.Quantifizierung1,
                        ["quantifizierung2"] = damage.Quantifizierung2,
                        ["ezd"] = damage.EZD,
                        ["ezs"] = damage.EZS,
                        ["ezb"] = damage.EZB,
                        ["zustandsklasse"] = haltung.Zustandsklasse,
                        ["source"] = damage.Source
                    }));
            }
        }

        return new GeoJsonFeatureCollection(features);
    }

    /// <summary>
    /// Aenderungsstand der Kataster-Datei (0 = keine Datei) — Teil des Cache-Fingerprints,
    /// damit ein neues XTF die gecachten GeoJSON-Bytes sofort ungueltig macht.
    /// </summary>
    public long GetNetworkStampTicks()
    {
        var xtfPath = KatasterXtfPathResolver.Resolve(_settings);
        if (string.IsNullOrWhiteSpace(xtfPath) || !File.Exists(xtfPath))
            return 0;

        return File.GetLastWriteTimeUtc(xtfPath).Ticks;
    }

    private NetworkLoadResult LoadNetwork()
    {
        var xtfPath = KatasterXtfPathResolver.Resolve(_settings);
        if (string.IsNullOrWhiteSpace(xtfPath) || !File.Exists(xtfPath))
            return new NetworkLoadResult(xtfPath, XtfFound: false, Array.Empty<HaltungGeometry>(),
                new Dictionary<string, HaltungGeometry>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        var ticks = File.GetLastWriteTimeUtc(xtfPath).Ticks;
        lock (_cacheSync)
        {
            if (string.Equals(_cachedXtfPath, xtfPath, StringComparison.OrdinalIgnoreCase)
                && _cachedXtfTicks == ticks)
            {
                return new NetworkLoadResult(
                    xtfPath, XtfFound: true, _cachedGeometries, _cachedGeometryByHolding, _cachedReversedNames);
            }

            var geometries = new NetworkGeometryCache(_networkCacheFilePath)
                .Load(xtfPath)
                .Where(g => g.Points.Count >= 2 && !string.IsNullOrWhiteSpace(g.Haltungsname))
                .ToList();

            _cachedXtfPath = xtfPath;
            _cachedXtfTicks = ticks;
            _cachedGeometries = geometries;
            _cachedGeometryByHolding = geometries
                .GroupBy(g => g.Haltungsname, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            _cachedReversedNames = BuildReversedNameIndex(_cachedGeometryByHolding);

            return new NetworkLoadResult(
                xtfPath, XtfFound: true, _cachedGeometries, _cachedGeometryByHolding, _cachedReversedNames);
        }
    }

    /// <summary>
    /// Baut den Index "umgekehrter Name -> Kataster-Bezeichnung" ("B-A" -> "A-B").
    /// Direkte Kataster-Namen haben immer Vorrang; mehrdeutige Umkehrungen entfallen.
    /// </summary>
    private static IReadOnlyDictionary<string, string> BuildReversedNameIndex(
        IReadOnlyDictionary<string, HaltungGeometry> geometryByHolding)
    {
        var reversed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var ambiguous = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var katasterName in geometryByHolding.Keys)
        {
            var (a, b) = HaltungCadastreExtractor.SplitShaftPair(katasterName);
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
                continue;

            var reversedName = $"{b}-{a}";
            if (geometryByHolding.ContainsKey(reversedName) || ambiguous.Contains(reversedName))
                continue;

            if (reversed.ContainsKey(reversedName))
            {
                // Zwei Kataster-Haltungen mit derselben Umkehrung -> nicht eindeutig.
                reversed.Remove(reversedName);
                ambiguous.Add(reversedName);
                continue;
            }

            reversed[reversedName] = katasterName;
        }

        return reversed;
    }

    /// <summary>
    /// Loest den Projekt-Haltungsnamen zur Kataster-Geometrie auf.
    /// reversedName = true, wenn der Projektname gegenlaeufig zum Kataster benannt ist
    /// (Aufnahme startete am Kataster-"nach"-Schacht, also am Ende der Polyline).
    /// </summary>
    private static bool TryResolveGeometry(
        NetworkLoadResult network,
        string holdingName,
        out HaltungGeometry geometry,
        out string katasterName,
        out bool reversedName)
    {
        if (network.GeometryByHolding.TryGetValue(holdingName, out geometry!))
        {
            katasterName = geometry.Haltungsname;
            reversedName = false;
            return true;
        }

        if (network.ReversedNames.TryGetValue(holdingName, out var kataster)
            && network.GeometryByHolding.TryGetValue(kataster, out geometry!))
        {
            katasterName = kataster;
            reversedName = true;
            return true;
        }

        geometry = null!;
        katasterName = "";
        reversedName = false;
        return false;
    }

    private static IReadOnlyDictionary<string, int?> BuildConditionIndex(QgisProjectSnapshot snapshot)
    {
        var map = new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase);
        foreach (var haltung in snapshot.Haltungen)
            map[haltung.Haltungsname] = haltung.Zustandsklasse;
        return map;
    }

    private static GeoJsonFeature CreateLineFeature(HaltungGeometry geometry, Dictionary<string, object?> properties)
    {
        // LV95 unveraendert durchreichen: keine Umrechnung = keine Naeherungsfehler.
        // Die WGS84-Naeherungsformel (~1 m) war auf Gebaeude-Zoomstufe sichtbar daneben.
        var coordinates = geometry.Points
            .Select(point => new[] { point.X, point.Y })
            .ToArray();

        return new GeoJsonFeature(new GeoJsonLineString(coordinates), properties);
    }

    private static QgisHaltungSnapshot? FindHaltung(QgisProjectSnapshot snapshot, string holding)
        => snapshot.Haltungen.FirstOrDefault(h =>
            string.Equals(h.Haltungsname, holding, StringComparison.OrdinalIgnoreCase));

    private static string MapConditionColor(int? condition)
        => ZustandColorMapper.Map(condition, invertiert: true) switch
        {
            ZustandFarbe.Gut => "gut",
            ZustandFarbe.Mittel => "mittel",
            ZustandFarbe.Schlecht => "schlecht",
            _ => "unbekannt"
        };

    /// <summary>Kartenposition eines Schadens: Streckenschaeden als Mittelpunkt der Strecke.</summary>
    private static double? GetDamageMeterForMap(QgisDamageSnapshot damage)
    {
        if (damage.MeterStart is double start && double.IsFinite(start))
        {
            if (damage.MeterEnd is double end && double.IsFinite(end) && end > start)
                return (start + end) / 2.0;

            return start;
        }

        return null;
    }

    private static (double X, double Y)? InterpolateLv95(HaltungGeometry geometry, double meter, bool fromEnd = false)
    {
        if (geometry.Points.Count == 0)
            return null;

        if (geometry.Points.Count == 1)
            return geometry.Points[0];

        var target = Math.Max(0, meter);
        if (fromEnd)
        {
            // Meterstand ab dem Polyline-ENDE (Aufnahme gegen die Fliessrichtung):
            // auf eine Vorwaerts-Position ab Polyline-Anfang umrechnen.
            target = Math.Max(0, TotalLength(geometry) - target);
        }

        var walked = 0.0;
        for (var i = 1; i < geometry.Points.Count; i++)
        {
            var a = geometry.Points[i - 1];
            var b = geometry.Points[i];
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var length = Math.Sqrt(dx * dx + dy * dy);
            if (length <= 0)
                continue;

            if (target <= walked + length)
            {
                var t = (target - walked) / length;
                return (a.X + dx * t, a.Y + dy * t);
            }

            walked += length;
        }

        return geometry.Points[^1];
    }

    private static double TotalLength(HaltungGeometry geometry)
    {
        var total = 0.0;
        for (var i = 1; i < geometry.Points.Count; i++)
        {
            var dx = geometry.Points[i].X - geometry.Points[i - 1].X;
            var dy = geometry.Points[i].Y - geometry.Points[i - 1].Y;
            total += Math.Sqrt(dx * dx + dy * dy);
        }

        return total;
    }

    private static DamageStats CountExportableDamages(
        QgisProjectSnapshot snapshot,
        NetworkLoadResult network)
    {
        var exportable = 0;
        var skipped = 0;

        foreach (var haltung in snapshot.Haltungen)
        {
            var hasGeometry = TryResolveGeometry(network, haltung.Haltungsname, out _, out _, out _);
            foreach (var damage in haltung.Schaeden)
            {
                if (hasGeometry && GetDamageMeterForMap(damage) is not null)
                    exportable++;
                else
                    skipped++;
            }
        }

        return new DamageStats(exportable, skipped);
    }

    private sealed record NetworkLoadResult(
        string XtfPath,
        bool XtfFound,
        IReadOnlyList<HaltungGeometry> Geometries,
        IReadOnlyDictionary<string, HaltungGeometry> GeometryByHolding,
        IReadOnlyDictionary<string, string> ReversedNames);

    private readonly record struct DamageStats(int Exportable, int Skipped);
}

internal sealed record QgisStatusPayload(
    bool Ok,
    string App,
    string CurrentHolding,
    long SelectionStamp,
    string ProjectName,
    int ProjectHoldingCount,
    int NetworkFeatureCount,
    int DamageFeatureCount,
    int SkippedDamageFeatureCount,
    string XtfPath,
    bool XtfFound);

internal sealed class GeoJsonFeatureCollection
{
    public static GeoJsonFeatureCollection Empty { get; } = new(Array.Empty<GeoJsonFeature>());

    public GeoJsonFeatureCollection(IEnumerable<GeoJsonFeature> features)
    {
        Features = features.ToList();
    }

    public string Type => "FeatureCollection";

    // Legacy-GeoJSON-CRS-Angabe: RFC 7946 kennt sie nicht mehr, aber OGR/QGIS
    // lesen sie weiterhin. Damit landen die LV95-Koordinaten (EPSG:2056) exakt
    // und ohne Reprojektionsfehler im Schweizer Kataster-Bezugsrahmen.
    public object Crs { get; } = new
    {
        type = "name",
        properties = new { name = "urn:ogc:def:crs:EPSG::2056" }
    };

    public List<GeoJsonFeature> Features { get; }
}

internal sealed record GeoJsonFeature
{
    public GeoJsonFeature(object geometry, IDictionary<string, object?> properties)
    {
        Geometry = geometry;
        Properties = properties;
    }

    public string Type => "Feature";
    public object Geometry { get; }
    public IDictionary<string, object?> Properties { get; }
}

internal sealed record GeoJsonLineString(double[][] Coordinates)
{
    public string Type => "LineString";
}

internal sealed record GeoJsonPoint(double[] Coordinates)
{
    public string Type => "Point";
}
