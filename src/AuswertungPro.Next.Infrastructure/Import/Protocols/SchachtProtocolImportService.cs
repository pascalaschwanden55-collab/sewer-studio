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
        return ParseFromText(extraction.FullText);
    }

    /// <summary>Reine Text-zu-Ergebnis-Logik, damit sie ohne echtes PDF testbar ist.</summary>
    internal static SchachtProtocolParseResult ParseFromText(string fullText)
    {
        var istSchacht = !string.IsNullOrWhiteSpace(fullText)
            && fullText.Contains("Schachtprotokoll", StringComparison.OrdinalIgnoreCase);
        if (!istSchacht)
        {
            return new SchachtProtocolParseResult(
                false, null, null, null, null, null, null, null,
                Array.Empty<(string, string)>());
        }

        var pf = LegacyPdfImportService.ParseSchachtFields(fullText);
        var damages = SchachtProtocolParser.ParseSchachtDamageEntries(fullText);
        return new SchachtProtocolParseResult(
            true, pf.SchachtNummer, pf.Datum, pf.Funktion, pf.PrimaereSchaeden,
            pf.Bemerkungen, pf.Status, pf.Link, damages);
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
