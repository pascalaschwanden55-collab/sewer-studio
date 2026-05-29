using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Media;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
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
        string pdfSourceFolder,
        string videoSourceFolder,
        string destGemeindeFolder,
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

        var pdfFiles = Directory.EnumerateFiles(pdfSourceFolder, "*.pdf", SearchOption.AllDirectories)
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
            xtfSourceFolder: xtfSourceFolder ?? pdfSourceFolder);
    }

    public static IReadOnlyList<DistributionResult> DistributeFiles(
        IEnumerable<string> pdfFiles,
        string videoSourceFolder,
        string destGemeindeFolder,
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
            xtfSourceFolder: derivedXtfFolder);
    }

    public static IReadOnlyList<DistributionResult> DistributeTxt(
        string txtSourceFolder,
        string videoSourceFolder,
        string destGemeindeFolder,
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

        var txtFiles = Directory.EnumerateFiles(txtSourceFolder, "kiDVDaten*.txt", SearchOption.AllDirectories)
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
            progress: progress);
    }

    public static IReadOnlyList<DistributionResult> DistributeTxtFiles(
        IEnumerable<string> txtFiles,
        string videoSourceFolder,
        string destGemeindeFolder,
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
            progress: progress);
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
        IProgress<DistributionProgress>? progress)
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

                var holdingFolder = Path.Combine(destGemeindeFolder, haltung);
                Directory.CreateDirectory(holdingFolder);

                var destTxtName = $"{dateStamp}_{haltung}.txt";
                var destTxtPath = EnsureUniquePath(Path.Combine(holdingFolder, destTxtName), overwrite);
                File.WriteAllText(destTxtPath, section.SectionText);

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
                        destVideoPath = EnsureUniquePath(Path.Combine(holdingFolder, destVideoName), overwrite);
                        MoveOrCopy(videoFind.VideoPath, destVideoPath, moveInsteadOfCopy);
                    }

                    if (project != null && !string.IsNullOrWhiteSpace(destVideoPath))
                    {
                        var record = FindRecordByHolding(project, haltung);
                        if (record != null)
                        {
                            var meta = record.FieldMeta.TryGetValue("Link", out var m) ? m : null;
                            if (meta == null || !meta.UserEdited)
                            {
                                record.SetFieldValue("Link", destVideoPath, AuswertungPro.Next.Domain.Models.FieldSource.Unknown, userEdited: false);
                                project.ModifiedAtUtc = DateTime.UtcNow;
                                project.Dirty = true;
                            }
                        }
                    }
                }
                else if (videoFind.Status == VideoMatchStatus.NotFound)
                {
                    var infoName = $"{dateStamp}_{haltung}_VIDEO_MISSING.txt";
                    infoPath = EnsureUniquePath(Path.Combine(holdingFolder, infoName), overwrite);
                    var filmName = string.IsNullOrWhiteSpace(section.VideoFileName) ? "<nicht gefunden>" : section.VideoFileName;
                    File.WriteAllText(infoPath, BuildMissingInfo(section.SourceTxtPath, filmName, date, haltungRaw));
                }
                else if (videoFind.Status == VideoMatchStatus.Ambiguous)
                {
                    var infoName = $"{dateStamp}_{haltung}_VIDEO_AMBIGUOUS.txt";
                    infoPath = EnsureUniquePath(Path.Combine(holdingFolder, infoName), overwrite);
                    File.WriteAllText(infoPath, BuildAmbiguousInfo(section.SourceTxtPath, section.VideoFileName, date, haltungRaw, videoFind.Candidates));
                    var unmatchedFolder = Path.Combine(destGemeindeFolder, unmatchedFolderName, haltung);
                    Directory.CreateDirectory(unmatchedFolder);
                    CopyCandidatesToUnmatched(unmatchedFolder, dateStamp, haltung, videoFind.Candidates);
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
        string? xtfSourceFolder = null)
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
                var pages = ReadPdfPages(pdfPath);
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

                    var result = HandleParsedDistribution(parsed, pdfPath, pdfPath, videoSourceFolder, destGemeindeFolder, moveInsteadOfCopy, overwrite, recursiveVideoSearch, unmatchedFolderName, null, project, videoFilesCache, sidecarVideoLinksByHolding, sidecarHoldingsByVideoLink, cdIndexVideoLinksByPhoto);
                    results.Add(result);
                    if (result.Success && result.HoldingFolder is not null && parsed.Haltung is not null)
                        distributedHoldings[NormalizeHaltungId(parsed.Haltung)] = result.HoldingFolder;
                    continue;
                }

                if (chunks.Count == 1 && pages.Count == chunks[0].Pages.Count)
                {
                    var result = HandleParsedDistribution(chunks[0].Parsed, pdfPath, pdfPath, videoSourceFolder, destGemeindeFolder, moveInsteadOfCopy, overwrite, recursiveVideoSearch, unmatchedFolderName, null, project, videoFilesCache, sidecarVideoLinksByHolding, sidecarHoldingsByVideoLink, cdIndexVideoLinksByPhoto);
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
                        var result = HandleParsedDistribution(chunk.Parsed, pdfPath, tempPdfPath, videoSourceFolder, destGemeindeFolder, moveInsteadOfCopy: false, overwrite, recursiveVideoSearch, unmatchedFolderName, pageRange, project, videoFilesCache, sidecarVideoLinksByHolding, sidecarHoldingsByVideoLink, cdIndexVideoLinksByPhoto);
                        results.Add(result);
                        if (result.Success && result.HoldingFolder is not null && chunk.Parsed.Haltung is not null)
                            distributedHoldings[NormalizeHaltungId(chunk.Parsed.Haltung)] = result.HoldingFolder;
                    }
                    finally
                    {
                        try { if (File.Exists(tempPdfPath)) File.Delete(tempPdfPath); } catch { }
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
            var holdingFolder = TryMatchPdfToHolding(pdfPath, distributedHoldings);
            if (holdingFolder is not null)
            {
                try
                {
                    var destPath = EnsureUniquePath(
                        Path.Combine(holdingFolder, Path.GetFileName(pdfPath)), overwrite);
                    MoveOrCopy(pdfPath, destPath, moveInsteadOfCopy);
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
                        var destSidecarPath = EnsureUniquePath(Path.Combine(destGemeindeFolder, Path.GetFileName(sidecarPath)), overwrite);
                        MoveOrCopy(sidecarPath, destSidecarPath, moveInsteadOfCopy);
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

    private static void CopyCandidatesToUnmatched(string unmatchedFolder, string dateStamp, string haltung, IReadOnlyList<string> candidates)
    {
        for (var i = 0; i < candidates.Count; i++)
        {
            var src = candidates[i];
            var ext = Path.GetExtension(src);
            var name = $"{dateStamp}_{haltung}_CANDIDATE_{(i + 1).ToString("00", CultureInfo.InvariantCulture)}{ext}";
            var dest = EnsureUniquePath(Path.Combine(unmatchedFolder, name), overwrite: false);
            File.Copy(src, dest, overwrite: false);
        }
    }

    private static string BuildMissingInfo(string pdfPath, string videoName, DateTime date, string haltung)
    {
        var sb = new StringBuilder();
        sb.AppendLine("VIDEO MISSING");
        sb.AppendLine($"PDF: {pdfPath}");
        sb.AppendLine($"Film: {videoName}");
        sb.AppendLine($"Datum: {date:dd.MM.yyyy}");
        sb.AppendLine($"Haltung: {haltung}");
        return sb.ToString();
    }

    private static string BuildAmbiguousInfo(string pdfPath, string videoName, DateTime date, string haltung, IReadOnlyList<string> candidates)
    {
        var sb = new StringBuilder();
        sb.AppendLine("VIDEO AMBIGUOUS");
        sb.AppendLine($"PDF: {pdfPath}");
        sb.AppendLine($"Film: {videoName}");
        sb.AppendLine($"Datum: {date:dd.MM.yyyy}");
        sb.AppendLine($"Haltung: {haltung}");
        sb.AppendLine("Candidates:");
        foreach (var c in candidates)
            sb.AppendLine($"- {c}");
        return sb.ToString();
    }

    private static void MoveOrCopy(string source, string dest, bool move)
    {
        if (move)
        {
            File.Move(source, dest);
        }
        else
        {
            File.Copy(source, dest, overwrite: false);
        }
    }
    public static IReadOnlyList<DistributionResult> DistributeShafts(
        string pdfSourceFolder,
        string destGemeindeFolder,
        bool moveInsteadOfCopy = false,
        bool overwrite = false,
        Project? project = null,
        IProgress<DistributionProgress>? progress = null)
    {
        if (!Directory.Exists(pdfSourceFolder))
            return new[] { new DistributionResult(false, $"PDF folder not found: {pdfSourceFolder}", pdfSourceFolder, null, null, null, null, null, VideoMatchStatus.NotChecked) };

        var pdfFiles = Directory.EnumerateFiles(pdfSourceFolder, "*.pdf", SearchOption.AllDirectories)
            .Where(p => !Path.GetFileName(p).StartsWith("split_", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (pdfFiles.Count == 0)
            return new[] { new DistributionResult(false, $"No PDF files found (recursive) in: {pdfSourceFolder}", pdfSourceFolder, null, null, null, null, null, VideoMatchStatus.NotChecked) };

        return DistributeShaftCore(pdfFiles, destGemeindeFolder, moveInsteadOfCopy, overwrite, project, progress);
    }

    public static IReadOnlyList<DistributionResult> DistributeShaftFiles(
        IEnumerable<string> pdfFiles,
        string destGemeindeFolder,
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

        validPdfFiles = ExpandSelectedShaftPdfFiles(validPdfFiles);

        if (validPdfFiles.Count == 0)
            return new[] { new DistributionResult(false, "No valid PDF files selected.", "", null, null, null, null, null, VideoMatchStatus.NotChecked) };

        return DistributeShaftCore(validPdfFiles, destGemeindeFolder, moveInsteadOfCopy, overwrite, project, progress);
    }

    private static IReadOnlyList<DistributionResult> DistributeShaftCore(
        IReadOnlyList<string> pdfFiles,
        string destGemeindeFolder,
        bool moveInsteadOfCopy,
        bool overwrite,
        Project? project,
        IProgress<DistributionProgress>? progress)
    {
        var results = new List<DistributionResult>();
        var shaftOutputPathByKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var processed = 0;
        foreach (var pdfPath in pdfFiles)
        {
            try
            {
                var pages = ReadPdfPages(pdfPath);
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

                    results.Add(HandleParsedShaftDistribution(parsed, pdfPath, pdfPath, destGemeindeFolder, moveInsteadOfCopy, overwrite, null, shaftOutputPathByKey, project));
                    continue;
                }

                if (chunks.Count == 1 && pages.Count == chunks[0].Pages.Count)
                {
                    results.Add(HandleParsedShaftDistribution(chunks[0].Parsed, pdfPath, pdfPath, destGemeindeFolder, moveInsteadOfCopy, overwrite, null, shaftOutputPathByKey, project));
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
                        results.Add(HandleParsedShaftDistribution(chunk.Parsed, pdfPath, tempPdfPath, destGemeindeFolder, moveInsteadOfCopy: false, overwrite, pageRange, shaftOutputPathByKey, project));
                    }
                    finally
                    {
                        try { if (File.Exists(tempPdfPath)) File.Delete(tempPdfPath); } catch { }
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

    private static List<string> ExpandSelectedShaftPdfFiles(IReadOnlyList<string> selectedPdfFiles)
    {
        var expanded = new HashSet<string>(selectedPdfFiles, StringComparer.OrdinalIgnoreCase);

        foreach (var pdfPath in selectedPdfFiles)
        {
            var directory = Path.GetDirectoryName(pdfPath);
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                continue;

            var name = Path.GetFileNameWithoutExtension(pdfPath);
            var isProtocol = name.Contains("schachtprotokoll", StringComparison.OrdinalIgnoreCase);
            var isPhotos = name.Contains("schachtfotos", StringComparison.OrdinalIgnoreCase);
            if (!isProtocol && !isPhotos)
                continue;

            foreach (var sibling in Directory.EnumerateFiles(directory, "*.pdf", SearchOption.TopDirectoryOnly))
            {
                var siblingName = Path.GetFileNameWithoutExtension(sibling);
                if (isProtocol && siblingName.Contains("schachtfotos", StringComparison.OrdinalIgnoreCase))
                    expanded.Add(sibling);
                else if (isPhotos && siblingName.Contains("schachtprotokoll", StringComparison.OrdinalIgnoreCase))
                    expanded.Add(sibling);
            }
        }

        return expanded.ToList();
    }

    // --- Dichtheitspruefungsprotokoll Distribution ---

    /// <summary>
    /// Verteilt Dichtheitspruefungsprotokolle (DP) aus einem Quellordner in die
    /// Haltungsordner-Struktur. Liest "oberer Schacht" / "unterer Schacht" aus dem PDF
    /// um die Haltung zu ermitteln.
    /// </summary>
    public static IReadOnlyList<DistributionResult> DistributeDichtheit(
        string pdfSourceFolder,
        string destGemeindeFolder,
        bool moveInsteadOfCopy = false,
        bool overwrite = false,
        Project? project = null,
        IProgress<DistributionProgress>? progress = null)
    {
        if (!Directory.Exists(pdfSourceFolder))
            return new[] { new DistributionResult(false, $"PDF folder not found: {pdfSourceFolder}", pdfSourceFolder, null, null, null, null, null, VideoMatchStatus.NotChecked) };

        var pdfFiles = Directory.EnumerateFiles(pdfSourceFolder, "*.pdf", SearchOption.AllDirectories)
            .Where(p => !Path.GetFileName(p).StartsWith("split_", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (pdfFiles.Count == 0)
            return new[] { new DistributionResult(false, $"No PDF files found in: {pdfSourceFolder}", pdfSourceFolder, null, null, null, null, null, VideoMatchStatus.NotChecked) };

        return DistributeDichtheitCore(pdfFiles, destGemeindeFolder, moveInsteadOfCopy, overwrite, project, progress);
    }

    /// <summary>
    /// Verteilt ausgewaehlte Dichtheitspruefungsprotokolle (DP) in die
    /// Haltungsordner-Struktur.
    /// </summary>
    public static IReadOnlyList<DistributionResult> DistributeDichtheitFiles(
        IEnumerable<string> pdfFiles,
        string destGemeindeFolder,
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
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (validPdfFiles.Count == 0)
            return new[] { new DistributionResult(false, "No valid PDF files selected.", "", null, null, null, null, null, VideoMatchStatus.NotChecked) };

        return DistributeDichtheitCore(validPdfFiles, destGemeindeFolder, moveInsteadOfCopy, overwrite, project, progress);
    }

    private static IReadOnlyList<DistributionResult> DistributeDichtheitCore(
        IReadOnlyList<string> pdfFiles,
        string destGemeindeFolder,
        bool moveInsteadOfCopy,
        bool overwrite,
        Project? project,
        IProgress<DistributionProgress>? progress)
    {
        var results = new List<DistributionResult>();
        var processed = 0;

        foreach (var pdfPath in pdfFiles)
        {
            try
            {
                var pages = ReadPdfPages(pdfPath);

                // Multi-Seiten-Erkennung: Jede Seite einzeln auf Haltungspaar pruefen.
                // KIT Bauinspekt PDFs haben pro Seite eine andere Haltung/Schacht.
                // Kontrollinformations-Seiten (Messdaten) gehoeren zur vorherigen Pruefseite.
                var pageResults = ExtractDichtheitPerPage(pages, project, destGemeindeFolder);

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
                        var holdingFolder = Path.Combine(destGemeindeFolder, haltung);
                        Directory.CreateDirectory(holdingFolder);

                        var suffix = pr.IsSchacht ? "SP" : "DP";
                        var destPdfName = $"{pr.DateStamp}_{haltung}_{suffix}.pdf";
                        var destPath = EnsureUniquePath(
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
                        var (shaftA, shaftB) = TryExtractDichtheitShafts(pdfText);
                        if (!string.IsNullOrWhiteSpace(shaftA) && !string.IsNullOrWhiteSpace(shaftB))
                            haltungId = ResolveDichtheitHaltungOrder(shaftA, shaftB, project, destGemeindeFolder);
                    }
                    if (string.IsNullOrWhiteSpace(haltungId))
                    {
                        var parsed = ParsePdfWithOcrFallback(pages);
                        if (parsed.Success && !string.IsNullOrWhiteSpace(parsed.Haltung))
                            haltungId = parsed.Haltung;
                    }
                    if (string.IsNullOrWhiteSpace(haltungId))
                        haltungId = TryExtractFromShafts(pdfText);

                    if (string.IsNullOrWhiteSpace(haltungId))
                    {
                        results.Add(new DistributionResult(false,
                            "Haltung nicht erkannt (oberer/unterer Schacht nicht gefunden)",
                            pdfPath, null, null, null, null, null, VideoMatchStatus.NotChecked));
                        continue;
                    }

                    var date = TryFindInspectionDate(pdfText);
                    var dateStamp = date?.ToString("yyyyMMdd", CultureInfo.InvariantCulture) ?? "00000000";

                    var haltung = SanitizePathSegment(NormalizeHaltungId(haltungId));
                    var holdingFolder = Path.Combine(destGemeindeFolder, haltung);
                    Directory.CreateDirectory(holdingFolder);

                    var destPdfName = $"{dateStamp}_{haltung}_DP.pdf";
                    var destPath = EnsureUniquePath(
                        Path.Combine(holdingFolder, destPdfName), overwrite);
                    MoveOrCopy(pdfPath, destPath, moveInsteadOfCopy);

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
    private static DistributionResult HandleParsedDistribution(
        ParsedPdf parsed,
        string sourcePdfPath,
        string pdfToStorePath,
        string videoSourceFolder,
        string destGemeindeFolder,
        bool moveInsteadOfCopy,
        bool overwrite,
        bool recursiveVideoSearch,
        string unmatchedFolderName,
        string? pageRange,
        AuswertungPro.Next.Domain.Models.Project? project = null,
        IReadOnlyList<string>? videoFilesCache = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? sidecarVideoLinksByHolding = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? sidecarHoldingsByVideoLink = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? cdIndexVideoLinksByPhoto = null)
    {
        var parsedHoldingRaw = parsed.Haltung ?? "UNKNOWN";
        var haltungRaw = PdfCorrectionMetadata.ResolveHolding(project, parsedHoldingRaw);
        if (string.IsNullOrWhiteSpace(haltungRaw))
            haltungRaw = parsedHoldingRaw;

        var haltungId = NormalizeHaltungId(haltungRaw);
        var haltung = SanitizePathSegment(haltungId);
        var originalHolding = SanitizePathSegment(NormalizeHaltungId(parsedHoldingRaw));
        if (parsed.Date is null)
            return new DistributionResult(false, "Date not found", sourcePdfPath, null, null, null, null, null, VideoMatchStatus.NotChecked);

        var date = parsed.Date.Value;
        var dateStamp = date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

        var pdfReplacements = BuildRenameReplacements(parsedHoldingRaw, haltungRaw);
        var correctionResult = TryCorrectPdfTextLayer(pdfToStorePath, pdfReplacements);
        var pdfSourceToStorePath = correctionResult.Corrected ? correctionResult.OutputPdfPath : pdfToStorePath;
        var removeOriginalAfterStore = moveInsteadOfCopy
            && correctionResult.Corrected
            && !string.Equals(pdfToStorePath, pdfSourceToStorePath, StringComparison.OrdinalIgnoreCase);

        // Suche Standard-Video
        VideoFindResult videoFind = string.IsNullOrWhiteSpace(parsed.VideoFile)
            ? FindVideoByHaltungDate(videoSourceFolder, haltung, dateStamp, recursiveVideoSearch, videoFilesCache)
            : FindVideo(parsed.VideoFile!, videoSourceFolder, haltung, dateStamp, recursiveVideoSearch, videoFilesCache);

        if (videoFind.Status != VideoMatchStatus.Matched
            && !string.Equals(originalHolding, haltung, StringComparison.OrdinalIgnoreCase))
        {
            var fallback = string.IsNullOrWhiteSpace(parsed.VideoFile)
                ? FindVideoByHaltungDate(videoSourceFolder, originalHolding, dateStamp, recursiveVideoSearch, videoFilesCache)
                : FindVideo(parsed.VideoFile!, videoSourceFolder, originalHolding, dateStamp, recursiveVideoSearch, videoFilesCache);

            if (fallback.Status == VideoMatchStatus.Matched
                || (videoFind.Status == VideoMatchStatus.NotFound && fallback.Status == VideoMatchStatus.Ambiguous))
                videoFind = fallback;
        }

        // Conservative fallback: use imported Link (e.g. HI116 from M150/MDB) when
        // normal matching did not produce a unique hit (NotFound/Ambiguous).
        // This keeps primary behavior but allows M150 mapping to disambiguate.
        if (videoFind.Status != VideoMatchStatus.Matched)
        {
            var fromLink = TryFindVideoFromRecordLink(project, haltung, videoSourceFolder, dateStamp, recursiveVideoSearch, videoFilesCache);
            if (fromLink.Status == VideoMatchStatus.Matched)
                videoFind = fromLink;
        }

        // Last-resort fallback for projects where videos are not named by holding, but M150/MDB carries the mapping.
        if (videoFind.Status != VideoMatchStatus.Matched)
        {
            var fromSidecar = TryFindVideoFromSidecarLinks(sidecarVideoLinksByHolding, haltung, videoSourceFolder, dateStamp, recursiveVideoSearch, videoFilesCache);
            if (fromSidecar.Status == VideoMatchStatus.Matched
                || (videoFind.Status == VideoMatchStatus.NotFound && fromSidecar.Status == VideoMatchStatus.Ambiguous))
                videoFind = fromSidecar;
        }

        // Last-resort fallback for WinCAN exports without MDB:
        // map photo filenames found in PDF pages via CDIndex.txt to video filenames.
        if (videoFind.Status != VideoMatchStatus.Matched)
        {
            var fromCdIndex = TryFindVideoFromCdIndexPhotoHints(
                cdIndexVideoLinksByPhoto,
                pdfToStorePath,
                haltung,
                videoSourceFolder,
                dateStamp,
                recursiveVideoSearch,
                videoFilesCache);
            if (fromCdIndex.Status == VideoMatchStatus.Matched
                || (videoFind.Status == VideoMatchStatus.NotFound && fromCdIndex.Status == VideoMatchStatus.Ambiguous))
                videoFind = fromCdIndex;
        }

        var holdingLabelAdjusted = false;
        if (videoFind.Status == VideoMatchStatus.Matched && videoFind.VideoPath is not null)
        {
            var mappedHolding = TryResolveHoldingFromMatchedVideo(
                sidecarHoldingsByVideoLink,
                sidecarVideoLinksByHolding,
                videoFind.VideoPath,
                haltung);
            if (!string.IsNullOrWhiteSpace(mappedHolding)
                && !string.Equals(mappedHolding, haltung, StringComparison.OrdinalIgnoreCase))
            {
                haltungRaw = mappedHolding;
                haltung = SanitizePathSegment(NormalizeHaltungId(mappedHolding));
                holdingLabelAdjusted = true;
            }
        }
        try
        {
            var holdingFolder = Path.Combine(destGemeindeFolder, haltung);
            Directory.CreateDirectory(holdingFolder);

            var destPdfName = $"{dateStamp}_{haltung}.pdf";
            var destPdfPath = EnsureUniquePath(Path.Combine(holdingFolder, destPdfName), overwrite);
            MoveOrCopy(pdfSourceToStorePath, destPdfPath, moveInsteadOfCopy);

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

            // Suche Gegeninspektions-Video (Dateiname: Haltungsnummer + 'g' vor Dateiendung)
            VideoFindResult videoFindG = FindVideoByHaltungDate(videoSourceFolder, haltung + "g", dateStamp, recursiveVideoSearch, videoFilesCache);

            string? destVideoPath = null;
            string? destVideoPathG = null;
            string? infoPath = null;
            var videoPaths = new List<string>();

            // Standard-Video kopieren (Duplikat-Check: gleiche Dateigroesse = bereits vorhanden)
            if (videoFind.Status == VideoMatchStatus.Matched && videoFind.VideoPath is not null)
            {
                var videoExt = Path.GetExtension(videoFind.VideoPath);
                var destVideoName = $"{dateStamp}_{haltung}{videoExt}";
                destVideoPath = Path.Combine(holdingFolder, destVideoName);
                var existingVideo = FindExistingVideo(holdingFolder, videoFind.VideoPath);
                if (existingVideo is not null)
                {
                    destVideoPath = existingVideo;
                }
                else
                {
                    destVideoPath = EnsureUniquePath(destVideoPath, overwrite);
                    MoveOrCopy(videoFind.VideoPath, destVideoPath, moveInsteadOfCopy);
                }
                videoPaths.Add(destVideoPath);

                // Automatisch Link im HaltungRecord setzen, falls Project übergeben
                if (project != null && !string.IsNullOrWhiteSpace(destVideoPath))
                {
                    var record = FindRecordByHolding(project, haltung);
                    if (record != null)
                    {
                        var meta = record.FieldMeta.TryGetValue("Link", out var m) ? m : null;
                        if (meta == null || !meta.UserEdited)
                        {
                            record.SetFieldValue("Link", destVideoPath, FieldSource.Unknown, userEdited: false);
                            project.ModifiedAtUtc = DateTime.UtcNow;
                            project.Dirty = true;
                        }
                    }
                }
            }

            // Gegeninspektions-Video kopieren (falls vorhanden und nicht identisch zum Standard-Video)
            if (videoFindG.Status == VideoMatchStatus.Matched && videoFindG.VideoPath is not null && !string.Equals(videoFindG.VideoPath, videoFind.VideoPath, StringComparison.OrdinalIgnoreCase))
            {
                var existingVideoG = FindExistingVideo(holdingFolder, videoFindG.VideoPath);
                if (existingVideoG is not null)
                {
                    destVideoPathG = existingVideoG;
                }
                else
                {
                    var videoExtG = Path.GetExtension(videoFindG.VideoPath);
                    var destVideoNameG = $"{dateStamp}_{haltung}-g{videoExtG}";
                    destVideoPathG = EnsureUniquePath(Path.Combine(holdingFolder, destVideoNameG), overwrite);
                    MoveOrCopy(videoFindG.VideoPath, destVideoPathG, moveInsteadOfCopy);
                }
                videoPaths.Add(destVideoPathG);
            }

            // Fehlerbehandlung wie bisher
            if (videoPaths.Count == 0)
            {
                if (videoFind.Status == VideoMatchStatus.NotFound && videoFindG.Status == VideoMatchStatus.NotFound)
                {
                    var infoName = $"{dateStamp}_{haltung}_VIDEO_MISSING.txt";
                    infoPath = EnsureUniquePath(Path.Combine(holdingFolder, infoName), overwrite);
                    var filmName = string.IsNullOrWhiteSpace(parsed.VideoFile) ? "<nicht gefunden>" : parsed.VideoFile!;
                    File.WriteAllText(infoPath, BuildMissingInfo(sourcePdfPath, filmName, date, haltungRaw));
                }
                else if (videoFind.Status == VideoMatchStatus.Ambiguous || videoFindG.Status == VideoMatchStatus.Ambiguous)
                {
                    var infoName = $"{dateStamp}_{haltung}_VIDEO_AMBIGUOUS.txt";
                    infoPath = EnsureUniquePath(Path.Combine(holdingFolder, infoName), overwrite);
                    var candidates = videoFind.Status == VideoMatchStatus.Ambiguous ? videoFind.Candidates : videoFindG.Candidates;
                    File.WriteAllText(infoPath, BuildAmbiguousInfo(sourcePdfPath, parsed.VideoFile!, date, haltungRaw, candidates));
                    var unmatchedFolder = Path.Combine(destGemeindeFolder, unmatchedFolderName, haltung);
                    Directory.CreateDirectory(unmatchedFolder);
                    CopyCandidatesToUnmatched(unmatchedFolder, dateStamp, haltung, candidates);
                }
            }

            var message = videoPaths.Count switch
            {
                2 => "OK (Standard+Gegeninspektion)",
                1 => "OK (1 Video)",
                0 when videoFind.Status == VideoMatchStatus.Ambiguous || videoFindG.Status == VideoMatchStatus.Ambiguous => "Video ambiguous",
                0 => "Video missing",
                _ => "OK"
            };

            if (!string.IsNullOrWhiteSpace(parsed.Message))
                message += $" / Parser: {parsed.Message}";

            if (holdingLabelAdjusted)
                message += " [Haltung korrigiert via M150/MDB]";

            if (videoFind.Status == VideoMatchStatus.Matched && !string.IsNullOrWhiteSpace(videoFind.Message))
            {
                if (videoFind.Message.Contains("M150/MDB sidecar", StringComparison.OrdinalIgnoreCase))
                    message += " [Quelle: M150/MDB]";
                else if (videoFind.Message.Contains("existing Link path", StringComparison.OrdinalIgnoreCase))
                    message += " [Quelle: Datensatz-Link]";
                else if (videoFind.Message.Contains("CDIndex", StringComparison.OrdinalIgnoreCase))
                    message += " [Quelle: CDIndex-Foto]";
            }

            if (correctionResult.Corrected)
                message += $" [PDF korrigiert: {correctionResult.MatchCount} Treffer auf {correctionResult.PageCount} Seiten]";
            else if (pdfReplacements.Count > 0 && !string.IsNullOrWhiteSpace(correctionResult.Message))
                message += $" [PDF-Korrektur: {correctionResult.Message}]";

            if (!string.IsNullOrWhiteSpace(pageRange))
                message = $"Split Seiten {pageRange} - {message}";

            return new DistributionResult(
                true,
                message,
                sourcePdfPath,
                videoFind.VideoPath,
                destPdfPath,
                destVideoPath,
                infoPath,
                holdingFolder,
                videoFind.Status,
                PdfCorrected: correctionResult.Corrected,
                PdfCorrectionMessage: correctionResult.Message);
        }
        finally
        {
            if (!moveInsteadOfCopy
                && correctionResult.Corrected
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
    private static DistributionResult HandleParsedShaftDistribution(
        ParsedShaftPdf parsed,
        string sourcePdfPath,
        string pdfToStorePath,
        string destGemeindeFolder,
        bool moveInsteadOfCopy,
        bool overwrite,
        string? pageRange,
        Dictionary<string, string> shaftOutputPathByKey,
        Project? project = null)
    {
        if (string.IsNullOrWhiteSpace(parsed.ShaftNumber))
            return new DistributionResult(false, "Schachtnummer nicht gefunden", sourcePdfPath, null, null, null, null, null, VideoMatchStatus.NotChecked);
        if (parsed.Date is null)
            return new DistributionResult(false, "Datum nicht gefunden", sourcePdfPath, null, null, null, null, null, VideoMatchStatus.NotChecked);

        var parsedShaftRaw = parsed.ShaftNumber.Trim();
        var shaftRaw = PdfCorrectionMetadata.ResolveShaft(project, parsedShaftRaw);
        if (string.IsNullOrWhiteSpace(shaftRaw))
            shaftRaw = parsedShaftRaw;

        var shaft = SanitizePathSegment(shaftRaw);
        var dateStamp = parsed.Date.Value.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var pdfReplacements = BuildRenameReplacements(parsedShaftRaw, shaftRaw);
        var correctionResult = TryCorrectPdfTextLayer(pdfToStorePath, pdfReplacements);
        var pdfSourceToStorePath = correctionResult.Corrected ? correctionResult.OutputPdfPath : pdfToStorePath;
        var removeOriginalAfterStore = moveInsteadOfCopy
            && correctionResult.Corrected
            && !string.Equals(pdfToStorePath, pdfSourceToStorePath, StringComparison.OrdinalIgnoreCase);

        try
        {
            var shaftFolder = Path.Combine(destGemeindeFolder, shaft);
            Directory.CreateDirectory(shaftFolder);

            var destPdfName = $"{dateStamp}_{shaft}.pdf";
            var shaftKey = $"{dateStamp}|{shaft}";
            string destPdfPath;
            var appendedToExisting = false;

            if (shaftOutputPathByKey.TryGetValue(shaftKey, out var existingPath)
                && !string.IsNullOrWhiteSpace(existingPath)
                && File.Exists(existingPath))
            {
                try
                {
                    AppendPdfFile(existingPath, pdfSourceToStorePath, moveInsteadOfCopy);
                    destPdfPath = existingPath;
                    appendedToExisting = true;
                }
                catch (Exception ex)
                {
                    return new DistributionResult(
                        false,
                        $"Konnte PDF nicht zusammenführen: {ex.Message}",
                        sourcePdfPath,
                        null,
                        null,
                        null,
                        null,
                        shaftFolder,
                        VideoMatchStatus.NotChecked);
                }
            }
            else
            {
                destPdfPath = EnsureUniquePath(Path.Combine(shaftFolder, destPdfName), overwrite);
                MoveOrCopy(pdfSourceToStorePath, destPdfPath, moveInsteadOfCopy);
                shaftOutputPathByKey[shaftKey] = destPdfPath;
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
            else if (pdfReplacements.Count > 0 && !string.IsNullOrWhiteSpace(correctionResult.Message))
                message += $" [PDF-Korrektur: {correctionResult.Message}]";
            if (!string.IsNullOrWhiteSpace(pageRange))
                message = $"Split Seiten {pageRange} - {message}";

            return new DistributionResult(
                true,
                message,
                sourcePdfPath,
                null,
                destPdfPath,
                null,
                null,
                shaftFolder,
                VideoMatchStatus.NotChecked,
                PdfCorrected: correctionResult.Corrected,
                PdfCorrectionMessage: correctionResult.Message);
        }
        finally
        {
            if (!moveInsteadOfCopy
                && correctionResult.Corrected
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
    private static readonly Regex PhotoAfterLabelRegex = new(
        @"Foto\s*:\s*(?<name>\d{1,5}_\d{1,5}_\d{1,7}_[A-Za-z](?:\.(?:jpe?g|png|bmp|tif|tiff))?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex PhotoTokenRegex = new(
        @"(?<![A-Za-z0-9])(?<name>\d{1,5}_\d{1,5}_\d{1,7}_[A-Za-z](?:\.(?:jpe?g|png|bmp|tif|tiff))?)(?![A-Za-z])",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
}
