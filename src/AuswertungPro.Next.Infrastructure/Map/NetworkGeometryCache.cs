using System.Collections.Generic;
using System.Text.Json;

namespace AuswertungPro.Next.Infrastructure.Map;

/// <summary>
/// Haelt eine schlanke lokale Kopie der Netz-Geometrie. Re-Parse nur, wenn die XTF neuer ist
/// als der Cache (gemeinsame Logik in <see cref="XtfJsonGeometryCache{T}"/>). Default-Cache:
/// %LOCALAPPDATA%/SewerStudio/map/network_cache.json
/// </summary>
public sealed class NetworkGeometryCache : XtfJsonGeometryCache<HaltungGeometry>
{
    private readonly XtfNetworkExtractor _extractor = new();

    // Version 2: IncludeFields=true serialisiert Wertetupel-Felder (Item1/Item2) korrekt.
    // Ohne diese Option liefert System.Text.Json nur leere Objekte {} fuer jeden Punkt.
    // Aeltere Caches (Version fehlt oder != 2) werden verworfen und neu gebaut.
    public NetworkGeometryCache(string? cacheFilePath = null)
        : base(
            cacheFilePath ?? DefaultCachePath("network_cache.json"),
            formatVersion: 2,
            new JsonSerializerOptions { IncludeFields = true })
    {
    }

    protected override IEnumerable<HaltungGeometry> Extract(string xtfPath) => _extractor.Extract(xtfPath);
}
