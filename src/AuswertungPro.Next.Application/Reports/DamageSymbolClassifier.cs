namespace AuswertungPro.Next.Application.Reports;

/// <summary>
/// Reine Klassifikations-Logik fuer Schadenssymbole: Code → Kategorie und Kategorie → Farbe.
/// Aus <see cref="ProtocolPdfExporter"/> extrahiert (verhaltensneutral), damit unit-testbar.
/// </summary>
public static class DamageSymbolClassifier
{
    /// <summary>
    /// Leitet aus einem VSA-Rohdaten-Code die Schadenssymbol-Kategorie ab.
    /// Gibt "default" zurueck, wenn kein bekannter Code-Praefix passt.
    /// </summary>
    public static string ResolveDamageSymbolCategory(string? rawCode)
    {
        var code = (rawCode ?? "").Trim().ToUpperInvariant();
        if (code.StartsWith("BAA", StringComparison.Ordinal)) return "deformation";  // Verformung
        if (code.StartsWith("BAB", StringComparison.Ordinal)) return "crack";        // Riss
        if (code.StartsWith("BAC", StringComparison.Ordinal)) return "break";        // Bruch / Einsturz
        if (code.StartsWith("BAD", StringComparison.Ordinal)) return "leak";         // Undichtheit
        if (code.StartsWith("BAE", StringComparison.Ordinal)) return "offset";       // Versatz
        if (code.StartsWith("BAF", StringComparison.Ordinal)) return "surface";      // Oberflaechenschaden
        if (code.StartsWith("BAH", StringComparison.Ordinal)) return "offset";       // Schadhafter Anschluss
        if (code.StartsWith("BAI", StringComparison.Ordinal)) return "obstacle";     // Hindernis
        if (code.StartsWith("BAJ", StringComparison.Ordinal)) return "offset";       // Verschobene Rohrverbindung
        if (code.StartsWith("BAK", StringComparison.Ordinal)) return "infiltration"; // Infiltration
        if (code.StartsWith("BAL", StringComparison.Ordinal)) return "exfiltration"; // Exfiltration
        if (code.StartsWith("BBA", StringComparison.Ordinal)) return "roots";        // Wurzeln / Bewuchs
        if (code.StartsWith("BBB", StringComparison.Ordinal)) return "incrustation"; // Anhaftende Stoffe / Inkrustation
        if (code.StartsWith("BBC", StringComparison.Ordinal)) return "deposit";      // Ablagerung
        return "default";
    }

    /// <summary>
    /// Gibt die harmonisierte Farbe (Hex-String) fuer eine Schadens-Kategorie zurueck.
    /// Bei unbekannter Kategorie wird <paramref name="fallback"/> verwendet.
    /// </summary>
    public static string GetDamageSymbolColor(string category, string fallback = "#006E9C")
    {
        return category switch
        {
            "crack" or "break"                           => "#D64541", // Rot – strukturell kritisch
            "deformation" or "offset" or "surface"       => "#E67E22", // Orange – Verformung / Oberflaeche
            "leak" or "infiltration" or "exfiltration"   => "#2196F3", // Blau – Wasser
            "roots"                                      => "#27AE60", // Gruen – biologisch
            "incrustation" or "deposit"                  => "#8B6914", // Braun – Anhaftung / Ablagerung
            "obstacle"                                   => "#6B7280", // Grau – Hindernis
            _ => fallback
        };
    }
}
