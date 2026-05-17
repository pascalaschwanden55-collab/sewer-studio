using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Infrastructure.Common;

namespace AuswertungPro.Next.Infrastructure;

/// <summary>
/// HoldingFolderDistributor — Distribute-Eintrittspforten + Cores
/// fuer Haltungs- und KINS-TXT-Distribution (partial class).
///
/// Refactor 2026-05-07 (Etappe 4, Charge R9): die sechs Public-/Core-
/// Distribute-Methoden fuer Haltungs-PDFs und KINS-TXT-Files
/// ausgegliedert. Mechanisch — keine Verhaltensaenderung.
///
/// Fuer Schacht- und Dichtheitspruefungs-Distribute siehe spaetere
/// Chargen R10 / R11.
/// </summary>
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

        // Robustheits-Fix 2026-05-10 (Deep-Dive #1): SafeFileEnumeration statt
        // Directory.EnumerateFiles direkt. Gesperrte/fluechtige Unterordner
        // brechen den Import jetzt nicht mehr global ab.
        var pdfFiles = Common.SafeFileEnumeration
            .EnumerateFilesSafe(pdfSourceFolder, "*.pdf", recursive: true)
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

        // Audit 2026-05-17: SafeFileEnumeration tolerant gegen gesperrte Unterordner.
        var txtFiles = SafeFileEnumeration.EnumerateFilesSafe(txtSourceFolder, "kiDVDaten*.txt", recursive: true)
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

                // Beide Match-Stati erlauben Kopie: Matched (sicher) + MatchedWithoutDate (Warnung)
                if (videoFind.Status != VideoMatchStatus.Matched && videoFind.Status != VideoMatchStatus.MatchedWithoutDate)
                {
                    var fromLink = TryFindVideoFromRecordLink(project, haltung, videoSourceFolder, dateStamp, recursiveVideoSearch, videoFilesCache);
                    if (fromLink.Status == VideoMatchStatus.Matched || fromLink.Status == VideoMatchStatus.MatchedWithoutDate)
                        videoFind = fromLink;
                }

                string? destVideoPath = null;
                string? infoPath = null;

                if ((videoFind.Status == VideoMatchStatus.Matched || videoFind.Status == VideoMatchStatus.MatchedWithoutDate) && videoFind.VideoPath is not null)
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
}
