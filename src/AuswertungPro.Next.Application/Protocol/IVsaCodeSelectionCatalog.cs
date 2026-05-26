using AuswertungPro.Next.Domain.VsaCatalog;

namespace AuswertungPro.Next.Application.Protocol;

public interface IVsaCodeSelectionCatalog
{
    IReadOnlyDictionary<string, GroupDef> Groups { get; }
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
    private readonly Dictionary<string, QuantRule> _quantRules = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ClockRule> _clockRules = new(StringComparer.OrdinalIgnoreCase);

    public CodeCatalogSelectionCatalog(ICodeCatalogProvider catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        Build(catalog.GetAll());
    }

    public IReadOnlyDictionary<string, GroupDef> Groups => _groups;

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

            ApplyQuantRule(code, def);
            ApplyClockRule(code, def);
        }

        foreach (var def in allCodes.Where(c => c.IsSelectable && !c.IsObservedExtension))
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
            Einheit = parameter.Unit
        };

    private static string NormalizeCode(string? code)
        => (code ?? string.Empty).Trim().ToUpperInvariant();
}
