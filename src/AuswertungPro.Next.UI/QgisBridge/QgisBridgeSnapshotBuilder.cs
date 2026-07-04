using System.IO;
using System.Globalization;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Map;
using AuswertungPro.Next.UI.Mapping;

namespace AuswertungPro.Next.UI.QgisBridge;

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

    public QgisBridgeSnapshotBuilder(AppSettings settings, string? networkCacheFilePath = null)
    {
        _settings = settings;
        _networkCacheFilePath = networkCacheFilePath;
    }

    public QgisStatusPayload BuildStatus(Project project, string? currentHolding)
    {
        var network = LoadNetwork();
        var damageStats = CountExportableDamages(project, network.GeometryByHolding);

        return new QgisStatusPayload(
            Ok: true,
            App: "SewerStudio",
            CurrentHolding: NormalizeHolding(currentHolding),
            ProjectName: project.Name,
            ProjectHoldingCount: project.Data.Count,
            NetworkFeatureCount: network.Geometries.Count,
            DamageFeatureCount: damageStats.Exportable,
            SkippedDamageFeatureCount: damageStats.Skipped,
            XtfPath: network.XtfPath,
            XtfFound: network.XtfFound);
    }

    public GeoJsonFeatureCollection BuildNetworkGeoJson(Project project)
    {
        var network = LoadNetwork();
        var conditions = HaltungConditionProvider.Build(project.Data);
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

    public GeoJsonFeatureCollection BuildCurrentGeoJson(Project project, string? currentHolding)
    {
        var holding = NormalizeHolding(currentHolding);
        if (string.IsNullOrWhiteSpace(holding))
            return GeoJsonFeatureCollection.Empty;

        var network = LoadNetwork();
        if (!network.GeometryByHolding.TryGetValue(holding, out var geometry))
            return GeoJsonFeatureCollection.Empty;

        var record = FindRecord(project, holding);
        var condition = TryParseCondition(record);
        var feature = CreateLineFeature(
            geometry,
            new Dictionary<string, object?>
            {
                ["haltung"] = holding,
                ["current"] = true,
                ["zustandsklasse"] = condition,
                ["zustand_farbe"] = MapConditionColor(condition),
                ["schaden_count"] = record?.VsaFindings?.Count ?? 0,
                ["source"] = "sewerstudio_selection"
            });

        return new GeoJsonFeatureCollection(new[] { feature });
    }

    public GeoJsonFeatureCollection BuildDamagesGeoJson(Project project)
    {
        var network = LoadNetwork();
        if (network.GeometryByHolding.Count == 0 || project.Data.Count == 0)
            return GeoJsonFeatureCollection.Empty;

        var features = new List<GeoJsonFeature>();
        foreach (var record in project.Data)
        {
            var holding = NormalizeHolding(record.GetFieldValue("Haltungsname"));
            if (string.IsNullOrWhiteSpace(holding))
                continue;

            if (!network.GeometryByHolding.TryGetValue(holding, out var geometry))
                continue;

            foreach (var finding in record.VsaFindings)
            {
                var meter = GetFindingMeterForMap(finding);
                if (meter is null)
                    continue;

                var point = InterpolateLv95(geometry, meter.Value);
                if (point is null)
                    continue;

                var (lon, lat) = CoordinateTransform.Lv95ToWgs84(point.Value.X, point.Value.Y);
                features.Add(new GeoJsonFeature(
                    new GeoJsonPoint(new[] { lon, lat }),
                    new Dictionary<string, object?>
                    {
                        ["haltung"] = holding,
                        ["code"] = EmptyToNull(finding.KanalSchadencode),
                        ["meter_start"] = finding.MeterStart,
                        ["meter_end"] = finding.MeterEnd,
                        ["mpeg"] = EmptyToNull(finding.MPEG),
                        ["raw"] = EmptyToNull(finding.Raw),
                        ["quantifizierung1"] = EmptyToNull(finding.Quantifizierung1),
                        ["quantifizierung2"] = EmptyToNull(finding.Quantifizierung2),
                        ["ezd"] = finding.EZD,
                        ["ezs"] = finding.EZS,
                        ["ezb"] = finding.EZB,
                        ["zustandsklasse"] = TryParseCondition(record),
                        ["source"] = "sewerstudio_project"
                    }));
            }
        }

        return new GeoJsonFeatureCollection(features);
    }

    private NetworkLoadResult LoadNetwork()
    {
        var xtfPath = KatasterXtfPathResolver.Resolve(_settings);
        if (string.IsNullOrWhiteSpace(xtfPath) || !File.Exists(xtfPath))
            return new NetworkLoadResult(xtfPath, XtfFound: false, Array.Empty<HaltungGeometry>(),
                new Dictionary<string, HaltungGeometry>(StringComparer.OrdinalIgnoreCase));

        var ticks = File.GetLastWriteTimeUtc(xtfPath).Ticks;
        lock (_cacheSync)
        {
            if (string.Equals(_cachedXtfPath, xtfPath, StringComparison.OrdinalIgnoreCase)
                && _cachedXtfTicks == ticks)
            {
                return new NetworkLoadResult(xtfPath, XtfFound: true, _cachedGeometries, _cachedGeometryByHolding);
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

            return new NetworkLoadResult(xtfPath, XtfFound: true, _cachedGeometries, _cachedGeometryByHolding);
        }
    }

    private static GeoJsonFeature CreateLineFeature(HaltungGeometry geometry, Dictionary<string, object?> properties)
    {
        var coordinates = geometry.Points
            .Select(point =>
            {
                var (lon, lat) = CoordinateTransform.Lv95ToWgs84(point.X, point.Y);
                return new[] { lon, lat };
            })
            .ToArray();

        return new GeoJsonFeature(new GeoJsonLineString(coordinates), properties);
    }

    private static HaltungRecord? FindRecord(Project project, string holding)
        => project.Data.FirstOrDefault(r => string.Equals(
            NormalizeHolding(r.GetFieldValue("Haltungsname")),
            holding,
            StringComparison.OrdinalIgnoreCase));

    private static int? TryParseCondition(HaltungRecord? record)
        => record is not null
           && int.TryParse(record.GetFieldValue("Zustandsklasse"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private static string MapConditionColor(int? condition)
        => ZustandColorMapper.Map(condition, invertiert: true) switch
        {
            ZustandFarbe.Gut => "gut",
            ZustandFarbe.Mittel => "mittel",
            ZustandFarbe.Schlecht => "schlecht",
            _ => "unbekannt"
        };

    private static double? GetFindingMeterForMap(VsaFinding finding)
    {
        if (finding.MeterStart is double start && double.IsFinite(start))
        {
            if (finding.MeterEnd is double end && double.IsFinite(end) && end > start)
                return (start + end) / 2.0;

            return start;
        }

        return null;
    }

    private static (double X, double Y)? InterpolateLv95(HaltungGeometry geometry, double meter)
    {
        if (geometry.Points.Count == 0)
            return null;

        if (geometry.Points.Count == 1)
            return geometry.Points[0];

        var target = Math.Max(0, meter);
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

    private static DamageStats CountExportableDamages(
        Project project,
        IReadOnlyDictionary<string, HaltungGeometry> geometryByHolding)
    {
        var exportable = 0;
        var skipped = 0;

        foreach (var record in project.Data)
        {
            var holding = NormalizeHolding(record.GetFieldValue("Haltungsname"));
            var hasGeometry = !string.IsNullOrWhiteSpace(holding) && geometryByHolding.ContainsKey(holding);

            foreach (var finding in record.VsaFindings)
            {
                if (hasGeometry && GetFindingMeterForMap(finding) is not null)
                    exportable++;
                else
                    skipped++;
            }
        }

        return new DamageStats(exportable, skipped);
    }

    private static string NormalizeHolding(string? value)
        => value?.Trim() ?? string.Empty;

    private static string? EmptyToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record NetworkLoadResult(
        string XtfPath,
        bool XtfFound,
        IReadOnlyList<HaltungGeometry> Geometries,
        IReadOnlyDictionary<string, HaltungGeometry> GeometryByHolding);

    private readonly record struct DamageStats(int Exportable, int Skipped);
}

internal sealed record QgisStatusPayload(
    bool Ok,
    string App,
    string CurrentHolding,
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
