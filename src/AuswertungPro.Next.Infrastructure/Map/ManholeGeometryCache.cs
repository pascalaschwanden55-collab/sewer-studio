using System.Collections.Generic;
using System.Text.Json;

namespace AuswertungPro.Next.Infrastructure.Map;

/// <summary>
/// Haelt eine schlanke lokale Kopie der Schacht-Punkte (Abwasserknoten) aus der XTF.
/// Re-Parse nur, wenn die XTF neuer ist als der Cache (gemeinsame Logik in
/// <see cref="XtfJsonGeometryCache{T}"/>). Default-Cache:
/// %LOCALAPPDATA%/SewerStudio/map/manhole_cache.json
/// </summary>
public sealed class ManholeGeometryCache : XtfJsonGeometryCache<ManholeGeometry>
{
    private readonly XtfManholeExtractor _extractor = new();

    public ManholeGeometryCache(string? cacheFilePath = null)
        : base(
            cacheFilePath ?? DefaultCachePath("manhole_cache.json"),
            formatVersion: 1,
            new JsonSerializerOptions())
    {
    }

    protected override IEnumerable<ManholeGeometry> Extract(string xtfPath) => _extractor.Extract(xtfPath);
}
