using System.Collections.Generic;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Reine Formatierung der einzeiligen Kurzbeschreibung einer Haltung
/// fuer die Listen-Spalte der Haltungsansicht: "DN 300 · 45.30 m · Mischabwasser".
/// Leere Teile werden ausgelassen.
/// </summary>
public static class HaltungSummaryFormatter
{
    public static string FormatSummary(string? dnMm, string? laengeM, string? nutzungsart)
    {
        var parts = new List<string>(3);

        if (!string.IsNullOrWhiteSpace(dnMm))
            parts.Add($"DN {dnMm.Trim()}");
        if (!string.IsNullOrWhiteSpace(laengeM))
            parts.Add($"{laengeM.Trim()} m");
        if (!string.IsNullOrWhiteSpace(nutzungsart))
            parts.Add(nutzungsart.Trim());

        return string.Join(" · ", parts);
    }

    public static string FormatSummary(HaltungRecord record)
        => FormatSummary(
            record.GetFieldValue("DN_mm"),
            record.GetFieldValue("Haltungslaenge_m"),
            record.GetFieldValue("Nutzungsart"));
}
