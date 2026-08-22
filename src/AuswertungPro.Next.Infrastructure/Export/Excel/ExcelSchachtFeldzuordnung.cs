using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Export.Excel;

/// <summary>
/// Verbindet die lesbaren Ueberschriften der Schacht-Vorlage mit den historisch
/// gespeicherten Feldschluesseln. Die Projektdateien enthalten mehrere zulässige
/// Schreibweisen; der Export darf deshalb nicht nur den sichtbaren Header abfragen.
/// </summary>
internal static class ExcelSchachtFeldzuordnung
{
    private static readonly IReadOnlyDictionary<string, string[]> Aliase =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["nr"] = ["NR.", "NR", "Nr.", "Nr"],
            ["primaereschaeden"] = ["Primäre Schäden", FieldKeys.PrimaryDamages],
            ["janein"] = ["Ja/Nein", FieldKeys.RenovationDecision, "Sanieren", "Sanieren ja/nein"],
            ["eigentuemer"] = ["Eigentümer", FieldKeys.Owner],
            ["ausgefuehrtdurch"] = ["Ausgefuehrt durch", "Ausgeführt durch", FieldKeys.RehabilitationExecutor],
            ["statusoffenabgeschlossen"] =
                ["Status offen/abgeschlossen", FieldKeys.WorkflowStatus, "Status", "offen/abgeschlossen"],
            ["ausfuehrungdatumjahr"] =
                ["Ausführung Datum/Jahr", "Ausfuehrung Datum/Jahr", FieldKeys.InspectionYear, "Datum/Jahr"],
            ["link"] = [FieldKeys.Link, FieldKeys.PdfPath, FieldKeys.PdfEigen, FieldKeys.PdfAll]
        };

    public static string Lese(SchachtRecord record, string header)
    {
        ArgumentNullException.ThrowIfNull(record);

        var normalisierterHeader = Normalisiere(header);
        var kandidaten = Aliase.TryGetValue(normalisierterHeader, out var aliase)
            ? aliase
            : [header];

        foreach (var kandidat in kandidaten)
        {
            var normalisierterKandidat = Normalisiere(kandidat);
            var treffer = record.Fields.FirstOrDefault(pair =>
                Normalisiere(pair.Key) == normalisierterKandidat
                && !string.IsNullOrWhiteSpace(pair.Value));
            if (!string.IsNullOrWhiteSpace(treffer.Value))
            {
                var wert = treffer.Value.Trim();
                return normalisierterHeader == "link"
                    ? ErsterLink(wert)
                    : wert;
            }
        }

        return string.Empty;
    }

    public static bool IstZahl(string header)
    {
        var normalisiert = Normalisiere(header);
        return normalisiert is "kosten" or "abdeckungstk";
    }

    public static bool IstLaufendeNummer(string header)
        => Normalisiere(header) == "nr";

    public static bool IstLink(string header)
        => Normalisiere(header) == "link";

    private static string ErsterLink(string wert)
        => wert
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(link => !string.IsNullOrWhiteSpace(link))
           ?? string.Empty;

    private static string Normalisiere(string? wert)
    {
        var text = ExcelCellFormatting.NormalizeHeader(wert);
        return new string(text.Where(char.IsLetterOrDigit).ToArray());
    }
}
