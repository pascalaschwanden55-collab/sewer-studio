// AuswertungPro – Einmaliger Orchestrator fuer das initiale YOLO-Training
// Verarbeitet alle ~2031 Haltungen in D:\Haltungen, extrahiert Ground-Truth
// aus PDFs, ordnet Video-Frames zu und generiert ein YOLO-Trainings-Dataset.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai.Training.Models;
using Microsoft.Extensions.Logging;
using AuswertungPro.Next.Application.Ai.Pipeline;

namespace AuswertungPro.Next.UI.Ai.Training.Services;

/// <summary>
/// Einmaliger Orchestrator: Scannt alle Haltungs-Verzeichnisse,
/// parst PDF-Protokolle, loest Frames auf und exportiert ein YOLO-Dataset.
/// Fuer nachfolgende Retraining-Laeufe ist <see cref="AuswertungPro.Next.UI.Ai.Training.YoloRetrainOrchestrator"/> zustaendig.
/// </summary>
public sealed class InitialTrainingOrchestrator
{
    /// <summary>Mindestanzahl Frames damit ein Training gestartet wird.</summary>
    private const int MinFramesForTraining = 100;

    private static readonly string[] VideoExtensions = [".mpg", ".mp4", ".mpeg", ".avi"];
    private static readonly string[] PdfExtensions = [".pdf"];

    private readonly MeterTimelineService _meterTimeline;
    private readonly VisionPipelineClient _sidecar;
    private readonly string _ffmpegPath;
    private readonly ILogger? _log;

    public InitialTrainingOrchestrator(
        MeterTimelineService meterTimeline,
        VisionPipelineClient sidecar,
        string ffmpegPath,
        ILogger? logger = null)
    {
        _meterTimeline = meterTimeline ?? throw new ArgumentNullException(nameof(meterTimeline));
        _sidecar = sidecar ?? throw new ArgumentNullException(nameof(sidecar));
        _ffmpegPath = ffmpegPath ?? throw new ArgumentNullException(nameof(ffmpegPath));
        _log = logger;
    }

    /// <summary>
    /// Fuehrt den kompletten initialen Training-Workflow durch.
    /// </summary>
    public async Task<InitialTrainingResult> RunAsync(
        string haltungenRoot,
        string datasetOutputDir,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(haltungenRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(datasetOutputDir);

        var sw = Stopwatch.StartNew();
        var result = new InitialTrainingResult();

        // 1. Haltungs-Verzeichnisse entdecken
        progress?.Report("Scanne Haltungs-Verzeichnisse...");
        var haltungen = DiscoverHaltungen(haltungenRoot);
        result.TotalHaltungen = haltungen.Count;
        _log?.LogInformation("Initiales Training: {Count} Haltungen gefunden in {Root}",
            haltungen.Count, haltungenRoot);

        if (haltungen.Count == 0)
        {
            progress?.Report("Keine Haltungen gefunden. Abbruch.");
            return result;
        }

        // 2. Pro Haltung: PDF parsen → GroundTruth → Frames aufloesen
        var allMappings = new List<(GroundTruthEntry Entry, string FramePath)>();
        var videoProbe = new VideoProbeService();
        var frameResolver = new MeterToFrameResolver(_meterTimeline, _log);

        for (int i = 0; i < haltungen.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var (dir, videoPath, pdfPath) = haltungen[i];
            var dirName = Path.GetFileName(dir);
            progress?.Report($"[{i + 1}/{haltungen.Count}] {dirName}");

            try
            {
                var mappings = await ProcessHaltungAsync(
                    dir, videoPath, pdfPath, datasetOutputDir,
                    videoProbe, frameResolver, ct).ConfigureAwait(false);

                if (mappings.Count > 0)
                {
                    allMappings.AddRange(mappings);
                    result.Processed++;
                    _log?.LogDebug("Haltung {Dir}: {Count} Frame-Mappings", dirName, mappings.Count);
                }
                else
                {
                    result.Skipped++;
                    _log?.LogDebug("Haltung {Dir}: uebersprungen (keine gueltigen Mappings)", dirName);
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                result.Failed++;
                _log?.LogWarning(ex, "Haltung {Dir}: Fehler bei Verarbeitung", dirName);
            }
        }

        progress?.Report($"Frame-Extraktion abgeschlossen: {allMappings.Count} Frames aus {result.Processed} Haltungen");

        // 3. YOLO-Dataset exportieren
        if (allMappings.Count > 0)
        {
            progress?.Report("Exportiere YOLO-Dataset...");
            result.DatasetStats = await YoloAnnotationGenerator.ExportDatasetAsync(
                allMappings, datasetOutputDir, trainSplit: 0.8, ct).ConfigureAwait(false);

            _log?.LogInformation(
                "Dataset exportiert: {Train} Train, {Val} Val, {Skipped} uebersprungen",
                result.DatasetStats.Train, result.DatasetStats.Val, result.DatasetStats.Skipped);
        }

        // 4. Training starten wenn genuegend Frames vorhanden
        if (allMappings.Count >= MinFramesForTraining)
        {
            progress?.Report("Starte YOLO-Training auf Sidecar...");
            try
            {
                var trainRequest = new YoloTrainRequestDto(
                    DatasetPath: datasetOutputDir,
                    Epochs: 50,
                    ImageSize: 640,
                    Batch: -1,
                    BaseModel: "yolo11m.pt");

                var trainJob = await _sidecar.StartYoloTrainingAsync(trainRequest, ct)
                    .ConfigureAwait(false);

                result.TrainingStarted = true;
                result.TrainingJobId = trainJob.JobId;

                _log?.LogInformation("YOLO-Training gestartet: JobId={JobId}", trainJob.JobId);
                progress?.Report($"Training gestartet (Job: {trainJob.JobId})");
            }
            catch (Exception ex)
            {
                _log?.LogError(ex, "Fehler beim Starten des YOLO-Trainings");
                progress?.Report($"Training-Start fehlgeschlagen: {ex.Message}");
            }
        }
        else
        {
            _log?.LogWarning(
                "Zu wenige Frames ({Count}) fuer Training (Minimum: {Min})",
                allMappings.Count, MinFramesForTraining);
            progress?.Report($"Nur {allMappings.Count} Frames — Training erfordert mindestens {MinFramesForTraining}");
        }

        sw.Stop();
        result.ElapsedSeconds = sw.Elapsed.TotalSeconds;
        progress?.Report(
            $"Fertig in {sw.Elapsed:hh\\:mm\\:ss} — " +
            $"{result.Processed} verarbeitet, {result.Skipped} uebersprungen, {result.Failed} fehlgeschlagen");

        return result;
    }

    // ═══ Haltungs-Verzeichnisse entdecken ═══════════════════════════════

    /// <summary>
    /// Scannt alle Unterverzeichnisse nach Video+PDF-Paaren.
    /// Pro Verzeichnis wird das neueste Video und das neueste PDF gewaehlt.
    /// </summary>
    private static List<(string Dir, string VideoPath, string PdfPath)> DiscoverHaltungen(string root)
    {
        var result = new List<(string, string, string)>();

        if (!Directory.Exists(root))
            return result;

        foreach (var dir in Directory.EnumerateDirectories(root))
        {
            var newestVideo = FindNewestFile(dir, VideoExtensions);
            var newestPdf = FindNewestFile(dir, PdfExtensions);

            if (newestVideo is not null && newestPdf is not null)
                result.Add((dir, newestVideo, newestPdf));
        }

        return result;
    }

    /// <summary>Findet die neueste Datei mit einer der angegebenen Endungen.</summary>
    private static string? FindNewestFile(string dir, string[] extensions)
    {
        return Directory.EnumerateFiles(dir)
            .Where(f => extensions.Any(ext =>
                f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    // ═══ Einzelne Haltung verarbeiten ════════════════════════════════════

    private async Task<List<(GroundTruthEntry Entry, string FramePath)>> ProcessHaltungAsync(
        string dir,
        string videoPath,
        string pdfPath,
        string datasetOutputDir,
        VideoProbeService videoProbe,
        MeterToFrameResolver frameResolver,
        CancellationToken ct)
    {
        // PDF parsen
        var parseResult = PdfProtocolTableParser.Parse(pdfPath, _log);
        if (!parseResult.HasEntries)
        {
            _log?.LogDebug("PDF ohne Eintraege: {Path}", pdfPath);
            return [];
        }

        // ProtocolDocument bauen (ProtocolToGroundTruthMapper erwartet ein ProtocolDocument)
        var doc = new ProtocolDocument
        {
            HaltungId = Path.GetFileName(dir),
            Original = new ProtocolRevision
            {
                Entries = parseResult.Entries
            }
        };

        // GroundTruth mappen
        var groundTruth = ProtocolToGroundTruthMapper.Map(
            doc,
            rohrmaterial: parseResult.Stammdaten.Rohrmaterial,
            nennweiteMm: parseResult.Stammdaten.NennweiteMm);

        if (groundTruth.Count == 0)
            return [];

        // Videodauer ermitteln
        var probeResult = await videoProbe.ProbeAsync(videoPath, ct).ConfigureAwait(false);
        if (!probeResult.Success || probeResult.DurationSeconds <= 0)
        {
            _log?.LogWarning("Videodauer nicht ermittelbar: {Path} — {Error}",
                videoPath, probeResult.Error);
            return [];
        }

        // Inspektionslaenge bestimmen (aus Stammdaten oder letztem Meterstand)
        var inspektionslaenge = parseResult.Stammdaten.HaltungslaengeMeter
            ?? groundTruth.Max(gt => gt.MeterEnd);

        if (inspektionslaenge <= 0)
            inspektionslaenge = groundTruth.Max(gt => gt.MeterEnd);

        // Frame-Output-Verzeichnis pro Haltung
        var frameOutputDir = Path.Combine(datasetOutputDir, "_frames", Path.GetFileName(dir));

        // Frames aufloesen
        var frameMappings = await frameResolver.ResolveAllAsync(
            videoPath,
            probeResult.DurationSeconds,
            inspektionslaenge,
            groundTruth,
            frameOutputDir,
            centeringOffsetMeter: 0.3,
            ct).ConfigureAwait(false);

        // Nur Mappings mit tatsaechlich extrahiertem Frame zurueckgeben
        return frameMappings
            .Where(fm => fm.FramePath is not null && File.Exists(fm.FramePath))
            .Select(fm => (fm.Entry, fm.FramePath!))
            .ToList();
    }
}

/// <summary>Ergebnis des initialen Trainings-Workflows.</summary>
public sealed class InitialTrainingResult
{
    /// <summary>Anzahl gefundener Haltungs-Verzeichnisse.</summary>
    public int TotalHaltungen { get; set; }

    /// <summary>Erfolgreich verarbeitete Haltungen (mindestens 1 Frame).</summary>
    public int Processed { get; set; }

    /// <summary>Uebersprungene Haltungen (kein PDF, keine Eintraege, etc.).</summary>
    public int Skipped { get; set; }

    /// <summary>Fehlgeschlagene Haltungen (Exception).</summary>
    public int Failed { get; set; }

    /// <summary>Dataset-Statistik (null wenn kein Export erfolgt).</summary>
    public YoloAnnotationGenerator.DatasetStats? DatasetStats { get; set; }

    /// <summary>True wenn ein Training-Job gestartet wurde.</summary>
    public bool TrainingStarted { get; set; }

    /// <summary>Job-ID des gestarteten Trainings (null wenn keins gestartet).</summary>
    public string? TrainingJobId { get; set; }

    /// <summary>Gesamtlaufzeit in Sekunden.</summary>
    public double ElapsedSeconds { get; set; }
}
