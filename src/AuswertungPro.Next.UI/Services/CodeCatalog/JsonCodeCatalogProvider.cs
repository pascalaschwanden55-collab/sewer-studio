using System;
using System.IO;
using System.Text.Json;

namespace AuswertungPro.Next.UI.Services.CodeCatalog;

public class JsonCodeCatalogProvider : ILocalCodeCatalogProvider
{
    private readonly string _catalogPath;
    private CodeCatalog _catalog = new();
    private readonly Dictionary<string, CodeDef> _byCode = new(StringComparer.OrdinalIgnoreCase);
    private List<string> _allowedCodes = new();
    private CodeCatalogTreeNode _tree = new("Root");

    public DateTimeOffset LoadedAt { get; private set; }

    public IReadOnlyList<string> AllowedCodes => _allowedCodes;

    public JsonCodeCatalogProvider(string baseDirectory)
    {
        // Erwartet: <app>/Data/vsa_codes.json (weil wir es ins Output kopieren)
        _catalogPath = Path.Combine(baseDirectory, "Data", "vsa_codes.json");
        Load();
    }

    public CodeDef? Get(string code)
        => _byCode.TryGetValue(code ?? "", out var def) ? def : null;

    public IReadOnlyList<CodeDef> Search(string query, int max = 50)
    {
        query ??= "";
        query = query.Trim();

        if (query.Length == 0)
            return _catalog.Codes.Take(max).ToList();

        return _catalog.Codes
            .Where(c =>
                c.Code.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                c.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                c.CategoryPath.Any(p => p.Contains(query, StringComparison.OrdinalIgnoreCase)))
            .Take(max)
            .ToList();
    }

    public CodeCatalogTreeNode GetTree() => _tree;

    private void Load()
    {
        if (!File.Exists(_catalogPath))
        {
            // Klarer Fehler, damit man sofort weiß was fehlt.
            throw new FileNotFoundException($"VSA Code-Katalog nicht gefunden: {_catalogPath}");
        }

        var json = File.ReadAllText(_catalogPath);

        var opts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        _catalog = JsonSerializer.Deserialize<CodeCatalog>(json, opts) ?? new CodeCatalog();

        // Index
        _byCode.Clear();
        foreach (var c in _catalog.Codes)
        {
            if (string.IsNullOrWhiteSpace(c.Code))
                continue;

            _byCode[c.Code.Trim()] = c;
        }

        _allowedCodes = _byCode.Keys
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Tree
        _tree = new CodeCatalogTreeNode("Root");
        foreach (var c in _catalog.Codes)
        {
            var node = _tree;
            foreach (var part in c.CategoryPath ?? new List<string>())
                node = node.GetOrAddChild(part);

            node.Codes.Add(c);
        }

        LoadedAt = DateTimeOffset.UtcNow;
    }
}

// Optional: falls irgendwo schon "DefaultCodeCatalogProvider" verwendet wird.
public sealed class DefaultCodeCatalogProvider : JsonCodeCatalogProvider
{
    public DefaultCodeCatalogProvider(string baseDirectory) : base(baseDirectory) { }
}
