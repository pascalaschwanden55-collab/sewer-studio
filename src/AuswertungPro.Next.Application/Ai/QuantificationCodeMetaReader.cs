using System.Collections.Generic;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Liest die fuer die VSA-Zustandsbewertung massgeblichen Quantifizierungswerte (Q1/Q2) aus den
/// CodeMeta-Parametern eines Befunds. Spiegelbild zu <see cref="QuantificationCodeMetaWriter"/>
/// (Teil 10): waehrend dort die KI je Code die richtigen vsa.*-Werte SCHREIBT, holt dieser Reader
/// sie codeabhaengig wieder als reine Zahl heraus, damit sie als VsaFinding.Quantifizierung1/2 in
/// die EZ-Klassifizierung gelangen (statt dort als Naeherung zu landen).
///
/// Vorrang: explizite, generische Schluessel (Quantifizierung1 / vsa.q1) gewinnen — das sind
/// importierte oder manuell gesetzte Werte. Erst wenn die fehlen, werden die code-spezifischen
/// KI-Schluessel (vsa.querschnitt.prozent / vsa.hoehe.mm ...) nach <see cref="QuantificationUnitPolicy"/>
/// herangezogen. Reine, testbare Logik.
/// </summary>
public static class QuantificationCodeMetaReader
{
    /// <summary>Massgeblicher Q1-Wert (als reine Zahl-Zeichenkette) fuer die Bewertung, oder null.</summary>
    public static string? ReadQ1(IReadOnlyDictionary<string, string>? parameters, string? code)
    {
        if (parameters is null) return null;

        // 1) Generische/explizite Werte haben Vorrang (Import, manuell).
        var explicitVal = TryGet(parameters, "Quantifizierung1") ?? TryGet(parameters, "vsa.q1");
        if (explicitVal != null) return explicitVal;

        // 2) Code-spezifischer KI-Wert gemaess Einheiten-Regel.
        var units = QuantificationUnitPolicy.GetUnits(code);
        return ReadByUnit(parameters, units.Q1);
    }

    /// <summary>Massgeblicher Q2-Wert (als reine Zahl-Zeichenkette) fuer die Bewertung, oder null.</summary>
    public static string? ReadQ2(IReadOnlyDictionary<string, string>? parameters, string? code)
    {
        if (parameters is null) return null;

        var explicitVal = TryGet(parameters, "Quantifizierung2") ?? TryGet(parameters, "vsa.q2");
        if (explicitVal != null) return explicitVal;

        var units = QuantificationUnitPolicy.GetUnits(code);
        return ReadByUnit(parameters, units.Q2);
    }

    private static string? ReadByUnit(
        IReadOnlyDictionary<string, string> parameters,
        QuantificationUnitPolicy.QuantUnit unit)
        => unit switch
        {
            QuantificationUnitPolicy.QuantUnit.CrossSectionPercent => TryGet(parameters, "vsa.querschnitt.prozent"),
            QuantificationUnitPolicy.QuantUnit.ExtentPercent => TryGet(parameters, "vsa.ausdehnung.prozent"),
            QuantificationUnitPolicy.QuantUnit.HeightMm => TryGet(parameters, "vsa.hoehe.mm"),
            QuantificationUnitPolicy.QuantUnit.WidthMm => TryGet(parameters, "vsa.breite.mm"),
            // LengthMm/OffsetMm werden vom Writer auf vsa.hoehe.mm abgelegt (SAM-Hoehe als groesste Ausdehnung).
            QuantificationUnitPolicy.QuantUnit.LengthMm => TryGet(parameters, "vsa.hoehe.mm"),
            QuantificationUnitPolicy.QuantUnit.OffsetMm => TryGet(parameters, "vsa.hoehe.mm"),
            _ => null
        };

    private static string? TryGet(IReadOnlyDictionary<string, string> parameters, string key)
        => parameters.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;
}
