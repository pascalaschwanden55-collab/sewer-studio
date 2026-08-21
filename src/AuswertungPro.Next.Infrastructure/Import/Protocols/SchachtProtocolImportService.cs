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
public sealed class SchachtProtocolImportService :
    ISchachtProtocolImportService,
    ISchachtProtocolRebuildService,
    ISchachtProtocolDistributionResultService
{
    private readonly IPdfTextExtractor _pdfTextExtractor;
    private readonly ISchachtProtocolOcrReader _ocrReader;

    public SchachtProtocolImportService()
        : this(
            PdfTextExtractor.Current,
            new SchachtProtocolOcrReaderService(
                PdfImportSafetyPolicy.Current,
                PdfOcrExtractor.Current))
    {
    }

    public SchachtProtocolImportService(
        IPdfTextExtractor pdfTextExtractor,
        ISchachtProtocolOcrReader ocrReader)
    {
        _pdfTextExtractor = pdfTextExtractor ?? throw new ArgumentNullException(nameof(pdfTextExtractor));
        _ocrReader = ocrReader ?? throw new ArgumentNullException(nameof(ocrReader));
    }

    public SchachtProtocolParseResult Parse(string pdfPfad)
    {
        var extraction = _pdfTextExtractor.ExtractPages(pdfPfad);
        return ParseWithOcrFallback(
            extraction.FullText,
            () => _ocrReader.TryRead(pdfPfad));
    }

    internal static SchachtProtocolParseResult ParseWithOcrFallback(
        string? extractedText,
        Func<SchachtProtocolOcrReadResult> readWithOcr)
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
        => Write(ziel, ergebnis, pdfPfadFuerFeld, rebuildFromProtocol: false);

    /// <summary>
    /// Aktualisieren eines einzelnen, bereits verknuepften Schachts: Das gerade neu
    /// gelesene Protokoll ersetzt seinen Stand vollstaendig.
    /// </summary>
    public void Rebuild(SchachtRecord ziel, SchachtProtocolParseResult ergebnis, string pdfPfadFuerFeld)
        => Write(ziel, ergebnis, pdfPfadFuerFeld, rebuildFromProtocol: true);

    private static void Write(
        SchachtRecord ziel,
        SchachtProtocolParseResult ergebnis,
        string pdfPfadFuerFeld,
        bool rebuildFromProtocol)
    {
        var pf = new LegacyPdfImportService.ParsedSchachtFields(
            ergebnis.Schachtnummer, ergebnis.Datum, ergebnis.Funktion,
            ergebnis.Schachtform, ergebnis.Dimension, ergebnis.Schachttiefe,
            ergebnis.PrimaereSchaeden, ergebnis.Bemerkungen, ergebnis.Status, ergebnis.Link);
        var key = (ergebnis.Schachtnummer ?? "").Trim();
        SchachtProtocolApplier.Apply(
            ziel,
            key,
            pf,
            ergebnis.Schaeden,
            pdfPfadFuerFeld,
            rebuildFromProtocol);
    }

    public string DistributePdf(
        string projektOrdner,
        string schachtnummer,
        string pdfQuelle)
        => DistributePdfWithResult(
            projektOrdner,
            schachtnummer,
            pdfQuelle).RelativePath;

    public SchachtProtocolDistributionResult DistributePdfWithResult(
        string projektOrdner,
        string schachtnummer,
        string pdfQuelle)
    {
        var writePathGuard = new ProjectWritePathGuard(projektOrdner);
        var destDir = writePathGuard.EnsureSafeDirectoryTarget(
            ProjectStructure.SchachtVerteiltDir(projektOrdner, schachtnummer));
        Directory.CreateDirectory(destDir);
        writePathGuard.EnsureSafeDirectoryTarget(destDir);

        var preferredDestination = writePathGuard.EnsureSafeFileTarget(
            Path.Combine(destDir, Path.GetFileName(pdfQuelle)));
        if (File.Exists(preferredDestination)
            && VerifiedImportFileCopy.ContentsEqual(pdfQuelle, preferredDestination))
        {
            return new SchachtProtocolDistributionResult(
                ProjectPathResolver.MakeRelative(preferredDestination, projektOrdner),
                FileCreated: false);
        }

        var destination = File.Exists(preferredDestination)
            ? ResolveUniquePath(preferredDestination)
            : preferredDestination;
        destination = writePathGuard.EnsureSafeFileTarget(destination);
        File.Copy(pdfQuelle, destination, overwrite: false);

        return new SchachtProtocolDistributionResult(
            ProjectPathResolver.MakeRelative(destination, projektOrdner),
            FileCreated: true);
    }

    private static string ResolveUniquePath(string preferredPath)
    {
        var directory = Path.GetDirectoryName(preferredPath) ?? string.Empty;
        var stem = Path.GetFileNameWithoutExtension(preferredPath);
        var extension = Path.GetExtension(preferredPath);
        for (var suffix = 1; suffix < 1000; suffix++)
        {
            var candidate = Path.Combine(directory, $"{stem}_{suffix:00}{extension}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
                return candidate;
        }

        throw new IOException($"Kein freier Dateiname fuer das Schachtprotokoll gefunden: {preferredPath}");
    }
}
