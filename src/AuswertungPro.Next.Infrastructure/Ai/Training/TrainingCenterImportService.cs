using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AuswertungPro.Next.Infrastructure.Common;
using AuswertungPro.Next.Infrastructure.Import.Pdf;
using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

public sealed class TrainingCenterImportService
{
    private static readonly string[] VideoExts = [..AuswertungPro.Next.Infrastructure.Media.MediaFileTypes.VideoExtensions, ".ts", ".m4v"];
    private static readonly string[] ProtocolExts = [".json", ".xml", ".pdf"];

    public Task<List<TrainingCase>> ScanAsync(string rootFolder)
    {
        if (string.IsNullOrWhiteSpace(rootFolder) || !Directory.Exists(rootFolder))
            return Task.FromResult(new List<TrainingCase>());

        var folders = EnumerateFolders(rootFolder);

        var cases = new List<TrainingCase>();

        foreach (var folder in folders)
        {
            try
            {
                var files = Directory.EnumerateFiles(folder, "*.*", SearchOption.TopDirectoryOnly).ToList();
                if (files.Count == 0)
                    continue;

                var videos = files.Where(f => VideoExts.Contains(Path.GetExtension(f).ToLowerInvariant())).ToList();
                var protos = files.Where(f => ProtocolExts.Contains(Path.GetExtension(f).ToLowerInvariant())).ToList();

                // Ohne Video UND ohne Protokoll: ueberspringen
                if (videos.Count == 0 && protos.Count == 0)
                    continue;

                var caseId = SafeRelativeId(rootFolder, folder);
                var bestVideo = videos.Count > 0 ? PickBestVideo(videos, caseId) : "";
                var bestProto = protos.Count > 0 ? PickBestProtocol(protos) ?? "" : "";

                cases.Add(new TrainingCase
                {
                    CaseId = caseId,
                    FolderPath = folder,
                    VideoPath = bestVideo,
                    ProtocolPath = bestProto,
                    Status = TrainingCaseStatus.New,
                    CreatedUtc = DateTime.UtcNow
                });
            }
            catch
            {
                // ignore folder errors
            }
        }

        // Stable ordering for UI
        cases = cases.OrderBy(c => c.CaseId, StringComparer.OrdinalIgnoreCase).ToList();
        return Task.FromResult(cases);
    }

    /// <summary>
    /// Scannt nur nach Protokollen (PDF/JSON), Video ist nicht erforderlich.
    /// Fuer den reinen Protokoll-Import ohne Videoanalyse.
    /// </summary>
    public Task<List<TrainingCase>> ScanProtocolOnlyAsync(string rootFolder)
    {
        if (string.IsNullOrWhiteSpace(rootFolder) || !Directory.Exists(rootFolder))
            return Task.FromResult(new List<TrainingCase>());

        var folders = EnumerateFolders(rootFolder);
        var cases = new List<TrainingCase>();

        foreach (var folder in folders)
        {
            try
            {
                var files = Directory.EnumerateFiles(folder, "*.*", SearchOption.TopDirectoryOnly).ToList();
                if (files.Count == 0) continue;

                var protos = files.Where(f => ProtocolExts.Contains(Path.GetExtension(f).ToLowerInvariant())).ToList();
                if (protos.Count == 0) continue;

                var videos = files.Where(f => VideoExts.Contains(Path.GetExtension(f).ToLowerInvariant())).ToList();
                var caseId = SafeRelativeId(rootFolder, folder);
                var bestVideo = videos.Count > 0 ? PickBestVideo(videos, caseId) : "";

                var proto = PickBestProtocol(protos);
                if (proto is null) continue; // Nur Non-Protocol-Dateien → ueberspringen

                cases.Add(new TrainingCase
                {
                    CaseId = caseId,
                    FolderPath = folder,
                    VideoPath = bestVideo,
                    ProtocolPath = proto,
                    Status = TrainingCaseStatus.New,
                    CreatedUtc = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                // Phase 1.2: Empty-catch-Sweep — Debug-Log statt stilles Schlucken.
                // Bei Import-Fehler wird der Case sonst stillschweigend uebergangen.
                Debug.WriteLine($"[TrainingCenterImport] Case-Import {folder}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        cases = cases.OrderBy(c => c.CaseId, StringComparer.OrdinalIgnoreCase).ToList();
        return Task.FromResult(cases);
    }

    /// <summary>
    /// Waehlt das beste Video aus mehreren Kandidaten.
    /// Prio: 1. CaseId im Namen, 2. Groesstes (laengstes) Video, 3. Grafik-Videos ausschliessen.
    /// </summary>
    private static string PickBestVideo(List<string> videos, string caseId)
    {
        if (videos.Count == 1)
            return videos[0];

        var nameNoExt = (string p) => Path.GetFileNameWithoutExtension(p).ToLowerInvariant();
        var nameWithExt = (string p) => Path.GetFileName(p).ToLowerInvariant();
        var caseIdLower = caseId.ToLowerInvariant().Replace("/", "").Replace("\\", "");

        // Grafik-Videos und Uebersichten ausschliessen (Matching auf voller Dateiname MIT Extension)
        var filtered = videos
            .Where(v => !VideoExcludePatterns.Any(pat => nameWithExt(v).Contains(pat)))
            .ToList();
        // Kein Fallback auf ausgeschlossene Videos — leere Liste wird vom Aufrufer behandelt
        if (filtered.Count == 0) return "";

        // 1. Prio: Video dessen Name die CaseId enthaelt
        var caseMatch = filtered.FirstOrDefault(v => nameNoExt(v).Contains(caseIdLower));
        if (caseMatch is not null) return caseMatch;

        // 2. Prio: Groesstes Video (korreliert mit Laenge, da Bitrate aehnlich)
        return filtered.Select(p => new FileInfo(p))
            .OrderByDescending(fi => fi.Length)
            .First().FullName;
    }

    private static IEnumerable<string> EnumerateFolders(string rootFolder)
    {
        yield return rootFolder;

        foreach (var dir in Directory.EnumerateDirectories(rootFolder, "*", SearchOption.AllDirectories))
            yield return dir;
    }

    // Dateinamen-Muster fuer Protokoll-PDFs (bevorzugt)
    private static readonly string[] ProtocolKeywords =
        ["protokoll", "haltung", "inspektion", "zustandsbericht", "bericht"];

    // Dateinamen-Muster die KEINE Inspektionsprotokolle sind
    // V4.3: " dp" und "-dp" zusaetzlich zu "_dp" wegen Filename-Varianten ("X DP.pdf" mit Space).
    private static readonly string[] NonProtocolKeywords =
        ["plan", "situationsplan", "_dp", " dp", "-dp", "dichtheit", "luftpr",
         "lageplan", "uebersicht", "übersicht"];

    // Video-Dateinamen die ausgeschlossen werden (Grafik-Videos, Uebersichten)
    // Hinweis: Matching auf Dateiname MIT Extension (ToLowerInvariant)
    private static readonly string[] VideoExcludePatterns = ["_g.mp", "_g.avi", "_g.ts", "_g.mkv", "_g.m4v", "uebersicht", "übersicht"];

    /// <summary>
    /// Waehlt das beste Protokoll. Gibt null zurueck wenn nur Non-Protocol-Dateien vorhanden.
    /// </summary>
    private static string? PickBestProtocol(List<string> protos)
    {
        // JSON hat hoechste Prio (strukturiert)
        var json = protos.FirstOrDefault(p => Path.GetExtension(p).Equals(".json", StringComparison.OrdinalIgnoreCase));
        if (json is not null) return json;

        // PDFs: Protokoll-Keywords bevorzugen, Non-Protocol ausschliessen
        var pdfs = protos.Where(p => Path.GetExtension(p).Equals(".pdf", StringComparison.OrdinalIgnoreCase)).ToList();
        if (pdfs.Count > 0)
        {
            var name = (string p) => Path.GetFileNameWithoutExtension(p).ToLowerInvariant();

            // 1. Prio: Dateiname enthaelt Protokoll-Keyword
            var protocolPdf = pdfs.FirstOrDefault(p => ProtocolKeywords.Any(k => name(p).Contains(k)));
            if (protocolPdf is not null) return protocolPdf;

            // 2. Prio: Dateiname ist KEIN bekanntes Non-Protocol
            var nonExcluded = pdfs.Where(p => !NonProtocolKeywords.Any(k => name(p).Contains(k))).ToList();
            if (nonExcluded.Count > 0)
            {
                // Groesstes PDF unter den verbleibenden (wahrscheinlich das Protokoll)
                return nonExcluded.OrderByDescending(p => new FileInfo(p).Length).First();
            }

            // Nur Non-Protocol-PDFs vorhanden → kein echtes Protokoll
            return null;
        }

        // XML als letzter Fallback
        return protos.FirstOrDefault(p => Path.GetExtension(p).Equals(".xml", StringComparison.OrdinalIgnoreCase));
    }

    // ── Haltungs-Verteilung ─────────────────────────────────────────────────
    // Teilt ein Multi-Haltungs-PDF in einzelne Ordner auf und ordnet Videos zu.

    /// <summary>
    /// Regex zum Extrahieren einer Haltungs-ID aus einem Dateinamen.
    /// Erkennt z.B. "H_42046-41412.mpg" → "42046-41412"
    /// </summary>
    private static readonly Regex HaltungIdInFilename = new(
        @"(?<id>\d[\d\.]*[-/]\d[\d\.]*)",
        RegexOptions.Compiled);

    public sealed record DistributeResult(
        int TotalChunks,
        int Distributed,
        int VideosMatched,
        int Uncertain,
        string OutputFolder,
        List<string> Messages);

    /// <summary>
    /// Verteilt ein Multi-Haltungs-PDF + Video-Ordner in einzelne Unterordner.
    /// Pro Haltung wird ein Ordner mit JSON-Protokoll und Video-Verweis erstellt.
    /// </summary>
    public Task<DistributeResult> DistributeByHaltungAsync(
        string pdfPath, string videoFolder, string outputFolder)
    {
        var messages = new List<string>();

        // 1. Text aus PDF extrahieren (seitenweise)
        PdfTextExtraction extraction;
        try
        {
            extraction = PdfTextExtractor.ExtractPages(pdfPath);
        }
        catch (Exception ex)
        {
            messages.Add($"PDF-Text konnte nicht extrahiert werden: {ex.Message}");
            return Task.FromResult(new DistributeResult(0, 0, 0, 0, outputFolder, messages));
        }

        if (extraction.Pages.Count == 0)
        {
            messages.Add("Kein Text im PDF gefunden.");
            return Task.FromResult(new DistributeResult(0, 0, 0, 0, outputFolder, messages));
        }

        // 2. PDF nach Haltungen aufteilen
        var parser = new PdfParser();
        var chunks = PdfChunking.SplitIntoHaltungChunks(extraction.Pages, parser);

        if (chunks.Count == 0)
        {
            messages.Add("Keine Haltungen im PDF erkannt.");
            return Task.FromResult(new DistributeResult(0, 0, 0, 0, outputFolder, messages));
        }

        // 3. Video-Index aufbauen: Haltungs-ID → Videodatei
        var videoIndex = BuildVideoIndex(videoFolder);

        // 4. Pro Haltung einen Ordner erstellen
        Directory.CreateDirectory(outputFolder);
        int distributed = 0;
        int videosMatched = 0;
        int uncertain = 0;

        foreach (var chunk in chunks)
        {
            if (string.IsNullOrWhiteSpace(chunk.DetectedId) || chunk.IsUncertain)
            {
                uncertain++;
                messages.Add($"Chunk {chunk.Index} (Seiten {chunk.PageRange}): keine Haltungs-ID erkannt, uebersprungen.");
                continue;
            }

            var haltungId = chunk.DetectedId;
            var safeId = Regex.Replace(haltungId, @"[^\w\-\.]", "_");
            var caseDir = Path.Combine(outputFolder, safeId);
            Directory.CreateDirectory(caseDir);

            // JSON-Protokoll schreiben (Format kompatibel mit PdfProtocolExtractor.ExtractFromJson)
            var entries = ExtractEntriesFromChunkText(chunk.Text);
            var jsonPath = Path.Combine(caseDir, $"{safeId}_protokoll.json");
            WriteProtocolJson(jsonPath, entries, haltungId, chunk.PageRange);

            // Video zuordnen
            string? videoPath = null;
            var normalizedId = NormalizeId(haltungId);
            if (videoIndex.TryGetValue(normalizedId, out var matchedVideo))
            {
                // Hardlink oder Kopie erstellen
                var videoTarget = Path.Combine(caseDir, Path.GetFileName(matchedVideo));
                if (!File.Exists(videoTarget))
                {
                    try
                    {
                        File.CreateSymbolicLink(videoTarget, matchedVideo);
                    }
                    catch
                    {
                        // Fallback: Pfad-Datei schreiben (Windows Symlinks brauchen Adminrechte)
                        File.WriteAllText(videoTarget + ".link", matchedVideo);
                        videoTarget = matchedVideo; // Original-Pfad verwenden
                    }
                }
                videoPath = videoTarget;
                videosMatched++;
            }
            else
            {
                messages.Add($"Haltung {haltungId}: kein Video gefunden.");
            }

            distributed++;
            messages.Add($"Haltung {haltungId}: Seiten {chunk.PageRange}, "
                + $"{entries.Count} Beobachtungen"
                + (videoPath is not null ? $", Video: {Path.GetFileName(videoPath)}" : ""));
        }

        return Task.FromResult(new DistributeResult(
            chunks.Count, distributed, videosMatched, uncertain, outputFolder, messages));
    }

    /// <summary>
    /// Erstellt einen Index: normalisierte Haltungs-ID → Videodatei-Pfad
    /// </summary>
    private static Dictionary<string, string> BuildVideoIndex(string videoFolder)
    {
        var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(videoFolder) || !Directory.Exists(videoFolder))
            return index;

        var videoExts = new HashSet<string>(
            AuswertungPro.Next.Infrastructure.Media.MediaFileTypes.VideoExtensions,
            StringComparer.OrdinalIgnoreCase)
        { ".ts", ".m4v" };

        // Audit 2026-05-17 (Nachzieh): SafeFileEnumeration ueberspringt gesperrte Unterordner.
        foreach (var file in SafeFileEnumeration.EnumerateFilesSafe(videoFolder, "*.*", recursive: true))
        {
            if (!videoExts.Contains(Path.GetExtension(file)))
                continue;

            var name = Path.GetFileNameWithoutExtension(file);
            var m = HaltungIdInFilename.Match(name);
            if (m.Success)
            {
                var id = NormalizeId(m.Groups["id"].Value);
                index.TryAdd(id, file);
            }
        }

        return index;
    }

    private static string NormalizeId(string id)
        => (id ?? "").Trim().Replace(" ", "").Replace("/", "-");

    /// <summary>
    /// Extrahiert Beobachtungen aus dem Chunk-Text (Fretz-Format + Standard).
    /// </summary>
    private static List<ProtocolEntry> ExtractEntriesFromChunkText(string text)
    {
        var entries = new List<ProtocolEntry>();
        if (string.IsNullOrWhiteSpace(text))
            return entries;

        // Fretz-Format: "[Foto?] [HH:MM:SS] [Meter] [Code] [Beschreibung]"
        var fretzRx = new Regex(
            @"^\s*(?:\d{1,5}\s+)?(?:\d{2}:\d{2}:\d{2}\s+)?(?<meter>\d{1,4}[.,]\d{1,3})\s+(?<code>[A-Z]{2,6}(?:\.[A-Z]{1,2})*)\s+(?<text>.+?)(?:\s{2,}|$)",
            RegexOptions.Multiline);

        foreach (Match m in fretzRx.Matches(text))
        {
            var code = m.Groups["code"].Value.Trim();
            var desc = m.Groups["text"].Value.Trim();
            if (double.TryParse(m.Groups["meter"].Value.Replace(',', '.'),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var meter))
            {
                entries.Add(new ProtocolEntry(code, desc, meter));
            }
        }

        return entries;
    }

    private sealed record ProtocolEntry(string Code, string Beschreibung, double MeterStart);

    private static void WriteProtocolJson(string path, List<ProtocolEntry> entries, string haltungId, string pageRange)
    {
        // Format kompatibel mit PdfProtocolExtractor.ExtractFromJson:
        // { "Current": { "Entries": [ { "Code": "BCD", "Beschreibung": "...", "MeterStart": 0.0 } ] } }
        var jsonEntries = entries.Select(e => new Dictionary<string, object>
        {
            ["Code"] = e.Code,
            ["Beschreibung"] = e.Beschreibung,
            ["MeterStart"] = e.MeterStart,
            ["MeterEnd"] = e.MeterStart,
            ["IsStreckenschaden"] = false,
            ["IsDeleted"] = false
        }).ToArray();

        var root = new Dictionary<string, object>
        {
            ["HaltungId"] = haltungId,
            ["PageRange"] = pageRange,
            ["Current"] = new Dictionary<string, object>
            {
                ["Entries"] = jsonEntries
            }
        };

        var json = JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private static string SafeRelativeId(string root, string folder)
    {
        try
        {
            var rel = Path.GetRelativePath(root, folder);
            if (string.IsNullOrWhiteSpace(rel) || rel == ".")
                return new DirectoryInfo(folder).Name;

            // Normalize slashes
            rel = rel.Replace('\\', '/');
            return rel;
        }
        catch
        {
            return new DirectoryInfo(folder).Name;
        }
    }
}
