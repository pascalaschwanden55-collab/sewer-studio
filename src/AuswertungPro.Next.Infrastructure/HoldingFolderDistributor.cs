using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using AuswertungPro.Next.Infrastructure.Import.Pdf;

namespace AuswertungPro.Next.Infrastructure;

public static class HoldingFolderDistributor
{
    private static readonly object SchachtDateIndexSync = new();
    private static readonly Dictionary<string, IReadOnlyDictionary<string, DateTime>> SchachtDateIndexCache =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly Regex KinsTxtHeaderRegex = new(
        @"^\s*(?<usage>\S+)\s+(?<from>[0-9.]+)\s*->\s*(?<to>[0-9.]+).*?@Datei=(?<video>[^\s]+)\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex KinsTxtDateRegex = new(
        @"(?<d>\d{2}\.\d{2}\.\d{2,4})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public sealed record DistributionResult(
        bool Success,
        string Message,
        string SourcePdfPath,
        string? SourceVideoPath,
        string? DestPdfPath,
        string? DestVideoPath,
        string? InfoPath,
        string? HoldingFolder,
        VideoMatchStatus VideoStatus,
        bool PdfCorrected = false,
        string? PdfCorrectionMessage = null);

    public sealed record DistributionProgress(int Processed, int Total, string? CurrentFile);

    public enum VideoMatchStatus
    {
        NotChecked,
        Matched,
        NotFound,
        Ambiguous
    }

    public sealed record VideoFindResult(
        VideoMatchStatus Status,
        string? VideoPath,
        IReadOnlyList<string> Candidates,
        string? Message);

    private sealed record KinsTxtSection(
        string SourceTxtPath,
        string HoldingRaw,
        string VideoFileName,
        DateTime Date,
        string SectionText);

    private sealed record PdfTextReplacement(string SearchText, string ReplacementText);
    private sealed record PdfTextReplacementMatch(
        PdfTextReplacement Replacement,
        int StartLetterIndex,
        int EndLetterIndex,
        double Left,
        double Bottom,
        double Right,
        double Top,
        PdfPoint StartBaseLine,
        double FontSize);
    private sealed record PdfCorrectionResult(
        bool Success,
        bool Corrected,
        string OutputPdfPath,
        int MatchCount,
        int PageCount,
        string Message);

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

        var txtFiles = Directory.EnumerateFiles(txtSourceFolder, "*.txt", SearchOption.AllDirectories)
            .Where(p =>
                string.Equals(Path.GetFileName(p), "kiDVDaten.txt", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(Path.GetFileName(p), "kiDVinfo.txt", StringComparison.OrdinalIgnoreCase))
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
                    var videoExt = Path.GetExtension(videoFind.VideoPath);
                    var destVideoName = $"{dateStamp}_{haltung}{videoExt}";
                    destVideoPath = EnsureUniquePath(Path.Combine(holdingFolder, destVideoName), overwrite);
                    MoveOrCopy(videoFind.VideoPath, destVideoPath, moveInsteadOfCopy);

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
                        results.Add(new DistributionResult(false, $"Parse failed: {parsed.Message}", pdfPath, null, null, null, null, null, VideoMatchStatus.NotChecked));
                        continue;
                    }

                    results.Add(HandleParsedDistribution(parsed, pdfPath, pdfPath, videoSourceFolder, destGemeindeFolder, moveInsteadOfCopy, overwrite, recursiveVideoSearch, unmatchedFolderName, null, project, videoFilesCache, sidecarVideoLinksByHolding, sidecarHoldingsByVideoLink, cdIndexVideoLinksByPhoto));
                    continue;
                }

                if (chunks.Count == 1 && pages.Count == chunks[0].Pages.Count)
                {
                    results.Add(HandleParsedDistribution(chunks[0].Parsed, pdfPath, pdfPath, videoSourceFolder, destGemeindeFolder, moveInsteadOfCopy, overwrite, recursiveVideoSearch, unmatchedFolderName, null, project, videoFilesCache, sidecarVideoLinksByHolding, sidecarHoldingsByVideoLink, cdIndexVideoLinksByPhoto));
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
                        results.Add(HandleParsedDistribution(chunk.Parsed, pdfPath, tempPdfPath, videoSourceFolder, destGemeindeFolder, moveInsteadOfCopy: false, overwrite, recursiveVideoSearch, unmatchedFolderName, pageRange, project, videoFilesCache, sidecarVideoLinksByHolding, sidecarHoldingsByVideoLink, cdIndexVideoLinksByPhoto));
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

    private static IReadOnlyList<KinsTxtSection> ParseTxtSections(string txtPath)
    {
        var lines = ReadAllTextLinesBestEffort(txtPath);
        var sections = new List<KinsTxtSection>();
        var defaultDate = TryReadTxtDate(txtPath) ?? File.GetLastWriteTime(txtPath);

        string? currentHolding = null;
        string? currentVideo = null;
        var currentLines = new List<string>();

        void FlushCurrent()
        {
            if (string.IsNullOrWhiteSpace(currentHolding))
                return;

            var content = string.Join(Environment.NewLine, currentLines);
            sections.Add(new KinsTxtSection(
                SourceTxtPath: txtPath,
                HoldingRaw: currentHolding,
                VideoFileName: currentVideo ?? string.Empty,
                Date: defaultDate,
                SectionText: content));

            currentHolding = null;
            currentVideo = null;
            currentLines = new List<string>();
        }

        foreach (var rawLine in lines)
        {
            var line = rawLine ?? string.Empty;
            if (TryParseTxtHeader(line, out var haltung, out var videoFile))
            {
                FlushCurrent();
                currentHolding = haltung;
                currentVideo = videoFile;
                currentLines.Add(line.TrimEnd());
                continue;
            }

            if (!string.IsNullOrWhiteSpace(currentHolding))
                currentLines.Add(line.TrimEnd());
        }

        FlushCurrent();
        return sections;
    }

    private static bool TryParseTxtHeader(string line, out string haltung, out string videoFile)
    {
        haltung = string.Empty;
        videoFile = string.Empty;
        var match = KinsTxtHeaderRegex.Match(line ?? string.Empty);
        if (!match.Success)
            return false;

        var from = match.Groups["from"].Value.Trim();
        var to = match.Groups["to"].Value.Trim();
        var video = match.Groups["video"].Value.Trim();
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
            return false;

        haltung = $"{from}-{to}";
        videoFile = video;
        return true;
    }

    private static DateTime? TryReadTxtDate(string txtPath)
    {
        var dir = Path.GetDirectoryName(txtPath);
        if (string.IsNullOrWhiteSpace(dir))
            return null;

        var current = new DirectoryInfo(dir);
        for (var depth = 0; current is not null && depth < 6; depth++, current = current.Parent)
        {
            var infoPath = Path.Combine(current.FullName, "kiDVinfo.txt");
            if (!File.Exists(infoPath))
                continue;

            var parsed = TryParseDateFromInfoFile(infoPath);
            if (parsed.HasValue)
                return parsed;
        }

        return null;
    }

    private static DateTime? TryParseDateFromInfoFile(string infoPath)
    {
        foreach (var line in ReadAllTextLinesBestEffort(infoPath))
        {
            var match = KinsTxtDateRegex.Match(line ?? string.Empty);
            if (!match.Success)
                continue;

            var raw = match.Groups["d"].Value;
            if (DateTime.TryParseExact(raw, new[] { "dd.MM.yy", "dd.MM.yyyy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                return parsed;
        }

        return null;
    }

    private static IReadOnlyList<string> ReadAllTextLinesBestEffort(string path)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        try
        {
            return File.ReadAllLines(path, Encoding.GetEncoding(1252));
        }
        catch
        {
            return File.ReadAllLines(path);
        }
    }

    private static IReadOnlyList<string> EnumerateVideoFiles(string root, bool recursive)
    {
        if (!Directory.Exists(root))
            return Array.Empty<string>();

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        return Directory.EnumerateFiles(root, "*.*", searchOption)
            .Where(f => HasVideoExtension(Path.GetFileName(f)))
            .ToList();
    }

    private static List<string> EnumerateSidecarFiles(string folder)
    {
        var sidecarFiles = new List<string>();
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return sidecarFiles;

        // Rekursiv suchen: M150/XML liegen in der Praxis oft in Unterordnern.
        // XML nicht mehr über Dateinamen filtern, da viele Exporte generische Namen haben.
        try { sidecarFiles.AddRange(Directory.EnumerateFiles(folder, "*.xtf", SearchOption.AllDirectories)); } catch { }
        try { sidecarFiles.AddRange(Directory.EnumerateFiles(folder, "*.m150", SearchOption.AllDirectories)); } catch { }
        try { sidecarFiles.AddRange(Directory.EnumerateFiles(folder, "*.mdb", SearchOption.AllDirectories)); } catch { }
        try { sidecarFiles.AddRange(Directory.EnumerateFiles(folder, "*.xml", SearchOption.AllDirectories)); } catch { }

        return sidecarFiles
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildSidecarVideoLinkIndex(
        string? xtfSourceFolder,
        IReadOnlyList<string> pdfFiles)
    {
        var index = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var sidecarFolders = ResolveSidecarFolders(xtfSourceFolder, pdfFiles);
        if (sidecarFolders.Count == 0)
            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

        var sidecarPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in sidecarFolders)
        {
            foreach (var path in EnumerateSidecarFiles(folder))
                sidecarPaths.Add(path);
        }

        foreach (var sidecarPath in sidecarPaths)
        {
            try
            {
                var ext = Path.GetExtension(sidecarPath);
                List<AuswertungPro.Next.Domain.Models.HaltungRecord> records;
                if (ext.Equals(".mdb", StringComparison.OrdinalIgnoreCase))
                {
                    if (!M150MdbImportHelper.TryParseMdbFile(sidecarPath, out records, out _, out _))
                        continue;
                }
                else if (ext.Equals(".m150", StringComparison.OrdinalIgnoreCase)
                         || ext.Equals(".xml", StringComparison.OrdinalIgnoreCase))
                {
                    records = M150MdbImportHelper.ParseM150File(sidecarPath, out _);
                }
                else
                {
                    continue;
                }

                foreach (var rec in records)
                {
                    var hRaw = rec.GetFieldValue("Haltungsname");
                    if (string.IsNullOrWhiteSpace(hRaw))
                        continue;

                    var haltung = SanitizePathSegment(NormalizeHaltungId(hRaw));
                    if (string.IsNullOrWhiteSpace(haltung) || string.Equals(haltung, "UNKNOWN", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var rawLink = rec.GetFieldValue("Link");
                    var normalizedLink = NormalizeVideoFileName(rawLink);
                    if (string.IsNullOrWhiteSpace(normalizedLink))
                        continue;

                    if (!index.TryGetValue(haltung, out var links))
                    {
                        links = new List<string>();
                        index[haltung] = links;
                    }

                    if (!links.Contains(normalizedLink, StringComparer.OrdinalIgnoreCase))
                        links.Add(normalizedLink);
                }
            }
            catch
            {
                // Sidecar parsing is best-effort fallback only.
            }
        }

        return index.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyList<string>)kv.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildSidecarHoldingByVideoIndex(
        IReadOnlyDictionary<string, IReadOnlyList<string>>? sidecarVideoLinksByHolding)
    {
        var index = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        if (sidecarVideoLinksByHolding is null || sidecarVideoLinksByHolding.Count == 0)
            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var kv in sidecarVideoLinksByHolding)
        {
            var holding = kv.Key;
            foreach (var rawLink in kv.Value)
            {
                var normalizedLink = NormalizeVideoFileName(rawLink);
                if (string.IsNullOrWhiteSpace(normalizedLink))
                    continue;

                foreach (var key in EnumerateVideoLookupKeys(normalizedLink))
                {
                    if (!index.TryGetValue(key, out var holdings))
                    {
                        holdings = new List<string>();
                        index[key] = holdings;
                    }

                    if (!holdings.Contains(holding, StringComparer.OrdinalIgnoreCase))
                        holdings.Add(holding);
                }
            }
        }

        return index.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyList<string>)kv.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> EnumerateVideoLookupKeys(string? videoFileName)
    {
        var normalized = NormalizeVideoFileName(videoFileName);
        if (string.IsNullOrWhiteSpace(normalized))
            yield break;

        yield return normalized;

        var noExt = Path.GetFileNameWithoutExtension(normalized);
        if (!string.IsNullOrWhiteSpace(noExt))
            yield return noExt;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildCdIndexVideoLinkIndex(
        string? xtfSourceFolder,
        IReadOnlyList<string> pdfFiles)
    {
        var index = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var cdIndexFolders = ResolveCdIndexFolders(xtfSourceFolder, pdfFiles);
        if (cdIndexFolders.Count == 0)
            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

        var cdIndexPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in cdIndexFolders)
        {
            try
            {
                foreach (var path in Directory.EnumerateFiles(folder, "CDIndex.txt", SearchOption.AllDirectories))
                    cdIndexPaths.Add(path);
            }
            catch
            {
                // Best effort only.
            }
        }

        foreach (var cdIndexPath in cdIndexPaths)
        {
            try
            {
                AddCdIndexMappings(cdIndexPath, index);
            }
            catch
            {
                // Best effort only.
            }
        }

        return index.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyList<string>)kv.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> ResolveCdIndexFolders(
        string? xtfSourceFolder,
        IReadOnlyList<string> pdfFiles)
    {
        var folders = new HashSet<string>(ResolveSidecarFolders(xtfSourceFolder, pdfFiles), StringComparer.OrdinalIgnoreCase);

        static void AddExisting(HashSet<string> set, string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;
            if (Directory.Exists(path))
                set.Add(path);
        }

        foreach (var pdfPath in pdfFiles)
        {
            var pdfDir = Path.GetDirectoryName(pdfPath);
            if (string.IsNullOrWhiteSpace(pdfDir))
                continue;

            var current = new DirectoryInfo(pdfDir);
            while (current is not null)
            {
                var name = current.Name;
                if (name.Equals("Haltungen", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("Schaechte", StringComparison.OrdinalIgnoreCase)
                    || name.Equals("Sch\u00E4chte", StringComparison.OrdinalIgnoreCase))
                {
                    AddExisting(folders, current.FullName);
                    AddExisting(folders, current.Parent?.FullName);
                    break;
                }

                current = current.Parent;
            }
        }

        return folders.ToList();
    }

    private static void AddCdIndexMappings(
        string cdIndexPath,
        Dictionary<string, List<string>> index)
    {
        string? currentVideo = null;
        foreach (var rawLine in File.ReadLines(cdIndexPath))
        {
            var line = (rawLine ?? "").Trim();
            if (string.IsNullOrWhiteSpace(line)
                || line.StartsWith("[", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Ident=", StringComparison.OrdinalIgnoreCase))
                continue;

            var entry = line.Split(';')[0].Trim();
            if (string.IsNullOrWhiteSpace(entry))
                continue;

            var fileName = NormalizeVideoFileName(entry);
            if (string.IsNullOrWhiteSpace(fileName))
                continue;

            if (HasVideoExtension(fileName))
            {
                currentVideo = fileName;
                continue;
            }

            if (!HasImageExtension(fileName) || string.IsNullOrWhiteSpace(currentVideo))
                continue;

            foreach (var key in EnumeratePhotoLookupKeys(fileName))
            {
                if (!index.TryGetValue(key, out var videos))
                {
                    videos = new List<string>();
                    index[key] = videos;
                }

                if (!videos.Contains(currentVideo, StringComparer.OrdinalIgnoreCase))
                    videos.Add(currentVideo);
            }
        }
    }

    private static IReadOnlyList<string> ResolveSidecarFolders(string? xtfSourceFolder, IReadOnlyList<string> pdfFiles)
    {
        var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        static void AddExisting(HashSet<string> set, string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;
            if (Directory.Exists(path))
                set.Add(path);
        }

        AddExisting(folders, xtfSourceFolder);

        foreach (var pdfPath in pdfFiles)
        {
            var pdfDir = Path.GetDirectoryName(pdfPath);
            AddExisting(folders, pdfDir);
            if (string.IsNullOrWhiteSpace(pdfDir))
                continue;

            var parent = Directory.GetParent(pdfDir)?.FullName;
            var grandParent = parent is null ? null : Directory.GetParent(parent)?.FullName;

            AddExisting(folders, parent);
            AddExisting(folders, Path.Combine(pdfDir, "XTF"));
            AddExisting(folders, Path.Combine(parent ?? "", "XTF"));
            AddExisting(folders, Path.Combine(parent ?? "", "xtf"));
            AddExisting(folders, Path.Combine(parent ?? "", "M150"));
            AddExisting(folders, Path.Combine(grandParent ?? "", "Imports", "XTF"));
        }

        return folders.ToList();
    }

    private static VideoFindResult FindVideo(
        string videoFileNameFromPdf,
        string videoSourceFolder,
        string haltung,
        string dateStamp,
        bool recursiveVideoSearch,
        IReadOnlyList<string>? videoFilesCache = null)
    {
        var files = videoFilesCache ?? EnumerateVideoFiles(videoSourceFolder, recursiveVideoSearch);
        return HoldingVideoMatching.FindVideo(videoFileNameFromPdf, haltung, dateStamp, files);
    }

    private static VideoFindResult FindVideoByHaltungDate(
        string videoSourceFolder,
        string haltung,
        string dateStamp,
        bool recursiveVideoSearch,
        IReadOnlyList<string>? videoFilesCache = null)
    {
        var files = videoFilesCache ?? EnumerateVideoFiles(videoSourceFolder, recursiveVideoSearch);
        return HoldingVideoMatching.FindVideoByHaltungDate(haltung, dateStamp, files);
    }

    private static VideoFindResult TryFindVideoFromSidecarLinks(
        IReadOnlyDictionary<string, IReadOnlyList<string>>? sidecarVideoLinksByHolding,
        string haltung,
        string videoSourceFolder,
        string dateStamp,
        bool recursiveVideoSearch,
        IReadOnlyList<string>? videoFilesCache)
    {
        if (sidecarVideoLinksByHolding is null)
            return new VideoFindResult(VideoMatchStatus.NotFound, null, Array.Empty<string>(), "No sidecar link hints");

        var links = new List<string>();
        foreach (var key in EnumerateHoldingLookupKeys(haltung))
        {
            if (!sidecarVideoLinksByHolding.TryGetValue(key, out var keyLinks) || keyLinks.Count == 0)
                continue;

            foreach (var link in keyLinks)
            {
                if (!links.Contains(link, StringComparer.OrdinalIgnoreCase))
                    links.Add(link);
            }
        }

        if (links.Count == 0)
            return new VideoFindResult(VideoMatchStatus.NotFound, null, Array.Empty<string>(), "No sidecar link hints");

        var matched = new List<string>();
        var ambiguousCandidates = new List<string>();
        foreach (var link in links)
        {
            var fromLink = FindVideo(link, videoSourceFolder, haltung, dateStamp, recursiveVideoSearch, videoFilesCache);
            if (fromLink.Status == VideoMatchStatus.Matched && !string.IsNullOrWhiteSpace(fromLink.VideoPath))
            {
                if (!matched.Contains(fromLink.VideoPath, StringComparer.OrdinalIgnoreCase))
                    matched.Add(fromLink.VideoPath);
                continue;
            }

            if (fromLink.Status == VideoMatchStatus.Ambiguous)
            {
                foreach (var c in fromLink.Candidates)
                {
                    if (!ambiguousCandidates.Contains(c, StringComparer.OrdinalIgnoreCase))
                        ambiguousCandidates.Add(c);
                }
            }
        }

        if (matched.Count == 1)
            return new VideoFindResult(VideoMatchStatus.Matched, matched[0], Array.Empty<string>(), "Matched by M150/MDB sidecar link");
        if (matched.Count > 1)
            return new VideoFindResult(VideoMatchStatus.Ambiguous, null, matched, "Multiple matches from M150/MDB sidecar links");
        if (ambiguousCandidates.Count > 0)
            return new VideoFindResult(VideoMatchStatus.Ambiguous, null, ambiguousCandidates, "Ambiguous from M150/MDB sidecar links");

        return new VideoFindResult(VideoMatchStatus.NotFound, null, Array.Empty<string>(), "No match from M150/MDB sidecar links");
    }

    private static VideoFindResult TryFindVideoFromCdIndexPhotoHints(
        IReadOnlyDictionary<string, IReadOnlyList<string>>? cdIndexVideoLinksByPhoto,
        string pdfPath,
        string haltung,
        string videoSourceFolder,
        string dateStamp,
        bool recursiveVideoSearch,
        IReadOnlyList<string>? videoFilesCache)
    {
        if (cdIndexVideoLinksByPhoto is null || cdIndexVideoLinksByPhoto.Count == 0)
            return new VideoFindResult(VideoMatchStatus.NotFound, null, Array.Empty<string>(), "No CDIndex mapping");

        var photos = ExtractPhotoHintsFromPdf(pdfPath);
        if (photos.Count == 0)
            return new VideoFindResult(VideoMatchStatus.NotFound, null, Array.Empty<string>(), "No photo hints in PDF");

        var scoreByLink = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var links = new List<string>();
        foreach (var photo in photos)
        {
            if (!cdIndexVideoLinksByPhoto.TryGetValue(photo, out var keyLinks) || keyLinks.Count == 0)
                continue;

            foreach (var link in keyLinks)
            {
                if (!links.Contains(link, StringComparer.OrdinalIgnoreCase))
                    links.Add(link);
            }

            // Strong signal: this hint maps to exactly one CDIndex video.
            if (keyLinks.Count == 1)
            {
                var key = keyLinks[0];
                scoreByLink.TryGetValue(key, out var current);
                scoreByLink[key] = current + 1;
            }
        }

        if (links.Count == 0)
            return new VideoFindResult(VideoMatchStatus.NotFound, null, Array.Empty<string>(), "No CDIndex match for photo hints");

        if (scoreByLink.Count > 0)
        {
            var ranked = scoreByLink.OrderByDescending(kv => kv.Value).ToList();
            var top = ranked[0];
            var secondScore = ranked.Count > 1 ? ranked[1].Value : -1;
            if (top.Value > secondScore)
            {
                var voted = FindVideo(top.Key, videoSourceFolder, haltung, dateStamp, recursiveVideoSearch, videoFilesCache);
                if (voted.Status == VideoMatchStatus.Matched && voted.VideoPath is not null)
                {
                    return new VideoFindResult(
                        VideoMatchStatus.Matched,
                        voted.VideoPath,
                        Array.Empty<string>(),
                        $"Matched by CDIndex photo hint vote ({top.Value})");
                }
            }
        }

        var matched = new List<string>();
        var ambiguousCandidates = new List<string>();
        foreach (var link in links)
        {
            var fromLink = FindVideo(link, videoSourceFolder, haltung, dateStamp, recursiveVideoSearch, videoFilesCache);
            if (fromLink.Status == VideoMatchStatus.Matched && !string.IsNullOrWhiteSpace(fromLink.VideoPath))
            {
                if (!matched.Contains(fromLink.VideoPath, StringComparer.OrdinalIgnoreCase))
                    matched.Add(fromLink.VideoPath);
                continue;
            }

            if (fromLink.Status == VideoMatchStatus.Ambiguous)
            {
                foreach (var c in fromLink.Candidates)
                {
                    if (!ambiguousCandidates.Contains(c, StringComparer.OrdinalIgnoreCase))
                        ambiguousCandidates.Add(c);
                }
            }
        }

        if (matched.Count == 1)
            return new VideoFindResult(VideoMatchStatus.Matched, matched[0], Array.Empty<string>(), "Matched by CDIndex photo hint");
        if (matched.Count > 1)
            return new VideoFindResult(VideoMatchStatus.Ambiguous, null, matched, "Multiple matches from CDIndex photo hints");
        if (ambiguousCandidates.Count > 0)
            return new VideoFindResult(VideoMatchStatus.Ambiguous, null, ambiguousCandidates, "Ambiguous from CDIndex photo hints");

        return new VideoFindResult(VideoMatchStatus.NotFound, null, Array.Empty<string>(), "No usable video from CDIndex photo hints");
    }

    private static string? TryResolveHoldingFromMatchedVideo(
        IReadOnlyDictionary<string, IReadOnlyList<string>>? sidecarHoldingsByVideoLink,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? sidecarVideoLinksByHolding,
        string? videoPath,
        string parsedHolding)
    {
        if (sidecarHoldingsByVideoLink is null || sidecarHoldingsByVideoLink.Count == 0)
            return null;
        if (string.IsNullOrWhiteSpace(videoPath))
            return null;

        var fileName = NormalizeVideoFileName(Path.GetFileName(videoPath));
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        var candidates = new List<string>();
        foreach (var key in EnumerateVideoLookupKeys(fileName))
        {
            if (!sidecarHoldingsByVideoLink.TryGetValue(key, out var holdings))
                continue;

            foreach (var holding in holdings)
            {
                if (!candidates.Contains(holding, StringComparer.OrdinalIgnoreCase))
                    candidates.Add(holding);
            }
        }

        if (candidates.Count != 1)
            return null;

        var candidate = candidates[0];
        if (string.Equals(candidate, parsedHolding, StringComparison.OrdinalIgnoreCase))
            return null;

        // Keep parsed holding only if it already maps to the same matched video.
        if (HoldingHasVideoLink(sidecarVideoLinksByHolding, parsedHolding, fileName))
            return null;
        if (!HoldingHasVideoLink(sidecarVideoLinksByHolding, candidate, fileName))
            return null;

        return candidate;
    }

    private static bool HoldingHasVideoLink(
        IReadOnlyDictionary<string, IReadOnlyList<string>>? sidecarVideoLinksByHolding,
        string holding,
        string videoFileName)
    {
        if (sidecarVideoLinksByHolding is null
            || string.IsNullOrWhiteSpace(holding)
            || string.IsNullOrWhiteSpace(videoFileName))
            return false;

        var videoKeys = EnumerateVideoLookupKeys(videoFileName)
            .ToList();

        foreach (var key in EnumerateHoldingLookupKeys(holding))
        {
            if (!sidecarVideoLinksByHolding.TryGetValue(key, out var links))
                continue;

            foreach (var link in links)
            {
                foreach (var linkKey in EnumerateVideoLookupKeys(link))
                {
                    if (videoKeys.Contains(linkKey, StringComparer.OrdinalIgnoreCase))
                        return true;
                }
            }
        }

        return false;
    }

    private static string? GetSuffixFromFirstUnderscore(string fileName)
    {
        var idx = fileName.IndexOf('_');
        if (idx < 0)
            return null;
        return fileName.Substring(idx);
    }

    private sealed record PageInfo(int PageNumber, string Text, string SourcePath);
    private sealed record PdfPageChunk(IReadOnlyList<int> Pages, ParsedPdf Parsed);

    private static IReadOnlyList<PageInfo> ReadPdfPages(string pdfPath)
    {
        try
        {
            var extraction = PdfTextExtractor.ExtractPages(pdfPath);
            if (extraction.Pages.Count == 0)
                return ReadPdfPagesWithPdfPig(pdfPath);

            var pages = new List<PageInfo>(extraction.Pages.Count);
            for (var i = 0; i < extraction.Pages.Count; i++)
            {
                var text = (extraction.Pages[i] ?? "").Replace("\r\n", "\n").Trim();
                pages.Add(new PageInfo(i + 1, text, pdfPath));
            }
            return pages;
        }
        catch
        {
            return ReadPdfPagesWithPdfPig(pdfPath);
        }
    }

    private static IReadOnlyList<PageInfo> ReadPdfPagesWithPdfPig(string pdfPath)
    {
        var pages = new List<PageInfo>();
        using var doc = PdfDocument.Open(pdfPath);
        var pageNumber = 0;
        foreach (var page in doc.GetPages())
        {
            pageNumber++;
            var text = (page.Text ?? "").Replace("\r\n", "\n").Trim();
            pages.Add(new PageInfo(pageNumber, text, pdfPath));
        }
        return pages;
    }

    private static string ReadPdfText(string pdfPath)
    {
        var sb = new StringBuilder();
        using var doc = PdfDocument.Open(pdfPath);
        foreach (var page in doc.GetPages())
            sb.AppendLine(page.Text);
        return sb.ToString();
    }

    // Temporarily public for diagnostic purposes
    public sealed record ParsedPdf(bool Success, string? Message, DateTime? Date, string? Haltung, string? VideoFile);
    public sealed record ParsedShaftPdf(bool Success, string? Message, DateTime? Date, string? ShaftNumber);

    private static string NormalizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text ?? string.Empty;

        return text
            .Replace('\u00A0', ' ')
            .Replace('–', '-')
            .Replace('—', '-')
            .Replace('−', '-')
            .Replace("\t", " ");
    }

    private static bool TryParseDateString(string value, out DateTime date)
    {
        return DateTime.TryParseExact(
            value,
            new[] { "dd.MM.yyyy", "dd.MM.yy", "dd/MM/yyyy", "dd/MM/yy", "dd-MM-yyyy", "dd-MM-yy", "yyyy-MM-dd" },
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);
    }

    private sealed record PdfShaftChunk(IReadOnlyList<int> Pages, ParsedShaftPdf Parsed);

    public static ParsedShaftPdf ParseSchachtPdf(string text)
    {
        text = NormalizeText(text);
        if (string.IsNullOrWhiteSpace(text))
            return new ParsedShaftPdf(false, "Empty page", null, null);

        return ParseSchachtPdfPage(text);
    }

    public static ParsedShaftPdf ParseSchachtPdfPage(string text)
    {
        text = NormalizeText(text);
        if (string.IsNullOrWhiteSpace(text))
            return new ParsedShaftPdf(false, "Empty page", null, null);

        var shaftNumber = TryFindSchachtNumber(text);
        var date = TryFindSchachtDate(text);

        if (string.IsNullOrWhiteSpace(shaftNumber) && date is null)
            return new ParsedShaftPdf(false, "Schachtnummer und Datum nicht gefunden", null, null);
        if (string.IsNullOrWhiteSpace(shaftNumber))
            return new ParsedShaftPdf(false, "Schachtnummer nicht gefunden", date, null);
        if (date is null)
            return new ParsedShaftPdf(false, "Datum nicht gefunden", null, shaftNumber);

        return new ParsedShaftPdf(true, null, date, shaftNumber);
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

    // Temporarily public for diagnostic purposes
    public static ParsedPdf ParsePdf(string text)
    {
        text = NormalizeText(text);
        // Match both "Haltungsinspektion" and "Haltungsbilder" headers (Fretz PDF page 1 vs page 2)
        var headerRx = new Regex(@"Haltungs(?:\s*inspektion|bilder)\s*[-–—]\s*(\d{2}\.\d{2}\.\d{2,4}|\d{4}-\d{2}-\d{2})\s*[-–—]\s*((?:\d{2,}\.\d{2,}|\d{4,})\s*[-/]\s*(?:\d{2,}\.\d{2,}|\d{4,}))", RegexOptions.IgnoreCase);
        var filmRx = new Regex(@"Film(?:name|datei)?\s*[:\-]?\s*([A-Za-z0-9_\-\. ]+?\.(?:mp2|mpg|mpeg|mp4|avi|mov|wmv|mkv))", RegexOptions.IgnoreCase);

        var headerMatch = headerRx.Match(text);
        if (!headerMatch.Success)
            return ParsePdfPage(text, null);

        if (!TryParseDateString(headerMatch.Groups[1].Value, out var date))
            return new ParsedPdf(false, "Date parse failed", null, null, null);

        var haltung = NormalizeHaltungId(headerMatch.Groups[2].Value);
        if (!IsValidHaltungId(haltung))
            return ParsePdfPage(text);

        var videoFile = TryFindFilmName(text, filmRx);
        return new ParsedPdf(true, videoFile is null ? "Film name not found" : null, date, haltung, videoFile);
    }

    // Temporarily public for diagnostic purposes
    public static ParsedPdf ParsePdfPage(string text, string? pdfPath = null)
    {
        text = NormalizeText(text);
        if (string.IsNullOrWhiteSpace(text))
            return new ParsedPdf(false, "Empty page", null, null, null);

        var isWinCan = text.Contains("wincan", StringComparison.OrdinalIgnoreCase);
        var filenameHaltung = isWinCan ? TryExtractHaltungFromPdfPath(pdfPath) : null;

        // Try header extraction first (Haltungsinspektion / Haltungsbilder headers)
        // This is the most reliable source for Fretz/IBAK PDFs (pages 1 + 2)
        var headerHaltung = TryExtractFromHeader(text);
        if (headerHaltung is not null)
        {
            var headerDate = TryFindInspectionDate(text);
            var filmRxH = new Regex(@"Film(?:name|datei)?\s*[:\-]?\s*([A-Za-z0-9_\-\. ]+?\.(?:mp2|mpg|mpeg|mp4|avi|mov|wmv|mkv))", RegexOptions.IgnoreCase);
            var videoFileH = TryFindFilmName(text, filmRxH);
            var baseMessageH = videoFileH is null ? "Film name not found" : null;
            return new ParsedPdf(true, baseMessageH, headerDate, headerHaltung, videoFileH);
        }

        // Fallback: extract from reliable sources:
        // 1. Haltung from Schacht/Punkt fields (Schacht oben/unten, Oberer/Unterer Punkt)
        // 2. Date from separate date field (Datum, Insp.datum, etc.)
        
        // Immer Haltungsnummer aus Schacht/Punkt-Feldern zusammensetzen
        var shaftHaltung = TryExtractFromShafts(text);
        var date = TryFindInspectionDate(text);
        var filmRx = new Regex(@"Film(?:name|datei)?\s*[:\-]?\s*([A-Za-z0-9_\-\. ]+?\.(?:mp2|mpg|mpeg|mp4|avi|mov|wmv|mkv))", RegexOptions.IgnoreCase);
        var videoFile = TryFindFilmName(text, filmRx);
        var baseMessage = videoFile is null ? "Film name not found" : null;

        // Extrahiere explizites Haltung-Feld (falls vorhanden)
        var explicitHaltung = TryFindHaltungId(text);

        if (!string.IsNullOrWhiteSpace(shaftHaltung) && date is not null)
        {
            var shaftNormalized = NormalizeHaltungId(shaftHaltung);
            if (!IsValidHaltungId(shaftNormalized))
            {
                // Continue with explicit/fallback extraction instead of hard-failing.
                shaftHaltung = null;
            }
        }

        if (!string.IsNullOrWhiteSpace(shaftHaltung) && date is not null)
        {
            var shaftNormalized = NormalizeHaltungId(shaftHaltung);
            var normalized = shaftNormalized;

            // Verifiziere: explizites Haltung-Feld muss mit zusammengesetzter Nummer übereinstimmen (falls vorhanden)
            if (!string.IsNullOrWhiteSpace(explicitHaltung))
            {
                var explicitNorm = NormalizeHaltungId(explicitHaltung);
                if (IsValidHaltungId(explicitNorm) &&
                    !string.Equals(explicitNorm, shaftNormalized, StringComparison.OrdinalIgnoreCase))
                {
                    if (IsSuspiciousShaftPair(shaftNormalized, explicitNorm))
                    {
                        return new ParsedPdf(true, MergeMessage(baseMessage, $"Explizite Haltung bevorzugt ({explicitNorm})"), date, explicitNorm, videoFile);
                    }

                    if (!string.IsNullOrWhiteSpace(filenameHaltung) &&
                        (string.Equals(filenameHaltung, shaftNormalized, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(filenameHaltung, explicitNorm, StringComparison.OrdinalIgnoreCase)))
                    {
                        return new ParsedPdf(true, MergeMessage(baseMessage, "Haltung mit Dateiname validiert"), date, filenameHaltung, videoFile);
                    }
                    return new ParsedPdf(false, $"Haltungsnummer stimmt nicht überein: Schacht={normalized}, Feld={explicitNorm}", date, normalized, videoFile);
                }
            }

            return new ParsedPdf(true, baseMessage, date, normalized, videoFile);
        }

        // Fallback: Wenn keine Schacht-Felder gefunden, versuche explizites Haltung-Feld
        if (!string.IsNullOrWhiteSpace(explicitHaltung) && date is not null)
        {
            var explicitNorm = NormalizeHaltungId(explicitHaltung);
            if (!IsValidHaltungId(explicitNorm))
                return new ParsedPdf(false, "Haltung invalid (aus Feld)", date, explicitNorm, videoFile);

            if (!string.IsNullOrWhiteSpace(filenameHaltung) &&
                !string.Equals(filenameHaltung, explicitNorm, StringComparison.OrdinalIgnoreCase))
            {
                return new ParsedPdf(
                    true,
                    MergeMessage(baseMessage, $"Dateiname bevorzugt ({filenameHaltung})"),
                    date,
                    filenameHaltung,
                    videoFile);
            }

            return new ParsedPdf(true, baseMessage, date, explicitNorm, videoFile);
        }

        if (date is not null && !string.IsNullOrWhiteSpace(filenameHaltung))
        {
            return new ParsedPdf(
                true,
                MergeMessage(baseMessage, "Haltung aus Dateiname"),
                date,
                filenameHaltung,
                videoFile);
        }

        // XTF-Fallback: Wenn WinCAN (erkennbar an typischem Text) und keine Haltung gefunden, versuche XTF
        if (isWinCan && !string.IsNullOrWhiteSpace(pdfPath))
        {
            var dir = Path.GetDirectoryName(pdfPath);
            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
            {
                var xtfFiles = Directory.GetFiles(dir, "*.xtf", SearchOption.AllDirectories);
                var xtfPath = XtfHelper.FindMatchingXtf(pdfPath, xtfFiles);
                if (!string.IsNullOrWhiteSpace(xtfPath))
                {
                    var holdings = XtfHelper.ParseHoldingsFromXtf(xtfPath);
                    var holding = holdings.FirstOrDefault();
                    if (holding != null && !string.IsNullOrWhiteSpace(holding.HaltungId))
                    {
                        return new ParsedPdf(true, "(aus XTF uebernommen)", date, holding.HaltungId, videoFile);
                    }
                }
            }
        }

        return new ParsedPdf(false, "Schacht-Felder und Haltung nicht gefunden", date, null, videoFile);
    }

    private static bool IsSuspiciousShaftPair(string shaftPair, string explicitPair)
    {
        var shaftParts = shaftPair.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var explicitParts = explicitPair.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (shaftParts.Length != 2 || explicitParts.Length != 2)
            return false;

        if (string.Equals(shaftParts[0], shaftParts[1], StringComparison.OrdinalIgnoreCase))
            return true;

        // If explicit pair has different endpoints but shaft pair collapsed to a repeated value, prefer explicit.
        if (!string.Equals(explicitParts[0], explicitParts[1], StringComparison.OrdinalIgnoreCase)
            && string.Equals(shaftParts[1], explicitParts[0], StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    private static string? MergeMessage(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a))
            return string.IsNullOrWhiteSpace(b) ? null : b;
        if (string.IsNullOrWhiteSpace(b))
            return a;
        return $"{a}; {b}";
    }

    private static string? TryExtractHaltungFromPdfPath(string? pdfPath)
    {
        if (string.IsNullOrWhiteSpace(pdfPath))
            return null;

        var fileName = Path.GetFileNameWithoutExtension(pdfPath);
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        var pairRx = new Regex(@"(?:\d{2,}\.\d{2,}|\d{4,})\s*[-_]\s*(?:\d{2,}\.\d{2,}|\d{4,})");
        var match = pairRx.Match(fileName);
        if (!match.Success)
            return null;

        var normalized = NormalizeHaltungId(match.Value.Replace('_', '-'));
        return IsValidHaltungId(normalized) ? normalized : null;
    }

    private static IReadOnlyList<PdfPageChunk> SplitPdfIntoHoldings(IReadOnlyList<PageInfo> pages)
    {
        var chunks = new List<PdfPageChunk>();
        if (pages.Count == 0) return chunks;

        List<int>? currentPages = null;
        ParsedPdf? currentParsed = null;

        foreach (var page in pages)
        {
            var parsed = ParsePdfPageWithOcrFallback(page);
            if (!parsed.Success)
            {
                if (IsContentsPage(page.Text))
                    continue;

                if (currentPages is not null && currentParsed is not null)
                    currentPages.Add(page.PageNumber);
                continue;
            }

            if (currentPages is not null
                && currentParsed is not null
                && string.Equals(parsed.Haltung, currentParsed.Haltung, StringComparison.OrdinalIgnoreCase)
                && parsed.Date == currentParsed.Date)
            {
                currentPages.Add(page.PageNumber);
                continue;
            }

            if (currentPages is not null && currentParsed is not null)
                chunks.Add(new PdfPageChunk(currentPages, currentParsed));

            currentPages = new List<int> { page.PageNumber };
            currentParsed = parsed;
        }

        if (currentPages is not null && currentParsed is not null)
            chunks.Add(new PdfPageChunk(currentPages, currentParsed));

        return chunks;
    }

    private static ParsedPdf ParsePdfWithOcrFallback(IReadOnlyList<PageInfo> pages)
    {
        var pdfText = string.Join("\n\n", pages.Select(p => p.Text));
        var parsed = ParsePdf(pdfText);
        if (parsed.Success)
            return parsed;

        var ocrTexts = new List<string>(pages.Count);
        string? firstOcrError = null;
        var ocrAttempted = false;

        foreach (var page in pages)
        {
            if (!string.IsNullOrWhiteSpace(page.Text))
            {
                ocrTexts.Add(page.Text);
                continue;
            }

            if (string.IsNullOrWhiteSpace(page.SourcePath) || !File.Exists(page.SourcePath))
                continue;

            ocrAttempted = true;
            var ocr = PdfOcrExtractor.TryExtractPageText(page.SourcePath, page.PageNumber);
            if (ocr.Success && !string.IsNullOrWhiteSpace(ocr.Text))
            {
                ocrTexts.Add(ocr.Text);
            }
            else if (string.IsNullOrWhiteSpace(firstOcrError) && !string.IsNullOrWhiteSpace(ocr.Message))
            {
                firstOcrError = ocr.Message;
            }
        }

        if (ocrTexts.Count == 0)
        {
            if (!ocrAttempted)
                return parsed;

            var ocrMessage = string.IsNullOrWhiteSpace(firstOcrError)
                ? "OCR lieferte keinen Text"
                : $"OCR: {firstOcrError}";
            return new ParsedPdf(false, MergeMessage(parsed.Message, ocrMessage), parsed.Date, parsed.Haltung, parsed.VideoFile);
        }

        var parsedFromOcr = ParsePdf(string.Join("\n\n", ocrTexts));
        if (parsedFromOcr.Success)
            return parsedFromOcr;

        var mergedMessage = string.IsNullOrWhiteSpace(firstOcrError)
            ? MergeMessage(parsed.Message, parsedFromOcr.Message)
            : MergeMessage(MergeMessage(parsed.Message, parsedFromOcr.Message), $"OCR: {firstOcrError}");
        var mergedDate = parsedFromOcr.Date ?? parsed.Date;
        var mergedHaltung = !string.IsNullOrWhiteSpace(parsedFromOcr.Haltung) ? parsedFromOcr.Haltung : parsed.Haltung;
        var mergedVideo = !string.IsNullOrWhiteSpace(parsedFromOcr.VideoFile) ? parsedFromOcr.VideoFile : parsed.VideoFile;
        return new ParsedPdf(false, mergedMessage, mergedDate, mergedHaltung, mergedVideo);
    }

    private static ParsedPdf ParsePdfPageWithOcrFallback(PageInfo page)
    {
        var parsed = ParsePdfPage(page.Text, page.SourcePath);
        if (parsed.Success)
            return parsed;

        if (string.IsNullOrWhiteSpace(page.SourcePath) || !File.Exists(page.SourcePath))
            return parsed;

        // OCR fallback is expensive; only run when direct extraction failed.
        var ocr = PdfOcrExtractor.TryExtractPageText(page.SourcePath, page.PageNumber);
        if (!ocr.Success || string.IsNullOrWhiteSpace(ocr.Text))
        {
            var ocrMessage = string.IsNullOrWhiteSpace(ocr.Message)
                ? "OCR lieferte keinen Text"
                : $"OCR: {ocr.Message}";
            return new ParsedPdf(false, MergeMessage(parsed.Message, ocrMessage), parsed.Date, parsed.Haltung, parsed.VideoFile);
        }

        var parsedFromOcr = ParsePdfPage(ocr.Text, page.SourcePath);
        if (!parsedFromOcr.Success)
        {
            var mergedDateFallback = parsedFromOcr.Date ?? parsed.Date;
            var mergedHaltungFallback = !string.IsNullOrWhiteSpace(parsedFromOcr.Haltung) ? parsedFromOcr.Haltung : parsed.Haltung;
            var mergedVideoFallback = !string.IsNullOrWhiteSpace(parsedFromOcr.VideoFile) ? parsedFromOcr.VideoFile : parsed.VideoFile;
            return new ParsedPdf(false, MergeMessage(parsed.Message, parsedFromOcr.Message), mergedDateFallback, mergedHaltungFallback, mergedVideoFallback);
        }

        var mergedDate = parsedFromOcr.Date ?? parsed.Date;
        var mergedHaltung = !string.IsNullOrWhiteSpace(parsedFromOcr.Haltung) ? parsedFromOcr.Haltung : parsed.Haltung;
        var mergedVideo = !string.IsNullOrWhiteSpace(parsedFromOcr.VideoFile) ? parsedFromOcr.VideoFile : parsed.VideoFile;
        var mergedMessage = MergeMessage(parsed.Message, parsedFromOcr.Message);
        return new ParsedPdf(true, mergedMessage, mergedDate, mergedHaltung, mergedVideo);
    }

    private static IReadOnlyList<PdfShaftChunk> SplitPdfIntoShafts(IReadOnlyList<PageInfo> pages)
    {
        var chunks = new List<PdfShaftChunk>();
        if (pages.Count == 0) return chunks;

        List<int>? currentPages = null;
        ParsedShaftPdf? currentParsed = null;

        foreach (var page in pages)
        {
            var parsed = ParseSchachtPdfPageWithOcrFallback(page);
            if (!parsed.Success)
            {
                if (currentPages is not null && currentParsed is not null)
                    currentPages.Add(page.PageNumber);
                continue;
            }

            if (currentPages is not null
                && currentParsed is not null
                && string.Equals(parsed.ShaftNumber, currentParsed.ShaftNumber, StringComparison.OrdinalIgnoreCase)
                && parsed.Date == currentParsed.Date)
            {
                currentPages.Add(page.PageNumber);
                continue;
            }

            if (currentPages is not null && currentParsed is not null)
                chunks.Add(new PdfShaftChunk(currentPages, currentParsed));

            currentPages = new List<int> { page.PageNumber };
            currentParsed = parsed;
        }

        if (currentPages is not null && currentParsed is not null)
            chunks.Add(new PdfShaftChunk(currentPages, currentParsed));

        return chunks;
    }

    private static ParsedShaftPdf ParseSchachtPdfPageWithOcrFallback(PageInfo page)
    {
        var parsed = ParseSchachtPdfPage(page.Text);
        if (parsed.Success)
            return parsed;

        if (string.IsNullOrWhiteSpace(page.SourcePath) || !File.Exists(page.SourcePath))
            return parsed;

        var completedFromSibling = TryCompleteShaftDateFromSiblingProtocol(page.SourcePath, parsed);
        if (completedFromSibling is not null)
            return completedFromSibling;

        // Many Schachtprotokolle are interactive PDF forms where values are not in page text.
        var parsedFromForm = TryParseSchachtPdfPageFromFormFields(page.SourcePath, page.PageNumber);
        if (parsedFromForm is not null)
            return parsedFromForm;

        // OCR fallback is expensive; only try when baseline parsing has no usable result.
        var ocr = PdfOcrExtractor.TryExtractPageText(page.SourcePath, page.PageNumber);
        if (!ocr.Success || string.IsNullOrWhiteSpace(ocr.Text))
            return parsed;

        var parsedFromOcr = ParseSchachtPdfPage(ocr.Text);
        var mergedShaft = !string.IsNullOrWhiteSpace(parsedFromOcr.ShaftNumber) ? parsedFromOcr.ShaftNumber : parsed.ShaftNumber;
        var mergedDate = parsedFromOcr.Date ?? parsed.Date;
        if (string.IsNullOrWhiteSpace(mergedShaft))
            return parsed;

        if (mergedDate is null)
        {
            var resolvedDate = TryResolveDateFromSiblingProtocol(page.SourcePath, mergedShaft);
            if (resolvedDate is not null)
                mergedDate = resolvedDate;
        }

        if (mergedDate is null)
            return parsed;

        return new ParsedShaftPdf(
            true,
            MergeMessage(parsedFromOcr.Message, "aus OCR"),
            mergedDate,
            mergedShaft);
    }

    private static ParsedShaftPdf? TryParseSchachtPdfPageFromFormFields(string pdfPath, int pageNumber)
    {
        var entries = PdfFormFieldExtractor.GetPageFieldEntries(pdfPath, pageNumber);
        if (entries.Count == 0)
            return null;

        // First pass: label-preserving synthetic text for existing parser rules.
        var syntheticText = BuildSyntheticFormText(entries);
        var parsed = ParseSchachtPdfPage(syntheticText);
        if (parsed.Success)
        {
            return new ParsedShaftPdf(
                true,
                MergeMessage(parsed.Message, "aus PDF-Formular"),
                parsed.Date,
                parsed.ShaftNumber);
        }

        // Second pass: value-only heuristics for generic field names.
        var date = TryExtractDateFromFormEntries(entries);
        var shaft = TryExtractSchachtNumberFromFormEntries(entries);
        if (string.IsNullOrWhiteSpace(shaft) || date is null)
            return null;

        return new ParsedShaftPdf(true, "aus PDF-Formular", date, shaft);
    }

    private static string BuildSyntheticFormText(IReadOnlyList<PdfFormFieldEntry> entries)
    {
        var lines = new List<string>(entries.Count * 2);
        foreach (var entry in entries)
        {
            var labels = new[] { entry.PartialName, entry.AlternateName, entry.MappingName }
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (labels.Count == 0)
            {
                lines.Add(entry.Value);
                continue;
            }

            foreach (var label in labels)
                lines.Add($"{label}: {entry.Value}");
        }

        return string.Join("\n", lines);
    }

    private static DateTime? TryExtractDateFromFormEntries(IReadOnlyList<PdfFormFieldEntry> entries)
    {
        var dateRx = new Regex(@"\b(?<d>\d{2}[./-]\d{2}[./-]\d{2,4}|\d{4}[./-]\d{2}[./-]\d{2})\b");

        // Prefer labeled date fields.
        foreach (var entry in entries)
        {
            var label = BuildFormEntryLabel(entry);
            if (!ContainsDateLabel(label))
                continue;

            var m = dateRx.Match(entry.Value);
            if (m.Success && TryParseDateString(m.Groups["d"].Value, out var parsed))
                return parsed;
        }

        // Fallback: first parseable date from any value.
        foreach (var entry in entries)
        {
            var m = dateRx.Match(entry.Value);
            if (m.Success && TryParseDateString(m.Groups["d"].Value, out var parsed))
                return parsed;
        }

        return null;
    }

    private static string? TryExtractSchachtNumberFromFormEntries(IReadOnlyList<PdfFormFieldEntry> entries)
    {
        // Prefer explicit labels.
        foreach (var entry in entries)
        {
            var label = BuildFormEntryLabel(entry);
            if (!ContainsSchachtNumberLabel(label))
                continue;

            var candidate = ExtractShaftNumberToken(entry.Value);
            if (!string.IsNullOrWhiteSpace(candidate))
                return candidate;
        }

        // Fallback: strict numeric tokens only.
        foreach (var entry in entries)
        {
            var candidate = ExtractShaftNumberToken(entry.Value);
            if (!string.IsNullOrWhiteSpace(candidate))
                return candidate;
        }

        return null;
    }

    private static string BuildFormEntryLabel(PdfFormFieldEntry entry)
    {
        return string.Join(" ",
            new[] { entry.PartialName, entry.AlternateName, entry.MappingName }
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!.Trim()));
    }

    private static bool ContainsDateLabel(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return false;

        return label.Contains("datum", StringComparison.OrdinalIgnoreCase)
               || label.Contains("date", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsSchachtNumberLabel(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return false;

        return label.Contains("schacht", StringComparison.OrdinalIgnoreCase)
               || label.Contains("nummer", StringComparison.OrdinalIgnoreCase)
               || Regex.IsMatch(label, @"\bnr\.?\b", RegexOptions.IgnoreCase);
    }

    private static string? ExtractShaftNumberToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        // Prefer standalone numeric values, common for Schachtnummer forms.
        var direct = Regex.Match(value.Trim(), @"^(?<nr>\d{3,8})$");
        if (direct.Success)
            return direct.Groups["nr"].Value;

        var any = Regex.Match(value, @"\b(?<nr>\d{3,8})\b");
        if (!any.Success)
            return null;

        var token = any.Groups["nr"].Value;
        // Avoid obvious date fragments (e.g. year values).
        if (token.Length == 4 && int.TryParse(token, out var year) && year >= 1900 && year <= 2100)
            return null;

        return token;
    }

    private static ParsedShaftPdf? TryCompleteShaftDateFromSiblingProtocol(string sourcePdfPath, ParsedShaftPdf parsed)
    {
        if (string.IsNullOrWhiteSpace(parsed.ShaftNumber) || parsed.Date is not null)
            return null;

        var resolvedDate = TryResolveDateFromSiblingProtocol(sourcePdfPath, parsed.ShaftNumber);
        if (resolvedDate is null)
            return null;

        return new ParsedShaftPdf(
            true,
            MergeMessage(parsed.Message, "Datum aus Schachtprotokoll"),
            resolvedDate,
            parsed.ShaftNumber);
    }

    private static DateTime? TryResolveDateFromSiblingProtocol(string sourcePdfPath, string shaftNumber)
    {
        if (string.IsNullOrWhiteSpace(sourcePdfPath)
            || string.IsNullOrWhiteSpace(shaftNumber)
            || !File.Exists(sourcePdfPath))
            return null;

        var dir = Path.GetDirectoryName(sourcePdfPath);
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            return null;

        var normalizedShaft = NormalizeShaftNumberKey(shaftNumber);
        if (string.IsNullOrWhiteSpace(normalizedShaft))
            return null;

        var siblingProtocolPdfs = Directory.EnumerateFiles(dir, "*.pdf", SearchOption.TopDirectoryOnly)
            .Where(path => !string.Equals(path, sourcePdfPath, StringComparison.OrdinalIgnoreCase))
            .Where(path =>
            {
                var name = Path.GetFileName(path);
                return name.Contains("schachtprotokoll", StringComparison.OrdinalIgnoreCase)
                       || name.Contains("protokoll", StringComparison.OrdinalIgnoreCase);
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (siblingProtocolPdfs.Count == 0)
            return null;

        foreach (var protocolPdf in siblingProtocolPdfs)
        {
            var index = GetOrBuildSchachtDateIndex(protocolPdf);
            if (index.Count == 0)
                continue;

            if (index.TryGetValue(normalizedShaft, out var date))
                return date;
        }

        return null;
    }

    private static IReadOnlyDictionary<string, DateTime> GetOrBuildSchachtDateIndex(string protocolPdfPath)
    {
        lock (SchachtDateIndexSync)
        {
            if (SchachtDateIndexCache.TryGetValue(protocolPdfPath, out var cached))
                return cached;
        }

        var built = BuildSchachtDateIndex(protocolPdfPath);

        lock (SchachtDateIndexSync)
        {
            SchachtDateIndexCache[protocolPdfPath] = built;
        }

        return built;
    }

    private static IReadOnlyDictionary<string, DateTime> BuildSchachtDateIndex(string protocolPdfPath)
    {
        var index = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var extraction = PdfTextExtractor.ExtractPages(protocolPdfPath);
            for (var i = 0; i < extraction.Pages.Count; i++)
            {
                ParsedShaftPdf? parsed = null;

                var fromText = ParseSchachtPdfPage(extraction.Pages[i]);
                if (fromText.Success)
                {
                    parsed = fromText;
                }
                else
                {
                    parsed = TryParseSchachtPdfPageFromFormFields(protocolPdfPath, i + 1);
                }

                if (parsed is null || !parsed.Success || parsed.Date is null || string.IsNullOrWhiteSpace(parsed.ShaftNumber))
                    continue;

                var key = NormalizeShaftNumberKey(parsed.ShaftNumber);
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                if (!index.ContainsKey(key))
                    index[key] = parsed.Date.Value;
            }
        }
        catch
        {
            // Best effort date index.
        }

        return index;
    }

    private static string NormalizeShaftNumberKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var digits = Regex.Replace(value, @"\D", "");
        if (string.IsNullOrWhiteSpace(digits))
            return string.Empty;

        return TrimLeadingZerosValue(digits);
    }

    private static string BuildPageRange(IReadOnlyList<int> pages)
    {
        if (pages.Count == 0) return "";
        var sorted = pages.Distinct().OrderBy(p => p).ToList();
        return sorted.Count == 1 ? $"{sorted[0]}" : $"{sorted.First()}-{sorted.Last()}";
    }

    private static bool IsContentsPage(string text)
        => text.Contains("Inhaltsverzeichnis", StringComparison.OrdinalIgnoreCase);

    private static DateTime? TryFindInspectionDate(string text)
    {
        var dateRx = new Regex(@"(\d{2}\.\d{2}\.\d{2,4}|\d{4}-\d{2}-\d{2})");
        var lines = text.Replace("\r\n", "\n").Split('\n');

        // Priority 1: Find date in header line
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.Contains("Haltungsinspektion", StringComparison.OrdinalIgnoreCase)
                || line.Contains("Haltungsbilder", StringComparison.OrdinalIgnoreCase))
            {
                var mHeader = dateRx.Match(line);
                if (mHeader.Success && TryParseDateString(mHeader.Groups[1].Value, out var dh))
                    return dh;
            }
        }
        
        // Priority 2: Find date near Inspektionsdatum or similar
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.Contains("Gedruckt", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!line.Contains("Insp", StringComparison.OrdinalIgnoreCase) &&
                !line.Contains("Inspekt", StringComparison.OrdinalIgnoreCase) &&
                !line.Contains("Datum", StringComparison.OrdinalIgnoreCase) &&
                !line.Contains("Aufnahme", StringComparison.OrdinalIgnoreCase))
                continue;

            var m = dateRx.Match(line);
            if (m.Success && TryParseDateString(m.Groups[1].Value, out var d1))
                return d1;

            var prev = FindNearbyDate(lines, i - 1, -1, 3, dateRx);
            if (prev is not null) return prev;
            var next = FindNearbyDate(lines, i + 1, 1, 3, dateRx);
            if (next is not null) return next;
        }

        // Priority 3: Any date, but skip Gedruckt lines
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.Contains("Gedruckt", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("erstellt", StringComparison.OrdinalIgnoreCase))
                continue;

            var any = dateRx.Match(line);
            if (any.Success && TryParseDateString(any.Groups[1].Value, out var d2))
            {
                // Validate reasonable date range (2000-2030)
                if (d2.Year >= 2000 && d2.Year <= 2030)
                    return d2;
            }
        }

        return null;
    }

    private static DateTime? TryFindSchachtDate(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var labeledDateRx = new Regex(@"Datum\s*[:\-]?\s*(?<date>\d{2}[./-]\d{2}[./-]\d{2,4})", RegexOptions.IgnoreCase);
        foreach (var line in lines)
        {
            var m = labeledDateRx.Match(line);
            if (!m.Success)
                continue;

            if (TryParseDateString(m.Groups["date"].Value, out var d))
                return d;
        }

        var genericDateRx = new Regex(@"\b(?<date>\d{2}[./-]\d{2}[./-]\d{2,4})\b");
        foreach (var line in lines)
        {
            if (line.Contains("Foto", StringComparison.OrdinalIgnoreCase))
                continue;

            var m = genericDateRx.Match(line);
            if (!m.Success)
                continue;

            if (TryParseDateString(m.Groups["date"].Value, out var d))
                return d;
        }

        return null;
    }

    private static DateTime? FindNearbyDate(string[] lines, int startIndex, int step, int maxLines, Regex dateRx)
    {
        if (startIndex < 0 || startIndex >= lines.Length) return null;
        var checkedLines = 0;
        for (var i = startIndex; i >= 0 && i < lines.Length && checkedLines < maxLines; i += step)
        {
            var line = lines[i];
            checkedLines++;
            if (string.IsNullOrWhiteSpace(line)) continue;
            var m = dateRx.Match(line);
            if (m.Success && TryParseDateString(m.Groups[1].Value, out var d))
                return d;
        }
        return null;
    }

    private static string? TryFindHaltungId(string text)
    {
        var idRx = new Regex(@"(?im)^.*Haltung.*[:\-\s]+(?<id>[\d\.\- ]{5,})", RegexOptions.IgnoreCase);
        var generalPairRx = new Regex(@"((?:\d{2,}\.\d{2,}|\d{4,})\s*[-]\s*(?:\d{2,}\.\d{2,}|\d{4,}))(?=[^\d]|$)");
        var gluedDatePairRx = new Regex(@"((?:\d{2,}\.\d{2,}|\d{4,})\s*-\s*(?:\d{2,}\.\d{2,}|\d{4,}?))(?=\d{2}\.\d{2}\.\d{2,4}|\d{4}-\d{2}-\d{2})");
        
        // Priority 1: Try extracting from Schacht oben/unten pattern first (most reliable)
        var shaftPattern = TryExtractFromShafts(text);
        if (!string.IsNullOrWhiteSpace(shaftPattern))
        {
            var normalized = NormalizeHaltungId(shaftPattern);
            if (IsValidHaltungId(normalized))
                return normalized;
        }

        // Priority 1b: Pair directly glued to a date (e.g. 23022-2159822.04.2014).
        var glued = gluedDatePairRx.Match(text);
        if (glued.Success)
        {
            var normalized = NormalizeHaltungId(glued.Groups[1].Value);
            if (IsValidHaltungId(normalized))
                return normalized;
        }

        // WinCAN compact line where "KS Nr." (non-numeric start node) is glued to node ids,
        // e.g. "... KS Nr. 221632025233 ...".
        var ksCompact = Regex.Match(text, @"KS\s*Nr\.?\s*(?<digits>\d{10,13})", RegexOptions.IgnoreCase);
        if (ksCompact.Success)
        {
            var ksCandidate = TryParseKsCompactHoldingDigits(ksCompact.Groups["digits"].Value);
            if (!string.IsNullOrWhiteSpace(ksCandidate))
                return ksCandidate;
        }

        // Priority 1c: concatenated numeric pair without dash (e.g. 2302221598 -> 23022-21598)
        var concatenatedRx = new Regex(
            @"(?:Haltungsname|Schacht\s*oben|Schacht\s*unten|Oberer\s*Punkt|Unterer\s*Punkt).{0,300}?(?<id>\d{10})(?!\d)",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var concatenated = concatenatedRx.Match(text);
        if (concatenated.Success)
        {
            var raw = concatenated.Groups["id"].Value;
            var candidate = $"{raw.Substring(0, 5)}-{raw.Substring(5, 5)}";
            var normalized = NormalizeHaltungId(candidate);
            if (IsValidHaltungId(normalized))
                return normalized;
        }
        
        // Priority 2: Jede Zeile mit "Haltung" prüfen, nach ":" oder nach dem Wort die erste passende Nummer extrahieren
        var lines = text.Replace("\r\n", "\n").Split('\n');
        foreach (var line in lines)
        {
            if (!line.Contains("Haltung", StringComparison.OrdinalIgnoreCase))
                continue;
            // Suche nach Zahl mit Trennzeichen nach 'Haltung' oder nach ':'
            var m = idRx.Match(line);
            if (m.Success)
            {
                var id = m.Groups["id"].Value?.Trim();
                if (!string.IsNullOrWhiteSpace(id))
                {
                    var normalized = NormalizeHaltungId(id);
                    if (IsValidHaltungId(normalized))
                        return normalized;
                }
            }
            // Fallback: Suche nach erstem Zahlenpaar im Stil 11111-2222
            var inline = generalPairRx.Match(line);
            if (inline.Success)
            {
                var normalized = NormalizeHaltungId(inline.Groups[1].Value);
                if (IsValidHaltungId(normalized))
                    return normalized;
            }
        }
        
        // Priority 4: Try "Leitung" field
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (!line.Contains("Leitung", StringComparison.OrdinalIgnoreCase))
                continue;

            var inline = generalPairRx.Match(line);
            if (inline.Success)
            {
                var normalized = NormalizeHaltungId(inline.Groups[1].Value);
                if (IsValidHaltungId(normalized))
                    return normalized;
            }

            var nextId = FindNextToken(lines, i + 1, @"(?:\d{2,}\.\d{2,}|\d{4,})\s*[-/]\s*(?:\d{2,}\.\d{2,}|\d{4,})");
            if (!string.IsNullOrWhiteSpace(nextId))
            {
                var normalized = NormalizeHaltungId(nextId);
                if (IsValidHaltungId(normalized))
                    return normalized;
            }
        }

        var oberer = TryFindPoint(lines, "Oberer");
        var unterer = TryFindPoint(lines, "Unterer");
        if (!string.IsNullOrWhiteSpace(oberer) && !string.IsNullOrWhiteSpace(unterer))
        {
            var combined = NormalizeHaltungId($"{oberer}-{unterer}");
            if (IsValidHaltungId(combined))
                return combined;
        }

        var loose = generalPairRx.Match(text);
        if (loose.Success)
        {
            var normalized = NormalizeHaltungId(loose.Groups[1].Value);
            if (IsValidHaltungId(normalized) && !LooksLikeDateFragment(normalized))
                return normalized;
        }

        var anyIdLine = lines.FirstOrDefault(l => Regex.IsMatch(l, @"^\s*(?:\d{2,}\.\d{2,}|\d{4,})\s*[-/]\s*(?:\d{2,}\.\d{2,}|\d{4,})\s*$"));
        if (!string.IsNullOrWhiteSpace(anyIdLine))
        {
            var normalized = NormalizeHaltungId(anyIdLine.Trim());
            if (IsValidHaltungId(normalized))
                return normalized;
        }

        return null;
    }

    private static string? TryParseKsCompactHoldingDigits(string rawDigits)
    {
        if (string.IsNullOrWhiteSpace(rawDigits))
            return null;

        var digits = Regex.Replace(rawDigits, @"\D", "");
        if (digits.Length < 10)
            return null;

        var candidates = new List<(int Score, string Value)>();

        for (var prefixLen = 0; prefixLen <= 3; prefixLen++)
        {
            var remaining = digits.Length - prefixLen;
            if (remaining < 10)
                continue;

            if (remaining == 11)
            {
                var a = digits.Substring(prefixLen, 5);
                var bRaw = digits.Substring(prefixLen + 5, 6);
                if (bRaw.StartsWith("0", StringComparison.Ordinal))
                {
                    var b = TrimLeadingZerosValue(bRaw);
                    var candidate = NormalizeHaltungId($"{a}-{b}");
                    if (IsValidHaltungId(candidate))
                        candidates.Add((2, candidate));
                }
            }

            if (remaining == 10)
            {
                var a = digits.Substring(prefixLen, 5);
                var b = digits.Substring(prefixLen + 5, 5);
                var candidate = NormalizeHaltungId($"{a}-{TrimLeadingZerosValue(b)}");
                if (IsValidHaltungId(candidate))
                    candidates.Add((1, candidate));
            }
        }

        if (candidates.Count == 0)
            return null;

        return candidates
            .OrderByDescending(c => c.Score)
            .Select(c => c.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static string? TryFindSchachtNumber(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');

        // Preferred protocol header pattern:
        // "Zustandsaufnahme Schacht Nr: <Schachtnummer>"
        var headerRx = new Regex(
            @"Zustandsaufnahme\s*Schacht\s*Nr\.?\s*[:\-]?\s*(?<nr>\d{3,10})\b",
            RegexOptions.IgnoreCase);
        var headerMatch = headerRx.Match(text);
        if (headerMatch.Success)
            return headerMatch.Groups["nr"].Value.Trim();

        var nrRx = new Regex(@"\bNr\.?\s*[:\-]?\s*(?<nr>\d{3,})\b", RegexOptions.IgnoreCase);
        foreach (var line in lines)
        {
            var m = nrRx.Match(line);
            if (m.Success)
                return m.Groups["nr"].Value.Trim();
        }

        var labelRx = new Regex(@"\bSchacht(?:nummer|nr\.?)?\s*[:\-]?\s*(?<nr>\d{3,})\b", RegexOptions.IgnoreCase);
        foreach (var line in lines)
        {
            var m = labelRx.Match(line);
            if (m.Success)
                return m.Groups["nr"].Value.Trim();
        }

        // Schachtfotos often contain only the shaft number as plain page text.
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (Regex.IsMatch(trimmed, @"^\d{3,8}$"))
                return trimmed;
        }

        return null;
    }

    private static readonly Regex WinCanValueRegex = new(@"\d{2,}(?:\.\d{2,})?", RegexOptions.Compiled);
    private static readonly Regex WinCanUpperLabelRegex = new(
        @"\b(Schacht\s*oben|Knoten\s*oben|Oberer\s*Punkt|Startschacht|Von)\b[:\s]*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex WinCanLowerLabelRegex = new(
        @"\b(Schacht\s*unten|Knoten\s*unten|Unterer\s*Punkt|Endschacht|Nach)\b[:\s]*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static string NormalizeLine(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        s = s.Replace('\u00A0', ' ');
        s = Regex.Replace(s, @"[ \t]+", " ");
        return s.Trim();
    }

    private static string? TryGetValueAfterLabel(IReadOnlyList<string> lines, Regex labelRegex, Regex valueRegex)
    {
        for (int i = 0; i < lines.Count; i++)
        {
            var line = NormalizeLine(lines[i]);
            if (line.Length == 0) continue;

            // 1) Label + Wert in derselben Zeile
            var m = labelRegex.Match(line);
            if (m.Success)
            {
                var tail = NormalizeLine(line.Substring(m.Index + m.Length));
                var v1 = valueRegex.Match(tail);
                if (v1.Success) return v1.Value;

                // 2) Wert steht in nächster Zeile
                if (i + 1 < lines.Count)
                {
                    var next = NormalizeLine(lines[i + 1]);
                    var v2 = valueRegex.Match(next);
                    if (v2.Success) return v2.Value;
                }

                // 3) Manchmal noch eine Zeile weiter (PDF-Layout)
                if (i + 2 < lines.Count)
                {
                    var next2 = NormalizeLine(lines[i + 2]);
                    var v3 = valueRegex.Match(next2);
                    if (v3.Success) return v3.Value;
                }
            }

            // 4) “Zerhacktes” Label über Zeilengrenze
            if (i + 1 < lines.Count)
            {
                var joined = NormalizeLine(line + " " + lines[i + 1]);
                var mj = labelRegex.Match(joined);
                if (mj.Success)
                {
                    var tail = NormalizeLine(joined.Substring(mj.Index + mj.Length));
                    var vj = valueRegex.Match(tail);
                    if (vj.Success) return vj.Value;

                    if (i + 2 < lines.Count)
                    {
                        var vNext = valueRegex.Match(NormalizeLine(lines[i + 2]));
                        if (vNext.Success) return vNext.Value;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts haltung pair from "Haltungsinspektion" or "Haltungsbilder" header lines.
    /// Both Fretz page 1 (Haltungsinspektion) and page 2 (Haltungsbilder) use this format.
    /// </summary>
    private static string? TryExtractFromHeader(string text)
    {
        var headerRx = new Regex(
            @"Haltungs(?:\s*inspektion|bilder)\s*[-–—]\s*(?:\d{2}\.\d{2}\.\d{2,4}|\d{4}-\d{2}-\d{2})\s*[-–—]\s*((?:\d{2,}\.\d{2,}|\d{4,})\s*[-/]\s*(?:\d{2,}\.\d{2,}|\d{4,}))",
            RegexOptions.IgnoreCase);
        var m = headerRx.Match(text);
        if (!m.Success) return null;
        var haltung = NormalizeHaltungId(m.Groups[1].Value);
        return IsValidHaltungId(haltung) ? haltung : null;
    }

    /// <summary>
    /// Returns true if the first part of a haltung pair looks like a date fragment (MM.YYYY).
    /// This prevents "09.2025-80638" from being treated as a valid haltung.
    /// </summary>
    private static bool LooksLikeDateFragment(string haltungId)
    {
        if (string.IsNullOrWhiteSpace(haltungId)) return false;
        // Match patterns like "09.2025-XXXXX" where "09.2025" is actually a date fragment
        var dateFragRx = new Regex(@"^(\d{2}\.\d{4})-");
        var m = dateFragRx.Match(haltungId);
        if (!m.Success) return false;
        // Check if the first number looks like MM.YYYY (month 01-12, year 2000-2099)
        var parts = m.Groups[1].Value.Split('.');
        if (parts.Length == 2
            && int.TryParse(parts[0], out var month) && month >= 1 && month <= 12
            && int.TryParse(parts[1], out var year) && year >= 2000 && year <= 2099)
            return true;
        return false;
    }

    private static string? TryExtractFromShafts(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');

        // WinCAN: robust Label->Value extraction (Schacht oben/unten, Start/End, Von/Nach)
        var upper = TryGetValueAfterLabel(lines, WinCanUpperLabelRegex, WinCanValueRegex);
        var lower = TryGetValueAfterLabel(lines, WinCanLowerLabelRegex, WinCanValueRegex);
        if (!string.IsNullOrWhiteSpace(upper) && !string.IsNullOrWhiteSpace(lower))
        {
            if (!string.Equals(upper, lower, StringComparison.OrdinalIgnoreCase))
                return $"{upper}-{lower}";
        }

        // Inline layouts without line breaks (common in some PdfPig extracts).
        var pairAfterLowerPoint = Regex.Match(
            text,
            @"Unterer\s*Punkt\s*(?<pair>(?:\d{2,}\.\d{2,}|\d{4,})\s*-\s*(?:\d{2,}\.\d{2,}|\d{4,}))",
            RegexOptions.IgnoreCase);
        if (pairAfterLowerPoint.Success)
            return pairAfterLowerPoint.Groups["pair"].Value;

        var upperPointInline = Regex.Match(text, @"Oberer\s*Punkt\s*(?<v>\d{2,}\.\d{3,}|\d{5,})", RegexOptions.IgnoreCase);
        var lowerPointInline = Regex.Match(text, @"Unterer\s*Punkt\s*(?<v>\d{2,}\.\d{3,}|\d{5,})", RegexOptions.IgnoreCase);
        if (upperPointInline.Success && lowerPointInline.Success)
        {
            var up = upperPointInline.Groups["v"].Value;
            var low = lowerPointInline.Groups["v"].Value;
            if (!string.Equals(up, low, StringComparison.OrdinalIgnoreCase))
                return $"{up}-{low}";
        }

        var upperSchachtInline = Regex.Match(text, @"Schacht\s*oben\s*[:\-]?\s*(?<v>\d{2,}\.\d{3,}|\d{5,})", RegexOptions.IgnoreCase);
        var lowerSchachtInline = Regex.Match(text, @"Schacht\s*unten\s*[:\-]?\s*(?<v>\d{2,}\.\d{3,}|\d{5,})", RegexOptions.IgnoreCase);
        if (upperSchachtInline.Success && lowerSchachtInline.Success)
        {
            var up = upperSchachtInline.Groups["v"].Value;
            var low = lowerSchachtInline.Groups["v"].Value;
            if (!string.Equals(up, low, StringComparison.OrdinalIgnoreCase))
                return $"{up}-{low}";
        }

        string? oben = null;
        string? unten = null;
        
        var pointRx = new Regex(@"\b(\d{2,}\.\d{3,}|\d{5,})\b");
        
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            
            // IBAK/Fretz Format: "Oberer Punkt" / "Unterer Punkt"
            if (line.Contains("Oberer", StringComparison.OrdinalIgnoreCase) && 
                line.Contains("Punkt", StringComparison.OrdinalIgnoreCase))
            {
                var m = pointRx.Match(line);
                if (m.Success)
                    oben = m.Groups[1].Value;
                else if (i + 1 < lines.Length)
                {
                    var nextM = pointRx.Match(lines[i + 1]);
                    if (nextM.Success)
                        oben = nextM.Groups[1].Value;
                }
            }
            
            if (line.Contains("Unterer", StringComparison.OrdinalIgnoreCase) && 
                line.Contains("Punkt", StringComparison.OrdinalIgnoreCase))
            {
                var m = pointRx.Match(line);
                if (m.Success)
                    unten = m.Groups[1].Value;
                else if (i + 1 < lines.Length)
                {
                    var nextM = pointRx.Match(lines[i + 1]);
                    if (nextM.Success)
                        unten = nextM.Groups[1].Value;
                }
            }
            
            // Alternative IBAK Format: "Oberer Schacht" / "Unterer Schacht"
            if (line.Contains("Oberer", StringComparison.OrdinalIgnoreCase) && 
                line.Contains("Schacht", StringComparison.OrdinalIgnoreCase))
            {
                var m = pointRx.Match(line);
                if (m.Success)
                    oben = m.Groups[1].Value;
                else if (i + 1 < lines.Length)
                {
                    var nextM = pointRx.Match(lines[i + 1]);
                    if (nextM.Success)
                        oben = nextM.Groups[1].Value;
                }
            }
            
            if (line.Contains("Unterer", StringComparison.OrdinalIgnoreCase) && 
                line.Contains("Schacht", StringComparison.OrdinalIgnoreCase))
            {
                var m = pointRx.Match(line);
                if (m.Success)
                    unten = m.Groups[1].Value;
                else if (i + 1 < lines.Length)
                {
                    var nextM = pointRx.Match(lines[i + 1]);
                    if (nextM.Success)
                        unten = nextM.Groups[1].Value;
                }
            }
        }
        
        if (!string.IsNullOrWhiteSpace(oben) && !string.IsNullOrWhiteSpace(unten))
        {
            if (!string.Equals(oben, unten, StringComparison.OrdinalIgnoreCase))
                return $"{oben}-{unten}";
        }
        
        return null;
    }
    
    private static string? TryFindPoint(string[] lines, string label)
    {
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (!line.Contains(label, StringComparison.OrdinalIgnoreCase) || !line.Contains("Punkt", StringComparison.OrdinalIgnoreCase))
                continue;

            var inline = Regex.Match(line, @"\b(\d{2,}\.\d{3,}|\d{5,})\b");
            if (inline.Success)
                return inline.Groups[1].Value.Trim();

            var next = FindNextToken(lines, i + 1, @"\d{2,}\.\d{3,}|\d{5,}");
            if (!string.IsNullOrWhiteSpace(next))
                return next.Trim();
        }
        return null;
    }

    private static string? FindNextToken(string[] lines, int startIndex, string pattern)
    {
        for (var i = startIndex; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;
            var m = Regex.Match(line, pattern);
            if (m.Success)
                return m.Value;
            break;
        }
        return null;
    }

    private static void WritePdfPages(string sourcePdfPath, IReadOnlyList<int> pages, string destPdfPath)
    {
        using var doc = PdfDocument.Open(sourcePdfPath);
        using var builder = new PdfDocumentBuilder();

        foreach (var pageNumber in pages)
            builder.AddPage(doc, pageNumber);

        var bytes = builder.Build();
        File.WriteAllBytes(destPdfPath, bytes);
    }

    private static void AppendPdfFile(string targetPdfPath, string additionalPdfPath, bool removeAdditionalWhenMoved)
    {
        if (string.IsNullOrWhiteSpace(targetPdfPath)
            || string.IsNullOrWhiteSpace(additionalPdfPath)
            || !File.Exists(targetPdfPath)
            || !File.Exists(additionalPdfPath))
            throw new FileNotFoundException("PDF for append not found.");

        if (string.Equals(targetPdfPath, additionalPdfPath, StringComparison.OrdinalIgnoreCase))
            return;

        var mergedTempPath = Path.Combine(Path.GetTempPath(), $"merge_{Guid.NewGuid():N}.pdf");
        try
        {
            using (var targetDoc = PdfDocument.Open(targetPdfPath))
            using (var additionalDoc = PdfDocument.Open(additionalPdfPath))
            using (var builder = new PdfDocumentBuilder())
            {
                foreach (var page in targetDoc.GetPages())
                    builder.AddPage(targetDoc, page.Number);

                foreach (var page in additionalDoc.GetPages())
                    builder.AddPage(additionalDoc, page.Number);

                var bytes = builder.Build();
                File.WriteAllBytes(mergedTempPath, bytes);
            }

            File.Copy(mergedTempPath, targetPdfPath, overwrite: true);

            if (removeAdditionalWhenMoved)
            {
                try
                {
                    if (File.Exists(additionalPdfPath))
                        File.Delete(additionalPdfPath);
                }
                catch
                {
                    // Best-effort cleanup for move semantics.
                }
            }
        }
        finally
        {
            try
            {
                if (File.Exists(mergedTempPath))
                    File.Delete(mergedTempPath);
            }
            catch
            {
                // ignore
            }
        }
    }

    private static IReadOnlyList<PdfTextReplacement> BuildRenameReplacements(string oldValue, string newValue)
    {
        var oldToken = (oldValue ?? string.Empty).Trim();
        var newToken = (newValue ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(oldToken)
            || string.IsNullOrWhiteSpace(newToken)
            || string.Equals(oldToken, newToken, StringComparison.OrdinalIgnoreCase))
            return Array.Empty<PdfTextReplacement>();

        return new[] { new PdfTextReplacement(oldToken, newToken) };
    }

    private static PdfCorrectionResult TryCorrectPdfTextLayer(string sourcePdfPath, IReadOnlyList<PdfTextReplacement> replacements)
    {
        if (string.IsNullOrWhiteSpace(sourcePdfPath) || !File.Exists(sourcePdfPath))
            return new PdfCorrectionResult(false, false, sourcePdfPath, 0, 0, "PDF nicht gefunden.");

        if (replacements.Count == 0)
            return new PdfCorrectionResult(true, false, sourcePdfPath, 0, 0, string.Empty);

        var effectiveReplacements = replacements
            .Where(r => !string.IsNullOrWhiteSpace(r.SearchText)
                        && !string.IsNullOrWhiteSpace(r.ReplacementText)
                        && !string.Equals(r.SearchText, r.ReplacementText, StringComparison.OrdinalIgnoreCase))
            .GroupBy(r => r.SearchText.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        if (effectiveReplacements.Count == 0)
            return new PdfCorrectionResult(true, false, sourcePdfPath, 0, 0, string.Empty);

        var correctedTempPath = Path.Combine(Path.GetTempPath(), $"pdfcorr_{Guid.NewGuid():N}.pdf");
        try
        {
            using var sourceDocument = PdfDocument.Open(sourcePdfPath);
            using var builder = new PdfDocumentBuilder();
            var overlayFont = builder.AddStandard14Font(Standard14Font.Helvetica);

            var totalMatches = 0;
            var pageCount = 0;

            foreach (var page in sourceDocument.GetPages())
            {
                var pageBuilder = builder.AddPage(sourceDocument, page.Number);
                var matches = FindPdfTextReplacementMatches(page, effectiveReplacements);
                if (matches.Count == 0)
                    continue;

                pageCount++;
                totalMatches += matches.Count;

                pageBuilder.NewContentStreamAfter();
                foreach (var match in matches.OrderBy(m => m.StartLetterIndex))
                {
                    var width = Math.Max(1d, match.Right - match.Left);
                    var height = Math.Max(1d, match.Top - match.Bottom);
                    var left = Math.Max(0d, match.Left - 0.40d);
                    var bottom = Math.Max(0d, match.Bottom - 0.25d);
                    var drawWidth = width + 0.80d;
                    var drawHeight = height + 0.50d;

                    pageBuilder.SetTextAndFillColor(255, 255, 255);
                    pageBuilder.DrawRectangle(
                        new PdfPoint((decimal)left, (decimal)bottom),
                        (decimal)drawWidth,
                        (decimal)drawHeight,
                        0.1m,
                        fill: true);

                    var fontSize = Math.Max(1d, match.FontSize);
                    var textPosition = new PdfPoint(match.StartBaseLine.X, match.StartBaseLine.Y);
                    var measuredLetters = pageBuilder.MeasureText(match.Replacement.ReplacementText, (decimal)fontSize, textPosition, overlayFont);
                    var measuredWidth = MeasureLettersWidth(measuredLetters);
                    if (measuredWidth > width && measuredWidth > 0d)
                    {
                        var scale = width / measuredWidth;
                        fontSize = Math.Max(1d, fontSize * scale);
                    }

                    pageBuilder.SetTextAndFillColor(0, 0, 0);
                    pageBuilder.AddText(match.Replacement.ReplacementText, (decimal)fontSize, textPosition, overlayFont);
                    pageBuilder.ResetColor();
                }
            }

            if (totalMatches == 0)
                return new PdfCorrectionResult(true, false, sourcePdfPath, 0, 0, "Keine Treffer im Text-Layer gefunden.");

            var bytes = builder.Build();
            File.WriteAllBytes(correctedTempPath, bytes);
            return new PdfCorrectionResult(
                true,
                true,
                correctedTempPath,
                totalMatches,
                pageCount,
                $"Text-Layer aktualisiert ({totalMatches} Treffer auf {pageCount} Seiten).");
        }
        catch (Exception ex)
        {
            try
            {
                if (File.Exists(correctedTempPath))
                    File.Delete(correctedTempPath);
            }
            catch
            {
                // Best-effort cleanup.
            }

            return new PdfCorrectionResult(false, false, sourcePdfPath, 0, 0, $"PDF-Korrektur fehlgeschlagen: {ex.Message}");
        }
    }

    private static IReadOnlyList<PdfTextReplacementMatch> FindPdfTextReplacementMatches(
        Page page,
        IReadOnlyList<PdfTextReplacement> replacements)
    {
        var letters = page.Letters?.ToList() ?? new List<Letter>();
        if (letters.Count == 0 || replacements.Count == 0)
            return Array.Empty<PdfTextReplacementMatch>();

        var flatTextBuilder = new StringBuilder();
        var charToLetterIndex = new List<int>(letters.Count * 2);
        for (var i = 0; i < letters.Count; i++)
        {
            var value = letters[i].Value;
            if (string.IsNullOrEmpty(value))
                continue;

            flatTextBuilder.Append(value);
            for (var c = 0; c < value.Length; c++)
                charToLetterIndex.Add(i);
        }

        var flatText = flatTextBuilder.ToString();
        if (flatText.Length == 0 || charToLetterIndex.Count == 0)
            return Array.Empty<PdfTextReplacementMatch>();

        var matches = new List<PdfTextReplacementMatch>();
        foreach (var replacement in replacements)
        {
            var search = replacement.SearchText.Trim();
            if (string.IsNullOrWhiteSpace(search) || search.Length > flatText.Length)
                continue;

            var searchStart = 0;
            while (searchStart <= flatText.Length - search.Length)
            {
                var foundIndex = flatText.IndexOf(search, searchStart, StringComparison.OrdinalIgnoreCase);
                if (foundIndex < 0)
                    break;

                if (IsReplacementBoundary(flatText, foundIndex, search.Length))
                {
                    var foundEnd = foundIndex + search.Length - 1;
                    if (foundEnd >= 0 && foundEnd < charToLetterIndex.Count)
                    {
                        var startLetterIndex = charToLetterIndex[foundIndex];
                        var endLetterIndex = charToLetterIndex[foundEnd];
                        var match = TryBuildPdfTextReplacementMatch(letters, replacement, startLetterIndex, endLetterIndex);
                        if (match is not null)
                            matches.Add(match);
                    }
                }

                searchStart = foundIndex + search.Length;
            }
        }

        if (matches.Count <= 1)
            return matches;

        return FilterOverlappingReplacementMatches(matches);
    }

    private static PdfTextReplacementMatch? TryBuildPdfTextReplacementMatch(
        IReadOnlyList<Letter> letters,
        PdfTextReplacement replacement,
        int startLetterIndex,
        int endLetterIndex)
    {
        if (startLetterIndex < 0 || endLetterIndex < startLetterIndex || endLetterIndex >= letters.Count)
            return null;

        var left = double.MaxValue;
        var right = double.MinValue;
        var bottom = double.MaxValue;
        var top = double.MinValue;
        double fontSizeSum = 0;
        var fontSizeCount = 0;
        var startBaseline = letters[startLetterIndex].StartBaseLine;

        for (var i = startLetterIndex; i <= endLetterIndex; i++)
        {
            var glyph = letters[i].GlyphRectangle;
            left = Math.Min(left, glyph.Left);
            right = Math.Max(right, glyph.Right);
            bottom = Math.Min(bottom, glyph.Bottom);
            top = Math.Max(top, glyph.Top);

            if (letters[i].FontSize > 0)
            {
                fontSizeSum += letters[i].FontSize;
                fontSizeCount++;
            }
        }

        if (left == double.MaxValue || right == double.MinValue || bottom == double.MaxValue || top == double.MinValue)
            return null;

        var fontSize = fontSizeCount > 0 ? fontSizeSum / fontSizeCount : 9d;
        return new PdfTextReplacementMatch(
            replacement,
            startLetterIndex,
            endLetterIndex,
            left,
            bottom,
            right,
            top,
            startBaseline,
            fontSize);
    }

    private static IReadOnlyList<PdfTextReplacementMatch> FilterOverlappingReplacementMatches(
        IReadOnlyList<PdfTextReplacementMatch> matches)
    {
        var accepted = new List<PdfTextReplacementMatch>();
        foreach (var candidate in matches
                     .OrderBy(m => m.StartLetterIndex)
                     .ThenByDescending(m => m.EndLetterIndex - m.StartLetterIndex))
        {
            var overlaps = accepted.Any(existing =>
                !(candidate.EndLetterIndex < existing.StartLetterIndex
                  || candidate.StartLetterIndex > existing.EndLetterIndex));
            if (!overlaps)
                accepted.Add(candidate);
        }

        return accepted;
    }

    private static bool IsReplacementBoundary(string text, int startIndex, int length)
    {
        if (startIndex < 0 || length <= 0 || startIndex + length > text.Length)
            return false;

        var before = startIndex > 0 ? text[startIndex - 1] : '\0';
        var afterIndex = startIndex + length;
        var after = afterIndex < text.Length ? text[afterIndex] : '\0';
        return !IsIdentifierCharacter(before) && !IsIdentifierCharacter(after);
    }

    private static bool IsIdentifierCharacter(char ch)
    {
        if (ch == '\0')
            return false;

        return char.IsLetterOrDigit(ch)
               || ch == '-'
               || ch == '_'
               || ch == '.';
    }

    private static double MeasureLettersWidth(IReadOnlyList<Letter> letters)
    {
        if (letters.Count == 0)
            return 0d;

        var left = double.MaxValue;
        var right = double.MinValue;
        foreach (var letter in letters)
        {
            var glyph = letter.GlyphRectangle;
            left = Math.Min(left, glyph.Left);
            right = Math.Max(right, glyph.Right);
        }

        if (left == double.MaxValue || right == double.MinValue)
            return 0d;

        return Math.Max(0d, right - left);
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

            // Standard-Video kopieren
            if (videoFind.Status == VideoMatchStatus.Matched && videoFind.VideoPath is not null)
            {
                var videoExt = Path.GetExtension(videoFind.VideoPath);
                var destVideoName = $"{dateStamp}_{haltung}{videoExt}";
                destVideoPath = EnsureUniquePath(Path.Combine(holdingFolder, destVideoName), overwrite);
                MoveOrCopy(videoFind.VideoPath, destVideoPath, moveInsteadOfCopy);
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
                var videoExtG = Path.GetExtension(videoFindG.VideoPath);
                var destVideoNameG = $"{dateStamp}_{haltung}-g{videoExtG}";
                destVideoPathG = EnsureUniquePath(Path.Combine(holdingFolder, destVideoNameG), overwrite);
                MoveOrCopy(videoFindG.VideoPath, destVideoPathG, moveInsteadOfCopy);
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

    private static AuswertungPro.Next.Domain.Models.HaltungRecord? FindRecordByHolding(
        AuswertungPro.Next.Domain.Models.Project? project,
        string haltung)
    {
        if (project is null || string.IsNullOrWhiteSpace(haltung))
            return null;

        var keys = EnumerateHoldingLookupKeys(haltung)
            .Select(SanitizePathSegment)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return project.Data.FirstOrDefault(x =>
        {
            var recKey = SanitizePathSegment(NormalizeHaltungId(x.GetFieldValue("Haltungsname")?.Trim() ?? ""));
            return keys.Contains(recKey, StringComparer.OrdinalIgnoreCase);
        });
    }

    private static IEnumerable<string> EnumerateHoldingLookupKeys(string haltung)
    {
        var normalized = NormalizeHaltungId(haltung);
        if (!string.IsNullOrWhiteSpace(normalized))
            yield return normalized;

        var reversed = ReverseHoldingId(normalized);
        if (!string.IsNullOrWhiteSpace(reversed)
            && !string.Equals(reversed, normalized, StringComparison.OrdinalIgnoreCase))
            yield return reversed;
    }

    private static string ReverseHoldingId(string? haltung)
    {
        if (string.IsNullOrWhiteSpace(haltung))
            return string.Empty;

        var parts = haltung.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
            return string.Empty;

        return $"{parts[1]}-{parts[0]}";
    }

    private static VideoFindResult TryFindVideoFromRecordLink(
        AuswertungPro.Next.Domain.Models.Project? project,
        string haltung,
        string videoSourceFolder,
        string dateStamp,
        bool recursiveVideoSearch,
        IReadOnlyList<string>? videoFilesCache)
    {
        var record = FindRecordByHolding(project, haltung);
        if (record is null)
            return new VideoFindResult(VideoMatchStatus.NotFound, null, Array.Empty<string>(), "No matching record");

        if (record.FieldMeta.TryGetValue("Link", out var linkMeta))
        {
            var src = linkMeta.Source;
            var isXtfLink = src == AuswertungPro.Next.Domain.Models.FieldSource.Xtf
                            || src == AuswertungPro.Next.Domain.Models.FieldSource.Xtf405;
            if (!isXtfLink)
                return new VideoFindResult(VideoMatchStatus.NotFound, null, Array.Empty<string>(), "Link source is not XTF/M150/MDB");
        }

        var link = (record.GetFieldValue("Link") ?? "").Trim();
        if (string.IsNullOrWhiteSpace(link) || !HasVideoExtension(link))
            return new VideoFindResult(VideoMatchStatus.NotFound, null, Array.Empty<string>(), "No usable video link");

        if (File.Exists(link))
            return new VideoFindResult(VideoMatchStatus.Matched, link, Array.Empty<string>(), "Matched by existing Link path");

        var linkFile = Path.GetFileName(link);
        if (string.IsNullOrWhiteSpace(linkFile))
            return new VideoFindResult(VideoMatchStatus.NotFound, null, Array.Empty<string>(), "Link filename missing");

        return FindVideo(linkFile, videoSourceFolder, haltung, dateStamp, recursiveVideoSearch, videoFilesCache);
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

    private static string? TryFindFilmName(string text, Regex filmRx)
    {
        var filmMatch = filmRx.Match(text);
        if (filmMatch.Success)
            return NormalizeVideoFileName(filmMatch.Groups[1].Value);

        // Fallback: any token with common video extension
        var extRx = new Regex(@"\b([A-Za-z0-9_\-\.]+?\.(?:mp2|mpg|mpeg|mp4|avi|mov|wmv|mkv))\b", RegexOptions.IgnoreCase);
        var extMatch = extRx.Match(text);
        if (extMatch.Success)
            return NormalizeVideoFileName(extMatch.Groups[1].Value);

        // Fallback: line with "Film" or "Video" -> take next non-empty token
        var lines = text.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (!line.Contains("Film", StringComparison.OrdinalIgnoreCase) &&
                !line.Contains("Video", StringComparison.OrdinalIgnoreCase))
                continue;

            var tokens = Tokenize(line);
            var candidate = tokens.FirstOrDefault(t => HasVideoExtension(t));
            if (!string.IsNullOrWhiteSpace(candidate))
                return NormalizeVideoFileName(candidate);

            if (i + 1 < lines.Length)
            {
                var nextTokens = Tokenize(lines[i + 1]);
                var nextCandidate = nextTokens.FirstOrDefault(t => HasVideoExtension(t));
                if (!string.IsNullOrWhiteSpace(nextCandidate))
                    return NormalizeVideoFileName(nextCandidate);
            }
        }

        return null;
    }

    private static readonly Regex PhotoAfterLabelRegex = new(
        @"Foto\s*:\s*(?<name>\d{1,5}_\d{1,5}_\d{1,7}_[A-Za-z](?:\.(?:jpe?g|png|bmp|tif|tiff))?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex PhotoTokenRegex = new(
        @"(?<![A-Za-z0-9])(?<name>\d{1,5}_\d{1,5}_\d{1,7}_[A-Za-z](?:\.(?:jpe?g|png|bmp|tif|tiff))?)(?![A-Za-z])",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static IReadOnlyList<string> ExtractPhotoHintsFromPdf(string pdfPath)
    {
        if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
            return Array.Empty<string>();

        const int maxPagesWithLabeledHints = 2;
        var labeledPhotoKeys = new List<string>();
        var genericPhotoKeys = new List<string>();
        IReadOnlyList<PageInfo> pages;
        try
        {
            pages = ReadPdfPages(pdfPath);
        }
        catch
        {
            return Array.Empty<string>();
        }

        var labeledPages = 0;
        foreach (var page in pages)
        {
            var labeledCountBefore = labeledPhotoKeys.Count;
            foreach (Match m in PhotoAfterLabelRegex.Matches(page.Text))
            {
                AddPhotoLookupKeys(m.Groups["name"].Value, labeledPhotoKeys);
            }

            if (labeledPhotoKeys.Count > labeledCountBefore)
            {
                labeledPages++;
                if (labeledPages >= maxPagesWithLabeledHints)
                    break;
            }

            foreach (Match m in PhotoTokenRegex.Matches(page.Text))
            {
                AddPhotoLookupKeys(m.Groups["name"].Value, genericPhotoKeys);
            }
        }

        if (labeledPhotoKeys.Count > 0)
            return labeledPhotoKeys;

        return genericPhotoKeys;
    }

    private static void AddPhotoLookupKeys(string? raw, List<string> keys)
    {
        foreach (var key in EnumeratePhotoLookupKeys(raw))
        {
            if (!keys.Contains(key, StringComparer.OrdinalIgnoreCase))
                keys.Add(key);
        }
    }

    private static IEnumerable<string> EnumeratePhotoLookupKeys(string? raw)
    {
        var fileName = NormalizeVideoFileName(raw);
        if (string.IsNullOrWhiteSpace(fileName))
            yield break;

        var noExt = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        var hasImageExt = HasImageExtension(fileName);

        if (hasImageExt)
            yield return fileName;

        if (!string.IsNullOrWhiteSpace(noExt))
            yield return noExt;

        var normalizedNoExt = NormalizePhotoToken(noExt);
        if (string.IsNullOrWhiteSpace(normalizedNoExt))
            yield break;

        if (!string.Equals(normalizedNoExt, noExt, StringComparison.OrdinalIgnoreCase))
            yield return normalizedNoExt;

        if (hasImageExt && !string.IsNullOrWhiteSpace(ext))
            yield return $"{normalizedNoExt}{ext}";
    }

    private static string TrimLeadingZerosValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        var trimmed = value.TrimStart('0');
        return string.IsNullOrEmpty(trimmed) ? "0" : trimmed;
    }

    private static string? NormalizePhotoToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        var m = Regex.Match(token, @"(?<a>\d{1,5})_(?<b>\d{1,5})_(?<c>\d{1,7})_(?<d>[A-Za-z])");
        if (!m.Success)
            return null;

        static string TrimLeadingZeros(string value)
        {
            var trimmed = value.TrimStart('0');
            return string.IsNullOrEmpty(trimmed) ? "0" : trimmed;
        }

        var a = TrimLeadingZeros(m.Groups["a"].Value);
        var b = TrimLeadingZeros(m.Groups["b"].Value);
        var c = TrimLeadingZeros(m.Groups["c"].Value);
        var d = char.ToUpperInvariant(m.Groups["d"].Value[0]);
        return $"{a}_{b}_{c}_{d}";
    }

    private static IReadOnlyList<string> Tokenize(string line)
        => line.Split(new[] { ' ', '\t', ';', ',', ':' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim()).ToList();

    private static bool HasVideoExtension(string token)
    {
        var normalized = NormalizeVideoFileName(token);
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        var ext = Path.GetExtension(normalized);
        if (string.IsNullOrWhiteSpace(ext)) return false;
        var e = ext.ToLowerInvariant();
        return e is ".mp2" or ".mpg" or ".mpeg" or ".mp4" or ".avi" or ".mov" or ".wmv" or ".mkv";
    }

    private static bool HasImageExtension(string token)
    {
        var normalized = NormalizeVideoFileName(token);
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        var ext = Path.GetExtension(normalized);
        if (string.IsNullOrWhiteSpace(ext))
            return false;

        var e = ext.ToLowerInvariant();
        return e is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".tif" or ".tiff";
    }

    private static string? NormalizeVideoFileName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var candidate = value.Trim().Trim('"', '\'');
        candidate = candidate.TrimEnd('.', ',', ';', ':', ')', ']', '}', '>');
        if (string.IsNullOrWhiteSpace(candidate))
            return null;

        candidate = candidate.Replace('\\', '/');
        var fileName = Path.GetFileName(candidate).Trim();
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        return fileName.Trim('"', '\'');
    }

    private static string SanitizePathSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (invalid.Contains(ch))
                sb.Append('_');
            else
                sb.Append(ch);
        }
        var cleaned = sb.ToString().Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "UNKNOWN" : cleaned;
    }

    private static string NormalizeHaltungId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "UNKNOWN";

        var text = NormalizeText(value).Trim();
        // Extract pair pattern: XXXXX-XXXXX or XX.XXXX-XX.XXXX
        var pairRx = new Regex(@"((?:\d{2,}\.\d{2,}|\d{4,})\s*[-]\s*(?:\d{2,}\.\d{2,}|\d{4,}))");
        var m = pairRx.Match(text);
        if (m.Success)
        {
            var normalized = m.Groups[1].Value.Replace(" ", "").Replace("/", "-");
            // Ensure exactly one dash
            normalized = Regex.Replace(normalized, @"\s*-+\s*", "-");
            return normalized;
        }

        return text;
    }

    private static string NormalizeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch))
                sb.Append(char.ToLowerInvariant(ch));
        }
        return sb.ToString();
    }

    private static bool IsValidHaltungId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = value.Trim();
        var rx = new Regex(@"^(?:\d{2,}\.\d{2,}|\d{4,})\s*-\s*(?:\d{2,}\.\d{2,}|\d{4,})$");
        if (!rx.IsMatch(normalized))
            return false;

        var parts = normalized.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
            return false;

        // Reject common OCR glue artifacts such as "04.201423022-215987" (date fragment + id).
        foreach (var part in parts)
        {
            if (Regex.IsMatch(part, @"^\d{2}\.20\d{2}\d+$"))
                return false;
        }

        return true;
    }

    private static string EnsureUniquePath(string path, bool overwrite)
    {
        if (overwrite || !File.Exists(path))
            return path;

        var dir = Path.GetDirectoryName(path) ?? "";
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        for (var i = 1; i < 1000; i++)
        {
            var candidate = Path.Combine(dir, $"{name}_{i.ToString("00", CultureInfo.InvariantCulture)}{ext}");
            if (!File.Exists(candidate))
                return candidate;
        }

        throw new IOException($"Unable to find free filename for {path}");
    }

#if DEMO
    public static class DemoProgram
    {
        public static void Main()
        {
            var results = Distribute(
                pdfSourceFolder: @"D:\Input\PDFs",
                videoSourceFolder: @"D:\Input\Videos",
                destGemeindeFolder: @"D:\Bauwerke\Haltungen\Buerglen",
                moveInsteadOfCopy: false,
                overwrite: false,
                recursiveVideoSearch: true,
                unmatchedFolderName: "__UNMATCHED",
                project: null,
                progress: null);

            foreach (var r in results)
                Console.WriteLine($"{(r.Success ? "OK" : "FAIL")} - {r.Message} - {r.SourcePdfPath}");
        }
    }
#endif
}

