namespace AuswertungPro.Next.Application.Reports;

/// <summary>
/// Reine Parser-Logik fuer Haltungsknoten und Fliessrichtung.
/// Aus <see cref="ProtocolPdfExporter"/> extrahiert (verhaltensneutral), damit unit-testbar.
/// </summary>
public static class HoldingNodeParser
{
    /// <summary>
    /// Zerlegt ein Haltungsname-Label (z.B. "865-864") in Anfangs- und Endknoten.
    /// Gibt (null, null) bei leerem oder fehlerhaftem Label zurueck.
    /// </summary>
    public static (string? Start, string? End) SplitHoldingNodes(string? holdingLabel)
    {
        if (string.IsNullOrWhiteSpace(holdingLabel))
            return (null, null);

        var parts = holdingLabel
            .Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 1)
            return (parts[0], null);
        if (parts.Length >= 2)
            return (parts[0], parts[1]);

        return (null, null);
    }

    /// <summary>
    /// Leitet aus einem Inspektionsrichtungs-Text die Fliessrichtung ab:
    /// true = fliesst in Kamerafahrtrichtung (Gegenstrominspektionsbegriff "in Fliessrichtung"),
    /// false = entgegen der Kamerafahrt, null = unbekannt.
    /// </summary>
    public static bool? ParseFlowDirection(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        if (text.Contains("gegen", StringComparison.OrdinalIgnoreCase))
            return false;
        if (text.Contains("in", StringComparison.OrdinalIgnoreCase))
            return true;

        return null;
    }
}
