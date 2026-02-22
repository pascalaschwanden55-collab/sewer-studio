using System.Text.Json;
using System.Text.Json.Serialization;

namespace AuswertungPro.Next.Application.Protocol;

public sealed class CodeCatalogDocument
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("codes")]
    public List<CodeDefinition> Codes { get; set; } = new();
}

public sealed class CodeDefinition
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("group")]
    public string Group { get; set; } = "Unbekannt";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("categoryPath")]
    public List<string> CategoryPath { get; set; } = new();

    [JsonPropertyName("parameters")]
    public List<CodeParameter> Parameters { get; set; } = new();

    [JsonPropertyName("examples")]
    public List<string> Examples { get; set; } = new();

    [JsonPropertyName("requiresRange")]
    public bool RequiresRange { get; set; }

    [JsonPropertyName("rangeThresholdM")]
    public double? RangeThresholdM { get; set; }

    [JsonPropertyName("rangeThresholdText")]
    public string? RangeThresholdText { get; set; }
}

public sealed class CodeParameter
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "string";

    [JsonPropertyName("allowedValues")]
    public List<string>? AllowedValues { get; set; }

    [JsonPropertyName("unit")]
    public string? Unit { get; set; }

    [JsonPropertyName("required")]
    public bool Required { get; set; }
}

public interface ICodeCatalogProvider
{
    IReadOnlyList<CodeDefinition> GetAll();
    bool TryGet(string code, out CodeDefinition def);
    void Save(IReadOnlyList<CodeDefinition> codes);
    IReadOnlyList<string> AllowedCodes();
    IReadOnlyList<string> Validate(IReadOnlyList<CodeDefinition>? codes = null);
}

public sealed class JsonCodeCatalogProvider : ICodeCatalogProvider
{
    private readonly string _catalogPath;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private List<CodeDefinition> _codes = new();
    public IReadOnlyList<string> LastLoadErrors { get; private set; } = Array.Empty<string>();
    public IReadOnlyList<string> LastLoadWarnings { get; private set; } = Array.Empty<string>();

    public JsonCodeCatalogProvider(string catalogPath)
    {
        _catalogPath = catalogPath;
        EnsureCatalogExists();
        LoadFromDisk();
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
        var normalized = NormalizeCodes(codes ?? Array.Empty<CodeDefinition>());
        var errors = Validate(normalized);
        if (errors.Count > 0)
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));

        var document = new CodeCatalogDocument
        {
            Version = 1,
            Codes = normalized
        };

        var directory = Path.GetDirectoryName(_catalogPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(document, _jsonOptions);
        File.WriteAllText(_catalogPath, json);
        _codes = normalized;
    }

    public IReadOnlyList<string> AllowedCodes()
    {
        return _codes
            .Select(x => x.Code)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

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
            else
            {
                if (codeDef.Code.Any(char.IsWhiteSpace))
                    errors.Add($"Code '{codeDef.Code}' darf keine Leerzeichen enthalten.");

                if (!seen.Add(codeDef.Code))
                    errors.Add($"Duplikat-Code '{codeDef.Code}'.");
            }

            if (string.IsNullOrWhiteSpace(codeDef.Title))
                errors.Add($"Title fehlt fuer Code '{SafeCodeLabel(codeDef.Code, row)}'.");
        }

        return errors;
    }

    private void EnsureCatalogExists()
    {
        var directory = Path.GetDirectoryName(_catalogPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        if (File.Exists(_catalogPath))
            return;

        var empty = new CodeCatalogDocument
        {
            Version = 1,
            Codes = new List<CodeDefinition>()
        };
        File.WriteAllText(_catalogPath, JsonSerializer.Serialize(empty, _jsonOptions));
    }

    private void LoadFromDisk()
    {
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
            LastLoadErrors = new[]
            {
                $"Code-Katalog konnte nicht gelesen werden: {ex.Message}"
            };
        }
    }

    public void Reload()
    {
        LoadFromDisk();
    }

    private static List<CodeDefinition> NormalizeCodes(IEnumerable<CodeDefinition> codes)
    {
        return (codes ?? Array.Empty<CodeDefinition>())
            .Select(CloneCode)
            .Select(NormalizeCodeDefinition)
            .ToList();
    }

    private static CodeDefinition NormalizeCodeDefinition(CodeDefinition code)
    {
        code.Code = NormalizeCode(code.Code);
        code.Title = (code.Title ?? string.Empty).Trim();
        code.Group = string.IsNullOrWhiteSpace(code.Group) ? "Unbekannt" : code.Group.Trim();
        code.Description = string.IsNullOrWhiteSpace(code.Description) ? null : code.Description.Trim();
        code.CategoryPath = (code.CategoryPath ?? new List<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToList();
        code.Parameters = (code.Parameters ?? new List<CodeParameter>()).Select(CloneParameter).ToList();
        code.Examples = (code.Examples ?? new List<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToList();
        if (code.RangeThresholdM is not null && code.RangeThresholdM <= 0)
            code.RangeThresholdM = null;

        return code;
    }

    private static CodeDefinition CloneCode(CodeDefinition source)
    {
        return new CodeDefinition
        {
            Code = source.Code ?? string.Empty,
            Title = source.Title ?? string.Empty,
            Group = source.Group ?? "Unbekannt",
            Description = source.Description,
            CategoryPath = (source.CategoryPath ?? new List<string>()).ToList(),
            Parameters = (source.Parameters ?? new List<CodeParameter>()).Select(CloneParameter).ToList(),
            Examples = (source.Examples ?? new List<string>()).ToList(),
            RequiresRange = source.RequiresRange,
            RangeThresholdM = source.RangeThresholdM,
            RangeThresholdText = source.RangeThresholdText
        };
    }

    private static CodeParameter CloneParameter(CodeParameter source)
    {
        return new CodeParameter
        {
            Name = source.Name ?? string.Empty,
            Type = string.IsNullOrWhiteSpace(source.Type) ? "string" : source.Type.Trim(),
            AllowedValues = source.AllowedValues?.ToList(),
            Unit = string.IsNullOrWhiteSpace(source.Unit) ? null : source.Unit.Trim(),
            Required = source.Required
        };
    }

    private static string NormalizeCode(string? code)
    {
        return (code ?? string.Empty).Trim().ToUpperInvariant();
    }

    private static string SafeCodeLabel(string code, int row)
    {
        return string.IsNullOrWhiteSpace(code) ? $"#{row}" : code;
    }

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

            var code = def.Code.Trim();
            counts[code] = counts.TryGetValue(code, out var count) ? count + 1 : 1;

            if (!byCode.TryGetValue(code, out var existing))
            {
                byCode[code] = def;
                continue;
            }

            var chosen = ChoosePreferred(existing, def);
            if (!ReferenceEquals(chosen, existing))
                byCode[code] = chosen;
        }

        foreach (var kvp in counts.Where(k => k.Value > 1).OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            warnings.Add($"Duplikat-Code '{kvp.Key}' ({kvp.Value}x)");

        return byCode.Values
            .OrderBy(c => c.Code, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static CodeDefinition ChoosePreferred(CodeDefinition first, CodeDefinition second)
    {
        var scoreFirst = Score(first);
        var scoreSecond = Score(second);
        if (scoreSecond > scoreFirst)
            return second;
        if (scoreFirst > scoreSecond)
            return first;

        var descFirst = first.Description?.Length ?? 0;
        var descSecond = second.Description?.Length ?? 0;
        if (descSecond > descFirst)
            return second;
        if (descFirst > descSecond)
            return first;

        return first;
    }

    private static int Score(CodeDefinition def)
    {
        var score = 0;
        if (!string.IsNullOrWhiteSpace(def.Title) && !string.Equals(def.Title, def.Code, StringComparison.OrdinalIgnoreCase))
            score += 3;
        if (!string.IsNullOrWhiteSpace(def.Description))
            score += 2;
        if (!string.IsNullOrWhiteSpace(def.Group) && !string.Equals(def.Group, "Unbekannt", StringComparison.OrdinalIgnoreCase))
            score += 1;
        score += Math.Min(def.CategoryPath?.Count ?? 0, 3);
        score += Math.Min(def.Parameters?.Count ?? 0, 3);
        score += Math.Min(def.Examples?.Count ?? 0, 2);
        if (def.RequiresRange)
            score += 1;
        if (def.RangeThresholdM is not null)
            score += 1;
        if (!string.IsNullOrWhiteSpace(def.RangeThresholdText))
            score += 1;
        return score;
    }
}
