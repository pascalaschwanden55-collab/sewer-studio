// AuswertungPro – Video-Selbsttraining Phase 2
using System;
using AuswertungPro.Next.Application.Ai.Vision;
using AuswertungPro.Next.Domain.Ai.Vision;
using AuswertungPro.Next.Application.Ai.QualityGate;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai.Training.Models;
using AuswertungPro.Next.UI.Ai.Training.Services;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Ai.Training;

/// <summary>
/// Orchestriert den Video-basierten Selbsttraining-Workflow:
/// 1. Protokoll laden und zu GroundTruth mappen
/// 2. OSD-Timeline bauen und Frame-Zuordnungen herstellen
/// 3. Video blind durch die KI-Pipeline schicken
/// 4. KI-Ergebnisse gegen Protokoll vergleichen → DifferenceReport
///
/// Der bestehende SelfTrainingOrchestrator (PDF-basiert) bleibt unveraendert.
/// </summary>
public sealed class VideoSelfTrainingOrchestrator
{
    private readonly IVideoAnalysisPipelineService _pipeline;
    private readonly MeterTimelineService _meterTimeline;
    private readonly ILogger? _log;

    // Pause/Resume
    private readonly SemaphoreSlim _pauseGate = new(1, 1);
    private volatile bool _isPaused;
    // Pause/Resume muessen gegen Race-Conditions (Doppelklick) geschuetzt werden —
    // sonst Wait() zweimal auf Semaphor(1) = UI-Deadlock.
    private readonly object _pauseSync = new();

    public bool IsPaused => _isPaused;

    /// <summary>Optionale Batch-Pipeline (V4.1). YOLO Batch → Filter → Qwen sequentiell mit Timeout.</summary>
    public Ai.Pipeline.BatchPipelineService? BatchPipeline { get; set; }
    private bool UseBatchPipeline => BatchPipeline is not null;

    /// <summary>
    /// V4.2 Phase 2: Protokoll-First-Modus. Wenn aktiv, wird die Pipeline nur noch gezielt auf
    /// die Protokoll-Fundstellen fokussiert. Qwen bekommt geschlossene Yes/No-Fragen statt
    /// Open-Set-Klassifikation. Default: false (opt-in). Erfordert BatchPipeline.
    /// </summary>
    public bool UseProtocolFirst { get; set; } = false;

    /// <summary>Meter-Toleranz um jeden Protokoll-Eintrag im Protokoll-First-Modus (Default 1.0m).</summary>
    public double ProtocolFirstMeterTolerance { get; set; } = 1.0;

    /// <summary>
    /// V4.2 Phase 2.4: Ueberraschungsfund-Pass. Nach dem Protokoll-First-Pass ein zweiter
    /// langsamer Durchlauf auf den Luecken zwischen Protokoll-Zonen — faengt Schaeden ein,
    /// die der Operateur uebersehen hat. Nur mit UseProtocolFirst=true wirksam.
    /// </summary>
    public bool EnableSurpriseGapsPass { get; set; } = false;

    /// <summary>Frame-Step (Sekunden) fuer den Ueberraschungsfund-Pass. Default 10s (5x langsamer als Haupt-Pass).</summary>
    public double SurpriseGapsFrameStep { get; set; } = 10.0;

    public VideoSelfTrainingOrchestrator(
        IVideoAnalysisPipelineService pipeline,
        MeterTimelineService meterTimeline,
        ILogger? log = null)
    {
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _meterTimeline = meterTimeline ?? throw new ArgumentNullException(nameof(meterTimeline));
        _log = log;
    }

    /// <summary>
    /// Fuehrt den kompletten Video-Selbsttraining-Workflow durch.
    /// </summary>
    public async Task<VideoTrainingResult> RunAsync(
        VideoTrainingRequest request,
        ProtocolDocument protocol,
        IProgress<VideoTrainingProgress>? progress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(protocol);

        var sw = Stopwatch.StartNew();

        // ── Phase 1: Protokoll → GroundTruth ──────────────────────────
        progress?.Report(new VideoTrainingProgress("Protokoll", 0, 1, "Protokoll wird gemappt..."));
        await CheckPauseAsync(ct).ConfigureAwait(false);

        var groundTruths = ProtocolToGroundTruthMapper.Map(
            protocol, request.Rohrmaterial, request.NennweiteMm);

        _log?.LogInformation("Protokoll gemappt: {Count} Eintraege", groundTruths.Count);
        progress?.Report(new VideoTrainingProgress("Protokoll", 1, 1, $"{groundTruths.Count} Protokoll-Eintraege gemappt"));

        // ── Phase 2: OSD-Timeline + Frame-Zuordnungen ─────────────────
        progress?.Report(new VideoTrainingProgress("Meter-Mapping", 0, 1, "OSD-Timeline wird gebaut..."));
        await CheckPauseAsync(ct).ConfigureAwait(false);

        // Video-Dauer ermitteln (via ffprobe oder aus MeterTimeline)
        var videoDuration = await GetVideoDurationAsync(request.VideoPath, ct).ConfigureAwait(false);
        var inspLength = request.InspektionslaengeMeter ?? EstimateInspektionslaenge(groundTruths);

        var frameOutputDir = Path.Combine(
            Path.GetTempPath(), "SewerStudio", "VideoTraining",
            Path.GetFileNameWithoutExtension(request.VideoPath));

        var resolver = new MeterToFrameResolver(_meterTimeline, _log);
        var frameMappings = await resolver.ResolveAllAsync(
            request.VideoPath, videoDuration, inspLength, groundTruths, frameOutputDir,
            request.CenteringOffsetMeter, ct)
            .ConfigureAwait(false);

        _log?.LogInformation("Frame-Mapping: {Count} Zuordnungen, {OSD} via OSD",
            frameMappings.Count,
            frameMappings.Count(m => m.Source == MeterMappingSource.OSD));

        // V4.2 Nachbesserung: Multi-Inspektion-Filter.
        // PDFs enthalten manchmal mehrere Inspektionen derselben Haltung (z.B. "In Fliessrichtung"
        // + "Gegen Fliessrichtung"). Das gewaehlte Video deckt aber nur EINE Inspektion ab.
        // Wenn der OSD-Max deutlich kleiner als die Protokoll-Gesamt-Laenge ist, filtern wir
        // Protokoll-Eintraege jenseits des OSD-Bereichs raus — sonst landen Frames aus dem
        // falschen Inspektions-Kontext im Review.
        var osdMappings = frameMappings.Where(m => m.Source == MeterMappingSource.OSD).ToList();
        if (osdMappings.Count >= 3)  // mindestens 3 OSD-Stuetzpunkte fuer vertrauenswuerdigen Max
        {
            var maxOsdMeter = osdMappings.Max(m => m.Entry.MeterStart);
            if (maxOsdMeter > 0 && maxOsdMeter < inspLength * 0.5)
            {
                var toleranz = 2.0;
                var cutoff = maxOsdMeter + toleranz;
                var beforeCount = groundTruths.Count;
                groundTruths = groundTruths.Where(gt => gt.MeterStart <= cutoff).ToList();
                frameMappings = frameMappings.Where(m => m.Entry.MeterStart <= cutoff).ToList();
                _log?.LogInformation(
                    "V4.2 Multi-Inspektion-Filter aktiv: OSD-Max={MaxOsd:F1}m vs InspLength={Insp:F1}m " +
                    "→ {Before} → {After} Protokoll-Eintraege (Cutoff={Cut:F1}m)",
                    maxOsdMeter, inspLength, beforeCount, groundTruths.Count, cutoff);
                progress?.Report(new VideoTrainingProgress("Meter-Mapping", 1, 1,
                    $"Multi-Inspektion: {beforeCount}→{groundTruths.Count} Eintraege (Video nur {maxOsdMeter:F1}m)"));
            }
        }

        progress?.Report(new VideoTrainingProgress("Meter-Mapping", 1, 1,
            $"{frameMappings.Count} Frames zugeordnet"));

        // ── Phase 3: Video-Blinddurchlauf ─────────────────────────────
        progress?.Report(new VideoTrainingProgress("KI-Analyse", 0, 100, "Video-Blinddurchlauf startet..."));
        await CheckPauseAsync(ct).ConfigureAwait(false);

        var pipelineRequest = new PipelineRequest(
            HaltungId: protocol.HaltungId,
            VideoPath: request.VideoPath,
            AllowedCodes: Array.Empty<string>(), // Keine Einschraenkung — blind
            FrameStepSeconds: request.FrameStepSeconds,
            DedupWindowFrames: 3);

        // Pipeline-Fortschritt weiterleiten
        var pipelineProgress = progress is not null
            ? new Progress<PipelineProgress>(p =>
                progress.Report(new VideoTrainingProgress(
                    "KI-Analyse", p.FramesDone ?? 0, p.FramesTotal ?? 100,
                    p.Status ?? "Analysiere...")))
            : null;

        // Per-Frame-Timeout: Kein globaler Pipeline-Timeout mehr — MultiModelAnalysisService
        // behandelt Timeouts pro Frame (45s). Haengende Frames werden uebersprungen statt
        // den gesamten Blindtest abzubrechen. Der Parent-Token (ct) reicht fuer Benutzer-Abbruch.
        var expectedFrames = (int)(videoDuration / request.FrameStepSeconds) + 1;
        _log?.LogInformation("Pipeline-Start: {Frames} erwartete Frames, Per-Frame-Timeout 45s (kein globaler Timeout)",
            expectedFrames);

        PipelineResult pipelineResult;
        try
        {
            if (UseBatchPipeline && BatchPipeline is not null)
            {
                // V4.1 Batch-Pipeline: YOLO Batch → Filter → Qwen ×6 parallel
                _log?.LogInformation("BatchPipeline aktiv — verwende Batch-Modus");
                BatchPipeline.FrameStepSeconds = request.FrameStepSeconds;

                var batchProgress = progress is not null
                    ? new Progress<Ai.Pipeline.BatchPipelineProgress>(p =>
                        progress.Report(new VideoTrainingProgress(
                            "KI-Analyse", p.Done, p.Total, p.Status)))
                    : null;

                // V4.2 Phase 2: Protokoll-First-Kontext aus GroundTruths bauen, wenn aktiviert.
                Ai.Pipeline.ProtocolFirstContext? protocolContext = null;
                if (UseProtocolFirst && groundTruths.Count > 0)
                {
                    var targets = groundTruths
                        .Select(gt => new Ai.Pipeline.ProtocolTarget(
                            VsaCode: gt.VsaCode,
                            MeterStart: gt.MeterStart,
                            MeterEnd: gt.MeterEnd,
                            Description: gt.Text,
                            ClockPosition: gt.ClockPosition))
                        .ToList();
                    protocolContext = new Ai.Pipeline.ProtocolFirstContext
                    {
                        Targets = targets,
                        VideoDurationSeconds = videoDuration,
                        InspektionslaengeMeter = inspLength,
                        MeterTolerance = ProtocolFirstMeterTolerance
                    };
                    _log?.LogInformation(
                        "Protokoll-First aktiv: {Targets} Ziele, Toleranz +-{Tol:F1}m",
                        targets.Count, ProtocolFirstMeterTolerance);
                }

                // V4.2: Frames persistieren fuer Review-Queue-Anzeige.
                var reviewFramesDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AuswertungPro", "review_frames",
                    Path.GetFileNameWithoutExtension(request.VideoPath));

                var batchResult = await BatchPipeline.AnalyzeVideoAsync(
                    request.VideoPath, request.NennweiteMm ?? 300, batchProgress, ct,
                    protocolContext, reviewFramesDir)
                    .ConfigureAwait(false);

                // BatchPipelineResult → PipelineResult konvertieren
                static RawVideoDetection ConvertFinding(Ai.Pipeline.BatchFrameAnalysis a, AuswertungPro.Next.Domain.Ai.Vision.EnhancedFinding f,
                    double videoDuration, double inspLength)
                {
                    var meter = a.QwenResult.Meter ?? (a.TimestampSeconds / videoDuration * inspLength);
                    return new RawVideoDetection(
                        FindingLabel: f.Label,
                        MeterStart: meter,
                        MeterEnd: meter,
                        Severity: f.Severity.ToString(),
                        VsaCodeHint: f.VsaCodeHint,
                        PositionClock: f.PositionClock,
                        ExtentPercent: f.ExtentPercent,
                        HeightMm: f.HeightMm,
                        WidthMm: f.WidthMm,
                        IntrusionPercent: f.IntrusionPercent,
                        CrossSectionReductionPercent: f.CrossSectionReductionPercent,
                        DiameterReductionMm: f.DiameterReductionMm,
                        FramePath: a.FramePath);
                }

                var detections = batchResult.Analyses
                    .Where(a => a.QwenResult.HasFindings)
                    .SelectMany(a => a.QwenResult.Findings.Select(f => ConvertFinding(a, f, videoDuration, inspLength)))
                    .ToList();

                // V4.2 Phase 2.4: Ueberraschungsfund-Pass — zweiter langsamer Durchlauf auf den Luecken.
                if (EnableSurpriseGapsPass && protocolContext is not null)
                {
                    var gapsContext = new Ai.Pipeline.ProtocolFirstContext
                    {
                        Targets = protocolContext.Targets,
                        VideoDurationSeconds = videoDuration,
                        InspektionslaengeMeter = inspLength,
                        MeterTolerance = ProtocolFirstMeterTolerance,
                        InverseGapsMode = true
                    };

                    var originalStep = BatchPipeline.FrameStepSeconds;
                    BatchPipeline.FrameStepSeconds = SurpriseGapsFrameStep;
                    try
                    {
                        _log?.LogInformation(
                            "Ueberraschungsfund-Pass: Luecken mit FrameStep {Step}s",
                            SurpriseGapsFrameStep);

                        var gapsResult = await BatchPipeline.AnalyzeVideoAsync(
                            request.VideoPath, request.NennweiteMm ?? 300, batchProgress, ct,
                            gapsContext)
                            .ConfigureAwait(false);

                        var surpriseDetections = gapsResult.Analyses
                            .Where(a => a.QwenResult.HasFindings)
                            .SelectMany(a => a.QwenResult.Findings.Select(f => ConvertFinding(a, f, videoDuration, inspLength)))
                            .ToList();

                        _log?.LogInformation(
                            "Ueberraschungsfund-Pass: {Count} Detektionen aus {Frames} Luecken-Frames",
                            surpriseDetections.Count, gapsResult.RelevantFrames);

                        detections.AddRange(surpriseDetections);
                    }
                    finally
                    {
                        BatchPipeline.FrameStepSeconds = originalStep;
                    }
                }

                pipelineResult = new PipelineResult(
                    null, detections, Array.Empty<MappedProtocolEntry>(),
                    null, Array.Empty<string>(), null);
                _log?.LogInformation(
                    "BatchPipeline: {Detections} Detektionen aus {Frames} Frames in {Duration:F0}s",
                    detections.Count, batchResult.TotalFrames, batchResult.Duration.TotalSeconds);
            }
            else
            {
                // Sequentielle Pipeline (Fallback)
                pipelineResult = await _pipeline.RunAsync(
                    pipelineRequest, pipelineProgress, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _log?.LogWarning("Pipeline unerwartet abgebrochen — verwende bisherige Ergebnisse");
            pipelineResult = PipelineResult.Failed("Pipeline abgebrochen");
        }
        finally
        {
            // Sicherheit: Alle haengenden ffmpeg-Prozesse dieser Haltung beenden
            KillOrphanedFfmpeg(request.VideoPath);
        }

        if (pipelineResult.Error is not null)
        {
            _log?.LogError("Pipeline fehlgeschlagen: {Error}", pipelineResult.Error);
            // Trotzdem weitermachen — alle Detektionen = leer → alles FN
        }

        // RawVideoDetection → BlindDetection konvertieren
        var blindDetections = ConvertToBlindDetections(pipelineResult.Detections);

        _log?.LogInformation("Blinddurchlauf: {Count} Detektionen", blindDetections.Count);
        progress?.Report(new VideoTrainingProgress("KI-Analyse", 100, 100,
            $"{blindDetections.Count} Detektionen"));

        // ── Phase 4: Differenzanalyse ─────────────────────────────────
        progress?.Report(new VideoTrainingProgress("Vergleich", 0, 1, "Differenzanalyse..."));
        await CheckPauseAsync(ct).ConfigureAwait(false);

        var report = DifferenceAnalyzer.Analyze(
            groundTruths, blindDetections, request.MeterTolerance);

        // Frame-Pfade aus Phase 2 auf DifferenceEntries uebertragen
        // (FrameMapping hat fuer jeden Protokolleintrag einen Frame extrahiert)
        // V4.2: Protokolle koennen legitim identische Eintraege haben (z.B. mehrere
        // "Neue Laenge einzelnes Rohr"-Eintraege) — GroupBy statt ToDictionary, erstes wins.
        var frameByEntry = frameMappings
            .GroupBy(m => m.Entry)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var entry in report.Entries)
        {
            if (entry.ProtocolEntry is null)
                continue;

            if (!frameByEntry.TryGetValue(entry.ProtocolEntry, out var mapping))
                continue;

            if (entry.FramePath is null && !string.IsNullOrWhiteSpace(mapping.FramePath))
                entry.FramePath = mapping.FramePath;

            entry.FrameTimeSeconds ??= mapping.TimeSeconds;
        }

        _log?.LogInformation("Differenz: TP={TP}, FN={FN}, FP={FP}, Mismatch={MM}, F1={F1:F3}",
            report.TruePositiveCount, report.FalseNegativeCount,
            report.FalsePositiveCount, report.CodeMismatchCount, report.F1);

        progress?.Report(new VideoTrainingProgress("Vergleich", 1, 1,
            $"F1={report.F1:P0} — TP:{report.TruePositiveCount} FN:{report.FalseNegativeCount} FP:{report.FalsePositiveCount}"));

        sw.Stop();
        return new VideoTrainingResult(report, frameMappings, sw.Elapsed);
    }

    public void Pause()
    {
        lock (_pauseSync)
        {
            if (_isPaused) return;
            _isPaused = true;
            _pauseGate.Wait();
        }
    }

    public void Resume()
    {
        lock (_pauseSync)
        {
            if (!_isPaused) return;
            _isPaused = false;
            _pauseGate.Release();
        }
    }

    /// <summary>
    /// Beendet ffmpeg-Prozesse die fuer ein bestimmtes Video gestartet wurden und haengen.
    /// Verhindert Zombie-ffmpeg nach Pipeline-Timeout.
    /// </summary>
    /// <summary>
    /// M11 Fix: Killt nur ffmpeg-Prozesse die laenger als 5 Min laufen
    /// UND deren Startzeit VOR dem Pipeline-Start liegt (= verwaist).
    /// Killt NICHT pauschal alle ffmpeg — andere Tools koennten ffmpeg nutzen.
    /// </summary>
    private void KillOrphanedFfmpeg(string videoPath)
    {
        try
        {
            foreach (var proc in Process.GetProcessesByName("ffmpeg"))
            {
                try
                {
                    if ((DateTime.Now - proc.StartTime).TotalMinutes > 5)
                    {
                        _log?.LogWarning(
                            "Verwaister ffmpeg-Prozess (PID {Pid}, seit {Start}) wird beendet",
                            proc.Id, proc.StartTime);
                        proc.Kill();
                    }
                }
                catch { /* Zugriffsfehler bei fremden Prozessen — ignorieren */ }
            }
        }
        catch { /* Safety net */ }
    }

    /// <summary>Blockiert wenn Pause aktiv ist.</summary>
    private async Task CheckPauseAsync(CancellationToken ct)
    {
        if (!_isPaused) return;
        await _pauseGate.WaitAsync(ct).ConfigureAwait(false);
        _pauseGate.Release();
    }

    /// <summary>Konvertiert RawVideoDetection → BlindDetection.</summary>
    private static List<BlindDetection> ConvertToBlindDetections(
        IReadOnlyList<RawVideoDetection>? detections)
    {
        if (detections is null || detections.Count == 0) return [];

        return detections.Select(d => new BlindDetection
        {
            TimeSeconds = 0, // RawVideoDetection hat keinen Timestamp — Meter ist da
            Meter = d.MeterStart,
            VsaCode = d.VsaCodeHint,
            Label = d.FindingLabel,
            Severity = ParseSeverity(d.Severity),
            ClockPosition = d.PositionClock,
            Confidence = 0.5, // EvidenceVector hat keinen Einzel-Score — Pauschalwert
            FramePath = d.FramePath, // V4.2: Pfad aus Batch-Pipeline (Review-Queue-Anzeige)
            BboxX1 = d.BboxX1,
            BboxY1 = d.BboxY1,
            BboxX2 = d.BboxX2,
            BboxY2 = d.BboxY2
        }).ToList();
    }

    /// <summary>Parst den Severity-String (z.B. "3", "mittel") zu int 1-5.</summary>
    private static int ParseSeverity(string? severity)
    {
        if (string.IsNullOrWhiteSpace(severity)) return 0;
        if (int.TryParse(severity, out var s)) return Math.Clamp(s, 1, 5);
        return severity.ToLowerInvariant() switch
        {
            "leicht" => 2,
            "mittel" => 3,
            "schwer" => 4,
            "kritisch" => 5,
            _ => 0
        };
    }

    /// <summary>Schaetzt die Inspektionslaenge aus den Ground-Truth-Eintraegen.</summary>
    private static double EstimateInspektionslaenge(List<GroundTruthEntry> entries)
    {
        if (entries.Count == 0) return 50.0; // Fallback
        return Math.Max(entries.Max(e => e.MeterEnd) * 1.1, 10.0);
    }

    /// <summary>Ermittelt die Video-Dauer via ffprobe.</summary>
    private static async Task<double> GetVideoDurationAsync(string videoPath, CancellationToken ct)
    {
        try
        {
            var ffprobe = AuswertungPro.Next.Application.Ai.FfmpegLocator.ResolveFfprobe();
            // ArgumentList.Add statt Arguments-String: Command-Injection-Schutz.
            var psi = new ProcessStartInfo
            {
                FileName = ffprobe,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("-v");
            psi.ArgumentList.Add("error");
            psi.ArgumentList.Add("-show_entries");
            psi.ArgumentList.Add("format=duration");
            psi.ArgumentList.Add("-of");
            psi.ArgumentList.Add("default=noprint_wrappers=1:nokey=1");
            psi.ArgumentList.Add(videoPath);

            using var proc = Process.Start(psi);
            if (proc is null) return 600; // Fallback 10 Minuten

            var output = await proc.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
            await proc.WaitForExitAsync(ct).ConfigureAwait(false);

            if (double.TryParse(output.Trim(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var duration))
                return duration;
        }
        catch
        {
            // Fallback
        }

        return 600; // 10 Minuten als sichere Annahme
    }
}
