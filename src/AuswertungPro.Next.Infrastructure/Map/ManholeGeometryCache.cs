using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Map;

/// <summary>
/// Haelt eine schlanke lokale Kopie der Schacht-Punkte (Abwasserknoten) aus der XTF.
/// Re-Parse nur, wenn die XTF neuer ist als der Cache — gleiche Konvention wie
/// <see cref="NetworkGeometryCache"/>. Default-Cache:
/// %LOCALAPPDATA%/SewerStudio/map/manhole_cache.json
/// </summary>
public sealed class ManholeGeometryCache
{
    private readonly string _cacheFilePath;
    private readonly XtfManholeExtractor _extractor = new();

    private const int CurrentFormatVersion = 1;

    private static readonly JsonSerializerOptions JsonOpts = new();

    public ManholeGeometryCache(string? cacheFilePath = null)
    {
        _cacheFilePath = cacheFilePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SewerStudio", "map", "manhole_cache.json");
    }

    private sealed record CacheFile(int FormatVersion, string XtfPath, long XtfTicks, List<ManholeGeometry> Items);

    public IReadOnlyList<ManholeGeometry> Load(string xtfPath)
    {
        var xtfTicks = File.GetLastWriteTimeUtc(xtfPath).Ticks;
        if (File.Exists(_cacheFilePath))
        {
            try
            {
                var cached = JsonSerializer.Deserialize<CacheFile>(File.ReadAllText(_cacheFilePath), JsonOpts);
                if (cached is not null
                    && cached.FormatVersion == CurrentFormatVersion
                    && cached.XtfPath == xtfPath
                    && cached.XtfTicks == xtfTicks)
                    return cached.Items;
            }
            catch { /* Cache defekt -> neu bauen */ }
        }

        var items = _extractor.Extract(xtfPath).ToList();
        Directory.CreateDirectory(Path.GetDirectoryName(_cacheFilePath)!);
        AtomicTextFileWriter.WriteAllText(_cacheFilePath,
            JsonSerializer.Serialize(new CacheFile(CurrentFormatVersion, xtfPath, xtfTicks, items), JsonOpts));
        return items;
    }
}
