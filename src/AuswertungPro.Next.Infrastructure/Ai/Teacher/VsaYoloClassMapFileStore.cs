using System.Text.Json;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

namespace AuswertungPro.Next.Infrastructure.Ai.Teacher;

/// <summary>
/// Haelt stabile YOLO-Klassen-IDs. Lesen ist strikt und ohne Schreibwirkung.
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
    private readonly bool _allowAutomaticClassCreation;
    private VsaYoloClassMapDocument? _document;
    private bool _mapFileExists;

    public VsaYoloClassMapFileStore()
        : this(
            () => Path.Combine(KnowledgeBasePaths.GetRoot(), "yolo_class_map.json"),
            allowAutomaticClassCreation: true)
    {
    }

    public VsaYoloClassMapFileStore(string mapPath)
        : this(mapPath, allowAutomaticClassCreation: true)
    {
    }

    public VsaYoloClassMapFileStore(
        string mapPath,
        bool allowAutomaticClassCreation)
        : this(() => mapPath, allowAutomaticClassCreation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mapPath);
    }

    internal VsaYoloClassMapFileStore(Func<string> mapPathProvider)
        : this(mapPathProvider, allowAutomaticClassCreation: true)
    {
    }

    internal VsaYoloClassMapFileStore(
        Func<string> mapPathProvider,
        bool allowAutomaticClassCreation)
    {
        _mapPathProvider = mapPathProvider
            ?? throw new ArgumentNullException(nameof(mapPathProvider));
        _allowAutomaticClassCreation = allowAutomaticClassCreation;
    }

    public int GetClassId(string vsaCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vsaCode);

        lock (_sync)
        {
            EnsureLoaded();
            if (VsaYoloClassMapResolver.TryResolveClassId(
                    _document!,
                    vsaCode,
                    Defaults,
                    out var id))
                return id;

            throw new KeyNotFoundException(
                $"Keine YOLO-Klasse fuer '{vsaCode.Trim()}' in der Klassenkarte vorhanden.");
        }
    }

    public int GetOrAddClassId(string vsaCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vsaCode);

        if (!_allowAutomaticClassCreation)
        {
            throw new InvalidOperationException(
                "Das automatische Anlegen von YOLO-Klassen ist fuer diesen Store deaktiviert.");
        }

        lock (_sync)
        {
            EnsureLoaded();
            if (VsaYoloClassMapResolver.TryResolveClassId(
                    _document!,
                    vsaCode,
                    Defaults,
                    out var id))
            {
                if (!_mapFileExists)
                {
                    Save(_document!);
                    _mapFileExists = true;
                }

                return id;
            }

            var key = VsaYoloClassMapResolver.GetKeyForAddition(
                _document!,
                vsaCode,
                Defaults);
            var classes = new Dictionary<string, int>(
                _document!.Classes,
                StringComparer.OrdinalIgnoreCase)
            {
                [key] = _document.Classes.Count
            };
            var updated = _document.WithClasses(classes);
            VsaYoloClassMapDocumentValidator.Validate(updated);

            Save(updated);
            _document = updated;
            _mapFileExists = true;
            return classes[key];
        }
    }

    public Dictionary<string, int> GetFullMap()
    {
        lock (_sync)
        {
            EnsureLoaded();
            return new Dictionary<string, int>(
                _document!.Classes,
                StringComparer.OrdinalIgnoreCase);
        }
    }

    public async Task ExportClassesTxtAsync(string outputPath)
    {
        var lines = GetFullMap()
            .OrderBy(item => item.Value)
            .Select(item => item.Key)
            .ToArray();
        await AtomicTextFileWriter.WriteAllTextAsync(
                outputPath,
                VsaYoloClassMapDocumentWriter.BuildClassesText(lines))
            .ConfigureAwait(false);
    }

    private void EnsureLoaded()
    {
        if (_document is not null)
            return;

        var path = GetMapPath();
        if (!File.Exists(path))
        {
            _document = new VsaYoloClassMapDocument(
                VsaYoloClassMapFormat.Legacy,
                CreateDefaultMap());
            _mapFileExists = false;
            return;
        }

        try
        {
            _document = VsaYoloClassMapDocumentReader.Read(path);
            _mapFileExists = true;
        }
        catch (Exception ex) when (ex is JsonException
                                   or IOException
                                   or UnauthorizedAccessException
                                   or InvalidDataException
                                   or NotSupportedException
                                   or ArgumentException)
        {
            throw new InvalidDataException(
                $"YOLO-Klassenkarte '{path}' ist nicht lesbar oder ungueltig: {ex.Message}",
                ex);
        }
    }

    private void Save(VsaYoloClassMapDocument document)
        => VsaYoloClassMapDocumentWriter.Write(GetMapPath(), document);

    private static Dictionary<string, int> CreateDefaultMap()
        => new(Defaults, StringComparer.OrdinalIgnoreCase);

    private string GetMapPath()
    {
        var path = _mapPathProvider();
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Path.GetFullPath(path);
    }

}
