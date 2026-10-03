using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.VsaCatalog;
using AuswertungPro.Next.Infrastructure.HoldingDistribution;
using AuswertungPro.Next.Infrastructure.Import.Pdf;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

public sealed class TrainingCenterImportService
{
    private static readonly string[] VideoExts = [..AuswertungPro.Next.Infrastructure.Media.MediaFileTypes.VideoExtensions, ".ts", ".m4v"];
    private static readonly string[] ProtocolExts = [".json", ".xml", ".pdf"];

    private readonly Func<string, PdfTextExtraction> _pdfSeitenLesen;
    private readonly Func<string, IEnumerable<string>> _dateienImOrdner;
    private readonly Action<string>? _nachHaltungsordner;

    public TrainingCenterImportService()
        : this(null, null, null)
    {
    }

    /// <summary>
    /// Testnaht: PDF-Leser, Dateiliste je Ordner und ein Haken nach jedem fertig angelegten
    /// Haltungsordner der Verteilung. Ohne Angabe gelten die echten Dateizugriffe.
    /// </summary>
    internal TrainingCenterImportService(
        Func<string, PdfTextExtraction>? pdfSeitenLesen,
        Func<string, IEnumerable<string>>? dateienImOrdner,
        Action<string>? nachHaltungsordner)
    {
        _pdfSeitenLesen = pdfSeitenLesen ?? (pfad => PdfTextExtractor.ExtractPages(pfad));
        _dateienImOrdner = dateienImOrdner
                           ?? (ordner => Directory.EnumerateFiles(ordner, "*.*", SearchOption.TopDirectoryOnly));
        _nachHaltungsordner = nachHaltungsordner;
    }

    public Task<List<TrainingCaseInput>> ScanAsync(string rootFolder)
        => ScanAsync(rootFolder, null, CancellationToken.None);

    /// <summary>
    /// Sucht Trainingsfaelle unter <paramref name="rootFolder"/>. Laeuft ausserhalb des
    /// aufrufenden Threads (Deepscan R6: das Training Center fror beim Scan grosser Ablagen ein);
    /// der Abbruch wird vor jedem Ordner geprueft. Nicht lesbare oder verknuepfte Ordner landen
    /// in <paramref name="uebersprungeneOrdner"/> statt still zu fehlen (Deepscan R8).
    /// </summary>
    public Task<List<TrainingCaseInput>> ScanAsync(
        string rootFolder,
        ICollection<string>? uebersprungeneOrdner,
        CancellationToken cancellationToken)
        => ScanAsync(rootFolder, uebersprungeneOrdner, null, cancellationToken);

    /// <summary>
    /// Wie oben; zusaetzlich landen Hinweise zu ungueltigen oder unlesbaren Videoverweisen
    /// (<c>&lt;Video&gt;.link</c>) in <paramref name="hinweise"/> (PR #85).
    /// </summary>
    public Task<List<TrainingCaseInput>> ScanAsync(
        string rootFolder,
        ICollection<string>? uebersprungeneOrdner,
        ICollection<string>? hinweise,
        CancellationToken cancellationToken)
        => Task.Run(() => Scan(rootFolder, uebersprungeneOrdner, hinweise, cancellationToken), cancellationToken);

    private List<TrainingCaseInput> Scan(
        string rootFolder,
        ICollection<string>? uebersprungeneOrdner,
        ICollection<string>? hinweise,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rootFolder) || !Directory.Exists(rootFolder))
            return new List<TrainingCaseInput>();

        var folders = EnumerateFolders(rootFolder, uebersprungeneOrdner);

        var cases = new List<TrainingCaseInput>();

        foreach (var folder in folders)
        {
            // Ausserhalb des try: der Fang fuer Ordnerfehler darf den Abbruch nicht verschlucken.
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var files = _dateienImOrdner(folder).ToList();
                if (files.Count == 0)
                    continue;

                var videos = files.Where(f => VideoExts.Contains(Path.GetExtension(f).ToLowerInvariant())).ToList();
                var protos = files.Where(f => ProtocolExts.Contains(Path.GetExtension(f).ToLowerInvariant())).ToList();

                // Ein echtes Video im Ordner hat Vorrang; sonst gelten die Videoverweise der Verteilung.
                if (videos.Count == 0)
                    videos = LoeseVideoverweiseAuf(files, hinweise);

                // Ohne Video UND ohne Protokoll: ueberspringen
                if (videos.Count == 0 && protos.Count == 0)
                    continue;

                var caseId = SafeRelativeId(rootFolder, folder);
                var (bestVideo, bestProto) = ResolvePair(videos, protos, caseId);
                var inspectionDate = ResolveInspectionDate(folder, bestProto, bestVideo);

                cases.Add(new TrainingCaseInput(
                    CaseId: caseId,
                    FolderPath: folder,
                    VideoPath: bestVideo,
                    ProtocolPath: bestProto,
                    InspectionDate: inspectionDate));
            }
            catch (Exception ex)
            {
                // Deepscan R8: Ein nicht lesbarer Ordner liefert keinen Trainingsfall. Er fehlt nicht mehr
                // still, sondern wird gesammelt und vom Training Center im Protokoll benannt.
                uebersprungeneOrdner?.Add(folder);
                System.Diagnostics.Trace.WriteLine(
                    $"[TrainingCenterImport] Ordner uebersprungen: {folder}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        // Stable ordering for UI
        return cases.OrderBy(c => c.CaseId, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Loest die Videoverweise <c>&lt;name&gt;.&lt;videoendung&gt;.link</c> der Haltungsverteilung nur lesend
    /// zum Originalvideo auf (PR #85: seit Deepscan R6 gibt es keinen Symlink mehr). Der Inhalt ist eine
    /// Zeile mit absolutem Pfad; uebernommen wird er nur mit Videoendung und vorhandener Datei. Sonst
    /// bleibt der Fall ohne Video, und der Verweis steht in <paramref name="hinweise"/>.
    /// </summary>
    private static List<string> LoeseVideoverweiseAuf(IEnumerable<string> files, ICollection<string>? hinweise)
    {
        var videos = new List<string>();
        foreach (var verweis in files.Where(IstVideoverweis))
        {
            string[] zeilen;
            try
            {
                zeilen = File.ReadAllLines(verweis)
                    .Where(zeile => !string.IsNullOrWhiteSpace(zeile))
                    .Select(zeile => zeile.Trim())
                    .ToArray();
            }
            catch (Exception ex) when (ex is IOException
                                       or UnauthorizedAccessException
                                       or System.Security.SecurityException)
            {
                // Lesefehler werden gemeldet, nicht verschluckt; der Fall bleibt ohne Video.
                MeldeVideoverweis(hinweise, $"Videoverweis «{verweis}» nicht lesbar: {UserError.Describe(ex)}", ex);
                continue;
            }

            var ziel = zeilen.Length == 1 ? zeilen[0] : null;
            if (ziel is null
                || !Path.IsPathFullyQualified(ziel)
                || !VideoExts.Contains(Path.GetExtension(ziel).ToLowerInvariant())
                || !File.Exists(ziel))
            {
                MeldeVideoverweis(
                    hinweise,
                    $"Videoverweis «{verweis}» zeigt auf kein vorhandenes Video; Fall ohne Video geladen.",
                    null);
                continue;
            }

            videos.Add(ziel);
        }

        return videos;
    }

    private static bool IstVideoverweis(string pfad)
        => pfad.EndsWith(".link", StringComparison.OrdinalIgnoreCase)
           && VideoExts.Contains(Path.GetExtension(Path.GetFileNameWithoutExtension(pfad)).ToLowerInvariant());

    private static void MeldeVideoverweis(ICollection<string>? hinweise, string meldung, Exception? ex)
    {
        hinweise?.Add(meldung);
        System.Diagnostics.Trace.WriteLine(
            $"[TrainingCenterImport] {meldung}" + (ex is null ? "" : $" ({ex.GetType().Name}: {ex.Message})"));
    }

    /// <summary>
    /// Scannt nur nach Protokollen (PDF/JSON), Video ist nicht erforderlich.
    /// Fuer den reinen Protokoll-Import ohne Videoanalyse.
    /// </summary>
    public Task<List<TrainingCaseInput>> ScanProtocolOnlyAsync(string rootFolder)
    {
        if (string.IsNullOrWhiteSpace(rootFolder) || !Directory.Exists(rootFolder))
            return Task.FromResult(new List<TrainingCaseInput>());

        var folders = EnumerateFolders(rootFolder);
        var cases = new List<TrainingCaseInput>();

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
                var (bestVideo, proto) = ResolveProtocolOnlyPair(videos, protos, caseId);
                var inspectionDate = ResolveInspectionDate(folder, proto, bestVideo);
                if (string.IsNullOrWhiteSpace(proto)) continue; // Nur Non-Protocol-Dateien -> ueberspringen

                cases.Add(new TrainingCaseInput(
                    CaseId: caseId,
                    FolderPath: folder,
                    VideoPath: bestVideo,
                    ProtocolPath: proto,
                    InspectionDate: inspectionDate));
            }
            catch (Exception ex)
            {
                // Frueher still verschluckt -> stiller Verlust von Trainingsfaellen. Jetzt sichtbar
                // (mit Ordnerpfad), damit nachvollziehbar ist, WELCHER Ordner und WARUM uebersprungen wurde.
                System.Diagnostics.Trace.WriteLine(
                    $"[TrainingCenterImport] Ordner uebersprungen: {folder}: {ex.GetType().Name}: {ex.Message}");
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

    private static DateTime? ResolveInspectionDate(string folder, string protocolPath, string videoPath)
    {
        foreach (var candidate in EnumerateDateCandidates(folder, protocolPath, videoPath))
        {
            var parsed = TrainingSampleEligibility.TryParseInspectionDate(candidate);
            if (parsed is not null)
                return parsed.Value;
        }

        return null;
    }

    private static IEnumerable<string> EnumerateDateCandidates(string folder, string protocolPath, string videoPath)
    {
        yield return Path.GetFileName(folder);
        yield return folder;

        if (!string.IsNullOrWhiteSpace(protocolPath))
        {
            yield return Path.GetFileName(protocolPath);
            foreach (var line in ReadTextDateCandidates(protocolPath))
                yield return line;
        }

        if (!string.IsNullOrWhiteSpace(videoPath))
            yield return Path.GetFileName(videoPath);
    }

    private static IEnumerable<string> ReadTextDateCandidates(string path)
    {
        var ext = Path.GetExtension(path);
        if (!string.Equals(ext, ".json", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(ext, ".xml", StringComparison.OrdinalIgnoreCase))
        {
            yield break;
        }

        foreach (var line in File.ReadLines(path).Take(200))
        {
            if (line.Contains("datum", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("date", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("aufnahme", StringComparison.OrdinalIgnoreCase))
            {
                yield return line;
            }
        }
    }

    private static IEnumerable<string> EnumerateFolders(
        string rootFolder,
        ICollection<string>? uebersprungeneOrdner = null)
    {
        // Root + alle erreichbaren Unterordner (gesperrte und verknuepfte werden uebersprungen statt zu
        // werfen und, falls der Aufrufer eine Liste mitgibt, dort eingetragen).
        foreach (var dir in AuswertungPro.Next.Infrastructure.Common.SafeFileEnumeration.EnumerateDirectoriesSafe(
                     rootFolder,
                     uebersprungeneOrdner))
            yield return dir;
    }

    // Dateinamen-Muster fuer Protokoll-PDFs (bevorzugt)
    private static readonly string[] ProtocolKeywords =
        ["protokoll", "haltung", "inspektion", "zustandsbericht", "bericht"];

    // Dateinamen-Muster die KEINE Inspektionsprotokolle sind
    private static readonly string[] NonProtocolKeywords =
        ["plan", "situationsplan", "_dp", "lageplan", "uebersicht", "übersicht"];

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

    internal static (string VideoPath, string ProtocolPath) ResolvePair(
        IReadOnlyList<string> videos,
        IReadOnlyList<string> protos,
        string caseId)
    {
        return ResolvePairCore(videos, protos, caseId, preserveProtocolOnConflict: false);
    }

    internal static (string VideoPath, string ProtocolPath) ResolveProtocolOnlyPair(
        IReadOnlyList<string> videos,
        IReadOnlyList<string> protos,
        string caseId)
    {
        return ResolvePairCore(videos, protos, caseId, preserveProtocolOnConflict: true);
    }

    private static (string VideoPath, string ProtocolPath) ResolvePairCore(
        IReadOnlyList<string> videos,
        IReadOnlyList<string> protos,
        string caseId,
        bool preserveProtocolOnConflict)
    {
        var videoList = videos.ToList();
        var protoList = protos.ToList();

        var bestVideo = videoList.Count > 0 ? PickBestVideo(videoList, caseId) : "";
        var bestProto = protoList.Count > 0 ? PickBestProtocol(protoList) ?? "" : "";

        if (videoList.Count <= 1 && protoList.Count <= 1)
        {
            return preserveProtocolOnConflict
                ? DropContradiction(bestVideo, bestProto, caseId, preserveProtocolOnConflict: true)
                : (bestVideo, bestProto);
        }

        var caseKey = EvalContaminationGuard.NormalizeHaltungKey(caseId);
        var matchingVideo = PickVideoByHaltungKey(videoList, caseKey, caseId);
        if (!string.IsNullOrWhiteSpace(matchingVideo))
        {
            bestVideo = matchingVideo;
        }
        else if (videoList.Count > 1)
        {
            // Mehrere Videos ohne eindeutigen Haltungs-Treffer sind unsicher.
            // Lieber kein Video verwenden als das groesste falsche Video koppeln.
            bestVideo = "";
        }

        var matchingProto = PickProtocolByHaltungKey(protoList, caseKey);
        if (!string.IsNullOrWhiteSpace(matchingProto))
            bestProto = matchingProto;

        return DropContradiction(bestVideo, bestProto, caseId, preserveProtocolOnConflict);
    }

    private static string PickVideoByHaltungKey(List<string> videos, string? caseKey, string caseId)
    {
        if (string.IsNullOrWhiteSpace(caseKey))
            return "";

        var matches = videos
            .Where(v => string.Equals(NormalizeFileHaltungKey(v), caseKey, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return matches.Count == 0 ? "" : PickBestVideo(matches, caseId);
    }

    private static string PickProtocolByHaltungKey(List<string> protos, string? caseKey)
    {
        if (string.IsNullOrWhiteSpace(caseKey))
            return "";

        var matches = protos
            .Where(p => string.Equals(NormalizeFileHaltungKey(p), caseKey, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return matches.Count == 0 ? "" : PickBestProtocol(matches) ?? "";
    }

    private static (string VideoPath, string ProtocolPath) DropContradiction(
        string videoPath,
        string protocolPath,
        string caseId,
        bool preserveProtocolOnConflict)
    {
        if (string.IsNullOrWhiteSpace(videoPath) || string.IsNullOrWhiteSpace(protocolPath))
            return (videoPath, protocolPath);

        var videoKey = NormalizeFileHaltungKey(videoPath);
        var protocolKey = NormalizeFileHaltungKey(protocolPath);
        if (videoKey is null || protocolKey is null)
            return (videoPath, protocolPath);

        if (string.Equals(videoKey, protocolKey, StringComparison.OrdinalIgnoreCase))
            return (videoPath, protocolPath);

        if (preserveProtocolOnConflict)
            return ("", protocolPath);

        var caseKey = EvalContaminationGuard.NormalizeHaltungKey(caseId);
        var videoMatchesCase = caseKey is not null
            && string.Equals(videoKey, caseKey, StringComparison.OrdinalIgnoreCase);
        var protocolMatchesCase = caseKey is not null
            && string.Equals(protocolKey, caseKey, StringComparison.OrdinalIgnoreCase);

        if (videoMatchesCase && !protocolMatchesCase)
            return (videoPath, "");
        if (protocolMatchesCase && !videoMatchesCase)
            return ("", protocolPath);

        return (videoPath, "");
    }

    private static string? NormalizeFileHaltungKey(string path)
    {
        return EvalContaminationGuard.NormalizeHaltungKey(Path.GetFileNameWithoutExtension(path));
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
    /// Laeuft ausserhalb des aufrufenden Threads (Deepscan R6); der Abbruch wird vor dem
    /// PDF-Lesen und vor jeder Haltung geprueft und wirft <see cref="OperationCanceledException"/>.
    /// </summary>
    public Task<DistributeResult> DistributeByHaltungAsync(
        string pdfPath, string videoFolder, string outputFolder, CancellationToken cancellationToken = default)
        => Task.Run(() => DistributeByHaltung(pdfPath, videoFolder, outputFolder, cancellationToken), cancellationToken);

    private DistributeResult DistributeByHaltung(
        string pdfPath, string videoFolder, string outputFolder, CancellationToken cancellationToken)
    {
        var messages = new List<string>();
        cancellationToken.ThrowIfCancellationRequested();

        // Deepscan R6: Der Ausgabeordner liegt neben der Kundenablage. Vor jedem Schreiben gilt der
        // gemeinsame Verteil-Pfadwaechter; ein verknuepfter Ausgabeordner wird ohne Schreiben abgelehnt.
        // PR #85: Der Waechter prueft nur ab dem Ausgabeordner abwaerts. Liegt das PDF unter einer
        // Verknuepfung, laege auch der abgeleitete Ausgabeordner darin; deshalb zuerst der ganze Pfad
        // bis zum Laufwerk (gemeinsamer VerknuepfungsSchutz, fehlender Rest erlaubt, Lesefehler sperren).
        var pfadBefund = VerknuepfungsSchutz.PruefePfadAbLaufwerk(outputFolder, VerknuepfungsRegel.GanzerPfad);
        if (!pfadBefund.IstSicher)
        {
            messages.Add($"Ausgabeordner «{outputFolder}» wird nicht beschrieben: «{pfadBefund.Pfad}» im Pfad ist "
                         + (pfadBefund.Befund == VerknuepfungsBefund.Verknuepfung
                             ? "eine Verknüpfung (Junction)."
                             : "nicht sicher prüfbar (keine Verknüpfung nachweisbar)."));
            return new DistributeResult(0, 0, 0, 0, outputFolder, messages);
        }

        DistributionWritePathGuard writePaths;
        try
        {
            writePaths = new DistributionWritePathGuard(outputFolder);
        }
        catch (Exception ex) when (IstPfadwaechterAblehnung(ex))
        {
            messages.Add($"Ausgabeordner «{outputFolder}» wird nicht beschrieben: "
                         + "Er ist eine Verknüpfung (Junction) oder nicht sicher prüfbar.");
            return new DistributeResult(0, 0, 0, 0, outputFolder, messages);
        }

        // 1. Text aus PDF extrahieren (seitenweise)
        PdfTextExtraction extraction;
        try
        {
            extraction = _pdfSeitenLesen(pdfPath);
        }
        catch (Exception ex)
        {
            messages.Add($"PDF-Text konnte nicht extrahiert werden: {ex.Message}");
            return new DistributeResult(0, 0, 0, 0, outputFolder, messages);
        }

        if (extraction.Pages.Count == 0)
        {
            messages.Add("Kein Text im PDF gefunden.");
            return new DistributeResult(0, 0, 0, 0, outputFolder, messages);
        }

        // 2. PDF nach Haltungen aufteilen
        var parser = new PdfParser();
        var chunks = PdfChunking.SplitIntoHaltungChunks(extraction.Pages, parser);

        if (chunks.Count == 0)
        {
            messages.Add("Keine Haltungen im PDF erkannt.");
            return new DistributeResult(0, 0, 0, 0, outputFolder, messages);
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
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(chunk.DetectedId) || chunk.IsUncertain)
            {
                uncertain++;
                messages.Add($"Chunk {chunk.Index} (Seiten {chunk.PageRange}): keine Haltungs-ID erkannt, übersprungen.");
                continue;
            }

            var haltungId = chunk.DetectedId;
            var safeId = Regex.Replace(haltungId, @"[^\w\-\.]", "_");
            var caseDir = Path.Combine(outputFolder, safeId);
            var jsonPath = Path.Combine(caseDir, $"{safeId}_protokoll.json");
            if (!IstSicheresZiel(writePaths, caseDir) || !IstSicheresZiel(writePaths, jsonPath))
            {
                messages.Add($"Haltung {haltungId}: Zielordner ist eine Verknüpfung oder nicht sicher prüfbar, nicht beschrieben.");
                continue;
            }

            Directory.CreateDirectory(caseDir);

            // JSON-Protokoll schreiben (Format kompatibel mit PdfProtocolExtractor.ExtractFromJson)
            var entries = ExtractEntriesFromChunkText(chunk.Text);
            WriteProtocolJson(jsonPath, entries, haltungId, chunk.PageRange);

            // Video zuordnen
            string? videoPath = null;
            var normalizedId = NormalizeId(haltungId);
            if (videoIndex.TryGetValue(normalizedId, out var matchedVideo))
            {
                // Deepscan R6: keine symbolische Verknuepfung mehr; der Verweis auf das Originalvideo
                // steht immer in einer .link-Datei (frueher nur der Rueckfall ohne Adminrechte).
                // PR #85: Gezaehlt und gemeldet wird ein Video nur mit geschriebenem Verweis oder einem
                // bereits vorhandenen Video aus frueheren Laeufen.
                var videoTarget = Path.Combine(caseDir, Path.GetFileName(matchedVideo));
                var linkPath = videoTarget + ".link";
                if (File.Exists(videoTarget))
                {
                    videoPath = videoTarget;
                }
                else if (IstSicheresZiel(writePaths, linkPath))
                {
                    AtomicTextFileWriter.WriteAllText(linkPath, matchedVideo);
                    videoPath = matchedVideo; // Original-Pfad verwenden
                }
                else
                {
                    messages.Add($"Haltung {haltungId}: Videoverweis ist eine Verknüpfung, nicht beschrieben.");
                }

                if (videoPath is not null)
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
            _nachHaltungsordner?.Invoke(caseDir);
        }

        return new DistributeResult(
            chunks.Count, distributed, videosMatched, uncertain, outputFolder, messages);
    }

    /// <summary>
    /// Prueft ein Schreibziel der Verteilung mit dem Verteil-Pfadwaechter. Eine Verknuepfung
    /// (oder ein nicht pruefbares Glied) im Ziel oder darueber bis zum Ausgabeordner sperrt.
    /// </summary>
    private static bool IstSicheresZiel(DistributionWritePathGuard writePaths, string path)
    {
        try
        {
            writePaths.EnsureFileTarget(path);
            return true;
        }
        catch (Exception ex) when (IstPfadwaechterAblehnung(ex))
        {
            return false;
        }
    }

    private static bool IstPfadwaechterAblehnung(Exception ex)
        => ex is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException
            or System.Security.SecurityException;

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

        foreach (var file in AuswertungPro.Next.Infrastructure.Common.SafeFileEnumeration.EnumerateFilesSafe(videoFolder, "*.*", recursive: true))
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
    /// Unbekannte Codes werden am Parse-Eintritt verworfen, damit PDF-Freitext
    /// nicht als Trainingslabel in den Batch gelangt.
    /// </summary>
    internal static List<ProtocolEntry> ExtractEntriesFromChunkText(string text)
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
            if (!VsaCodeValidator.IsKnownCode(code))
                continue;

            var desc = m.Groups["text"].Value.Trim();
            if (double.TryParse(m.Groups["meter"].Value.Replace(',', '.'),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var meter))
            {
                entries.Add(new ProtocolEntry(code.Replace(".", "").ToUpperInvariant(), desc, meter));
            }
        }

        return entries;
    }

    internal sealed record ProtocolEntry(string Code, string Beschreibung, double MeterStart);

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

        var json = JsonSerializer.Serialize(root, JsonDefaults.Indented);
        AtomicTextFileWriter.WriteAllText(path, json);
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
