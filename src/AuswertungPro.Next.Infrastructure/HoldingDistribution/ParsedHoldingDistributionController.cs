using System.Globalization;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using Distributor = AuswertungPro.Next.Infrastructure.HoldingFolderDistributor;

namespace AuswertungPro.Next.Infrastructure.HoldingDistribution;

/// <summary>
/// Legt ein bereits erkanntes Haltungs-PDF ab und ordnet Standard- sowie Gegeninspektionsvideos zu.
/// </summary>
internal static class ParsedHoldingDistributionController
{
    internal static Distributor.DistributionResult Distribute(
        Distributor.ParsedPdf parsed,
        string sourcePdfPath,
        string pdfToStorePath,
        string videoSourceFolder,
        string destinationMunicipalityFolder,
        bool moveInsteadOfCopy,
        bool overwrite,
        bool recursiveVideoSearch,
        string unmatchedFolderName,
        string? pageRange,
        Project? project = null,
        IReadOnlyList<string>? videoFilesCache = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? sidecarVideoLinksByHolding = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? sidecarHoldingsByVideoLink = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? cdIndexVideoLinksByPhoto = null)
    {
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
        var pdfReplacements = Distributor.BuildRenameReplacements(parsedHoldingRaw, holdingRaw);
        var correctionResult = Distributor.TryCorrectPdfTextLayer(pdfToStorePath, pdfReplacements);
        var pdfSourceToStorePath = correctionResult.Corrected
            ? correctionResult.OutputPdfPath
            : pdfToStorePath;
        var removeOriginalAfterStore = moveInsteadOfCopy
                                       && correctionResult.Corrected
                                       && !string.Equals(
                                           pdfToStorePath,
                                           pdfSourceToStorePath,
                                           StringComparison.OrdinalIgnoreCase);

        var videoFind = string.IsNullOrWhiteSpace(parsed.VideoFile)
            ? Distributor.FindVideoByHaltungDate(
                videoSourceFolder,
                holding,
                dateStamp,
                recursiveVideoSearch,
                videoFilesCache)
            : Distributor.FindVideo(
                parsed.VideoFile,
                videoSourceFolder,
                holding,
                dateStamp,
                recursiveVideoSearch,
                videoFilesCache);

        if (videoFind.Status != Distributor.VideoMatchStatus.Matched
            && !string.Equals(originalHolding, holding, StringComparison.OrdinalIgnoreCase))
        {
            var fallback = string.IsNullOrWhiteSpace(parsed.VideoFile)
                ? Distributor.FindVideoByHaltungDate(
                    videoSourceFolder,
                    originalHolding,
                    dateStamp,
                    recursiveVideoSearch,
                    videoFilesCache)
                : Distributor.FindVideo(
                    parsed.VideoFile,
                    videoSourceFolder,
                    originalHolding,
                    dateStamp,
                    recursiveVideoSearch,
                    videoFilesCache);
            if (fallback.Status == Distributor.VideoMatchStatus.Matched
                || (videoFind.Status == Distributor.VideoMatchStatus.NotFound
                    && fallback.Status == Distributor.VideoMatchStatus.Ambiguous))
            {
                videoFind = fallback;
            }
        }

        if (videoFind.Status != Distributor.VideoMatchStatus.Matched)
        {
            var fromLink = Distributor.TryFindVideoFromRecordLink(
                project,
                holding,
                videoSourceFolder,
                dateStamp,
                recursiveVideoSearch,
                videoFilesCache);
            if (fromLink.Status == Distributor.VideoMatchStatus.Matched)
                videoFind = fromLink;
        }

        if (videoFind.Status != Distributor.VideoMatchStatus.Matched)
        {
            var fromSidecar = Distributor.TryFindVideoFromSidecarLinks(
                sidecarVideoLinksByHolding,
                holding,
                videoSourceFolder,
                dateStamp,
                recursiveVideoSearch,
                videoFilesCache);
            if (fromSidecar.Status == Distributor.VideoMatchStatus.Matched
                || (videoFind.Status == Distributor.VideoMatchStatus.NotFound
                    && fromSidecar.Status == Distributor.VideoMatchStatus.Ambiguous))
            {
                videoFind = fromSidecar;
            }
        }

        if (videoFind.Status != Distributor.VideoMatchStatus.Matched)
        {
            var fromCdIndex = Distributor.TryFindVideoFromCdIndexPhotoHints(
                cdIndexVideoLinksByPhoto,
                pdfToStorePath,
                holding,
                videoSourceFolder,
                dateStamp,
                recursiveVideoSearch,
                videoFilesCache);
            if (fromCdIndex.Status == Distributor.VideoMatchStatus.Matched
                || (videoFind.Status == Distributor.VideoMatchStatus.NotFound
                    && fromCdIndex.Status == Distributor.VideoMatchStatus.Ambiguous))
            {
                videoFind = fromCdIndex;
            }
        }

        var holdingLabelAdjusted = false;
        if (videoFind.Status == Distributor.VideoMatchStatus.Matched && videoFind.VideoPath is not null)
        {
            var mappedHolding = Distributor.TryResolveHoldingFromMatchedVideo(
                sidecarHoldingsByVideoLink,
                sidecarVideoLinksByHolding,
                videoFind.VideoPath,
                holding);
            if (!string.IsNullOrWhiteSpace(mappedHolding)
                && !string.Equals(mappedHolding, holding, StringComparison.OrdinalIgnoreCase))
            {
                holdingRaw = mappedHolding;
                holding = ProjectPathResolver.SanitizePathSegment(
                    HoldingIdNormalizer.NormalizeHaltungId(mappedHolding));
                holdingLabelAdjusted = true;
            }
        }

        if (!holdingLabelAdjusted
            && videoFind.Status == Distributor.VideoMatchStatus.Matched
            && videoFind.VideoPath is not null)
        {
            var mappedHolding = Distributor.TryResolveHoldingFromMatchedVideoName(
                project,
                videoFind.VideoPath,
                holding);
            if (!string.IsNullOrWhiteSpace(mappedHolding))
            {
                holdingRaw = mappedHolding;
                holding = ProjectPathResolver.SanitizePathSegment(
                    HoldingIdNormalizer.NormalizeHaltungId(mappedHolding));
                holdingLabelAdjusted = true;
            }
        }

        try
        {
            var holdingFolder = Path.Combine(destinationMunicipalityFolder, holding);
            Directory.CreateDirectory(holdingFolder);

            var destinationPdfName = $"{dateStamp}_{holding}.pdf";
            var destinationPdfPath = DistributionFileTransfer.EnsureUniquePath(
                Path.Combine(holdingFolder, destinationPdfName),
                overwrite);
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

            var counterInspection = Distributor.FindVideoByHaltungDate(
                videoSourceFolder,
                holding + "g",
                dateStamp,
                recursiveVideoSearch,
                videoFilesCache);
            string? destinationVideoPath = null;
            string? destinationCounterVideoPath = null;
            string? infoPath = null;
            var videoPaths = new List<string>();

            if (videoFind.Status == Distributor.VideoMatchStatus.Matched && videoFind.VideoPath is not null)
            {
                var videoExtension = Path.GetExtension(videoFind.VideoPath);
                var destinationVideoName = $"{dateStamp}_{holding}{videoExtension}";
                destinationVideoPath = Path.Combine(holdingFolder, destinationVideoName);
                var existingVideo = Distributor.FindExistingVideo(holdingFolder, videoFind.VideoPath);
                if (existingVideo is not null)
                {
                    destinationVideoPath = existingVideo;
                }
                else
                {
                    destinationVideoPath = DistributionFileTransfer.EnsureUniquePath(
                        destinationVideoPath,
                        overwrite);
                    DistributionFileTransfer.MoveOrCopy(
                        videoFind.VideoPath,
                        destinationVideoPath,
                        moveInsteadOfCopy,
                        overwrite);
                }
                videoPaths.Add(destinationVideoPath);
                UpdateRecordLink(
                    project,
                    holding,
                    "Link",
                    destinationVideoPath,
                    destinationMunicipalityFolder);
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
                    destinationCounterVideoPath = existingCounterVideo;
                }
                else
                {
                    var extension = Path.GetExtension(counterInspection.VideoPath);
                    var destinationName = $"{dateStamp}_{holding}-g{extension}";
                    destinationCounterVideoPath = DistributionFileTransfer.EnsureUniquePath(
                        Path.Combine(holdingFolder, destinationName),
                        overwrite);
                    DistributionFileTransfer.MoveOrCopy(
                        counterInspection.VideoPath,
                        destinationCounterVideoPath,
                        moveInsteadOfCopy,
                        overwrite);
                }
                videoPaths.Add(destinationCounterVideoPath);
                UpdateRecordLink(
                    project,
                    holding,
                    "Link_G",
                    destinationCounterVideoPath,
                    destinationMunicipalityFolder);
            }

            if (videoPaths.Count == 0)
            {
                if (videoFind.Status == Distributor.VideoMatchStatus.NotFound
                    && counterInspection.Status == Distributor.VideoMatchStatus.NotFound)
                {
                    var infoName = $"{dateStamp}_{holding}_VIDEO_MISSING.txt";
                    infoPath = DistributionFileTransfer.EnsureUniquePath(
                        Path.Combine(holdingFolder, infoName),
                        overwrite);
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
                else if (videoFind.Status == Distributor.VideoMatchStatus.Ambiguous
                         || counterInspection.Status == Distributor.VideoMatchStatus.Ambiguous)
                {
                    var infoName = $"{dateStamp}_{holding}_VIDEO_AMBIGUOUS.txt";
                    infoPath = DistributionFileTransfer.EnsureUniquePath(
                        Path.Combine(holdingFolder, infoName),
                        overwrite);
                    var candidates = videoFind.Status == Distributor.VideoMatchStatus.Ambiguous
                        ? videoFind.Candidates
                        : counterInspection.Candidates;
                    AtomicTextFileWriter.WriteAllText(
                        infoPath,
                        VideoConflictArtifacts.BuildAmbiguousInfo(
                            sourcePdfPath,
                            parsed.VideoFile!,
                            date,
                            holdingRaw,
                            candidates));
                    var unmatchedFolder = Path.Combine(
                        destinationMunicipalityFolder,
                        unmatchedFolderName,
                        holding);
                    Directory.CreateDirectory(unmatchedFolder);
                    VideoConflictArtifacts.CopyCandidates(
                        unmatchedFolder,
                        dateStamp,
                        holding,
                        candidates);
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
            else if (pdfReplacements.Count > 0 && !string.IsNullOrWhiteSpace(correctionResult.Message))
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
