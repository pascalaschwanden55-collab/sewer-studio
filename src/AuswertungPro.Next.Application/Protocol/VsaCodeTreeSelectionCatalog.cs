using AuswertungPro.Next.Domain.VsaCatalog;

namespace AuswertungPro.Next.Application.Protocol;

/// <summary>
/// Picker-Katalog mit der gewohnten ISYBAU-/WinCan-Anordnung (Gruppen → Hauptcode →
/// Char1 → Char2 aus dem kuratierten <see cref="VsaCodeTree"/>), aber den Mengen- und
/// Uhrlage-Regeln des aktuellen VSA-Katalogs – so bleibt die Anwendungsregelung
/// VSA-konform und passt zur Zustandsbewertung. Faellt auf den Baum zurueck, wenn der
/// aktuelle Katalog fuer einen Code keine Regel kennt.
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

    public IReadOnlyDictionary<string, string>? GetChar2Options(VsaCodeDef codeDef, string char1Key)
        => VsaCodeTree.GetChar2Options(codeDef, char1Key);

    public bool IsInvalidCombo(VsaCodeDef codeDef, string char1Key, string char2Key)
        => VsaCodeTree.IsInvalidCombo(codeDef, char1Key, char2Key);

    // ── Regeln: aktueller VSA-Katalog (VSA-konform), Fallback Baum ────────
    public (QuantField? Q1, QuantField? Q2) GetQuantRule(string codeKey, string? char1Key)
    {
        var (q1, q2) = _rules.GetQuantRule(codeKey, char1Key);
        return q1 is null && q2 is null
            ? VsaCodeTree.GetQuantRule(codeKey, char1Key)
            : (q1, q2);
    }

    public ClockRule GetClockRule(string codeKey)
    {
        var rule = _rules.GetClockRule(codeKey);
        return rule is { Mode: "none" }
            ? VsaCodeTree.GetClockRule(codeKey)
            : rule;
    }
}
