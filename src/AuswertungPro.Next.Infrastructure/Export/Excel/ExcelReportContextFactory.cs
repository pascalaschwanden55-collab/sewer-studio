using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Export.Excel;

/// <summary>
/// Baut die Kopfangaben des Berichts aus dem Projekt. Frueher stand in der Vorlage ein
/// Platzhalter aus einem fremden Projekt, der bei jedem Export von Hand ueberschrieben
/// werden musste.
/// </summary>
public static class ExcelReportContextFactory
{
    /// <summary>
    /// Projektname aus dem Projekt, Aufnahmejahr aus den Inspektionsdaten. Laesst sich
    /// kein Jahr sicher ableiten, bleibt die Angabe leer - eine erfundene Jahreszahl im
    /// Berichtskopf waere schlimmer als gar keine.
    /// </summary>
    public static ExcelReportContext AusProjekt(Project project, bool schaechte = false)
    {
        ArgumentNullException.ThrowIfNull(project);

        var name = string.IsNullOrWhiteSpace(project.Name) || project.Name == "Neues Projekt"
            ? string.Empty
            : project.Name.Trim();

        var zone = project.Metadata.TryGetValue("Zone", out var gespeicherteZone)
            && !string.IsNullOrWhiteSpace(gespeicherteZone)
                ? gespeicherteZone.Trim()
                : null;

        return new ExcelReportContext(
            name,
            Zone: zone,
            Aufnahmen: LeiteAufnahmenAb(project, schaechte));
    }

    /// <summary>
    /// Sammelt die Jahreszahlen aus "Datum/Jahr". Ein Jahr -> "2025", mehrere ->
    /// "2024-2025". Nichts Verwertbares -> null.
    /// </summary>
    private static string? LeiteAufnahmenAb(Project project, bool schaechte)
    {
        var jahre = new SortedSet<int>();

        if (schaechte)
        {
            foreach (var record in project.SchaechteData)
            {
                var datum = ExcelSchachtFeldzuordnung.Lese(record, "Ausführung Datum/Jahr");
                foreach (var jahr in JahreAus(datum))
                    jahre.Add(jahr);
            }
        }
        else
        {
            foreach (var record in project.Data)
            {
                foreach (var jahr in JahreAus(record.GetFieldValue(FieldKeys.InspectionYear)))
                    jahre.Add(jahr);
            }
        }

        if (jahre.Count == 0)
            return null;
        if (jahre.Count == 1)
            return jahre.First().ToString(System.Globalization.CultureInfo.InvariantCulture);

        return $"{jahre.First()}-{jahre.Last()}";
    }

    /// <summary>
    /// Zieht alle vierstelligen Jahreszahlen aus Werten wie "2025", "25.10.2024"
    /// oder "2024/2025". Alles andere gilt als nicht lesbar.
    /// </summary>
    private static IEnumerable<int> JahreAus(string? wert)
    {
        if (string.IsNullOrWhiteSpace(wert))
            yield break;

        foreach (System.Text.RegularExpressions.Match treffer in
                 System.Text.RegularExpressions.Regex.Matches(wert, @"(?<!\d)(19|20)\d{2}(?!\d)"))
        {
            if (int.TryParse(treffer.Value, out var jahr))
                yield return jahr;
        }
    }
}
