using AuswertungPro.Next.Domain.VsaCatalog;

namespace AuswertungPro.Next.Application.Protocol;

/// <summary>
/// Picker-Katalog mit der gewohnten Kanal-Anordnung (Gruppen → Hauptcode →
/// Char1 → Char2 aus dem kuratierten <see cref="VsaCodeTree"/>). Der aktive
/// Manifestkatalog bleibt für Endcode-Whitelist und exakten Klartext massgebend.
/// Der Baum liefert die semantisch vollständigen Mengen-/Uhrregeln der angebotenen
/// Kanalcodes, weil das Manifest Einheiten und Grenzen vielfach nicht enthält.
/// </summary>
public sealed class VsaCodeTreeSelectionCatalog : IVsaCodeSelectionCatalog
{
    private readonly IVsaCodeSelectionCatalog _rules;

    public VsaCodeTreeSelectionCatalog(IVsaCodeSelectionCatalog ruleSource)
    {
        _rules = ruleSource ?? throw new ArgumentNullException(nameof(ruleSource));
    }

    // ── Anordnung: kuratierter Baum (ISYBAU) ─────────────────────────────
    public IReadOnlyDictionary<string, GroupDef> Groups => VsaCodeTree.Groups;

    public string? LookupExactLabel(string code)
        => _rules.LookupExactLabel(code);

    public string? LookupNavigationLabel(string codePrefix)
        => _rules.LookupNavigationLabel(codePrefix);

    public VsaCodeDef? LookupExactCodeDef(string code)
        => _rules.LookupExactCodeDef(code);

    public bool IsSelectableCode(string code)
        => _rules.IsSelectableCode(code);

    public IReadOnlyDictionary<string, string>? GetChar2Options(VsaCodeDef codeDef, string char1Key)
        => VsaCodeTree.GetChar2Options(codeDef, char1Key);

    public bool IsInvalidCombo(VsaCodeDef codeDef, string char1Key, string char2Key)
        => VsaCodeTree.IsInvalidCombo(codeDef, char1Key, char2Key);

    // ── Regeln: kuratierte Kanalsemantik; unbekannte Codes → Manifest ─────
    public (QuantField? Q1, QuantField? Q2) GetQuantRule(string codeKey, string? char1Key)
    {
        var baseCode = NormalizeBaseCode(codeKey);

        // Das generierte Manifest kennt bei Q1/Q2 vielfach nur die
        // Feld-Praesenz, aber weder Einheit noch fachliche Grenzwerte. Fuer die
        // im Picker kuratierten VSA-Codes ist deshalb die semantische Regel des
        // Codebaums massgebend. Nur unbekannte Codes fallen auf den Katalog.
        return VsaCodeTree.QuantRules.ContainsKey(baseCode)
            ? VsaCodeTree.GetQuantRule(baseCode, char1Key)
            : _rules.GetQuantRule(codeKey, char1Key);
    }

    public ClockRule GetClockRule(string codeKey)
    {
        var baseCode = NormalizeBaseCode(codeKey);
        if (VsaCodeTree.ClockRules.TryGetValue(baseCode, out var curatedRule))
            return curatedRule;

        var rule = _rules.GetClockRule(codeKey);
        return rule is { Mode: "none" }
            ? VsaCodeTree.DefaultClockRule
            : rule;
    }

    private static string NormalizeBaseCode(string? code)
    {
        var normalized = (code ?? string.Empty).Trim().ToUpperInvariant();
        return normalized.Length > 3 ? normalized[..3] : normalized;
    }
}
