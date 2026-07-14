using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Pdf;

namespace AuswertungPro.Next.Infrastructure.Import.Protocols;

/// <summary>
/// Liest ein einzelnes Schacht-Protokoll-PDF und wendet es auf einen Schacht an.
/// Nutzt die bestehende Lese-/Schaden-Parser-Technik (PdfTextExtractor,
/// SchachtProtocolParser) und die gemeinsame Anwende-Logik (SchachtProtocolApplier).
/// </summary>
public sealed class SchachtProtocolImportService : ISchachtProtocolImportService
{
    public SchachtProtocolParseResult Parse(string pdfPfad)
    {
        var extraction = PdfTextExtractor.ExtractPages(pdfPfad);
        return ParseWithOcrFallback(
            extraction.FullText,
            () => PdfDocumentOcrExtractor.TryExtract(pdfPfad));
    }

    internal static SchachtProtocolParseResult ParseWithOcrFallback(
        string? extractedText,
        Func<OcrDocumentExtractionResult> readWithOcr)
    {
        ArgumentNullException.ThrowIfNull(readWithOcr);

        var directResult = ParseFromText(extractedText ?? string.Empty);
        if (directResult.IstSchachtprotokoll)
            return directResult;

        if (!IsEmptyOrNearlyEmpty(extractedText))
        {
            return directResult with
            {
                Lesehinweis = "Das PDF enthaelt lesbaren Text, wurde aber nicht als Schachtprotokoll erkannt."
            };
        }

        var ocr = readWithOcr();
        if (!ocr.Success)
        {
            return directResult with
            {
                Lesehinweis = "Das PDF ist vermutlich ein Bild-Scan ohne Textebene. " +
                              $"Die Texterkennung konnte nicht ausgefuehrt werden: {ocr.Message}"
            };
        }

        var ocrResult = ParseFromText(ocr.Text);
        if (!ocrResult.IstSchachtprotokoll)
        {
            return ocrResult with
            {
                Lesehinweis = "Die Texterkennung wurde ausgefuehrt, der Inhalt wurde aber nicht als Schachtprotokoll erkannt."
            };
        }

        var partialHint = ocr.ExtractedPages < ocr.TotalPages
            ? $" Nicht lesbare Seiten: {ocr.TotalPages - ocr.ExtractedPages}."
            : string.Empty;
        return ocrResult with
        {
            Lesehinweis = $"Texterkennung (OCR) verwendet: {ocr.ExtractedPages} von {ocr.TotalPages} Seiten.{partialHint}"
        };
    }

    /// <summary>Reine Text-zu-Ergebnis-Logik, damit sie ohne echtes PDF testbar ist.</summary>
    internal static SchachtProtocolParseResult ParseFromText(string fullText)
    {
        var istSchacht = SchachtProtocolDetector.IsSchachtProtocol(fullText);
        if (!istSchacht)
        {
            return new SchachtProtocolParseResult(
                false, null, null, null, null, null, null, null, null, null, null,
                Array.Empty<(string, string)>());
        }

        var pf = LegacyPdfImportService.ParseSchachtFields(fullText);
        var damages = SchachtProtocolParser.ParseSchachtDamageEntries(fullText);
        return new SchachtProtocolParseResult(
            true, pf.SchachtNummer, pf.Datum, pf.Funktion,
            pf.Schachtform, pf.Dimension, pf.Schachttiefe, pf.PrimaereSchaeden,
            pf.Bemerkungen, pf.Status, pf.Link, damages);
    }

    private static bool IsEmptyOrNearlyEmpty(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return true;

        var meaningfulCharacters = text.Count(character =>
            !char.IsWhiteSpace(character) && !char.IsControl(character));
        return meaningfulCharacters < 40;
    }

    public SchachtRecord? FindSchacht(Project project, string? schachtnummer)
    {
        if (string.IsNullOrWhiteSpace(schachtnummer))
            return null;

        var key = schachtnummer.Trim();
        return project.SchaechteData.FirstOrDefault(r =>
            string.Equals((r.GetFieldValue("Schachtnummer") ?? "").Trim(), key, StringComparison.OrdinalIgnoreCase) ||
            string.Equals((r.GetFieldValue("Nr.") ?? "").Trim(), key, StringComparison.OrdinalIgnoreCase) ||
            string.Equals((r.GetFieldValue("NR.") ?? "").Trim(), key, StringComparison.OrdinalIgnoreCase));
    }

    public void Apply(SchachtRecord ziel, SchachtProtocolParseResult ergebnis, string pdfPfadFuerFeld)
    {
        var pf = new LegacyPdfImportService.ParsedSchachtFields(
            ergebnis.Schachtnummer, ergebnis.Datum, ergebnis.Funktion,
            ergebnis.Schachtform, ergebnis.Dimension, ergebnis.Schachttiefe,
            ergebnis.PrimaereSchaeden, ergebnis.Bemerkungen, ergebnis.Status, ergebnis.Link);
        var key = (ergebnis.Schachtnummer ?? "").Trim();
        SchachtProtocolApplier.Apply(ziel, key, pf, ergebnis.Schaeden, pdfPfadFuerFeld);
    }

    public string DistributePdf(string projektOrdner, string schachtnummer, string pdfQuelle)
    {
        var destDir = ProjectStructure.SchachtVerteiltDir(projektOrdner, schachtnummer);
        Directory.CreateDirectory(destDir);

        var dest = Path.Combine(destDir, Path.GetFileName(pdfQuelle));
        if (!File.Exists(dest))
            File.Copy(pdfQuelle, dest, overwrite: false);

        return ProjectPathResolver.MakeRelative(dest, projektOrdner);
    }
}
