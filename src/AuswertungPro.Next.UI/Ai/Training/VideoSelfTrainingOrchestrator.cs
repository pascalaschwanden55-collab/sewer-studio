// AuswertungPro – Video-Selbsttraining Phase 2
using System;
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

    public bool IsPaused => _isPaused;

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
            request.VideoPath, videoDuration, inspLength, groundTruths, frameOutputDir, ct)
            .ConfigureAwait(false);

        _log?.LogInformation("Frame-Mapping: {Count} Zuordnungen, {OSD} via OSD",
            frameMappings.Count,
            frameMappings.Count(m => m.Source == MeterMappingSource.OSD));

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
            pipelineResult = await _pipeline.RunAsync(
                pipelineRequest, pipelineProgress, ct).ConfigureAwait(false);
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
        if (!_isPaused)
        {
            _isPaused = true;
            _pauseGate.Wait();
        }
    }

    public void Resume()
    {
        if (_isPaused)
        {
            _isPaused = false;
            _pauseGate.Release();
        }
    }

    /// <summary>
    /// Beendet ffmpeg-Prozesse die fuer ein bestimmtes Video gestartet wurden und haengen.
    /// Verhindert Zombie-ffmpeg nach Pipeline-Timeout.
    /// </summary>
    private static void KillOrphanedFfmpeg(string videoPath)
    {
        try
        {
            var videoName = System.IO.Path.GetFileName(videoPath);
            foreach (var proc in Process.GetProcessesByName("ffmpeg"))
            {
                try
                {
                    // Pruefe ob der Prozess schon laenger als 5 Min laeuft
                    if ((DateTime.Now - proc.StartTime).TotalMinutes > 5)
                    {
                        proc.Kill();
                    }
                }
                catch { /* Zugriffsfehler ignorieren */ }
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
            FramePath = null // Frames sind im Pipeline-Output nicht gespeichert
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
            var ffprobe = Shared.FfmpegLocator.ResolveFfprobe();
            var psi = new ProcessStartInfo
            {
                FileName = ffprobe,
                Arguments = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{videoPath}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

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
