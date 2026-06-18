using System;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Wissens-Schicht (VSA-PDF): WELCHE physikalische Groesse die Quantifizierung Q1/Q2 eines
/// VSA-Hauptcodes traegt (mm / % / Grad). Das Manifest sagt nur, OB Q1/Q2 erlaubt sind
/// (IVsaCodeSelectionCatalog.GetQuantRule), aber nicht die Einheit (unit ist dort null).
///
/// Diese Tabelle wird NUR fuer Codes konsultiert, deren Manifest ueberhaupt eine Quantifizierung
/// vorsieht — sie erfindet keine Quantifizierung. Codes ohne Eintrag liefern <see cref="QuantUnit.Unknown"/>:
/// dann wird die Manifest-Q zwar grundsaetzlich erlaubt, aber mangels passender SAM-Groesse nicht
/// automatisch befuellt (konservativ, statt eine falsche Einheit zu schreiben).
///
/// Reine, testbare Logik (gleiche Bauweise wie CodingDedupPolicy). Quelle der Einheiten:
/// VSA-Richtlinie Schadencodierung, vom User 2026-06-16 bestaetigt.
/// </summary>
public static class QuantificationUnitPolicy
{
    /// <summary>Physikalische Bedeutung eines Quantifizierungs-Feldes.</summary>
    public enum QuantUnit
    {
        /// <summary>Keine bekannte/zuordenbare Einheit -> nicht automatisch befuellen.</summary>
        Unknown,
        /// <summary>Hoehe in mm.</summary>
        HeightMm,
        /// <summary>Breite in mm.</summary>
        WidthMm,
        /// <summary>Laenge in mm.</summary>
        LengthMm,
        /// <summary>Versatz/Abstand in mm.</summary>
        OffsetMm,
        /// <summary>Querschnittsverminderung in %.</summary>
        CrossSectionPercent,
        /// <summary>Ausdehnung/Hoehe in % (z.B. Ablagerungshoehe, Wasserspiegelhoehe).</summary>
        ExtentPercent,
        /// <summary>Winkel in Grad.</summary>
        AngleDegrees
    }

    /// <summary>Einheiten von Q1 und Q2 eines Hauptcodes.</summary>
    public readonly record struct UnitRule(QuantUnit Q1, QuantUnit Q2);

    /// <summary>
    /// Liefert die Einheiten-Regel fuer einen VSA-Code (per Hauptcode = erste 3 Buchstaben).
    /// Verbindlich aus der VSA-PDF (User 2026-06-16). Nicht gelistete Codes -> (Unknown, Unknown).
    /// </summary>
    public static UnitRule GetUnits(string? code)
    {
        var main = MainCode(code);
        return main switch
        {
            // Anschluss (einziger Code mit zwei mm-Massen): Hoehe + Breite
            "BCA" => new UnitRule(QuantUnit.HeightMm, QuantUnit.WidthMm),
            "DCA" => new UnitRule(QuantUnit.HeightMm, QuantUnit.WidthMm), // Schacht-Anschluss, analog
            "DCG" => new UnitRule(QuantUnit.HeightMm, QuantUnit.WidthMm), // Schacht Zu-/Ablauf, analog

            // Riss: Rissbreite mm (Haarriss-Untercode ohne Mass -> siehe IsHaarriss)
            "BAB" => new UnitRule(QuantUnit.WidthMm, QuantUnit.Unknown),

            // Einragendes Dichtungsmaterial: Querschnittsminderung % (VSA Schadencodierung 2018
            // "BAI = Querschnittsminderung %" + Zustandsrichtlinie Tabelle 15: q1, Einheit %).
            // Frueher faelschlich LengthMm -> die %-basierte EZ-Bewertung haette den Wert nie gefunden.
            "BAI" => new UnitRule(QuantUnit.CrossSectionPercent, QuantUnit.Unknown),

            // Verschobene Rohrverbindung: Versatz mm (Q2 Winkel ist im Manifest nicht hinterlegt -> Unknown)
            "BAJ" => new UnitRule(QuantUnit.OffsetMm, QuantUnit.Unknown),

            // Querschnittsverminderung %
            "BBA" => new UnitRule(QuantUnit.CrossSectionPercent, QuantUnit.Unknown), // Wurzeln
            "BBB" => new UnitRule(QuantUnit.CrossSectionPercent, QuantUnit.Unknown), // Anhaftende Stoffe
            "BBD" => new UnitRule(QuantUnit.CrossSectionPercent, QuantUnit.Unknown), // Eindringen Bodenmaterial
            "BBE" => new UnitRule(QuantUnit.CrossSectionPercent, QuantUnit.Unknown), // Andere Hindernisse

            // Hoehe in % der lichten Hoehe
            "BBC" => new UnitRule(QuantUnit.ExtentPercent, QuantUnit.Unknown), // Ablagerungshoehe %
            "BDD" => new UnitRule(QuantUnit.ExtentPercent, QuantUnit.Unknown), // Wasserspiegelhoehe %

            _ => new UnitRule(QuantUnit.Unknown, QuantUnit.Unknown)
        };
    }

    /// <summary>
    /// Haarrisse (BAB Charakterisierung 1 = "A", d.h. Codes BABAx) bekommen laut VSA KEINE
    /// Quantifizierung — auch wenn das Manifest fuer die BAB-Gruppe Q1 vorsieht.
    /// </summary>
    public static bool IsHaarriss(string? code)
    {
        var c = (code ?? string.Empty).Trim().Replace(".", "").ToUpperInvariant();
        // BAB + Char1 'A' (Position 4) = Haarriss/Oberflaechenriss
        return c.Length >= 4 && c.StartsWith("BAB", StringComparison.Ordinal) && c[3] == 'A';
    }

    private static string? MainCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;
        var trimmed = code.Trim().Replace(".", "").ToUpperInvariant();
        return trimmed.Length >= 3 ? trimmed[..3] : null;
    }
}
