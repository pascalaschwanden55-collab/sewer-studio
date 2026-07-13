using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Infrastructure.Import.Pdf;

/// <summary>
/// Wendet ein geparstes Schachtprotokoll auf EINEN bestehenden SchachtRecord an
/// (Felder + PDF-Pfad + strukturiertes Protokoll). Herausgeloest aus
/// LegacyPdfImportService.ImportSchachtPdf, damit der Einzel-Import-Dienst dieselbe
/// Logik nutzt (kein Duplikat). Sucht/legt KEINEN Record an - das bleibt beim Aufrufer.
/// </summary>
internal static class SchachtProtocolApplier
{
    /// <summary>
    /// Schreibt die geparsten Felder + Schaeden auf <paramref name="target"/>.
    /// Gibt die Liste der fuer die Import-Meldung relevanten gesetzten Felder zurueck.
    /// </summary>
    public static IReadOnlyList<string> Apply(
        SchachtRecord target,
        string key,
        LegacyPdfImportService.ParsedSchachtFields parsed,
        IReadOnlyList<(string Component, string Damage)> damageEntries,
        string pdfPath)
    {
        SetSchachtField(target, "Schachtnummer", key);
        SetSchachtField(target, "NR.", key);
        SetSchachtField(target, "Nr.", key);

        if (!string.IsNullOrWhiteSpace(parsed.Datum))
            SetSchachtField(target, "Ausfuehrung Datum/Jahr", parsed.Datum);

        if (!string.IsNullOrWhiteSpace(parsed.Funktion))
            SetSchachtField(target, "Funktion", parsed.Funktion);

        if (!string.IsNullOrWhiteSpace(parsed.Schachtform))
            SetSchachtField(target, "Schachtform", parsed.Schachtform);

        if (!string.IsNullOrWhiteSpace(parsed.Dimension))
            SetSchachtField(target, "Dimension", parsed.Dimension);

        if (!string.IsNullOrWhiteSpace(parsed.Schachttiefe))
            SetSchachtField(target, "Schachttiefe", parsed.Schachttiefe);

        if (!string.IsNullOrWhiteSpace(parsed.PrimaereSchaeden))
            SetSchachtField(target, "Primaere Schaeden", parsed.PrimaereSchaeden);

        if (!string.IsNullOrWhiteSpace(parsed.Bemerkungen))
            SetSchachtField(target, "Bemerkungen", parsed.Bemerkungen);

        if (!string.IsNullOrWhiteSpace(parsed.Link))
            SetSchachtField(target, "Link", parsed.Link);

        if (!string.IsNullOrWhiteSpace(parsed.Status))
            SetSchachtField(target, "Status offen/abgeschlossen", parsed.Status);

        // PDF-Pfad speichern fuer spaeteres Oeffnen per Rechtsklick.
        target.SetFieldValue("PDF_Path", pdfPath);

        // Strukturiertes Protokoll aus Bauteil-Schaeden erstellen.
        if (damageEntries.Count > 0)
        {
            var protocolEntries = damageEntries.Select(d => new ProtocolEntry
            {
                Code = d.Component,
                Beschreibung = d.Damage,
                Source = ProtocolEntrySource.Imported
            }).ToList();

            var originalRevision = new ProtocolRevision
            {
                Comment = $"Import aus PDF: {Path.GetFileName(pdfPath)}",
                Entries = protocolEntries
            };
            var currentRevision = new ProtocolRevision
            {
                Comment = "Arbeitskopie",
                Entries = protocolEntries.Select(e => new ProtocolEntry
                {
                    Code = e.Code,
                    Beschreibung = e.Beschreibung,
                    Source = e.Source
                }).ToList()
            };

            target.Protocol = new ProtocolDocument
            {
                HaltungId = key,
                Original = originalRevision,
                Current = currentRevision
            };
        }

        var imported = new List<string>();
        if (!string.IsNullOrWhiteSpace(parsed.SchachtNummer)) imported.Add("Schachtnummer");
        if (!string.IsNullOrWhiteSpace(parsed.Datum)) imported.Add("Ausfuehrung Datum/Jahr");
        if (!string.IsNullOrWhiteSpace(parsed.Funktion)) imported.Add("Funktion");
        if (!string.IsNullOrWhiteSpace(parsed.Schachtform)) imported.Add("Schachtform");
        if (!string.IsNullOrWhiteSpace(parsed.Dimension)) imported.Add("Dimension");
        if (!string.IsNullOrWhiteSpace(parsed.Schachttiefe)) imported.Add("Schachttiefe");
        if (!string.IsNullOrWhiteSpace(parsed.PrimaereSchaeden)) imported.Add("Primaere Schaeden");
        if (!string.IsNullOrWhiteSpace(parsed.Bemerkungen)) imported.Add("Bemerkungen");
        if (damageEntries.Count > 0) imported.Add($"Protokoll ({damageEntries.Count} Beobachtungen)");
        return imported;
    }

    private static void SetSchachtField(SchachtRecord record, string logicalField, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        foreach (var candidate in GetSchachtFieldAliases(logicalField))
            record.SetFieldValue(candidate, value);
    }

    private static IReadOnlyList<string> GetSchachtFieldAliases(string logicalField)
    {
        return logicalField switch
        {
            "Schachtnummer" => new[] { "Schachtnummer" },
            "Funktion" => new[] { "Funktion" },
            "Primaere Schaeden" => new[]
            {
                "Prim\u00e4re Sch\u00e4den",
                "Primaere Schaeden",
                "Prim\u00c3\u00a4re Sch\u00c3\u00a4den",
                "Prim\u00c3\u0192\u00c2\u00a4re Sch\u00c3\u0192\u00c2\u00a4den"
            },
            "Bemerkungen" => new[] { "Bemerkungen" },
            "Link" => new[] { "Link" },
            "NR." => new[] { "NR.", "Nr." },
            "Nr." => new[] { "Nr.", "NR." },
            "Ausfuehrung Datum/Jahr" => new[]
            {
                "Ausf\u00fchrung Datum/Jahr",
                "Ausf\u00fchrung\nDatum/Jahr",
                "Ausfuehrung Datum/Jahr",
                "Ausfuehrung\nDatum/Jahr",
                "Ausf\u00c3\u00bchrung Datum/Jahr",
                "Ausf\u00c3\u0192\u00c2\u00bchrung Datum/Jahr"
            },
            "Status offen/abgeschlossen" => new[] { "Status offen/abgeschlossen", "Status\noffen/abgeschlossen" },
            _ => new[] { logicalField }
        };
    }
}
