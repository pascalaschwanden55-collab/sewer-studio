namespace AuswertungPro.Next.Application.Export;

/// <summary>
/// Kopfangaben des Excel-Berichts. Frueher stand hier ein Platzhalter aus einem
/// fremden Projekt ("Auswertung Seitenanschluesse ..."), der bei jedem Export von
/// Hand ueberschrieben werden musste.
/// </summary>
/// <param name="Projekt">Bezeichnung der Auswertung, z. B. "Auswertung GEP Altdorf".</param>
/// <param name="Zone">Gebiet oder Zone, z. B. "Zone 1.09 Langmatt-Hagen". Darf leer sein.</param>
/// <param name="Aufnahmen">Jahr oder Zeitraum der Aufnahmen. Darf leer sein.</param>
public sealed record ExcelReportContext(string Projekt, string? Zone = null, string? Aufnahmen = null)
{
    /// <summary>
    /// Baut die Titelzeile. Leere Angaben fallen weg, damit kein "  /  " stehen bleibt.
    /// </summary>
    /// <param name="blatt">"Haltungen" oder "Schächte".</param>
    public string TitelFuer(string blatt)
    {
        var teile = new System.Collections.Generic.List<string>();
        if (!string.IsNullOrWhiteSpace(Projekt))
            teile.Add(Projekt.Trim());
        if (!string.IsNullOrWhiteSpace(Zone)
            && (string.IsNullOrWhiteSpace(Projekt)
                || !Projekt.Contains(Zone.Trim(), System.StringComparison.OrdinalIgnoreCase)))
            teile.Add(Zone!.Trim());

        var kopf = string.Join(" ", teile);
        if (!string.IsNullOrWhiteSpace(Aufnahmen))
            kopf = string.IsNullOrWhiteSpace(kopf)
                ? $"Aufnahmen {Aufnahmen!.Trim()}"
                : $"{kopf} / Aufnahmen {Aufnahmen!.Trim()}";

        return string.IsNullOrWhiteSpace(kopf) ? blatt : $"{kopf} {blatt}";
    }
}
