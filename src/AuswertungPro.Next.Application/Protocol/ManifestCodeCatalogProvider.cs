using System.Text.Json;

namespace AuswertungPro.Next.Application.Protocol;

public sealed class ManifestCodeCatalogProvider : ICodeCatalogProvider
{
    private readonly string _catalogPath;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private List<CodeDefinition> _codes = new();
    public IReadOnlyList<string> LastLoadErrors { get; private set; } = Array.Empty<string>();
    public IReadOnlyList<string> LastLoadWarnings { get; private set; } = Array.Empty<string>();

    public ManifestCodeCatalogProvider(string catalogPath)
    {
        _catalogPath = catalogPath;
        Reload();
    }

    public IReadOnlyList<CodeDefinition> GetAll()
        => _codes.Select(CloneCode).ToList();

    public bool TryGet(string code, out CodeDefinition def)
    {
        var normalized = NormalizeCode(code);
        var match = _codes.FirstOrDefault(x => string.Equals(x.Code, normalized, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            def = new CodeDefinition();
            return false;
        }

        def = CloneCode(match);
        return true;
    }

    public void Save(IReadOnlyList<CodeDefinition> codes)
    {
        throw new InvalidOperationException("Manifest-Katalog ist read-only. Bitte das VSA-KEK-2020-Manifest neu generieren.");
    }

    public IReadOnlyList<string> AllowedCodes()
        => CodeCatalogNormalization.AllowedCodes(_codes);

    public IReadOnlyList<string> Validate(IReadOnlyList<CodeDefinition>? codes = null)
    {
        var list = NormalizeCodes(codes ?? _codes);
        var errors = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < list.Count; i++)
        {
            var codeDef = list[i];
            var row = i + 1;

            if (string.IsNullOrWhiteSpace(codeDef.Code))
                errors.Add($"Code fehlt (Eintrag #{row}).");
            else if (!seen.Add(codeDef.Code))
                errors.Add($"Duplikat-Code '{codeDef.Code}'.");

            if (string.IsNullOrWhiteSpace(codeDef.Title))
                errors.Add($"Title fehlt fuer Code '{(string.IsNullOrWhiteSpace(codeDef.Code) ? $"#{row}" : codeDef.Code)}'.");
        }

        return errors;
    }

    public void Reload()
    {
        if (!File.Exists(_catalogPath))
        {
            _codes = new List<CodeDefinition>();
            LastLoadWarnings = Array.Empty<string>();
            LastLoadErrors = new[] { $"Manifest-Katalog nicht gefunden: {_catalogPath}" };
            return;
        }

        try
        {
            var json = File.ReadAllText(_catalogPath);
            var document = JsonSerializer.Deserialize<CodeCatalogDocument>(json, _jsonOptions) ?? new CodeCatalogDocument();
            var normalized = NormalizeCodes(document.Codes);
            _codes = DeduplicateCodes(normalized, out var warnings);
            LastLoadWarnings = warnings;
            LastLoadErrors = Array.Empty<string>();
        }
        catch (Exception ex)
        {
            _codes = new List<CodeDefinition>();
            LastLoadWarnings = Array.Empty<string>();
            LastLoadErrors = new[] { $"Manifest-Katalog konnte nicht gelesen werden: {ex.Message}" };
        }
    }

    private static List<CodeDefinition> NormalizeCodes(IEnumerable<CodeDefinition> codes)
        => CodeCatalogNormalization.NormalizeCodes(codes);

    private static CodeDefinition CloneCode(CodeDefinition source)
        => CodeDefinitionCloning.CloneCode(source);

    private static string NormalizeCode(string? code)
        => CodeCatalogNormalization.NormalizeCode(code);

    private static List<CodeDefinition> DeduplicateCodes(
        IReadOnlyList<CodeDefinition> codes,
        out List<string> warnings)
    {
        warnings = new List<string>();
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var byCode = new Dictionary<string, CodeDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var def in codes)
        {
            if (string.IsNullOrWhiteSpace(def.Code))
                continue;

            counts[def.Code] = counts.TryGetValue(def.Code, out var count) ? count + 1 : 1;
            byCode.TryAdd(def.Code, def);
        }

        foreach (var kvp in counts.Where(k => k.Value > 1).OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            warnings.Add($"Duplikat-Code '{kvp.Key}' ({kvp.Value}x)");

        return byCode.Values.OrderBy(c => c.Code, StringComparer.OrdinalIgnoreCase).ToList();
    }
}

public sealed class SourceDecoratingCodeCatalogProvider : ICodeCatalogProvider
{
    private readonly ICodeCatalogProvider _inner;
    private readonly string _source;

    public SourceDecoratingCodeCatalogProvider(ICodeCatalogProvider inner, string source)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _source = string.IsNullOrWhiteSpace(source) ? "Unbekannt" : source.Trim();
    }

    public IReadOnlyList<CodeDefinition> GetAll()
        => _inner.GetAll().Select(Decorate).ToList();

    public bool TryGet(string code, out CodeDefinition def)
    {
        if (!_inner.TryGet(code, out var innerDef))
        {
            def = new CodeDefinition();
            return false;
        }

        def = Decorate(innerDef);
        return true;
    }

    public void Save(IReadOnlyList<CodeDefinition> codes)
        => _inner.Save(codes);

    public IReadOnlyList<string> AllowedCodes()
        => _inner.AllowedCodes();

    public IReadOnlyList<string> Validate(IReadOnlyList<CodeDefinition>? codes = null)
        => _inner.Validate(codes);

    public void Reload()
    {
        switch (_inner)
        {
            case XmlCodeCatalogProvider xml:
                xml.Reload();
                break;
            case JsonCodeCatalogProvider json:
                json.Reload();
                break;
            case ManifestCodeCatalogProvider manifest:
                manifest.Reload();
                break;
            case CompositeCodeCatalogProvider composite:
                composite.Reload();
                break;
        }
    }

    public IReadOnlyList<string> GetWarnings()
    {
        return _inner switch
        {
            XmlCodeCatalogProvider xml => xml.LastLoadWarnings,
            JsonCodeCatalogProvider json => json.LastLoadWarnings,
            ManifestCodeCatalogProvider manifest => manifest.LastLoadWarnings.Concat(manifest.LastLoadErrors).ToList(),
            CompositeCodeCatalogProvider composite => composite.GetWarnings(),
            _ => Array.Empty<string>()
        };
    }

    private CodeDefinition Decorate(CodeDefinition source)
    {
        source.Source = string.IsNullOrWhiteSpace(source.Source) ? _source : source.Source;
        source.CanonicalCode = string.IsNullOrWhiteSpace(source.CanonicalCode) ? source.Code : source.CanonicalCode;
        return source;
    }
}
