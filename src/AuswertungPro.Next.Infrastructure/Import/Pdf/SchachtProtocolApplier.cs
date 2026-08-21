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
    /// <param name="rebuildFromProtocol">
    /// Nur fuer das ausdrueckliche Aktualisieren EINES bereits verknuepften Schachts:
    /// Dann gilt das neu gelesene Protokoll als alleinige Wahrheit. Ein Feld, das im
    /// PDF jetzt fehlt, wird geleert, und das Beobachtungs-Protokoll wird auch dann
    /// ersetzt, wenn im PDF keine Beobachtung mehr steht. Der normale Import ergaenzt
    /// dagegen weiter nur (false), damit er Werte aus anderen Quellen nicht loescht.
    /// </param>
    public static IReadOnlyList<string> Apply(
        SchachtRecord target,
        string key,
        LegacyPdfImportService.ParsedSchachtFields parsed,
        IReadOnlyList<(string Component, string Damage)> damageEntries,
        string pdfPath,
        bool rebuildFromProtocol = false,
        bool fillMissingOnly = false)
    {
        var onlyMissing = fillMissingOnly && !rebuildFromProtocol;
        SetSchachtField(target, "Schachtnummer", key, onlyMissing);
        SetSchachtField(target, "NR.", key, onlyMissing);
        SetSchachtField(target, "Nr.", key, onlyMissing);

        WriteProtocolField(target, "Ausfuehrung Datum/Jahr", parsed.Datum, rebuildFromProtocol, onlyMissing);
        WriteProtocolField(target, "Funktion", parsed.Funktion, rebuildFromProtocol, onlyMissing);
        WriteProtocolField(target, "Schachtform", parsed.Schachtform, rebuildFromProtocol, onlyMissing);
        WriteProtocolField(target, "Dimension", parsed.Dimension, rebuildFromProtocol, onlyMissing);
        WriteProtocolField(target, "Schachttiefe", parsed.Schachttiefe, rebuildFromProtocol, onlyMissing);
        WriteProtocolField(target, "Primaere Schaeden", parsed.PrimaereSchaeden, rebuildFromProtocol, onlyMissing);
        WriteProtocolField(target, "Bemerkungen", parsed.Bemerkungen, rebuildFromProtocol, onlyMissing);

        // "Link" und "PDF_Path" zeigen auf die Datei selbst. Sie werden nur ueberschrieben,
        // nie geleert - sonst verliert der Schacht beim Neuaufbau den Weg zu seinem Protokoll.
        if (!string.IsNullOrWhiteSpace(parsed.Link))
            SetSchachtField(target, "Link", parsed.Link, onlyMissing);

        WriteProtocolField(target, "Status offen/abgeschlossen", parsed.Status, rebuildFromProtocol, onlyMissing);

        // PDF-Pfad speichern fuer spaeteres Oeffnen per Rechtsklick.
        if (!onlyMissing || string.IsNullOrWhiteSpace(target.GetFieldValue("PDF_Path")))
            target.SetFieldValue("PDF_Path", pdfPath, FieldSource.Pdf, userEdited: false);

        // Strukturiertes Protokoll aus Bauteil-Schaeden erstellen. Beim Neuaufbau auch
        // dann, wenn keine Beobachtung mehr im PDF steht - sonst bleiben geloeschte
        // Beobachtungen unsichtbar am Schacht haengen.
        if ((damageEntries.Count > 0 || rebuildFromProtocol)
            && !(onlyMissing && HasProtocolContent(target.Protocol)))
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

            if (rebuildFromProtocol || !HasProtocolContent(target.Protocol))
            {
                var history = target.Protocol?.History.ToList() ?? new List<ProtocolRevision>();
                if (rebuildFromProtocol && target.Protocol is not null)
                    history.Add(target.Protocol.Current);

                target.Protocol = new ProtocolDocument
                {
                    HaltungId = key,
                    Original = originalRevision,
                    Current = currentRevision,
                    History = history
                };
            }
            else if (!Common.ProtocolContentFingerprint.HasSameContent(
                         target.Protocol!.Current,
                         protocolEntries))
            {
                // Ein normaler Reimport ersetzt nicht mehr das ganze Dokument. Der
                // bisherige Arbeitsstand bleibt als Revision nachvollziehbar erhalten.
                target.Protocol.History.Add(target.Protocol.Current);
                target.Protocol.Current = currentRevision;
            }
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

    /// <summary>
    /// Schreibt ein Protokollfeld. Im Ergaenzungsmodus bleibt ein vorhandener Wert stehen,
    /// wenn das PDF an dieser Stelle nichts liefert. Beim Neuaufbau wird er geleert.
    /// </summary>
    private static void WriteProtocolField(
        SchachtRecord record,
        string logicalField,
        string? value,
        bool rebuildFromProtocol,
        bool fillMissingOnly)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            SetSchachtField(record, logicalField, value, fillMissingOnly);
            return;
        }

        if (rebuildFromProtocol)
            ClearSchachtField(record, logicalField);
    }

    private static void ClearSchachtField(SchachtRecord record, string logicalField)
    {
        foreach (var candidate in GetSchachtFieldAliases(logicalField))
        {
            // Nur wirklich vorhandene Spalten anfassen. Sonst entstuenden aus den
            // Schreibweise-Aliasen leere Zusatzfelder, die es vorher nicht gab.
            if (record.Fields.ContainsKey(candidate))
                record.SetFieldValue(candidate, string.Empty);
        }
    }

    private static void SetSchachtField(
        SchachtRecord record,
        string logicalField,
        string value,
        bool fillMissingOnly)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        if (fillMissingOnly && HasNonEmptySchachtField(record, logicalField))
            return;

        foreach (var candidate in GetSchachtFieldAliases(logicalField))
            record.SetFieldValue(candidate, value, FieldSource.Pdf, userEdited: false);
    }

    private static bool HasNonEmptySchachtField(SchachtRecord record, string logicalField)
        => GetSchachtFieldAliases(logicalField)
            .Any(candidate => !string.IsNullOrWhiteSpace(record.GetFieldValue(candidate)));

    private static bool HasProtocolContent(ProtocolDocument? protocol)
        => protocol is not null
           && (protocol.Original.Entries.Count > 0
               || protocol.Current.Entries.Count > 0
               || protocol.History.Count > 0);

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
