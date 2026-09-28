using System.Globalization;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Domain.Models;
using Distributor = AuswertungPro.Next.Infrastructure.HoldingFolderDistributor;

namespace AuswertungPro.Next.Infrastructure.HoldingDistribution;

/// <summary>Welche PDF abgelegt wird: das Original, die abzulegende Datei (bei Sammelberichten ein Teil) und ihr Seitenbereich.</summary>
internal sealed record HoldingPdfSource(string SourcePdfPath, string PdfToStorePath, string? PageRange = null);

/// <summary>Wohin und wie abgelegt wird.</summary>
internal sealed record HoldingDistributionTarget(
    string DestinationMunicipalityFolder,
    bool MoveInsteadOfCopy,
    bool Overwrite,
    string UnmatchedFolderName,
    DistributionTargetConfig? DirectoryConfig = null,
    DistributionVariant Variant = DistributionVariant.Normal);

/// <summary>
/// Legt ein bereits erkanntes Haltungs-PDF ab und ordnet Standard- sowie Gegeninspektionsvideos zu.
/// Ablauf: Haltung bestimmen → Video suchen (<see cref="HoldingVideoSearch"/>) → alle Ziele pruefen
/// → erst dann Dateien schreiben und Links setzen.
/// </summary>
internal static class ParsedHoldingDistributionController
{
    internal static Distributor.DistributionResult Distribute(
        Distributor.ParsedPdf parsed,
        HoldingPdfSource source,
        HoldingVideoSearchContext search,
        HoldingDistributionTarget target,
        Project? project = null)
    {
        var sourcePdfPath = source.SourcePdfPath;
        var pdfToStorePath = source.PdfToStorePath;
        var pageRange = source.PageRange;
        var destinationMunicipalityFolder = target.DestinationMunicipalityFolder;
        var moveInsteadOfCopy = target.MoveInsteadOfCopy;
        var overwrite = target.Overwrite;
        var unmatchedFolderName = target.UnmatchedFolderName;

        var parsedHoldingRaw = parsed.Haltung ?? "UNKNOWN";
        var holdingRaw = PdfCorrectionMetadata.ResolveHolding(project, parsedHoldingRaw);
        if (string.IsNullOrWhiteSpace(holdingRaw))
            holdingRaw = parsedHoldingRaw;

        var holdingId = HoldingIdNormalizer.NormalizeHaltungId(holdingRaw);
        var holding = ProjectPathResolver.SanitizePathSegment(holdingId);
        var originalHolding = ProjectPathResolver.SanitizePathSegment(
            HoldingIdNormalizer.NormalizeHaltungId(parsedHoldingRaw));
        if (parsed.Date is null)
        {
            return new Distributor.DistributionResult(
                false,
                "Date not found",
                sourcePdfPath,
                null,
                null,
                null,
                null,
                null,
                Distributor.VideoMatchStatus.NotChecked);
        }

        var date = parsed.Date.Value;
        var dateStamp = date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var canCorrectPdf = PdfTextLayerRewriter.CanRewrite(parsedHoldingRaw, holdingRaw);
        var correctionResult = PdfTextLayerRewriter.TryRewriteHoldingNumber(
            pdfToStorePath,
            parsedHoldingRaw,
            holdingRaw);
        var pdfSourceToStorePath = correctionResult.Corrected
            ? correctionResult.OutputPdfPath
            : pdfToStorePath;
        var removeOriginalAfterStore = moveInsteadOfCopy
                                       && correctionResult.Corrected
                                       && !string.Equals(
                                           pdfToStorePath,
                                           pdfSourceToStorePath,
                                           StringComparison.OrdinalIgnoreCase);

        var suche = HoldingVideoSearch.Find(
            search, project, parsed.VideoFile, holdingRaw, holding, originalHolding, dateStamp, pdfToStorePath);
        var videoFind = suche.Video;
        holdingRaw = suche.HoldingRaw;
        holding = suche.Holding;
        var holdingLabelAdjusted = suche.HoldingLabelAdjusted;

        try
        {
            var writePaths = new DistributionWritePathGuard(destinationMunicipalityFolder);
            var treeContext = new DistributionPatternContext(
                Datum: date,
                Gemeinde: DistributionDirectoryTreeController.GetMunicipality(project),
                Haltung: holding);
            var holdingFolder = DistributionDirectoryTreeController.ResolveObjectFolder(
                destinationMunicipalityFolder,
                target.DirectoryConfig,
                treeContext,
                "{Haltung}",
                target.Variant,
                "{Datum}_{Haltung}");
            holdingFolder = writePaths.EnsureDirectoryTarget(holdingFolder);
            var destinationPdfName = $"{dateStamp}_{holding}.pdf";
            var destinationPdfPath = writePaths.ResolveUniqueFileTarget(
                Path.Combine(holdingFolder, destinationPdfName),
                overwrite);
            var counterInspection = HoldingVideoSearch.FindCounterInspection(search, holding, dateStamp);
            string? destinationVideoPath = null;
            string? destinationCounterVideoPath = null;
            string? infoPath = null;
            string? unmatchedFolder = null;
            IReadOnlyList<string> ambiguousCandidates = Array.Empty<string>();
            var copyStandardVideo = false;
            var copyCounterVideo = false;
            var videoPaths = new List<string>();

            if (videoFind.Status == Distributor.VideoMatchStatus.Matched && videoFind.VideoPath is not null)
            {
                var videoExtension = Path.GetExtension(videoFind.VideoPath);
                var destinationVideoName = $"{dateStamp}_{holding}{videoExtension}";
                destinationVideoPath = Path.Combine(holdingFolder, destinationVideoName);
                var existingVideo = Distributor.FindExistingVideo(holdingFolder, videoFind.VideoPath);
                if (existingVideo is not null)
                {
                    destinationVideoPath = writePaths.EnsureFileTarget(existingVideo);
                }
                else
                {
                    destinationVideoPath = writePaths.ResolveUniqueFileTarget(
                        destinationVideoPath,
                        overwrite);
                    copyStandardVideo = true;
                }
                videoPaths.Add(destinationVideoPath);
            }

            if (counterInspection.Status == Distributor.VideoMatchStatus.Matched
                && counterInspection.VideoPath is not null
                && !string.Equals(
                    counterInspection.VideoPath,
                    videoFind.VideoPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                var existingCounterVideo = Distributor.FindExistingVideo(
                    holdingFolder,
                    counterInspection.VideoPath);
                if (existingCounterVideo is not null)
                {
                    destinationCounterVideoPath = writePaths.EnsureFileTarget(existingCounterVideo);
                }
                else
                {
                    var extension = Path.GetExtension(counterInspection.VideoPath);
                    var destinationName = $"{dateStamp}_{holding}-g{extension}";
                    destinationCounterVideoPath = writePaths.ResolveUniqueFileTarget(
                        Path.Combine(holdingFolder, destinationName),
                        overwrite);
                    copyCounterVideo = true;
                }
                videoPaths.Add(destinationCounterVideoPath);
            }

            if (videoPaths.Count == 0)
            {
                if (videoFind.Status == Distributor.VideoMatchStatus.NotFound
                    && counterInspection.Status == Distributor.VideoMatchStatus.NotFound)
                {
                    var infoName = $"{dateStamp}_{holding}_VIDEO_MISSING.txt";
                    infoPath = writePaths.ResolveUniqueFileTarget(
                        Path.Combine(holdingFolder, infoName),
                        overwrite);
                }
                else if (videoFind.Status == Distributor.VideoMatchStatus.Ambiguous
                         || counterInspection.Status == Distributor.VideoMatchStatus.Ambiguous)
                {
                    var infoName = $"{dateStamp}_{holding}_VIDEO_AMBIGUOUS.txt";
                    infoPath = writePaths.ResolveUniqueFileTarget(
                        Path.Combine(holdingFolder, infoName),
                        overwrite);
                    ambiguousCandidates = videoFind.Status == Distributor.VideoMatchStatus.Ambiguous
                        ? videoFind.Candidates
                        : counterInspection.Candidates;
                    var holdingParent = Directory.GetParent(holdingFolder)?.FullName
                                        ?? destinationMunicipalityFolder;
                    var safeUnmatchedFolderName = ProjectPathResolver.SanitizePathSegment(unmatchedFolderName);
                    unmatchedFolder = Path.Combine(
                        holdingParent,
                        safeUnmatchedFolderName,
                        holding);
                    unmatchedFolder = writePaths.EnsureDirectoryTarget(unmatchedFolder);
                }
            }

            // Alle Ziele sind geprueft. Erst jetzt duerfen Quellen verschoben oder
            // bereits vorhandene Projektdateien veraendert werden.
            Directory.CreateDirectory(holdingFolder);
            DistributionFileTransfer.MoveOrCopy(
                pdfSourceToStorePath,
                destinationPdfPath,
                moveInsteadOfCopy,
                overwrite);

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

            if (videoFind.Status == Distributor.VideoMatchStatus.Matched
                && videoFind.VideoPath is not null
                && destinationVideoPath is not null)
            {
                if (copyStandardVideo)
                {
                    DistributionFileTransfer.MoveOrCopy(
                        videoFind.VideoPath,
                        destinationVideoPath,
                        moveInsteadOfCopy,
                        overwrite);
                }

                UpdateRecordLink(
                    project,
                    holding,
                    "Link",
                    destinationVideoPath,
                    destinationMunicipalityFolder);
            }

            if (counterInspection.Status == Distributor.VideoMatchStatus.Matched
                && counterInspection.VideoPath is not null
                && destinationCounterVideoPath is not null)
            {
                if (copyCounterVideo)
                {
                    DistributionFileTransfer.MoveOrCopy(
                        counterInspection.VideoPath,
                        destinationCounterVideoPath,
                        moveInsteadOfCopy,
                        overwrite);
                }

                UpdateRecordLink(
                    project,
                    holding,
                    "Link_G",
                    destinationCounterVideoPath,
                    destinationMunicipalityFolder);
            }

            if (videoPaths.Count == 0 && infoPath is not null)
            {
                if (videoFind.Status == Distributor.VideoMatchStatus.NotFound
                    && counterInspection.Status == Distributor.VideoMatchStatus.NotFound)
                {
                    var filmName = string.IsNullOrWhiteSpace(parsed.VideoFile)
                        ? "<nicht gefunden>"
                        : parsed.VideoFile;
                    AtomicTextFileWriter.WriteAllText(
                        infoPath,
                        VideoConflictArtifacts.BuildMissingInfo(
                            sourcePdfPath,
                            filmName,
                            date,
                            holdingRaw));
                }
                else if (unmatchedFolder is not null)
                {
                    AtomicTextFileWriter.WriteAllText(
                        infoPath,
                        VideoConflictArtifacts.BuildAmbiguousInfo(
                            sourcePdfPath,
                            parsed.VideoFile!,
                            date,
                            holdingRaw,
                            ambiguousCandidates));
                    Directory.CreateDirectory(unmatchedFolder);
                    VideoConflictArtifacts.CopyCandidates(
                        unmatchedFolder,
                        dateStamp,
                        holding,
                        ambiguousCandidates);
                }
            }

            var message = videoPaths.Count switch
            {
                2 => "OK (Standard+Gegeninspektion)",
                1 => "OK (1 Video)",
                0 when videoFind.Status == Distributor.VideoMatchStatus.Ambiguous
                    || counterInspection.Status == Distributor.VideoMatchStatus.Ambiguous => "Video ambiguous",
                0 => "Video missing",
                _ => "OK"
            };
            if (!string.IsNullOrWhiteSpace(parsed.Message))
                message += $" / Parser: {parsed.Message}";
            if (holdingLabelAdjusted)
                message += " [Haltung korrigiert via M150/MDB]";
            if (videoFind.Status == Distributor.VideoMatchStatus.Matched
                && !string.IsNullOrWhiteSpace(videoFind.Message))
            {
                if (videoFind.Message.Contains("M150/MDB sidecar", StringComparison.OrdinalIgnoreCase))
                    message += " [Quelle: M150/MDB]";
                else if (videoFind.Message.Contains("existing Link path", StringComparison.OrdinalIgnoreCase))
                    message += " [Quelle: Datensatz-Link]";
                else if (videoFind.Message.Contains("CDIndex", StringComparison.OrdinalIgnoreCase))
                    message += " [Quelle: CDIndex-Foto]";
            }
            if (correctionResult.Corrected)
            {
                message += $" [PDF korrigiert: {correctionResult.MatchCount} Treffer auf {correctionResult.PageCount} Seiten]";
            }
            else if (canCorrectPdf && !string.IsNullOrWhiteSpace(correctionResult.Message))
            {
                message += $" [PDF-Korrektur: {correctionResult.Message}]";
            }
            if (!string.IsNullOrWhiteSpace(pageRange))
                message = $"Split Seiten {pageRange} - {message}";

            return new Distributor.DistributionResult(
                true,
                message,
                sourcePdfPath,
                videoFind.VideoPath,
                destinationPdfPath,
                destinationVideoPath,
                infoPath,
                holdingFolder,
                videoFind.Status,
                correctionResult.Corrected,
                correctionResult.Message);
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

    private static void UpdateRecordLink(
        Project? project,
        string holding,
        string fieldName,
        string destinationVideoPath,
        string destinationMunicipalityFolder)
    {
        if (project is null || string.IsNullOrWhiteSpace(destinationVideoPath))
            return;

        var record = Distributor.FindRecordByHolding(project, holding);
        if (record is null)
            return;

        var metadata = record.FieldMeta.TryGetValue(fieldName, out var value) ? value : null;
        if (metadata is not null && metadata.UserEdited)
            return;

        record.SetFieldValue(
            fieldName,
            Distributor.MakeProjectRelativeLink(destinationVideoPath, destinationMunicipalityFolder),
            FieldSource.Unknown,
            userEdited: false);
        project.ModifiedAtUtc = DateTime.UtcNow;
        project.Dirty = true;
    }
}
