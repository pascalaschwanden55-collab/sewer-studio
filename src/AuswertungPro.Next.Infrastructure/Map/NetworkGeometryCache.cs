using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace AuswertungPro.Next.Infrastructure.Map;

/// <summary>
/// Haelt eine schlanke lokale Kopie der Netz-Geometrie. Re-Parse nur, wenn die
/// XTF neuer ist als der Cache. Default-Cache: %LOCALAPPDATA%/SewerStudio/map/network_cache.json
/// </summary>
public sealed class NetworkGeometryCache
{
    private readonly string _cacheFilePath;
    private readonly XtfNetworkExtractor _extractor = new();

    // Version 2: IncludeFields=true serialisiert Wertetupel-Felder (Item1/Item2) korrekt.
    // Aeltere Caches (Version fehlt oder != 2) werden verworfen und neu gebaut.
    private const int CurrentFormatVersion = 2;

    // IncludeFields=true ist noetig, damit Wertetupel-Felder (Item1/Item2) serialisiert werden.
    // Ohne diese Option liefert System.Text.Json nur leere Objekte {} fuer jeden Punkt.
    private static readonly JsonSerializerOptions JsonOpts = new() { IncludeFields = true };

    public NetworkGeometryCache(string? cacheFilePath = null)
    {
        _cacheFilePath = cacheFilePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SewerStudio", "map", "network_cache.json");
    }

    private sealed record CacheFile(int FormatVersion, string XtfPath, long XtfTicks, List<HaltungGeometry> Items);

    public IReadOnlyList<HaltungGeometry> Load(string xtfPath)
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
        File.WriteAllText(_cacheFilePath,
            JsonSerializer.Serialize(new CacheFile(CurrentFormatVersion, xtfPath, xtfTicks, items), JsonOpts));
        return items;
    }
}
