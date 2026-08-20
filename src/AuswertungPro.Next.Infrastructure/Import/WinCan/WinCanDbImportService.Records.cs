using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Import.WinCan;

/// <summary>
/// Fuellt die Haltungs- und Schacht-Datensaetze aus der WinCan-Datenbank und
/// verknuepft sie mit ihren PDF-Protokollen. Aus der Hauptdatei herausgeloest,
/// die dort nur noch den Ablauf des Imports enthaelt.
/// </summary>
public sealed partial class WinCanDbImportService
{
    private static void LinkSectionPdf(HaltungRecord record, string sectionKey, Dictionary<string, List<string>> index)
    {
        // Gemeinsame PDF-Treffer-Suche via Common-Helfer
        var matches = Common.PdfFileIndexHelper.ResolvePdfMatches(index, sectionKey);

        if (matches.Count == 0)
            return;

        var first = matches[0];
        record.SetFieldValue("PDF_Path", first, FieldSource.Legacy, userEdited: false);
        if (matches.Count > 1)
            record.SetFieldValue("PDF_All", string.Join(";", matches), FieldSource.Legacy, userEdited: false);
    }

    private static void ApplySectionFields(HaltungRecord record, WinCanDbSection section, WinCanDbInspection? inspection)
    {
        ApplyField(record, "Strasse", section.Street);
        ApplyField(record, "Rohrmaterial", NormalizeMaterial(section.Material));
        ApplyField(record, "DN_mm", NormalizeNumber(section.Size1) ?? NormalizeNumber(section.PipeHeightOrDia));
        ApplyField(record, "Haltungslaenge_m", NormalizeNumber(section.Length) ?? NormalizeNumber(section.RealLength) ?? NormalizeNumber(section.PipeLength));
        ApplyField(record, "Nutzungsart", NormalizeUsage(section.Usage));
        ApplyField(record, "Eigentuemer", section.Ownership);
        ApplyField(record, "Bemerkungen", section.Memo);
        // Datum_Jahr = INSPEKTIONSdatum (INS_StartDate), konsistent mit VSA_KEK (Untersuchungs-Zeitpunkt)
        // und dem PDF-Import. Das Bau-/Konstruktionsjahr (OBJ_ConstructionDate) wird bewusst NICHT als
        // Fallback verwendet, sonst mischt sich wieder ein Baujahr ein. Ohne Inspektionsdatum bleibt leer.
        ApplyField(record, "Datum_Jahr", NormalizeDate(null, inspection?.StartDate));
        ApplyField(record, "Inspektionsrichtung", NormalizeInspectionDir(inspection?.InspectionDir));
    }

    private static void ImportNodes(
        Project project,
        List<WinCanDbNode> nodes,
        Dictionary<string, List<string>> index,
        IReadOnlyCollection<string> haltungsnamen,
        IImportPdfReferenceResolver pdfReferenzen,
        List<string> messages,
        ref int found,
        ref int created,
        ref int updated,
        ref int uncertain,
        ImportRunContext? ctx)
    {
        if (nodes.Count == 0)
            return;

        foreach (var node in nodes)
        {
            var rawKey = node.Key ?? node.Number ?? string.Empty;
            var key = NormalizeHoldingKey(rawKey);
            if (string.IsNullOrWhiteSpace(key))
            {
                uncertain++;
                messages.Add("Schacht ohne Nummer in DB gefunden (ignoriert).");
                continue;
            }

            var record = FindSchachtRecord(project.SchaechteData, key);
            if (record is null)
            {
                record = new SchachtRecord();
                if (ctx is null)
                    project.SchaechteData.Add(record);
                else
                    ctx.WithCollectionLock(() => project.SchaechteData.Add(record));
                created++;
                messages.Add($"Schacht neu angelegt: {rawKey}");
            }

            found++;
            ApplyNodeFields(record, node);
            LinkNodePdf(record, rawKey, index, haltungsnamen, pdfReferenzen);
            updated++;
        }
    }

    private static void AddRecord(Project project, HaltungRecord record, ImportRunContext? ctx)
    {
        if (ctx is null)
            project.AddRecord(record);
        else
            ctx.WithCollectionLock(() => project.AddRecord(record));
    }

    private static SchachtRecord? FindSchachtRecord(IEnumerable<SchachtRecord> records, string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        foreach (var record in records)
        {
            foreach (var field in SchachtKeyFields)
            {
                var value = record.GetFieldValue(field);
                if (string.IsNullOrWhiteSpace(value))
                    continue;
                if (string.Equals(NormalizeHoldingKey(value), key, StringComparison.OrdinalIgnoreCase))
                    return record;
            }
        }

        return null;
    }

    private static void ApplyNodeFields(SchachtRecord record, WinCanDbNode node)
    {
        SetSchachtField(record, "Schachtnummer", node.Key ?? node.Number);
        SetSchachtField(record, "Funktion", node.Type ?? node.NodeType ?? node.Usage);
        SetSchachtField(record, "Strasse", node.Street ?? node.Locality);
        SetSchachtField(record, "Eigentümer", node.Ownership ?? node.LandOwner);
        SetSchachtField(record, "Bemerkungen", node.Memo);
        SetSchachtField(record, "Zustandsklasse", NormalizeNumber(node.Condition));
        SetSchachtField(record, "Abdeckung Stk.", NormalizeNumber(node.CoversCount));
        SetSchachtField(record, "Status", node.State);
        SetSchachtField(record, "offen/abgeschlossen", NormalizeAccessible(node.Accessible));
        SetSchachtField(record, "Ausführung", node.ConstructionStyle);
        SetSchachtField(record, "Datum/Jahr", NormalizeDate(node.ConstructionYearText, node.ConstructionDate));

        // Zusaetzliche Schacht-Stammdaten aus der NODE-Tabelle (bisher gelesen, aber nie gesetzt).
        // Additiv/empty-only ueber SetSchachtField — schliesst dokumentierte Schacht-Datenluecken.
        SetSchachtField(record, "Schachtform", node.Shape);
        var d1 = NormalizeNumber(node.Size1);
        var d2 = NormalizeNumber(node.Size2);
        var durchmesser = (!string.IsNullOrWhiteSpace(d1) && !string.IsNullOrWhiteSpace(d2))
            ? $"{d1} x {d2}"      // rechteckiger Schacht: beide Kanten
            : (d1 ?? d2);          // rund: nur Durchmesser
        SetSchachtField(record, "Durchmesser", durchmesser);
        SetSchachtField(record, "Schachttiefe", NormalizeNumber(node.RimToInvert) ?? NormalizeNumber(node.DepthToInvert));
        SetSchachtField(record, "Material", NormalizeMaterial(node.Material));
    }

    private static void LinkNodePdf(
        SchachtRecord record,
        string nodeKey,
        Dictionary<string, List<string>> index,
        IReadOnlyCollection<string> haltungsnamen,
        IImportPdfReferenceResolver pdfReferenzen)
    {
        if (string.IsNullOrWhiteSpace(nodeKey))
            return;

        // Gemeinsame PDF-Treffer-Suche via Common-Helfer
        var matches = Common.PdfFileIndexHelper.ResolvePdfMatches(index, nodeKey);

        if (matches.Count == 0)
            return;

        // Die reine Namenssuche liefert auch Haltungsprotokolle, weil deren Dateiname
        // beide Schachtnummern enthaelt. Nur ein PDF, das eindeutig auf DIESEN Schacht
        // zeigt, darf angehaengt werden - sonst haengt am Schacht das Protokoll seiner
        // Haltung (real gemessen: 36 von 55 Schaechten).
        var schachtNummern = new[] { nodeKey };
        var treffer = matches.FirstOrDefault(pfad =>
        {
            var referenz = pdfReferenzen.Resolve(
                Path.GetFileName(pfad), haltungsnamen, schachtNummern);
            return referenz is not null
                   && referenz.Value.Kind == ImportPdfReferenceKind.Schacht
                   && string.Equals(referenz.Value.Name, nodeKey, StringComparison.OrdinalIgnoreCase);
        });

        if (string.IsNullOrWhiteSpace(treffer))
            return;

        // Beide Felder setzen - genau wie der Verteilweg (SchachtProtocolApplier).
        // "Link" allein reichte nicht: Anzeige, Export und Rechtsklick lesen "PDF_Path".
        SetSchachtField(record, "Link", treffer);
        SetSchachtField(record, "PDF_Path", treffer);
    }
}
