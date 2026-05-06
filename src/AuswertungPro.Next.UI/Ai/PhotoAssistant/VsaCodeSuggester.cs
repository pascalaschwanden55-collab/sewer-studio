using System;
using System.Globalization;

namespace AuswertungPro.Next.UI.Ai.PhotoAssistant;

/// <summary>
/// Mappt Foto-Assistent-Mess-Werte auf VSA-KEK-Codes (BAA, BAJ, BCA).
/// Reine statische Logik, ohne Datenbank-/UI-Abhaengigkeiten.
/// </summary>
public static class VsaCodeSuggester
{
    public sealed record CodeSuggestion(string Code, string Description);

    /// <summary>
    /// BAA — Querschnittsverformung.
    /// Eingabe: Verformung in Prozent (= 100 - QuerschnittProzent).
    ///   <= 5 %  -> BAA 1
    ///   > 5 % bis 10 %  -> BAA 2
    ///   > 10 % bis 20 % -> BAA 3
    ///   > 20 %  -> BAA 4
    /// </summary>
    public static CodeSuggestion ForDeformation(double verformungProzent)
    {
        var stufe = verformungProzent switch
        {
            <= 5 => 1,
            <= 10 => 2,
            <= 20 => 3,
            _ => 4
        };
        var desc = string.Format(CultureInfo.InvariantCulture,
            "Querschnittsverformung {0:F1} % (Stufe {1})", verformungProzent, stufe);
        return new CodeSuggestion($"BAA {stufe}", desc);
    }

    /// <summary>
    /// BAJ — Knick / Bogen.
    /// Eingabe: Bogenwinkel in Grad.
    ///   &lt; 5°  -> BAJ 1
    ///   5°..15° -> BAJ 2
    ///   15°..30° -> BAJ 3
    ///   &gt; 30° -> BAJ 4
    /// </summary>
    public static CodeSuggestion ForBendAngle(double bendAngleDegrees)
    {
        var stufe = bendAngleDegrees switch
        {
            < 5 => 1,
            < 15 => 2,
            < 30 => 3,
            _ => 4
        };
        var desc = string.Format(CultureInfo.InvariantCulture,
            "Knickwinkel {0:F0}° (Stufe {1})", bendAngleDegrees, stufe);
        return new CodeSuggestion($"BAJ {stufe}", desc);
    }

    /// <summary>
    /// BCA — Anschluss.
    /// Liefert "BCA" mit Beschreibung "Anschluss [hour]h · Ø [mm] mm ([%] von DN[dn]) · [angle]°".
    /// </summary>
    public static CodeSuggestion ForLateral(int hour, double lateralDnPercent, double lateralAngleDegrees, int dnMm)
    {
        if (hour < 1 || hour > 12) hour = ((hour - 1) % 12 + 12) % 12 + 1;
        var latMm = dnMm * lateralDnPercent / 100.0;
        var desc = string.Format(CultureInfo.InvariantCulture,
            "Anschluss {0}h · Ø {1:F0} mm ({2:F0}% von DN{3}) · {4:F0}°",
            hour, latMm, lateralDnPercent, dnMm, lateralAngleDegrees);
        return new CodeSuggestion("BCA", desc);
    }
}
