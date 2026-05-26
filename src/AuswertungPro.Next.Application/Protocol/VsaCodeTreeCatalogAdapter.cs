using AuswertungPro.Next.Domain.VsaCatalog;

namespace AuswertungPro.Next.Application.Protocol;

public static class VsaCodeTreeCatalogAdapter
{
    public static void Apply(ICodeCatalogProvider catalog)
    {
        var allCodes = catalog.GetAll()
            .Where(c => !string.IsNullOrWhiteSpace(c.Code))
            .OrderBy(c => c.Code, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (allCodes.Count == 0)
            return;

        VsaCodeTree.Groups.Clear();
        VsaCodeTree.QuantRules.Clear();
        VsaCodeTree.ClockRules.Clear();
        VsaCodeTree.CatalogLabels.Clear();

        foreach (var def in allCodes)
        {
            var code = NormalizeCode(def.Code);
            if (code.Length < 2)
                continue;

            var label = string.IsNullOrWhiteSpace(def.Title) ? code : def.Title.Trim();
            VsaCodeTree.CatalogLabels[code] = label;
            if (!string.IsNullOrWhiteSpace(def.CanonicalCode) &&
                !string.Equals(def.CanonicalCode, code, StringComparison.OrdinalIgnoreCase))
            {
                VsaCodeTree.CatalogLabels.TryAdd(NormalizeCode(def.CanonicalCode), label);
            }

            ApplyQuantRule(code, def);
            ApplyClockRule(code, def);
        }

        foreach (var def in allCodes.Where(c => c.IsSelectable && !c.IsObservedExtension))
        {
            var code = NormalizeCode(def.Code);
            if (code.Length < 2)
                continue;

            var groupKey = code[..2];
            if (!VsaCodeTree.Groups.TryGetValue(groupKey, out var group))
            {
                group = CreateGroup(groupKey, def);
                VsaCodeTree.Groups[groupKey] = group;
            }

            var label = string.IsNullOrWhiteSpace(def.Title) ? code : def.Title.Trim();
            group.Codes[code] = new VsaCodeDef
            {
                Label = label,
                FinalCode = code,
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

        return string.IsNullOrWhiteSpace(type) ? "IKAS" : $"IKAS {type}";
    }

    private static string? ResolveWarning(CodeDefinition def)
    {
        if (string.Equals(def.Source, IkasCatalogSources.WinCanFallback, StringComparison.OrdinalIgnoreCase))
            return "WinCan-Fallback: nicht im IKAS-Hauptkatalog gefunden.";

        if (string.Equals(def.Source, IkasCatalogSources.IkasIcm, StringComparison.OrdinalIgnoreCase))
            return "IKAS-ICM-Regelcode.";

        return null;
    }

    private static void ApplyQuantRule(string code, CodeDefinition def)
    {
        var q1 = FindParameter(def, "Q1");
        var q2 = FindParameter(def, "Q2");
        if (q1 is null && q2 is null)
            return;

        VsaCodeTree.QuantRules[code] = new QuantRule
        {
            Q1 = q1 is null ? null : ToQuantField(q1),
            Q2 = q2 is null ? null : ToQuantField(q2)
        };
    }

    private static void ApplyClockRule(string code, CodeDefinition def)
    {
        var hasClock = def.Parameters.Any(p =>
            string.Equals(p.DataKey, "SchadenlageAnfang", StringComparison.OrdinalIgnoreCase)
            || string.Equals(p.DataKey, "SchadenlageEnde", StringComparison.OrdinalIgnoreCase));

        VsaCodeTree.ClockRules[code] = hasClock
            ? new ClockRule { Mode = "range", Hint = "Lage am Umfang (IKAS)" }
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
