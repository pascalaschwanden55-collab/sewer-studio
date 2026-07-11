using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Infrastructure.Map;
using Mapsui.Nts;
using NetTopologySuite.Geometries;

namespace AuswertungPro.Next.UI.Mapping;

/// <summary>
/// Baut die Mapsui-Netzlinien + einen raeumlichen Index EINMAL und haelt sie im RAM. Wird ueber
/// alle Kartenoeffnungen wiederverwendet (kein Neuaufbau der zehntausenden Features), nur bei
/// Aenderung von XTF/Zustandsdaten/Skala neu gebaut. Vorladbar beim Programmstart. Thread-safe,
/// damit Vorladen und Kartenoeffnen sich nicht ins Gehege kommen. Singleton via ServiceProvider.
///
/// Die Schaechte (Kreise) werden BEWUSST getrennt gebaut (<see cref="EnsureManholesBuilt"/>):
/// ihr XTF-Parse ist beim ersten Mal langsam und darf das Erscheinen des Netzes nicht blockieren
/// und es auch nicht bei einem Fehler verwerfen.
/// </summary>
public sealed class NetworkFeatureCache
{
    private const double CellSizeMeters = 500; // ~500 m Zellen in WebMercator

    private readonly object _lock = new();

    private string _key = "";
    private GridSpatialIndex<GeometryFeature>? _index;
    private int _count;
    private MapBounds? _networkBounds;
    private Dictionary<string, MapBounds>? _boundsByName;
    private Dictionary<string, GeometryFeature>? _featureByName; // fuer die Puls-Hervorhebung

    // Schaechte-Index: eigener Schluessel (haengt nur an der XTF, nicht an Zustandsdaten/Skala).
    private string _manholeKey = "";
    private GridSpatialIndex<GeometryFeature>? _manholeIndex;

    public bool HasGeometry { get { lock (_lock) { return _count > 0; } } }
    public int Count { get { lock (_lock) { return _count; } } }
    public MapBounds? NetworkBounds { get { lock (_lock) { return _networkBounds; } } }

    /// <summary>Baut den Netz-Index, falls sich XTF/Zustandsdaten/Skala geaendert haben; sonst No-op.</summary>
    public void EnsureBuilt(string? xtfPath, IReadOnlyDictionary<string, int?> kondition, bool invertiert)
        => EnsureBuilt(xtfPath, kondition, invertiert, dnByName: null);

    /// <summary>Premium-Variante mit Nennweiten: Linienbreite nach DN. Key beruecksichtigt die DN-Daten.</summary>
    public void EnsureBuilt(
        string? xtfPath,
        IReadOnlyDictionary<string, int?> kondition,
        bool invertiert,
        IReadOnlyDictionary<string, int?>? dnByName)
    {
        var key = BuildKey(xtfPath, kondition, invertiert, dnByName);
        lock (_lock)
        {
            if (key == _key && _index is not null)
                return;

            var index = new GridSpatialIndex<GeometryFeature>(CellSizeMeters);
            var boundsByName = new Dictionary<string, MapBounds>(StringComparer.OrdinalIgnoreCase);
            var featureByName = new Dictionary<string, GeometryFeature>(StringComparer.OrdinalIgnoreCase);
            MapBounds? bounds = null;
            var count = 0;

            if (!string.IsNullOrWhiteSpace(xtfPath) && File.Exists(xtfPath))
            {
                var geometries = new NetworkGeometryCache().Load(xtfPath);
                var projected = NetworkViewportFilter.Project(geometries);
                foreach (var g in projected)
                {
                    var feature = KarteNetzFeatureBuilder.Build(g, kondition, invertiert, dnByName);
                    index.Add(g.Bounds, feature);
                    boundsByName[g.Haltungsname] = g.Bounds;
                    featureByName[g.Haltungsname] = feature;
                    bounds = bounds is { } b ? Union(b, g.Bounds) : g.Bounds;
                }
                count = projected.Count;
            }

            _index = index;
            _boundsByName = boundsByName;
            _featureByName = featureByName;
            _count = count;
            _networkBounds = bounds;
            _key = key;
        }
    }

    /// <summary>
    /// Baut den Schaechte-Index (Kreise) aus DEMSELBEN XTF — separat und ausserhalb des Locks,
    /// weil das XTF-Parsen beim ersten Mal langsam ist. So erscheint das Netz sofort; die
    /// Schaechte kommen nach (sie werden ohnehin erst beim Reinzoomen gezeigt). Idempotent.
    /// </summary>
    public void EnsureManholesBuilt(string? xtfPath)
    {
        var key = BuildManholeKey(xtfPath);
        lock (_lock)
        {
            if (key == _manholeKey && _manholeIndex is not null)
                return;
        }

        // Schweres Parsen bewusst OHNE Lock, damit QueryVisible (Netz) nicht blockiert.
        var index = new GridSpatialIndex<GeometryFeature>(CellSizeMeters);
        if (!string.IsNullOrWhiteSpace(xtfPath) && File.Exists(xtfPath))
        {
            foreach (var m in new ManholeGeometryCache().Load(xtfPath))
            {
                var (mx, my) = CoordinateTransform.Lv95ToWebMercator(m.X, m.Y);
                index.Add(new MapBounds(mx, my, mx, my), KarteSchachtFeatureBuilder.Build(m.Bezeichnung, mx, my));
            }
        }

        lock (_lock)
        {
            _manholeIndex = index;
            _manholeKey = key;
        }
    }

    /// <summary>Nur die im Sichtfenster liegenden Netzlinien — schnell ueber den Gitter-Index.</summary>
    public IReadOnlyList<GeometryFeature> QueryVisible(MapBounds viewport)
    {
        GridSpatialIndex<GeometryFeature>? idx;
        lock (_lock) { idx = _index; }
        return idx is null ? Array.Empty<GeometryFeature>() : idx.Query(viewport);
    }

    /// <summary>Nur die im Sichtfenster liegenden Schaechte (Kreise) — erst beim Reinzoomen genutzt.</summary>
    public IReadOnlyList<GeometryFeature> QueryVisibleManholes(MapBounds viewport)
    {
        GridSpatialIndex<GeometryFeature>? idx;
        lock (_lock) { idx = _manholeIndex; }
        return idx is null ? Array.Empty<GeometryFeature>() : idx.Query(viewport);
    }

    /// <summary>Bounds einer Haltung per Name (exakt, sonst tolerant umgekehrt/Suffix). null = unbekannt.</summary>
    public MapBounds? TryGetBounds(string? haltungsname)
    {
        if (string.IsNullOrWhiteSpace(haltungsname))
            return null;

        lock (_lock)
        {
            var map = _boundsByName;
            if (map is null)
                return null;
            if (map.TryGetValue(haltungsname.Trim(), out var exact))
                return exact;
            foreach (var kv in map)
                if (ViewModels.Pages.KarteHaltungNameMatcher.Matches(haltungsname, kv.Key))
                    return kv.Value;
            return null;
        }
    }

    /// <summary>Geometrie (Linie) einer Haltung per Name — fuer die Puls-Hervorhebung. null = unbekannt.</summary>
    public Geometry? TryGetGeometry(string? haltungsname)
    {
        if (string.IsNullOrWhiteSpace(haltungsname))
            return null;

        lock (_lock)
        {
            var map = _featureByName;
            if (map is null)
                return null;
            if (map.TryGetValue(haltungsname.Trim(), out var exact))
                return exact.Geometry;
            foreach (var kv in map)
                if (ViewModels.Pages.KarteHaltungNameMatcher.Matches(haltungsname, kv.Key))
                    return kv.Value.Geometry;
            return null;
        }
    }

    private static MapBounds Union(MapBounds a, MapBounds b)
        => new(Math.Min(a.MinX, b.MinX), Math.Min(a.MinY, b.MinY), Math.Max(a.MaxX, b.MaxX), Math.Max(a.MaxY, b.MaxY));

    // Key aendert sich bei anderem XTF (Pfad/mtime/Groesse), anderen Zustands-/DN-Daten oder Skala.
    private static string BuildKey(
        string? xtfPath,
        IReadOnlyDictionary<string, int?> kondition,
        bool invertiert,
        IReadOnlyDictionary<string, int?>? dnByName)
    {
        var (mtime, size) = XtfStamp(xtfPath);

        var hash = new HashCode();
        foreach (var kv in kondition.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            hash.Add(kv.Key, StringComparer.Ordinal);
            hash.Add(kv.Value);
        }

        var dnHash = new HashCode();
        if (dnByName is not null)
        {
            foreach (var kv in dnByName.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                dnHash.Add(kv.Key, StringComparer.Ordinal);
                dnHash.Add(kv.Value);
            }
        }

        return $"{xtfPath}|{mtime}|{size}|{invertiert}|{kondition.Count}|{hash.ToHashCode()}|{dnByName?.Count ?? -1}|{dnHash.ToHashCode()}";
    }

    // Schaechte haengen nur an der XTF selbst (nicht an Zustandsdaten/Skala).
    private static string BuildManholeKey(string? xtfPath)
    {
        var (mtime, size) = XtfStamp(xtfPath);
        return $"{xtfPath}|{mtime}|{size}";
    }

    private static (long Mtime, long Size) XtfStamp(string? xtfPath)
    {
        if (string.IsNullOrWhiteSpace(xtfPath) || !File.Exists(xtfPath))
            return (0, 0);
        var fi = new FileInfo(xtfPath);
        return (fi.LastWriteTimeUtc.Ticks, fi.Length);
    }
}
