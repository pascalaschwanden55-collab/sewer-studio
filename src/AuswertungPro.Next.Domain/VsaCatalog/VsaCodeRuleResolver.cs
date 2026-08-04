using System.Collections.Generic;

namespace AuswertungPro.Next.Domain.VsaCatalog;

/// <summary>
/// Liest Quant-Regeln, Uhrzeiger-Regeln und Char2-Optionen aus dem unveraenderlichen VSA-Katalog.
/// Keine IO-Abhaengigkeiten.
/// </summary>
public static class VsaCodeRuleResolver
{
    /// <summary>
    /// Ermittelt die effektiven Q1-/Q2-Regeln fuer einen Code und optionalen
    /// Char1-Schluessel. Eine Regel mit Pflicht="V" wird aus der passenden
    /// Per-Char1-Tabelle aufgeloest.
    /// </summary>
    public static (QuantField? Q1, QuantField? Q2) GetQuantRule(string codeKey, string? c1Key)
    {
        if (!VsaCodeTree.QuantRules.TryGetValue(codeKey, out var rule))
            return (null, null);

        var q1 = rule.Q1;
        if (q1 is { Pflicht: "V" } && rule.Q1PerChar1 is not null && c1Key is not null)
        {
            q1 = rule.Q1PerChar1.TryGetValue(c1Key, out var perChar) ? perChar : null;
        }

        var q2 = rule.Q2;
        if (q2 is { Pflicht: "V" } && rule.Q2PerChar1 is not null && c1Key is not null)
        {
            q2 = rule.Q2PerChar1.TryGetValue(c1Key, out var perChar) ? perChar : null;
        }

        return (q1, q2);
    }

    /// <summary>
    /// Ermittelt die Uhrzeiger-Regel fuer einen Code.
    /// Gibt DefaultClockRule ("range") zurueck, wenn kein Eintrag vorhanden.
    /// </summary>
    public static ClockRule GetClockRule(string codeKey)
    {
        return VsaCodeTree.ClockRules.TryGetValue(codeKey, out var rule)
            ? rule
            : VsaCodeTree.DefaultClockRule;
    }

    /// <summary>
    /// Ermittelt die Char2-Optionen fuer einen Code und Char1-Schluessel.
    /// Aufloesung: Char2PerChar1 → CharDef.Char2 → globales Char2.
    /// </summary>
    public static Dictionary<string, string>? GetChar2Options(VsaCodeDef cd, string c1)
    {
        if (cd.Char2PerChar1 is not null)
            return cd.Char2PerChar1.TryGetValue(c1, out var c2) ? c2 : null;

        if (cd.Char2 is not null)
            return cd.Char2;

        if (cd.Char1 is not null && cd.Char1.TryGetValue(c1, out var charDef) && charDef.Char2 is not null)
            return charDef.Char2;

        return null;
    }

    /// <summary>
    /// Prueft ob eine Char1 × Char2-Kombination ungueltig ist.
    /// Gibt false zurueck, wenn AllValid=true oder kein Invalid-Dictionary vorhanden.
    /// </summary>
    public static bool IsInvalidCombo(VsaCodeDef cd, string c1, string c2)
    {
        if (cd.AllValid) return false;
        return cd.Invalid is not null
            && cd.Invalid.TryGetValue(c1, out var set)
            && set.Contains(c2);
    }
}
