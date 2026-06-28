using System.Collections.Generic;

namespace AuswertungPro.Next.Domain.VsaCatalog;

/// <summary>
/// Baut die offizielle Bezeichnung fuer VSA-Codes auf.
/// Liest ausschliesslich den unveraenderlichen VSA-Katalog – keine IO-Abhaengigkeiten.
/// </summary>
public static class VsaLabelBuilder
{
    /// <summary>
    /// Baut die offizielle Bezeichnung fuer einen VSA-Code auf.
    /// Dreistufige Char2-Aufloesung: Char2PerChar1 → CharDef.Char2 → globales Char2.
    /// Beispiel: "BABA" → "Risse, Haarriss, laengs"
    ///           "BCD"  → "Rohranfang"
    ///           "???"  → null
    /// </summary>
    public static string? LookupLabel(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length < 2) return null;

        var groupKey = code[..2]; // z.B. "BA", "BC", "BD"
        if (!VsaCodeTree.Groups.TryGetValue(groupKey, out var group)) return null;

        // 2-Zeichen-Code = Gruppenname
        if (code.Length == 2) return group.Label;

        // Hauptcode (3 Zeichen): z.B. "BAB"
        var mainKey = code[..3];
        if (!group.Codes.TryGetValue(mainKey, out var mainDef)) return null;

        if (code.Length == 3) return mainDef.Label;

        // Char1 (4 Zeichen): z.B. "BABA" → "Risse, Haarriss"
        var parts = new List<string> { mainDef.Label };
        var c1Key = code[3].ToString();
        string? c1Label = null;
        if (mainDef.Char1 != null && mainDef.Char1.TryGetValue(c1Key, out var c1Def))
            c1Label = c1Def.Label;

        if (c1Label != null) parts.Add(c1Label);

        // Char2 (5 Zeichen): z.B. "BABAA" → "Risse, Haarriss, laengs"
        if (code.Length >= 5)
        {
            var c2Key = code[4].ToString();
            string? c2Label = null;

            // 1. Char2 pro Char1 zuerst pruefen
            if (mainDef.Char2PerChar1 != null
                && mainDef.Char2PerChar1.TryGetValue(c1Key, out var perC1)
                && perC1.TryGetValue(c2Key, out var label))
                c2Label = label;
            // 2. CharDef-eigenes Char2
            else if (c1Label != null && mainDef.Char1 != null
                && mainDef.Char1.TryGetValue(c1Key, out var charDef)
                && charDef.Char2 != null
                && charDef.Char2.TryGetValue(c2Key, out var cLabel))
                c2Label = cLabel;
            // 3. Globales Char2
            else if (mainDef.Char2 != null && mainDef.Char2.TryGetValue(c2Key, out var gLabel))
                c2Label = gLabel;

            if (c2Label != null) parts.Add(c2Label);
        }

        return string.Join(", ", parts);
    }
}
