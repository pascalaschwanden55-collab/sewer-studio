using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>Eine Zeile der Primäre-Schäden-Mini-Tabelle (reine Anzeigedaten).</summary>
public sealed record SchadenZeile(string Meter, string Code, string Klartext, string Kategorie);

/// <summary>
/// Reine Projektion ProtocolEntry → SchadenZeile für die Haltungsansicht-Mini-Tabelle.
/// Keine Abhängigkeit auf Katalog/Resolver: Klartext kommt aus ProtocolEntry.Beschreibung
/// (Fallback Code), Kategorie aus der VSA-Hauptgruppe (2 Buchstaben).
/// </summary>
public static class SchadenZeileFormatter
{
    public static SchadenZeile Format(ProtocolEntry entry)
    {
        var meter = FormatMeter(entry);
        var klartext = string.IsNullOrWhiteSpace(entry.Beschreibung) ? entry.Code : entry.Beschreibung.Trim();
        return new SchadenZeile(meter, entry.Code, klartext, Kategorie(entry.Code));
    }

    public static IReadOnlyList<SchadenZeile> FormatList(IEnumerable<ProtocolEntry> entries)
        => entries
            .Where(e => !e.IsDeleted && !string.IsNullOrWhiteSpace(e.Code))
            .Select(Format)
            .ToList();

    public static string FormatMeter(ProtocolEntry entry)
    {
        var start = entry.MeterStart ?? 0.0;
        var s = start.ToString("0.00", CultureInfo.InvariantCulture);
        if (entry.IsStreckenschaden && entry.MeterEnd is { } end && end > start)
            return $"{s}–{end.ToString("0.00", CultureInfo.InvariantCulture)} m";
        return $"{s} m";
    }

    /// <summary>VSA-Hauptgruppe → grobe Kategorie. BA=Zustand, BB/BD=Betrieb, BC=Bestand, sonst "".</summary>
    public static string Kategorie(string? code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length < 2)
            return "";
        return code[..2].ToUpperInvariant() switch
        {
            "BA" => "Zustand",
            "BB" => "Betrieb",
            "BC" => "Bestand",
            "BD" => "Betrieb",
            _ => ""
        };
    }
}
