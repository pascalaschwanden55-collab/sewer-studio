using System.Globalization;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Domain.Models;
using Distributor = AuswertungPro.Next.Infrastructure.HoldingFolderDistributor;

namespace AuswertungPro.Next.Infrastructure.HoldingDistribution;

/// <summary>
/// Legt ein bereits erkanntes Schachtprotokoll ab und fuehrt Teil-PDFs desselben
/// Schachts sicher zusammen. Haelt diese Verantwortung aus dem grossen Verteiler heraus.
/// </summary>
internal static class ParsedShaftDistributionController
{
    internal static Distributor.DistributionResult Distribute(
        Distributor.ParsedShaftPdf parsed,
        string sourcePdfPath,
        string pdfToStorePath,
        string destinationMunicipalityFolder,
        bool moveInsteadOfCopy,
        bool overwrite,
        string? pageRange,
        Dictionary<string, string> shaftOutputPathByKey,
        Project? project = null,
        DistributionTargetConfig? directoryConfig = null,
        DistributionVariant variant = DistributionVariant.Normal)
    {
        if (string.IsNullOrWhiteSpace(parsed.ShaftNumber))
        {
            return new Distributor.DistributionResult(
                false,
                "Schachtnummer nicht gefunden",
                sourcePdfPath,
                null,
                null,
                null,
                null,
                null,
                Distributor.VideoMatchStatus.NotChecked);
        }

        if (parsed.Date is null)
        {
            return new Distributor.DistributionResult(
                false,
                "Datum nicht gefunden",
                sourcePdfPath,
                null,
                null,
                null,
                null,
                null,
                Distributor.VideoMatchStatus.NotChecked);
        }

        var parsedShaftRaw = parsed.ShaftNumber.Trim();
        var shaftRaw = PdfCorrectionMetadata.ResolveShaft(project, parsedShaftRaw);
        if (string.IsNullOrWhiteSpace(shaftRaw))
            shaftRaw = parsedShaftRaw;

        var shaft = ProjectPathResolver.SanitizePathSegment(shaftRaw);
        var dateStamp = parsed.Date.Value.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var canCorrectPdf = PdfTextLayerRewriter.CanRewrite(parsedShaftRaw, shaftRaw);
        var correctionResult = PdfTextLayerRewriter.TryRewriteHoldingNumber(
            pdfToStorePath,
            parsedShaftRaw,
            shaftRaw);
        var pdfSourceToStorePath = correctionResult.Corrected
            ? correctionResult.OutputPdfPath
            : pdfToStorePath;
        var removeOriginalAfterStore = moveInsteadOfCopy
            && correctionResult.Corrected
            && !string.Equals(pdfToStorePath, pdfSourceToStorePath, StringComparison.OrdinalIgnoreCase);

        try
        {
            var writePaths = new DistributionWritePathGuard(destinationMunicipalityFolder);
            var treeContext = new DistributionPatternContext(
                Datum: parsed.Date.Value,
                Gemeinde: DistributionDirectoryTreeController.GetMunicipality(project),
                Schachtnummer: shaft);
            var shaftFolder = DistributionDirectoryTreeController.ResolveObjectFolder(
                destinationMunicipalityFolder,
                directoryConfig,
                treeContext,
                "{Schachtnummer}",
                variant,
                "{Datum}_{Schachtnummer}");
            shaftFolder = writePaths.EnsureDirectoryTarget(shaftFolder);
            Directory.CreateDirectory(shaftFolder);

            var destinationPdfName = $"{dateStamp}_{shaft}.pdf";
            var shaftKey = $"{dateStamp}|{shaft}";
            string destinationPdfPath;
            var appendedToExisting = false;

            if (shaftOutputPathByKey.TryGetValue(shaftKey, out var existingPath)
                && !string.IsNullOrWhiteSpace(existingPath)
                && File.Exists(existingPath))
            {
                try
                {
                    existingPath = writePaths.EnsureFileTarget(existingPath);
                    Distributor.AppendPdfFile(existingPath, pdfSourceToStorePath, moveInsteadOfCopy);
                    destinationPdfPath = existingPath;
                    appendedToExisting = true;
                }
                catch (Exception ex)
                {
                    return new Distributor.DistributionResult(
                        false,
                        $"Konnte PDF nicht zusammenführen: {ex.Message}",
                        sourcePdfPath,
                        null,
                        null,
                        null,
                        null,
                        shaftFolder,
                        Distributor.VideoMatchStatus.NotChecked);
                }
            }
            else
            {
                destinationPdfPath = writePaths.ResolveUniqueFileTarget(
                    Path.Combine(shaftFolder, destinationPdfName),
                    overwrite);
                DistributionFileTransfer.MoveOrCopy(
                    pdfSourceToStorePath,
                    destinationPdfPath,
                    moveInsteadOfCopy,
                    overwrite);
                shaftOutputPathByKey[shaftKey] = destinationPdfPath;
            }

            if (removeOriginalAfterStore
                && File.Exists(pdfToStorePath)
                && !string.Equals(pdfToStorePath, pdfSourceToStorePath, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    File.Delete(pdfToStorePath);
                }
                catch
                {
                    // Best-effort cleanup for move semantics.
                }
            }

            var message = "OK (Schachtprotokoll)";
            if (appendedToExisting)
                message += " + Seite angehängt";
            if (correctionResult.Corrected)
                message += $" [PDF korrigiert: {correctionResult.MatchCount} Treffer auf {correctionResult.PageCount} Seiten]";
            else if (canCorrectPdf && !string.IsNullOrWhiteSpace(correctionResult.Message))
                message += $" [PDF-Korrektur: {correctionResult.Message}]";
            if (!string.IsNullOrWhiteSpace(pageRange))
                message = $"Split Seiten {pageRange} - {message}";

            return new Distributor.DistributionResult(
                true,
                message,
                sourcePdfPath,
                null,
                destinationPdfPath,
                null,
                null,
                shaftFolder,
                Distributor.VideoMatchStatus.NotChecked,
                PdfCorrected: correctionResult.Corrected,
                PdfCorrectionMessage: correctionResult.Message);
        }
        finally
        {
            if (correctionResult.Corrected
                && !string.Equals(correctionResult.OutputPdfPath, pdfToStorePath, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    if (File.Exists(correctionResult.OutputPdfPath))
                        File.Delete(correctionResult.OutputPdfPath);
                }
                catch
                {
                    // Best-effort cleanup.
                }
            }
        }
    }
}
