using AuswertungPro.Next.Domain.VsaCatalog;

namespace AuswertungPro.Next.Application.Protocol;

public interface IVsaCodeSelectionCatalog
{
    IReadOnlyDictionary<string, GroupDef> Groups { get; }
    string? LookupExactLabel(string code) => null;
    string? LookupNavigationLabel(string codePrefix) => null;
    VsaCodeDef? LookupExactCodeDef(string code) => null;
    bool IsSelectableCode(string code) => false;
    (QuantField? Q1, QuantField? Q2) GetQuantRule(string codeKey, string? char1Key);
    ClockRule GetClockRule(string codeKey);
    IReadOnlyDictionary<string, string>? GetChar2Options(VsaCodeDef codeDef, string char1Key);
    bool IsInvalidCombo(VsaCodeDef codeDef, string char1Key, string char2Key);
}

public sealed class EmptyVsaCodeSelectionCatalog : IVsaCodeSelectionCatalog
{
    public static EmptyVsaCodeSelectionCatalog Instance { get; } = new();

    private EmptyVsaCodeSelectionCatalog()
    {
    }

    public IReadOnlyDictionary<string, GroupDef> Groups { get; } =
        new Dictionary<string, GroupDef>(StringComparer.OrdinalIgnoreCase);

    public (QuantField? Q1, QuantField? Q2) GetQuantRule(string codeKey, string? char1Key)
        => (null, null);

    public ClockRule GetClockRule(string codeKey)
        => new() { Mode = "none" };

    public bool IsSelectableCode(string code) => false;

    public IReadOnlyDictionary<string, string>? GetChar2Options(VsaCodeDef codeDef, string char1Key)
        => null;

    public bool IsInvalidCombo(VsaCodeDef codeDef, string char1Key, string char2Key)
        => false;
}

public sealed class CodeCatalogSelectionCatalog : IVsaCodeSelectionCatalog
{
    private static readonly ClockRule DefaultClockRule = new()
    {
        Mode = "range",
        Hint = "Lage am Umfang (Uhrzeiger)"
    };

    private readonly Dictionary<string, GroupDef> _groups = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _labels = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, VsaCodeDef> _exactCodeDefs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, QuantRule> _quantRules = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ClockRule> _clockRules = new(StringComparer.OrdinalIgnoreCase);

    public CodeCatalogSelectionCatalog(ICodeCatalogProvider catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        Build(catalog.GetAll());
    }

    public IReadOnlyDictionary<string, GroupDef> Groups => _groups;

    public string? LookupExactLabel(string code)
        => _labels.TryGetValue(NormalizeCode(code), out var label)
            ? label
            : null;

    public string? LookupNavigationLabel(string codePrefix)
    {
        var prefix = NormalizeCode(codePrefix);
        if (prefix.Length == 0)
            return null;

        var descendantLabels = _exactCodeDefs
            .Where(pair =>
                pair.Key.Length > prefix.Length
                && pair.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(pair => pair.Value.Label)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (descendantLabels.Count == 0)
            return null;

        var commonLabel = FindCommonWordPrefix(descendantLabels);
        if (string.IsNullOrWhiteSpace(commonLabel))
            return null;

        var parentCode = prefix.Length > 3 ? prefix[..^1] : prefix;
        return string.Equals(
            commonLabel,
            LookupExactLabel(parentCode),
            StringComparison.OrdinalIgnoreCase)
            ? null
            : commonLabel;
    }

    public VsaCodeDef? LookupExactCodeDef(string code)
        => _exactCodeDefs.TryGetValue(NormalizeCode(code), out var codeDef)
            ? codeDef
            : null;

    public bool IsSelectableCode(string code)
        => _exactCodeDefs.ContainsKey(NormalizeCode(code));

    public (QuantField? Q1, QuantField? Q2) GetQuantRule(string codeKey, string? char1Key)
    {
        if (!_quantRules.TryGetValue(NormalizeCode(codeKey), out var rule))
            return (null, null);

        var q1 = rule.Q1;
        if (q1 is { Pflicht: "V" } && rule.Q1PerChar1 is not null && char1Key is not null)
            q1 = rule.Q1PerChar1.TryGetValue(char1Key, out var perChar) ? perChar : null;

        return (q1, rule.Q2);
    }

    public ClockRule GetClockRule(string codeKey)
        => _clockRules.TryGetValue(NormalizeCode(codeKey), out var rule)
            ? rule
            : DefaultClockRule;

    public IReadOnlyDictionary<string, string>? GetChar2Options(VsaCodeDef codeDef, string char1Key)
    {
        if (codeDef.Char2PerChar1 is not null)
            return codeDef.Char2PerChar1.TryGetValue(char1Key, out var char2) ? char2 : null;

        if (codeDef.Char2 is not null)
            return codeDef.Char2;

        if (codeDef.Char1 is not null
            && codeDef.Char1.TryGetValue(char1Key, out var charDef)
            && charDef.Char2 is not null)
        {
            return charDef.Char2;
        }

        return null;
    }

    public bool IsInvalidCombo(VsaCodeDef codeDef, string char1Key, string char2Key)
    {
        if (codeDef.AllValid)
            return false;

        return codeDef.Invalid is not null
            && codeDef.Invalid.TryGetValue(char1Key, out var invalid)
            && invalid.Contains(char2Key);
    }

    private void Build(IReadOnlyList<CodeDefinition> definitions)
    {
        var allCodes = definitions
            .Where(c => !string.IsNullOrWhiteSpace(c.Code))
            .OrderBy(c => c.Code, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var def in allCodes)
        {
            var code = NormalizeCode(def.Code);
            if (code.Length < 2)
                continue;

            if (IsAuthoritativeSelectable(def) && !string.IsNullOrWhiteSpace(def.Title))
            {
                _labels[code] = def.Title.Trim();
                _exactCodeDefs[code] = ToExactCodeDef(code, def);
            }

            ApplyQuantRule(code, def);
            ApplyClockRule(code, def);
        }

        foreach (var def in allCodes.Where(IsAuthoritativeSelectable))
        {
            var code = NormalizeCode(def.Code);
            if (code.Length < 2)
                continue;

            var groupKey = code[..2];
            if (!_groups.TryGetValue(groupKey, out var group))
            {
                group = CreateGroup(groupKey, def);
                _groups[groupKey] = group;
            }

            var label = string.IsNullOrWhiteSpace(def.Title) ? code : def.Title.Trim();
            group.Codes[code] = new VsaCodeDef
            {
                Label = label,
                FinalCode = code,
                Source = def.Source,
                CanonicalCode = string.IsNullOrWhiteSpace(def.CanonicalCode) ? code : NormalizeCode(def.CanonicalCode),
                StandardAnnotation = def.StandardAnnotation,
                Warn = ResolveWarning(def)
            };
        }
    }

    private static GroupDef CreateGroup(string groupKey, CodeDefinition def)
    {
        var (label, color, icon) = groupKey switch
        {
            "BA" => ("Baulicher Zustand", "#DC2626", "BA"),
            "BB" => ("Betrieblicher Zustand", "#F59E0B", "BB"),
            "BC" => ("Anschluesse/Reparaturen", "#2563EB", "BC"),
            "BD" => ("Inspektion/Betrieb", "#64748B", "BD"),
            "AE" => ("Geometrie/Profil", "#0F766E", "AE"),
            "DA" => ("Schacht baulich", "#DC2626", "DA"),
            "DB" => ("Schacht Oberflaeche", "#F59E0B", "DB"),
            "DC" => ("Schacht Anschluesse", "#2563EB", "DC"),
            "DD" => ("Schacht Betrieb", "#64748B", "DD"),
            _ => (ResolveObjectType(def), "#64748B", groupKey)
        };

        return new GroupDef(label, color, icon, new Dictionary<string, VsaCodeDef>(StringComparer.OrdinalIgnoreCase));
    }

    private static string ResolveObjectType(CodeDefinition def)
    {
        var type = def.CategoryPath.FirstOrDefault(x =>
            string.Equals(x, "Kanal", StringComparison.OrdinalIgnoreCase)
            || string.Equals(x, "Schacht", StringComparison.OrdinalIgnoreCase));

        return string.IsNullOrWhiteSpace(type) ? "VSA-KEK 2020" : $"VSA-KEK 2020 {type}";
    }

    private static string? ResolveWarning(CodeDefinition def)
    {
        if (string.Equals(def.Source, VsaKekCatalogSources.WinCanFallback, StringComparison.OrdinalIgnoreCase))
            return "WinCan-Fallback: nicht im VSA-KEK-2020-Hauptkatalog gefunden.";

        if (string.Equals(def.Source, VsaKekCatalogSources.Icm, StringComparison.OrdinalIgnoreCase))
            return "VSA-KEK-2020-ICM-Regelcode.";

        if (string.Equals(def.Source, VsaKekCatalogSources.Heading, StringComparison.OrdinalIgnoreCase))
            return "VSA-KEK-2020-Basisgruppe.";

        return null;
    }

    private void ApplyQuantRule(string code, CodeDefinition def)
    {
        var q1 = FindParameter(def, "Q1");
        var q2 = FindParameter(def, "Q2");
        if (q1 is null && q2 is null)
            return;

        _quantRules[code] = new QuantRule
        {
            Q1 = q1 is null ? null : ToQuantField(q1),
            Q2 = q2 is null ? null : ToQuantField(q2)
        };
    }

    private void ApplyClockRule(string code, CodeDefinition def)
    {
        var hasClock = def.Parameters.Any(p =>
            string.Equals(p.DataKey, "SchadenlageAnfang", StringComparison.OrdinalIgnoreCase)
            || string.Equals(p.DataKey, "SchadenlageEnde", StringComparison.OrdinalIgnoreCase));

        _clockRules[code] = hasClock
            ? new ClockRule { Mode = "range", Hint = "Lage am Umfang (VSA-KEK 2020)" }
            : new ClockRule { Mode = "none" };
    }

    private static CodeParameter? FindParameter(CodeDefinition def, string dataKey)
        => def.Parameters.FirstOrDefault(p =>
            string.Equals(p.DataKey, dataKey, StringComparison.OrdinalIgnoreCase));

    private static QuantField ToQuantField(CodeParameter parameter)
        => new()
        {
            Pflicht = parameter.Required ? "P" : "O",
            Label = string.IsNullOrWhiteSpace(parameter.Name) ? parameter.DataKey : parameter.Name,
            Einheit = NormalizeUnit(parameter.Unit)
        };

    private static VsaCodeDef ToExactCodeDef(string code, CodeDefinition def)
        => new()
        {
            Label = def.Title.Trim(),
            FinalCode = code,
            Source = def.Source,
            CanonicalCode = string.IsNullOrWhiteSpace(def.CanonicalCode)
                ? code
                : NormalizeCode(def.CanonicalCode),
            StandardAnnotation = def.StandardAnnotation,
            Warn = ResolveWarning(def)
        };

    private static bool IsAuthoritativeSelectable(CodeDefinition def)
        => def.IsSelectable
           && !def.IsObservedExtension
           && IsAuthoritativeSource(def.Source);

    private static bool IsAuthoritativeSource(string? source)
        => string.Equals(source, VsaKekCatalogSources.Ili, StringComparison.OrdinalIgnoreCase)
           || string.Equals(source, VsaKekCatalogSources.Icm, StringComparison.OrdinalIgnoreCase)
           || string.Equals(source, VsaKekCatalogSources.Heading, StringComparison.OrdinalIgnoreCase);

    private static string? FindCommonWordPrefix(IReadOnlyList<string> labels)
    {
        var words = labels
            .Select(label => label.Split(
                [' ', '\t', '\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToList();
        var commonCount = words.Min(parts => parts.Length);

        for (var index = 0; index < commonCount; index++)
        {
            var first = words[0][index];
            if (words.Skip(1).Any(parts =>
                    !string.Equals(first, parts[index], StringComparison.OrdinalIgnoreCase)))
            {
                commonCount = index;
                break;
            }
        }

        while (commonCount > 0 && IsTrailingConnector(words[0][commonCount - 1]))
            commonCount--;

        return commonCount == 0
            ? null
            : string.Join(' ', words[0].Take(commonCount));
    }

    private static bool IsTrailingConnector(string value)
    {
        var word = value.Trim().Trim(',', ':', ';', '-', '\u2013', '\u2014');
        return word.Equals("nach", StringComparison.OrdinalIgnoreCase)
               || word.Equals("mit", StringComparison.OrdinalIgnoreCase)
               || word.Equals("und", StringComparison.OrdinalIgnoreCase)
               || word.Equals("oder", StringComparison.OrdinalIgnoreCase)
               || word.Equals("von", StringComparison.OrdinalIgnoreCase)
               || word.Equals("im", StringComparison.OrdinalIgnoreCase)
               || word.Equals("in", StringComparison.OrdinalIgnoreCase)
               || word.Equals("am", StringComparison.OrdinalIgnoreCase)
               || word.Equals("an", StringComparison.OrdinalIgnoreCase)
               || word.Equals("zu", StringComparison.OrdinalIgnoreCase)
               || word.Equals("zur", StringComparison.OrdinalIgnoreCase)
               || word.Equals("zum", StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeUnit(string? unit)
    {
        var value = unit?.Trim();
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.ToLowerInvariant() switch
        {
            "mm" or "millimeter" => "mm",
            "%" or "prozent" or "percent" => "%",
            "\u00b0" or "grad" or "degree" or "degrees" or "deg" => "\u00b0",
            "st" or "stk" or "stk." or "stueck" or "st\u00fcck" => "Stk.",
            _ => value
        };
    }

    private static string NormalizeCode(string? code)
        => (code ?? string.Empty).Trim().ToUpperInvariant();
}
