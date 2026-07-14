using System.Text.Json;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

namespace AuswertungPro.Next.Infrastructure.Ai.Teacher;

/// <summary>
/// Hält stabile YOLO-Klassen-IDs und speichert neue VSA-Hauptcodes atomar.
/// </summary>
public sealed class VsaYoloClassMapFileStore : IVsaYoloClassMapStore
{
    internal static IReadOnlyDictionary<string, int> Defaults { get; } =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["BCD"] = 0,
            ["BCE"] = 1,
            ["BCA"] = 2,
            ["BCC"] = 3,
            ["BAB"] = 4,
            ["BAC"] = 5,
            ["BAF"] = 6,
            ["BAH"] = 7,
            ["BAI"] = 8,
            ["BAJ"] = 9,
            ["BBB"] = 10,
            ["BBA"] = 11,
            ["BBC"] = 12,
            ["BBD"] = 13,
            ["BDA"] = 14,
            ["BAA"] = 15
        };

    private readonly object _sync = new();
    private readonly Func<string> _mapPathProvider;
    private Dictionary<string, int>? _map;

    public VsaYoloClassMapFileStore()
        : this(() => Path.Combine(KnowledgeBasePaths.GetRoot(), "yolo_class_map.json"))
    {
    }

    public VsaYoloClassMapFileStore(string mapPath)
        : this(() => mapPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mapPath);
    }

    internal VsaYoloClassMapFileStore(Func<string> mapPathProvider)
    {
        _mapPathProvider = mapPathProvider
            ?? throw new ArgumentNullException(nameof(mapPathProvider));
    }

    public int GetClassId(string vsaCode)
    {
        if (string.IsNullOrWhiteSpace(vsaCode))
            return 0;

        var category = ExtractCategory(vsaCode);
        lock (_sync)
        {
            EnsureLoaded();
            if (_map!.TryGetValue(category, out var id))
                return id;

            var nextId = _map.Count > 0 ? _map.Values.Max() + 1 : 0;
            _map[category] = nextId;
            SaveSync();
            return nextId;
        }
    }

    public Dictionary<string, int> GetFullMap()
    {
        lock (_sync)
        {
            EnsureLoaded();
            return new Dictionary<string, int>(_map!, StringComparer.OrdinalIgnoreCase);
        }
    }

    public async Task ExportClassesTxtAsync(string outputPath)
    {
        var lines = GetFullMap()
            .OrderBy(item => item.Value)
            .Select(item => item.Key)
            .ToArray();
        await AtomicTextFileWriter.WriteAllTextAsync(outputPath, BuildClassesText(lines))
            .ConfigureAwait(false);
    }

    private void EnsureLoaded()
    {
        if (_map is not null)
            return;

        var path = _mapPathProvider();
        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path);
                _map = JsonSerializer.Deserialize<Dictionary<string, int>>(json)
                       ?? CreateDefaultMap();
                return;
            }
            catch
            {
                _map = CreateDefaultMap();
                return;
            }
        }

        _map = CreateDefaultMap();
        SaveSync();
    }

    private void SaveSync()
    {
        try
        {
            var path = _mapPathProvider();
            var directory = Path.GetDirectoryName(path);
            if (directory is not null)
                Directory.CreateDirectory(directory);

            var json = JsonSerializer.Serialize(_map, JsonDefaults.Indented);
            AtomicTextFileWriter.WriteAllText(path, json);

            var classesPath = Path.Combine(directory!, "classes.txt");
            var lines = _map!
                .OrderBy(item => item.Value)
                .Select(item => item.Key)
                .ToArray();
            AtomicTextFileWriter.WriteAllText(classesPath, BuildClassesText(lines));
        }
        catch
        {
            // Die Klassenkarte bleibt im Speicher nutzbar, auch wenn die Ausgabe fehlschlägt.
        }
    }

    private static Dictionary<string, int> CreateDefaultMap()
        => new(Defaults, StringComparer.OrdinalIgnoreCase);

    private static string ExtractCategory(string vsaCode)
    {
        var clean = vsaCode.Replace(".", string.Empty).Trim().ToUpperInvariant();
        if (clean.Length < 2)
            return clean;

        if (clean.StartsWith("BCD", StringComparison.Ordinal)) return "BCD";
        if (clean.StartsWith("BCE", StringComparison.Ordinal)) return "BCE";
        if (clean.StartsWith("BCA", StringComparison.Ordinal)) return "BCA";
        if (clean.StartsWith("BCC", StringComparison.Ordinal)) return "BCC";

        if (clean.Length >= 3)
        {
            var prefix = clean[..3];
            if (Defaults.ContainsKey(prefix))
                return prefix;
        }

        return clean.Length >= 3 ? clean[..3] : clean;
    }

    private static string BuildClassesText(IReadOnlyCollection<string> lines)
        => lines.Count == 0
            ? string.Empty
            : string.Join(Environment.NewLine, lines) + Environment.NewLine;
}
