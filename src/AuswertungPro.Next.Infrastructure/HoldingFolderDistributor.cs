using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.HoldingDistribution;
using AuswertungPro.Next.Infrastructure.Media;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
using AuswertungPro.Next.Infrastructure.Map;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using AuswertungPro.Next.Infrastructure.Import.Pdf;

namespace AuswertungPro.Next.Infrastructure;

public static partial class HoldingFolderDistributor
{
    public static IReadOnlyList<DistributionResult> Distribute(
        string pdfSourceFolder, string videoSourceFolder, string destGemeindeFolder,
        bool moveInsteadOfCopy = false, bool overwrite = false, bool recursiveVideoSearch = true, string unmatchedFolderName = "__UNMATCHED",
        AuswertungPro.Next.Domain.Models.Project? project = null, IProgress<DistributionProgress>? progress = null, string? xtfSourceFolder = null)
        => Distribute(pdfSourceFolder, videoSourceFolder, destGemeindeFolder, null, moveInsteadOfCopy, overwrite, recursiveVideoSearch, unmatchedFolderName, project, progress, xtfSourceFolder);

    public static IReadOnlyList<DistributionResult> Distribute(
        string pdfSourceFolder,
        string videoSourceFolder,
        string destGemeindeFolder,
        DistributionTargetConfig? directoryConfig,
        bool moveInsteadOfCopy = false,
        bool overwrite = false,
        bool recursiveVideoSearch = true,
        string unmatchedFolderName = "__UNMATCHED",
        AuswertungPro.Next.Domain.Models.Project? project = null,
        IProgress<DistributionProgress>? progress = null,
        string? xtfSourceFolder = null)
    {
        if (!Directory.Exists(pdfSourceFolder))
            return new[] { new DistributionResult(false, $"PDF folder not found: {pdfSourceFolder}", pdfSourceFolder, null, null, null, null, null, VideoMatchStatus.NotChecked) };

        var pdfFiles = Common.SafeFileEnumeration.EnumerateFilesSafe(pdfSourceFolder, "*.pdf", recursive: true)
            .Where(p => !Path.GetFileName(p).StartsWith("split_", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (pdfFiles.Count == 0)
            return new[] { new DistributionResult(false, $"No PDF files found (recursive) in: {pdfSourceFolder}", pdfSourceFolder, null, null, null, null, null, VideoMatchStatus.NotChecked) };

        return DistributeCore(
            pdfFiles: pdfFiles,
            videoSourceFolder: videoSourceFolder,
            destGemeindeFolder: destGemeindeFolder,
            moveInsteadOfCopy: moveInsteadOfCopy,
            overwrite: overwrite,
            recursiveVideoSearch: recursiveVideoSearch,
            unmatchedFolderName: unmatchedFolderName,
            project: project,
            progress: progress,
            xtfSourceFolder: xtfSourceFolder ?? pdfSourceFolder,
            directoryConfig: directoryConfig);
    }

    public static IReadOnlyList<DistributionResult> DistributeFiles(
        IEnumerable<string> pdfFiles, string videoSourceFolder, string destGemeindeFolder,
        bool moveInsteadOfCopy = false, bool overwrite = false, bool recursiveVideoSearch = true, string unmatchedFolderName = "__UNMATCHED",
        AuswertungPro.Next.Domain.Models.Project? project = null, IProgress<DistributionProgress>? progress = null, string? xtfSourceFolder = null)
        => DistributeFiles(pdfFiles, videoSourceFolder, destGemeindeFolder, null, moveInsteadOfCopy, overwrite, recursiveVideoSearch, unmatchedFolderName, project, progress, xtfSourceFolder);

    public static IReadOnlyList<DistributionResult> DistributeFiles(
        IEnumerable<string> pdfFiles,
        string videoSourceFolder,
        string destGemeindeFolder,
        DistributionTargetConfig? directoryConfig,
        bool moveInsteadOfCopy = false,
        bool overwrite = false,
        bool recursiveVideoSearch = true,
        string unmatchedFolderName = "__UNMATCHED",
        AuswertungPro.Next.Domain.Models.Project? project = null,
        IProgress<DistributionProgress>? progress = null,
        string? xtfSourceFolder = null)
    {
        var validPdfFiles = pdfFiles
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Where(File.Exists)
            .Where(p => string.Equals(Path.GetExtension(p), ".pdf", StringComparison.OrdinalIgnoreCase))
            .Where(p => !Path.GetFileName(p).StartsWith("split_", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (validPdfFiles.Count == 0)
            return new[] { new DistributionResult(false, "No valid PDF files selected.", "", null, null, null, null, null, VideoMatchStatus.NotChecked) };

        // Derive XTF source from parent directories of selected PDFs
        var derivedXtfFolder = xtfSourceFolder;
        if (string.IsNullOrWhiteSpace(derivedXtfFolder) && validPdfFiles.Count > 0)
            derivedXtfFolder = Path.GetDirectoryName(validPdfFiles[0]);

        return DistributeCore(
            pdfFiles: validPdfFiles,
            videoSourceFolder: videoSourceFolder,
            destGemeindeFolder: destGemeindeFolder,
            moveInsteadOfCopy: moveInsteadOfCopy,
            overwrite: overwrite,
            recursiveVideoSearch: recursiveVideoSearch,
            unmatchedFolderName: unmatchedFolderName,
            project: project,
            progress: progress,
            xtfSourceFolder: derivedXtfFolder,
            directoryConfig: directoryConfig);
    }

    public static IReadOnlyList<DistributionResult> DistributeTxt(
        string txtSourceFolder, string videoSourceFolder, string destGemeindeFolder,
        bool moveInsteadOfCopy = false, bool overwrite = false, bool recursiveVideoSearch = true, string unmatchedFolderName = "__UNMATCHED",
        AuswertungPro.Next.Domain.Models.Project? project = null, IProgress<DistributionProgress>? progress = null)
        => DistributeTxt(txtSourceFolder, videoSourceFolder, destGemeindeFolder, null, moveInsteadOfCopy, overwrite, recursiveVideoSearch, unmatchedFolderName, project, progress);

    public static IReadOnlyList<DistributionResult> DistributeTxt(
        string txtSourceFolder,
        string videoSourceFolder,
        string destGemeindeFolder,
        DistributionTargetConfig? directoryConfig,
        bool moveInsteadOfCopy = false,
        bool overwrite = false,
        bool recursiveVideoSearch = true,
        string unmatchedFolderName = "__UNMATCHED",
        AuswertungPro.Next.Domain.Models.Project? project = null,
        IProgress<DistributionProgress>? progress = null)
    {
        if (!Directory.Exists(txtSourceFolder))
        {
            return new[]
            {
                new DistributionResult(false, $"TXT folder not found: {txtSourceFolder}", txtSourceFolder, null, null, null, null, null, VideoMatchStatus.NotChecked)
            };
        }

        var txtFiles = Common.SafeFileEnumeration.EnumerateFilesSafe(txtSourceFolder, "kiDVDaten*.txt", recursive: true)
            .ToList();

        if (txtFiles.Count == 0)
        {
            return new[]
            {
                new DistributionResult(false, $"No TXT files found (recursive) in: {txtSourceFolder}", txtSourceFolder, null, null, null, null, null, VideoMatchStatus.NotChecked)
            };
        }

        return DistributeTxtCore(
            txtFiles: txtFiles,
            videoSourceFolder: videoSourceFolder,
            destGemeindeFolder: destGemeindeFolder,
            moveInsteadOfCopy: moveInsteadOfCopy,
            overwrite: overwrite,
            recursiveVideoSearch: recursiveVideoSearch,
            unmatchedFolderName: unmatchedFolderName,
            project: project,
            progress: progress,
            directoryConfig: directoryConfig);
    }

    public static IReadOnlyList<DistributionResult> DistributeTxtFiles(
        IEnumerable<string> txtFiles, string videoSourceFolder, string destGemeindeFolder,
        bool moveInsteadOfCopy = false, bool overwrite = false, bool recursiveVideoSearch = true, string unmatchedFolderName = "__UNMATCHED",
        AuswertungPro.Next.Domain.Models.Project? project = null, IProgress<DistributionProgress>? progress = null)
        => DistributeTxtFiles(txtFiles, videoSourceFolder, destGemeindeFolder, null, moveInsteadOfCopy, overwrite, recursiveVideoSearch, unmatchedFolderName, project, progress);

    public static IReadOnlyList<DistributionResult> DistributeTxtFiles(
        IEnumerable<string> txtFiles,
        string videoSourceFolder,
        string destGemeindeFolder,
        DistributionTargetConfig? directoryConfig,
        bool moveInsteadOfCopy = false,
        bool overwrite = false,
        bool recursiveVideoSearch = true,
        string unmatchedFolderName = "__UNMATCHED",
        AuswertungPro.Next.Domain.Models.Project? project = null,
        IProgress<DistributionProgress>? progress = null)
    {
        var validTxtFiles = txtFiles
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Where(File.Exists)
            .Where(p => string.Equals(Path.GetExtension(p), ".txt", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (validTxtFiles.Count == 0)
        {
            return new[]
            {
                new DistributionResult(false, "No valid TXT files selected.", "", null, null, null, null, null, VideoMatchStatus.NotChecked)
            };
        }

        return DistributeTxtCore(
            txtFiles: validTxtFiles,
            videoSourceFolder: videoSourceFolder,
            destGemeindeFolder: destGemeindeFolder,
            moveInsteadOfCopy: moveInsteadOfCopy,
            overwrite: overwrite,
            recursiveVideoSearch: recursiveVideoSearch,
            unmatchedFolderName: unmatchedFolderName,
            project: project,
            progress: progress,
            directoryConfig: directoryConfig);
    }

    private static IReadOnlyList<DistributionResult> DistributeTxtCore(
        IReadOnlyList<string> txtFiles,
        string videoSourceFolder,
        string destGemeindeFolder,
        bool moveInsteadOfCopy,
        bool overwrite,
        bool recursiveVideoSearch,
        string unmatchedFolderName,
        AuswertungPro.Next.Domain.Models.Project? project,
        IProgress<DistributionProgress>? progress,
        DistributionTargetConfig? directoryConfig)
    {
        var results = new List<DistributionResult>();

        IReadOnlyList<string>? videoFilesCache = null;
        if (!moveInsteadOfCopy)
            videoFilesCache = EnumerateVideoFiles(videoSourceFolder, recursiveVideoSearch);

        var sections = new List<KinsTxtSection>();
        foreach (var txtPath in txtFiles)
        {
            try
            {
                sections.AddRange(ParseTxtSections(txtPath));
            }
            catch (Exception ex)
            {
                results.Add(new DistributionResult(false, $"TXT parse failed: {ex.Message}", txtPath, null, null, null, null, null, VideoMatchStatus.NotChecked));
            }
        }

        var processed = 0;
        var total = sections.Count;
        foreach (var section in sections)
        {
            try
            {
                var haltungRaw = section.HoldingRaw;
                var haltungId = NormalizeHaltungId(haltungRaw);
                var haltung = SanitizePathSegment(haltungId);
                var date = section.Date;
                var dateStamp = date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

                var treeContext = new DistributionPatternContext(
                    Datum: date,
                    Gemeinde: DistributionDirectoryTreeController.GetMunicipality(project),
                    Haltung: haltung);
                var holdingFolder = DistributionDirectoryTreeController.ResolveObjectFolder(
                    destGemeindeFolder,
                    directoryConfig,
                    treeContext,
                    "{Haltung}");
                Directory.CreateDirectory(holdingFolder);

                var destTxtName = $"{dateStamp}_{haltung}.txt";
                var destTxtPath = DistributionFileTransfer.EnsureUniquePath(Path.Combine(holdingFolder, destTxtName), overwrite);
                AtomicTextFileWriter.WriteAllText(destTxtPath, section.SectionText);

                VideoFindResult videoFind = !string.IsNullOrWhiteSpace(section.VideoFileName)
                    ? FindVideo(section.VideoFileName, videoSourceFolder, haltung, dateStamp, recursiveVideoSearch, videoFilesCache)
                    : FindVideoByHaltungDate(videoSourceFolder, haltung, dateStamp, recursiveVideoSearch, videoFilesCache);

                if (videoFind.Status != VideoMatchStatus.Matched)
                {
                    var fromLink = TryFindVideoFromRecordLink(project, haltung, videoSourceFolder, dateStamp, recursiveVideoSearch, videoFilesCache);
                    if (fromLink.Status == VideoMatchStatus.Matched)
                        videoFind = fromLink;
                }

                string? destVideoPath = null;
                string? infoPath = null;

                if (videoFind.Status == VideoMatchStatus.Matched && videoFind.VideoPath is not null)
                {
                    var existingVid = FindExistingVideo(holdingFolder, videoFind.VideoPath);
                    if (existingVid is not null)
                    {
                        destVideoPath = existingVid;
                    }
                    else
                    {
                        var videoExt = Path.GetExtension(videoFind.VideoPath);
                        var destVideoName = $"{dateStamp}_{haltung}{videoExt}";
                        destVideoPath = DistributionFileTransfer.EnsureUniquePath(Path.Combine(holdingFolder, destVideoName), overwrite);
                        DistributionFileTransfer.MoveOrCopy(videoFind.VideoPath, destVideoPath, moveInsteadOfCopy, overwrite);
                    }

                    if (project != null && !string.IsNullOrWhiteSpace(destVideoPath))
                    {
                        var record = FindRecordByHolding(project, haltung);
                        if (record != null)
                        {
                            var meta = record.FieldMeta.TryGetValue("Link", out var m) ? m : null;
                            if (meta == null || !meta.UserEdited)
                            {
                                record.SetFieldValue(
                                    "Link",
                                    MakeProjectRelativeLink(destVideoPath, destGemeindeFolder),
                                    AuswertungPro.Next.Domain.Models.FieldSource.Unknown,
                                    userEdited: false);
                                project.ModifiedAtUtc = DateTime.UtcNow;
                                project.Dirty = true;
                            }
                        }
                    }
                }
                else if (videoFind.Status == VideoMatchStatus.NotFound)
                {
                    var infoName = $"{dateStamp}_{haltung}_VIDEO_MISSING.txt";
                    infoPath = DistributionFileTransfer.EnsureUniquePath(Path.Combine(holdingFolder, infoName), overwrite);
                    var filmName = string.IsNullOrWhiteSpace(section.VideoFileName) ? "<nicht gefunden>" : section.VideoFileName;
                    AtomicTextFileWriter.WriteAllText(infoPath, VideoConflictArtifacts.BuildMissingInfo(section.SourceTxtPath, filmName, date, haltungRaw));
                }
                else if (videoFind.Status == VideoMatchStatus.Ambiguous)
                {
                    var infoName = $"{dateStamp}_{haltung}_VIDEO_AMBIGUOUS.txt";
                    infoPath = DistributionFileTransfer.EnsureUniquePath(Path.Combine(holdingFolder, infoName), overwrite);
                    AtomicTextFileWriter.WriteAllText(infoPath, VideoConflictArtifacts.BuildAmbiguousInfo(section.SourceTxtPath, section.VideoFileName, date, haltungRaw, videoFind.Candidates));
                    var holdingParent = Directory.GetParent(holdingFolder)?.FullName ?? destGemeindeFolder;
                    var safeUnmatchedFolderName = ProjectPathResolver.SanitizePathSegment(unmatchedFolderName);
                    var unmatchedFolder = Path.Combine(holdingParent, safeUnmatchedFolderName, haltung);
                    Directory.CreateDirectory(unmatchedFolder);
                    VideoConflictArtifacts.CopyCandidates(unmatchedFolder, dateStamp, haltung, videoFind.Candidates);
                }

                var message = videoFind.Status switch
                {
                    VideoMatchStatus.Matched => "OK (TXT+Video)",
                    VideoMatchStatus.Ambiguous => "Video ambiguous",
                    VideoMatchStatus.NotFound => "Video missing",
                    _ => "OK (TXT)"
                };

                results.Add(new DistributionResult(
                    true,
                    message,
                    section.SourceTxtPath,
                    videoFind.VideoPath,
                    destTxtPath,
                    destVideoPath,
                    infoPath,
                    holdingFolder,
                    videoFind.Status));
            }
            catch (Exception ex)
            {
                results.Add(new DistributionResult(
                    false,
                    ex.Message,
                    section.SourceTxtPath,
                    null,
                    null,
                    null,
                    null,
                    null,
                    VideoMatchStatus.NotChecked));
            }
            finally
            {
                processed++;
                progress?.Report(new DistributionProgress(processed, total, section.SourceTxtPath));
            }
        }

        return results;
    }

    private static IReadOnlyList<DistributionResult> DistributeCore(
        IReadOnlyList<string> pdfFiles,
        string videoSourceFolder,
        string destGemeindeFolder,
        bool moveInsteadOfCopy,
        bool overwrite,
        bool recursiveVideoSearch,
        string unmatchedFolderName,
        AuswertungPro.Next.Domain.Models.Project? project,
        IProgress<DistributionProgress>? progress,
        string? xtfSourceFolder = null,
        DistributionTargetConfig? directoryConfig = null)
    {
        var results = new List<DistributionResult>();

        // PERF: Enumerating all videos repeatedly is expensive for large folders.
        // Safety: only cache when copying (move would mutate the source folder during processing).
        IReadOnlyList<string>? videoFilesCache = null;
        if (!moveInsteadOfCopy)
            videoFilesCache = EnumerateVideoFiles(videoSourceFolder, recursiveVideoSearch);

        // Optional fallback index from M150/MDB sidecars (holding -> video link hints).
        // Used only when standard matching cannot resolve a video.
        var sidecarVideoLinksByHolding = BuildSidecarVideoLinkIndex(xtfSourceFolder, pdfFiles);
        var sidecarHoldingsByVideoLink = BuildSidecarHoldingByVideoIndex(sidecarVideoLinksByHolding);
        var cdIndexVideoLinksByPhoto = BuildCdIndexVideoLinkIndex(xtfSourceFolder, pdfFiles);

        // Index: Haltung (normalisiert) -> Zielordner-Pfad
        // Wird beim Verteilen gefuellt, damit nicht-parsbare PDFs per Dateiname zugeordnet werden koennen.
        var distributedHoldings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var unmatchedPdfs = new List<string>();

        var processed = 0;
        foreach (var pdfPath in pdfFiles)
        {
            try
            {
                var pages = HoldingDistribution.DistributionPdfAssignmentController.ReadPages(pdfPath);
                var chunks = SplitPdfIntoHoldings(pages);

                if (chunks.Count == 0)
                {
                    var parsed = ParsePdfWithOcrFallback(pages);
                    if (!parsed.Success)
                    {
                        // PDF konnte nicht geparst werden (z.B. Dichtheitspruefungsprotokoll).
                        // Spaeter per Dateiname-Muster dem passenden Haltungsordner zuordnen.
                        unmatchedPdfs.Add(pdfPath);
                        continue;
                    }

                    var result = ParsedHoldingDistributionController.Distribute(parsed, pdfPath, pdfPath, videoSourceFolder, destGemeindeFolder, moveInsteadOfCopy, overwrite, recursiveVideoSearch, unmatchedFolderName, null, project, videoFilesCache, sidecarVideoLinksByHolding, sidecarHoldingsByVideoLink, cdIndexVideoLinksByPhoto, directoryConfig);
                    results.Add(result);
                    if (result.Success && result.HoldingFolder is not null && parsed.Haltung is not null)
                        distributedHoldings[NormalizeHaltungId(parsed.Haltung)] = result.HoldingFolder;
                    continue;
                }

                if (chunks.Count == 1 && pages.Count == chunks[0].Pages.Count)
                {
                    var result = ParsedHoldingDistributionController.Distribute(chunks[0].Parsed, pdfPath, pdfPath, videoSourceFolder, destGemeindeFolder, moveInsteadOfCopy, overwrite, recursiveVideoSearch, unmatchedFolderName, null, project, videoFilesCache, sidecarVideoLinksByHolding, sidecarHoldingsByVideoLink, cdIndexVideoLinksByPhoto, directoryConfig);
                    results.Add(result);
                    if (result.Success && result.HoldingFolder is not null && chunks[0].Parsed.Haltung is not null)
                        distributedHoldings[NormalizeHaltungId(chunks[0].Parsed.Haltung)] = result.HoldingFolder;
                    continue;
                }

                foreach (var chunk in chunks)
                {
                    if (!chunk.Parsed.Success)
                    {
                        results.Add(new DistributionResult(false, "Parse failed for chunk", pdfPath, null, null, null, null, null, VideoMatchStatus.NotChecked));
                        continue;
                    }

                    var pageRange = BuildPageRange(chunk.Pages);
                    var tempPdfPath = Path.Combine(Path.GetTempPath(), $"split_{Guid.NewGuid():N}.pdf");
                    try
                    {
                        WritePdfPages(pdfPath, chunk.Pages, tempPdfPath);
                        var result = ParsedHoldingDistributionController.Distribute(chunk.Parsed, pdfPath, tempPdfPath, videoSourceFolder, destGemeindeFolder, moveInsteadOfCopy: false, overwrite, recursiveVideoSearch, unmatchedFolderName, pageRange, project, videoFilesCache, sidecarVideoLinksByHolding, sidecarHoldingsByVideoLink, cdIndexVideoLinksByPhoto, directoryConfig);
                        results.Add(result);
                        if (result.Success && result.HoldingFolder is not null && chunk.Parsed.Haltung is not null)
                            distributedHoldings[NormalizeHaltungId(chunk.Parsed.Haltung)] = result.HoldingFolder;
                    }
                    finally
                    {
                        AuswertungPro.Next.Application.Common.BestEffort.Try(() => { if (File.Exists(tempPdfPath)) File.Delete(tempPdfPath); }, "PDF-Verteilung: Temp loeschen");
                    }
                }
            }
            catch (Exception ex)
            {
                results.Add(new DistributionResult(false, ex.Message, pdfPath, null, null, null, null, null, VideoMatchStatus.NotChecked));
            }
            finally
            {
                processed++;
                progress?.Report(new DistributionProgress(processed, pdfFiles.Count, pdfPath));
            }
        }

        // Nicht-parsbare PDFs (z.B. Dichtheitspruefungsprotokolle) per Dateiname-Muster
        // dem passenden Haltungsordner zuordnen und mit Originalnamen kopieren.
        foreach (var pdfPath in unmatchedPdfs)
        {
            var holdingFolder = HoldingDistribution.DistributionPdfAssignmentController.MatchPdfToHolding(
                pdfPath,
                distributedHoldings);
            if (holdingFolder is not null)
            {
                try
                {
                    var destPath = DistributionFileTransfer.EnsureUniquePath(
                        Path.Combine(holdingFolder, Path.GetFileName(pdfPath)), overwrite);
                    DistributionFileTransfer.MoveOrCopy(pdfPath, destPath, moveInsteadOfCopy, overwrite);
                    results.Add(new DistributionResult(true, "OK (Begleit-PDF)", pdfPath, null, destPath, null, null, holdingFolder, VideoMatchStatus.NotChecked));
                }
                catch (Exception ex)
                {
                    results.Add(new DistributionResult(false, $"Begleit-PDF Fehler: {ex.Message}", pdfPath, null, null, null, null, null, VideoMatchStatus.NotChecked));
                }
            }
            else
            {
                results.Add(new DistributionResult(false, "Parse failed, kein passender Haltungsordner gefunden", pdfPath, null, null, null, null, null, VideoMatchStatus.NotChecked));
            }
        }

        // XTF distribution: copy *.xtf files from source folder to destination Gemeinde folder
        if (!string.IsNullOrWhiteSpace(xtfSourceFolder) && Directory.Exists(xtfSourceFolder))
        {
            try
            {
                var sidecarFiles = EnumerateSidecarFiles(xtfSourceFolder);

                foreach (var sidecarPath in sidecarFiles.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    try
                    {
                        var destSidecarPath = DistributionFileTransfer.EnsureUniquePath(Path.Combine(destGemeindeFolder, Path.GetFileName(sidecarPath)), overwrite);
                        DistributionFileTransfer.MoveOrCopy(sidecarPath, destSidecarPath, moveInsteadOfCopy, overwrite);
                        results.Add(new DistributionResult(true, $"Quelldatei kopiert: {Path.GetFileName(sidecarPath)}", sidecarPath, null, destSidecarPath, null, null, destGemeindeFolder, VideoMatchStatus.NotChecked));
                    }
                    catch (Exception ex)
                    {
                        results.Add(new DistributionResult(false, $"Quelldatei Fehler: {ex.Message}", sidecarPath, null, null, null, null, null, VideoMatchStatus.NotChecked));
                    }
                }
            }
            catch { /* XTF enumeration failed – non-critical */ }
        }

        return results;
    }

    public static IReadOnlyList<DistributionResult> DistributeShafts(
        string pdfSourceFolder, string destGemeindeFolder, bool moveInsteadOfCopy = false, bool overwrite = false,
        Project? project = null, IProgress<DistributionProgress>? progress = null)
        => DistributeShafts(pdfSourceFolder, destGemeindeFolder, null, moveInsteadOfCopy, overwrite, project, progress);

    public static IReadOnlyList<DistributionResult> DistributeShafts(
        string pdfSourceFolder,
        string destGemeindeFolder,
        DistributionTargetConfig? directoryConfig,
        bool moveInsteadOfCopy = false,
        bool overwrite = false,
        Project? project = null,
        IProgress<DistributionProgress>? progress = null)
    {
        if (!Directory.Exists(pdfSourceFolder))
            return new[] { new DistributionResult(false, $"PDF folder not found: {pdfSourceFolder}", pdfSourceFolder, null, null, null, null, null, VideoMatchStatus.NotChecked) };

        var pdfFiles = Common.SafeFileEnumeration.EnumerateFilesSafe(pdfSourceFolder, "*.pdf", recursive: true)
            .Where(p => !Path.GetFileName(p).StartsWith("split_", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (pdfFiles.Count == 0)
            return new[] { new DistributionResult(false, $"No PDF files found (recursive) in: {pdfSourceFolder}", pdfSourceFolder, null, null, null, null, null, VideoMatchStatus.NotChecked) };

        return DistributeShaftCore(pdfFiles, destGemeindeFolder, moveInsteadOfCopy, overwrite, project, progress, directoryConfig);
    }

    public static IReadOnlyList<DistributionResult> DistributeShaftFiles(
        IEnumerable<string> pdfFiles, string destGemeindeFolder, bool moveInsteadOfCopy = false, bool overwrite = false,
        Project? project = null, IProgress<DistributionProgress>? progress = null)
        => DistributeShaftFiles(pdfFiles, destGemeindeFolder, null, moveInsteadOfCopy, overwrite, project, progress);

    public static IReadOnlyList<DistributionResult> DistributeShaftFiles(
        IEnumerable<string> pdfFiles,
        string destGemeindeFolder,
        DistributionTargetConfig? directoryConfig,
        bool moveInsteadOfCopy = false,
        bool overwrite = false,
        Project? project = null,
        IProgress<DistributionProgress>? progress = null)
    {
        var validPdfFiles = pdfFiles
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Where(File.Exists)
            .Where(p => string.Equals(Path.GetExtension(p), ".pdf", StringComparison.OrdinalIgnoreCase))
            .Where(p => !Path.GetFileName(p).StartsWith("split_", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        validPdfFiles = ShaftPdfSelectionExpander.Expand(validPdfFiles);

        if (validPdfFiles.Count == 0)
            return new[] { new DistributionResult(false, "No valid PDF files selected.", "", null, null, null, null, null, VideoMatchStatus.NotChecked) };

        return DistributeShaftCore(validPdfFiles, destGemeindeFolder, moveInsteadOfCopy, overwrite, project, progress, directoryConfig);
    }

    private static IReadOnlyList<DistributionResult> DistributeShaftCore(
        IReadOnlyList<string> pdfFiles,
        string destGemeindeFolder,
        bool moveInsteadOfCopy,
        bool overwrite,
        Project? project,
        IProgress<DistributionProgress>? progress,
        DistributionTargetConfig? directoryConfig)
    {
        var results = new List<DistributionResult>();
        var shaftOutputPathByKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var processed = 0;
        foreach (var pdfPath in pdfFiles)
        {
            try
            {
                var pages = HoldingDistribution.DistributionPdfAssignmentController.ReadPages(pdfPath);
                var chunks = SplitPdfIntoShafts(pages);

                if (chunks.Count == 0)
                {
                    var pdfText = string.Join("\n\n", pages.Select(p => p.Text));
                    var parsed = ParseSchachtPdf(pdfText);
                    if (!parsed.Success)
                    {
                        results.Add(new DistributionResult(false, $"Parse failed: {parsed.Message}", pdfPath, null, null, null, null, null, VideoMatchStatus.NotChecked));
                        continue;
                    }

                    results.Add(ParsedShaftDistributionController.Distribute(parsed, pdfPath, pdfPath, destGemeindeFolder, moveInsteadOfCopy, overwrite, null, shaftOutputPathByKey, project, directoryConfig));
                    continue;
                }

                if (chunks.Count == 1 && pages.Count == chunks[0].Pages.Count)
                {
                    results.Add(ParsedShaftDistributionController.Distribute(chunks[0].Parsed, pdfPath, pdfPath, destGemeindeFolder, moveInsteadOfCopy, overwrite, null, shaftOutputPathByKey, project, directoryConfig));
                    continue;
                }

                foreach (var chunk in chunks)
                {
                    if (!chunk.Parsed.Success)
                    {
                        results.Add(new DistributionResult(false, "Parse failed for chunk", pdfPath, null, null, null, null, null, VideoMatchStatus.NotChecked));
                        continue;
                    }

                    var pageRange = BuildPageRange(chunk.Pages);
                    var tempPdfPath = Path.Combine(Path.GetTempPath(), $"split_{Guid.NewGuid():N}.pdf");
                    try
                    {
                        WritePdfPages(pdfPath, chunk.Pages, tempPdfPath);
                        results.Add(ParsedShaftDistributionController.Distribute(chunk.Parsed, pdfPath, tempPdfPath, destGemeindeFolder, moveInsteadOfCopy: false, overwrite, pageRange, shaftOutputPathByKey, project, directoryConfig));
                    }
                    finally
                    {
                        AuswertungPro.Next.Application.Common.BestEffort.Try(() => { if (File.Exists(tempPdfPath)) File.Delete(tempPdfPath); }, "PDF-Verteilung: Temp loeschen");
                    }
                }
            }
            catch (Exception ex)
            {
                results.Add(new DistributionResult(false, ex.Message, pdfPath, null, null, null, null, null, VideoMatchStatus.NotChecked));
            }
            finally
            {
                processed++;
                progress?.Report(new DistributionProgress(processed, pdfFiles.Count, pdfPath));
            }
        }

        return results;
    }

    // --- Dichtheitspruefungsprotokoll Distribution ---

    /// <summary>
    /// Verteilt Dichtheitspruefungsprotokolle (DP) aus einem Quellordner in die
    /// Haltungsordner-Struktur. Liest "oberer Schacht" / "unterer Schacht" aus dem PDF
    /// um die Haltung zu ermitteln.
    /// </summary>
    public static IReadOnlyList<DistributionResult> DistributeDichtheit(
        string pdfSourceFolder, string destGemeindeFolder, bool moveInsteadOfCopy = false, bool overwrite = false,
        Project? project = null, IProgress<DistributionProgress>? progress = null, IHaltungCadastreResolver? cadastre = null)
        => DistributeDichtheit(pdfSourceFolder, destGemeindeFolder, null, moveInsteadOfCopy, overwrite, project, progress, cadastre);

    public static IReadOnlyList<DistributionResult> DistributeDichtheit(
        string pdfSourceFolder,
        string destGemeindeFolder,
        DistributionTargetConfig? directoryConfig,
        bool moveInsteadOfCopy = false,
        bool overwrite = false,
        Project? project = null,
        IProgress<DistributionProgress>? progress = null,
        IHaltungCadastreResolver? cadastre = null)
    {
        if (!Directory.Exists(pdfSourceFolder))
            return new[] { new DistributionResult(false, $"PDF folder not found: {pdfSourceFolder}", pdfSourceFolder, null, null, null, null, null, VideoMatchStatus.NotChecked) };

        var pdfFiles = Common.SafeFileEnumeration.EnumerateFilesSafe(pdfSourceFolder, "*.pdf", recursive: true)
            .Where(p => !Path.GetFileName(p).StartsWith("split_", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (pdfFiles.Count == 0)
            return new[] { new DistributionResult(false, $"No PDF files found in: {pdfSourceFolder}", pdfSourceFolder, null, null, null, null, null, VideoMatchStatus.NotChecked) };

        return DistributeDichtheitCore(pdfFiles, destGemeindeFolder, moveInsteadOfCopy, overwrite, project, progress, cadastre, directoryConfig);
    }

    /// <summary>
    /// Verteilt ausgewaehlte Dichtheitspruefungsprotokolle (DP) in die
    /// Haltungsordner-Struktur.
    /// </summary>
    public static IReadOnlyList<DistributionResult> DistributeDichtheitFiles(
        IEnumerable<string> pdfFiles, string destGemeindeFolder, bool moveInsteadOfCopy = false, bool overwrite = false,
        Project? project = null, IProgress<DistributionProgress>? progress = null, IHaltungCadastreResolver? cadastre = null)
        => DistributeDichtheitFiles(pdfFiles, destGemeindeFolder, null, moveInsteadOfCopy, overwrite, project, progress, cadastre);

    public static IReadOnlyList<DistributionResult> DistributeDichtheitFiles(
        IEnumerable<string> pdfFiles,
        string destGemeindeFolder,
        DistributionTargetConfig? directoryConfig,
        bool moveInsteadOfCopy = false,
        bool overwrite = false,
        Project? project = null,
        IProgress<DistributionProgress>? progress = null,
        IHaltungCadastreResolver? cadastre = null)
    {
        var validPdfFiles = pdfFiles
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Where(File.Exists)
            .Where(p => string.Equals(Path.GetExtension(p), ".pdf", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (validPdfFiles.Count == 0)
            return new[] { new DistributionResult(false, "No valid PDF files selected.", "", null, null, null, null, null, VideoMatchStatus.NotChecked) };

        return DistributeDichtheitCore(validPdfFiles, destGemeindeFolder, moveInsteadOfCopy, overwrite, project, progress, cadastre, directoryConfig);
    }

    private static IReadOnlyList<DistributionResult> DistributeDichtheitCore(
        IReadOnlyList<string> pdfFiles,
        string destGemeindeFolder,
        bool moveInsteadOfCopy,
        bool overwrite,
        Project? project,
        IProgress<DistributionProgress>? progress,
        IHaltungCadastreResolver? cadastre = null,
        DistributionTargetConfig? directoryConfig = null)
    {
        var results = new List<DistributionResult>();
        var processed = 0;

        foreach (var pdfPath in pdfFiles)
        {
            try
            {
                var pages = HoldingDistribution.DistributionPdfAssignmentController.ReadPages(pdfPath);

                // Multi-Seiten-Erkennung: Jede Seite einzeln auf Haltungspaar pruefen.
                // KIT Bauinspekt PDFs haben pro Seite eine andere Haltung/Schacht.
                // Kontrollinformations-Seiten (Messdaten) gehoeren zur vorherigen Pruefseite.
                var pageResults = HoldingDistribution.DistributionPdfAssignmentController.ExtractDichtheitPerPage(
                    pages,
                    project,
                    destGemeindeFolder,
                    cadastre);

                // Multi-Split nur wenn VERSCHIEDENE Haltungen erkannt wurden.
                // PDFs mit mehreren Seiten aber gleicher Haltung (z.B. Pruefbericht + Anhang)
                // werden als Ganzes behandelt.
                var distinctHaltungen = pageResults
                    .Where(pr => !string.IsNullOrWhiteSpace(pr.HaltungId))
                    .Select(pr => pr.HaltungId!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();

                if (distinctHaltungen > 1)
                {
                    // Multi-Haltungs-PDF: seitenweise splitten und verteilen
                    foreach (var pr in pageResults)
                    {
                        if (string.IsNullOrWhiteSpace(pr.HaltungId))
                        {
                            results.Add(new DistributionResult(false,
                                $"Seite {pr.MainPage}: Haltung nicht erkannt",
                                pdfPath, null, null, null, null, null, VideoMatchStatus.NotChecked));
                            continue;
                        }

                        var haltung = SanitizePathSegment(NormalizeHaltungId(pr.HaltungId));
                        // Nicht im Kataster bekannte Haltungen in den Sammelordner "keine_Zuordnung"
                        // umlenken (reguläre Ablage-Logik, nur eine Ebene tiefer).
                        var destRoot = HoldingDistribution.DistributionPdfAssignmentController.ResolveDistributionRoot(
                            destGemeindeFolder,
                            pr.HaltungId,
                            cadastre);
                        var hasTreeDate = DateTime.TryParseExact(
                            pr.DateStamp,
                            "yyyyMMdd",
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.None,
                            out var treeDate);
                        var treeContext = new DistributionPatternContext(
                            Datum: hasTreeDate ? treeDate : null,
                            Gemeinde: DistributionDirectoryTreeController.GetMunicipality(project),
                            Haltung: haltung);
                        var holdingFolder = DistributionDirectoryTreeController.ResolveObjectFolder(
                            destRoot,
                            directoryConfig,
                            treeContext,
                            "{Haltung}");
                        Directory.CreateDirectory(holdingFolder);

                        var suffix = pr.IsSchacht ? "SP" : "DP";
                        var destPdfName = $"{pr.DateStamp}_{haltung}_{suffix}.pdf";
                        var destPath = DistributionFileTransfer.EnsureUniquePath(
                            Path.Combine(holdingFolder, destPdfName), overwrite);

                        // Einzelseite(n) als neues PDF schreiben
                        WritePdfPages(pdfPath, pr.PageNumbers, destPath);

                        results.Add(new DistributionResult(true,
                            $"OK -> {haltung} (S{pr.MainPage}, {pr.PageNumbers.Count} Seite(n))",
                            pdfPath, null, destPath, null, null, holdingFolder, VideoMatchStatus.NotChecked));
                    }
                }
                else
                {
                    // Single-Haltung oder Fallback: gesamtes PDF einer Haltung zuordnen
                    var pdfText = string.Join("\n\n", pages.Select(p => p.Text));
                    string? haltungId = pageResults.Count == 1 ? pageResults[0].HaltungId : null;

                    // Bestehende Fallback-Kette wenn seitenweise Extraktion nichts ergab
                    if (string.IsNullOrWhiteSpace(haltungId))
                    {
                        var (shaftA, shaftB) = DichtheitShaftParser.TryExtractShafts(pdfText);
                        if (!string.IsNullOrWhiteSpace(shaftA) && !string.IsNullOrWhiteSpace(shaftB))
                            haltungId = HoldingDistribution.DistributionPdfAssignmentController.ResolveHoldingOrder(
                                shaftA,
                                shaftB,
                                project,
                                destGemeindeFolder);
                    }
                    if (string.IsNullOrWhiteSpace(haltungId))
                    {
                        var parsed = ParsePdfWithOcrFallback(pages);
                        if (parsed.Success && !string.IsNullOrWhiteSpace(parsed.Haltung))
                            haltungId = parsed.Haltung;
                    }
                    if (string.IsNullOrWhiteSpace(haltungId))
                        haltungId = TryExtractFromShafts(pdfText);

                    // Letzter Rettungsanker: amtlicher Kataster-Abgleich (universell, formatunabhaengig).
                    if (string.IsNullOrWhiteSpace(haltungId) && cadastre is not null)
                        haltungId = HoldingDistribution.DistributionPdfAssignmentController.ResolveViaCadastre(
                            pdfText,
                            cadastre);

                    if (string.IsNullOrWhiteSpace(haltungId))
                    {
                        results.Add(new DistributionResult(false,
                            "Haltung nicht erkannt (oberer/unterer Schacht nicht gefunden)",
                            pdfPath, null, null, null, null, null, VideoMatchStatus.NotChecked));
                        continue;
                    }

                    var date = TryFindInspectionDate(pdfText);
                    if (date is null
                        && directoryConfig is not null
                        && pageResults.Count > 0
                        && DateTime.TryParseExact(
                            pageResults[0].DateStamp,
                            "yyyyMMdd",
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.None,
                            out var pageDate))
                    {
                        date = pageDate;
                    }
                    var dateStamp = date?.ToString("yyyyMMdd", CultureInfo.InvariantCulture) ?? "00000000";

                    var haltung = SanitizePathSegment(NormalizeHaltungId(haltungId));
                    // Nicht im Kataster bekannte Haltungen in den Sammelordner "keine_Zuordnung"
                    // umlenken (reguläre Ablage-Logik, nur eine Ebene tiefer).
                    var destRoot = HoldingDistribution.DistributionPdfAssignmentController.ResolveDistributionRoot(
                        destGemeindeFolder,
                        haltungId,
                        cadastre);
                    var treeContext = new DistributionPatternContext(
                        Datum: date,
                        Gemeinde: DistributionDirectoryTreeController.GetMunicipality(project),
                        Haltung: haltung);
                    var holdingFolder = DistributionDirectoryTreeController.ResolveObjectFolder(
                        destRoot,
                        directoryConfig,
                        treeContext,
                        "{Haltung}");
                    Directory.CreateDirectory(holdingFolder);

                    var destPdfName = $"{dateStamp}_{haltung}_DP.pdf";
                    var destPath = DistributionFileTransfer.EnsureUniquePath(
                        Path.Combine(holdingFolder, destPdfName), overwrite);
                    DistributionFileTransfer.MoveOrCopy(pdfPath, destPath, moveInsteadOfCopy, overwrite);

                    results.Add(new DistributionResult(true, $"OK -> {haltung}",
                        pdfPath, null, destPath, null, null, holdingFolder, VideoMatchStatus.NotChecked));
                }
            }
            catch (Exception ex)
            {
                results.Add(new DistributionResult(false, ex.Message, pdfPath, null, null, null, null, null, VideoMatchStatus.NotChecked));
            }
            finally
            {
                processed++;
                progress?.Report(new DistributionProgress(processed, pdfFiles.Count, pdfPath));
            }
        }

        return results;
    }
}
