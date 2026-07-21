using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Map;

/// <summary>
/// Gemeinsame Basis fuer die XTF-Geometrie-Caches (Schacht-Punkte, Netz-Geometrie): haelt eine
/// schlanke lokale JSON-Kopie und baut sie nur neu, wenn die XTF neuer ist als der Cache.
/// Item-Typ, Extractor, Cache-Datei, Formatversion und JSON-Optionen legen die abgeleiteten
/// Klassen fest — die Invalidierungs- und atomare Schreiblogik bleibt an genau einer Stelle.
/// </summary>
public abstract class XtfJsonGeometryCache<T>
{
    private readonly string _cacheFilePath;
    private readonly int _formatVersion;
    private readonly JsonSerializerOptions _jsonOpts;

    protected XtfJsonGeometryCache(string cacheFilePath, int formatVersion, JsonSerializerOptions jsonOpts)
    {
        _cacheFilePath = cacheFilePath;
        _formatVersion = formatVersion;
        _jsonOpts = jsonOpts;
    }

    /// <summary>Baut die Geometrie aus der XTF neu (nur bei Cache-Miss aufgerufen).</summary>
    protected abstract IEnumerable<T> Extract(string xtfPath);

    /// <summary>Default-Cachepfad unter %LOCALAPPDATA%/SewerStudio/map/&lt;fileName&gt;.</summary>
    protected static string DefaultCachePath(string fileName) => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SewerStudio", "map", fileName);

    private sealed record CacheFile(int FormatVersion, string XtfPath, long XtfTicks, List<T> Items);

    public IReadOnlyList<T> Load(string xtfPath)
    {
        var xtfTicks = File.GetLastWriteTimeUtc(xtfPath).Ticks;
        if (File.Exists(_cacheFilePath))
        {
            try
            {
                var cached = JsonSerializer.Deserialize<CacheFile>(File.ReadAllText(_cacheFilePath), _jsonOpts);
                if (cached is not null
                    && cached.FormatVersion == _formatVersion
                    && cached.XtfPath == xtfPath
                    && cached.XtfTicks == xtfTicks)
                    return cached.Items;
            }
            catch { /* Cache defekt -> neu bauen */ }
        }

        var items = Extract(xtfPath).ToList();
        Directory.CreateDirectory(Path.GetDirectoryName(_cacheFilePath)!);
        AtomicTextFileWriter.WriteAllText(_cacheFilePath,
            JsonSerializer.Serialize(new CacheFile(_formatVersion, xtfPath, xtfTicks, items), _jsonOpts));
        return items;
    }
}
