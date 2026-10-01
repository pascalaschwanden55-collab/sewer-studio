using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Shared;
using Microsoft.Extensions.Logging;
using VsaCodeResolver = AuswertungPro.Next.Infrastructure.Ai.VsaCodeResolver;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

// Ausgelagerte, gekapselte Hilfsmethoden der Multi-Model-Pipeline (reiner Move aus
// MultiModelAnalysisService.cs, kein Verhaltensunterschied) — haelt die Hauptdatei unter dem
// 1000-Zeilen-Deckel, damit der Frame-Loop Raum fuer neue Logik behaelt.
public sealed partial class MultiModelAnalysisService
{
    public double FrameStepSeconds { get; set; } = 3.0;
    public int DedupWindowFrames { get; set; } = 3;

    // Aeusserer Per-Frame-Qwen-Cap = Standard 120s (#9). Der effektiv wirksame Cap
    // bleibt der innere FrameTimeout in EnhancedVisionAnalysisService.
    public TimeSpan QwenFrameTimeout { get; set; } = TimeSpan.FromSeconds(120);

    /// <summary>YOLO-cls Vorfilter aktivieren/deaktivieren (Fallback: aus wenn kein Modell).</summary>
    public bool UseClsPrefilter { get; set; } = true;

    private static string NormalizePath(string path)
    {
        path = path.Trim();
        if (path.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            path = new Uri(path).LocalPath;
        return Path.GetFullPath(path);
    }

    /// <summary>
    /// Standard-Frame-Quelle: oeffnet einen VideoFrameStream und gibt seine Frames zurueck.
    /// Als separater Helper, damit der await-using-Dispose korrekt ablaeuft.
    /// </summary>
    private static async IAsyncEnumerable<FrameData> DefaultFrameSource(
        string ffmpegPath,
        string videoPath,
        double stepSeconds,
        double duration,
        MultiModelRunCompleteness completeness,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await using var stream = VideoFrameStream.Open(ffmpegPath, videoPath, stepSeconds, duration, ct);
        await foreach (var frame in stream.ReadFramesAsync(ct).ConfigureAwait(false))
            yield return frame;
        completeness.Extraction = stream.Completion;
    }

    private async Task<double> GetVideoDurationAsync(string videoPath, CancellationToken ct)
    {
        var result = await _videoProbe.ProbeAsync(videoPath, ct).ConfigureAwait(false);
        if (result.Success)
            return result.DurationSeconds;

        _logger.LogWarning("Videodauer konnte nicht ermittelt werden: {Error}", result.Error);
        return 0;
    }

    // Delegiert an gemeinsamen Helfer in FfmpegLocator (verhaltensneutral).
    private static string DeriveFfprobePath(string ffmpegPath) =>
        FfmpegLocator.DeriveFfprobeFrom(ffmpegPath);

    internal static bool CanUseClassifierDecision(YoloClassifyResponse cls)
        => cls.ClassifierLoaded && !cls.BendVetoFailed;

    internal static void MarkTraceDegraded(PipelineFrameTrace trace, string reason)
    {
        trace.Degraded = true;
        if (string.IsNullOrWhiteSpace(trace.DegradedReason))
        {
            trace.DegradedReason = reason;
            return;
        }

        if (!trace.DegradedReason.Contains(reason, StringComparison.OrdinalIgnoreCase))
            trace.DegradedReason += $";{reason}";
    }

    /// <summary>
    /// Liest die Detektor-Qualifikation aus der Sidecar-Gesundheit.
    /// Null bei Fehler oder altem Sidecar bleibt bewusst "nicht freigegeben".
    /// </summary>
    private async Task<SidecarDetectorQualification?> ReadDetectorQualificationAsync(CancellationToken ct)
    {
        try
        {
            var health = await _client.HealthCheckAsync(ct).ConfigureAwait(false);
            return health?.DetectorQualification;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Modell-Tag fuer den Trace: Name + Kurz-Hash aus der Sidecar-Response.</summary>
    internal static string? ClassifierModelTag(YoloClassifyResponse? cls)
    {
        if (cls is null || string.IsNullOrEmpty(cls.ModelName))
            return null;
        var sha = cls.ModelSha256;
        return string.IsNullOrEmpty(sha) ? cls.ModelName : $"{cls.ModelName}@{sha[..Math.Min(12, sha.Length)]}";
    }

    /// <summary>
    /// Convert a MultiModelFrameResult to EnhancedFrameAnalysis
    /// (for compatibility with the existing pipeline).
    /// </summary>
    public static EnhancedFrameAnalysis ToEnhancedAnalysis(
        MultiModelFrameResult result,
        int pipeDiameterMm)
        => MultiModelFrameAnalysisMapper.Map(result, pipeDiameterMm);

    /// <summary>Geschaetzte Haltungslaenge in Metern (wird durch OSD-Korrektur von Qwen ueberschrieben).</summary>
    private Task WriteTraceAsync(PipelineFrameTrace trace)
        => PipelineTraceWriteGuard.WriteAsync(
            _pipelineTraceWriter,
            PipelineTraceEntryMapper.Map(trace));

    public double EstimatedReachLengthM { get; set; } = 50.0; // Typisch 15-80m, Fallback 50m

    private double EstimateMeter(MultiModelLaufZustand run, double t)
    {
        run.LastMeter = MultiModelMeterSchaetzung.Schaetze(
            t, run.Duration, EstimatedReachLengthM, run.LastMeter, run.LetzterOsdMeter);
        return Math.Round(run.LastMeter, 2);
    }

    /// <summary>
    /// Checkpoint-Journal (Resume): Oeffnet das Journal des Videos. Nur der lueckenlose,
    /// gueltige update/advance-Anfang ab Frame 1 eines nicht abgeschlossenen Journals mit
    /// identischer Video-Identitaet wird uebernommen und exakt so durch den
    /// TemporalFindingDeduplicator gespielt wie im Original-Lauf (update → Update(...),
    /// advance → AdvanceAll()) — der Dedup-State ist dadurch identisch mit dem eines
    /// ununterbrochenen Laufs. Liefert den zuletzt journalierten Frame-Index und den
    /// fortzusetzenden Meterstand.
    /// Bekannte v1-Kanten: Code-Voting und der Qwen-Vorbefund-Kontext starten am
    /// Resume-Punkt neu (nicht journaliert; der belegte OSD-Meter ist journaliert und wird
    /// mitgenommen); ffmpeg dekodiert weiter ab Anfang, die
    /// journalierten Frames werden nur dekodiert, nicht erneut inferiert (spart die
    /// teure GPU-Inferenz; bewusster v1-Kompromiss).
    /// </summary>
    private async Task RestoreCheckpointAsync(MultiModelLaufZustand run, CancellationToken ct)
    {
        if (_checkpointJournal is null)
            return;

        var detections = run.Detections;
        var deduplicator = run.Deduplicator;
        var lastMeter = run.LastMeter;
        var state = await _checkpointJournal.OpenAsync(run.VideoPath, FrameStepSeconds, ct).ConfigureAwait(false);
        if (!state.HasResume)
            return;

        foreach (var frame in state.Frames)
        {
            // Exakt dieselben Dedup-Methoden wie im Original-Lauf: update-Frames gingen
            // durch Update(...), advance-Frames durch AdvanceAll(). Nur so ist der
            // Dedup-State am Resume-Punkt identisch mit dem eines ununterbrochenen Laufs.
            if (frame.Kind == CheckpointFrameKind.Update)
            {
                detections.AddRange(deduplicator.Update(
                    frame.Findings, frame.Meter, frame.Evidence,
                    meterSource: frame.MeterSource, isMeterEstimated: frame.IsMeterEstimated));
            }
            else
            {
                detections.AddRange(deduplicator.AdvanceAll());
            }
            // Belegter OSD-Anker der 5-m/s-Pruefung und der Meterschaetzung (Entscheid 01.10.2026)
            // steht im Journal. Wie im ununterbrochenen Lauf setzt er den laufenden Meterstand auf sich.
            var osdAnker = !frame.IsMeterEstimated
                && frame.MeterSource == GetDedupMeterMetadata(qwenMeterAccepted: true).MeterSource;
            lastMeter = osdAnker ? frame.Meter : Math.Max(lastMeter, frame.Meter);
            if (osdAnker)
                run.LetzterOsdMeter = (frame.Meter, frame.TimeSec);
        }

        _logger.LogInformation(
            "Checkpoint-Journal: Fortsetzung ab Frame {Frame} ({Count} Frames aus Journal uebernommen).",
            state.LastFrameIndex + 1, state.Frames.Count);
        run.Progress?.Report(new VideoAnalysisProgress(state.LastFrameIndex, run.TotalFrames,
            $"Checkpoint: Fortsetzung ab Frame {state.LastFrameIndex + 1} ({state.Frames.Count} Frames übernommen)."));
        run.ResumedFrames = state.LastFrameIndex;
        run.LastMeter = lastMeter;
    }

    /// <summary>Frame-Record ans Checkpoint-Journal anhaengen (No-op ohne Journal).</summary>
    private Task AppendCheckpointAsync(AnalysisCheckpointFrame frame, CancellationToken ct)
        => _checkpointJournal?.AppendFrameAsync(frame, ct) ?? Task.CompletedTask;

    private static void ReportCompletion(IProgress<VideoAnalysisProgress>? progress, int totalFrames,
        int skippedFrames, VideoAnalysisResult result)
    {
        var status = result.Incomplete ? "Multi-Model Analyse unvollständig" : result.Degraded
            ? "Multi-Model abgeschlossen mit Einschränkungen" : "Multi-Model fertig";
        progress?.Report(new VideoAnalysisProgress(result.Incomplete ? result.FramesAnalyzed : totalFrames, totalFrames,
            $"{status} – {result.Detections.Count} Schäden, {skippedFrames} Frames übersprungen. " + result.DegradedReason));
    }

    /// <summary>
    /// Baut das Abschluss-Ergebnis: Degraded-Gruende (Sidecar-Ausfall, Qwen-Serie,
    /// Detektor-Qualifikation, VRAM-Kapazitaetsmangel) und die Unvollstaendigkeits-
    /// Kennzeichnung aus der Skip-Quote (mehr als 10 % fehlerbedingt uebersprungene
    /// Frames des Laufs).
    /// </summary>
    private VideoAnalysisResult BuildResult(MultiModelLaufZustand run, TelemetrySummary summary)
    {
        var completeness = run.Completeness;
        var qwenOutage = run.QwenOutage;
        var vramInsufficientMessage = run.VramInsufficientMessage;
        var detectorQualificationReason = run.DetectorQualificationReason;
        var degradedReasons = new List<string>();
        if (completeness.ExtractionWarning is { } extractionWarning)
            degradedReasons.Add(extractionWarning);
        if (completeness.SamFailureFrames > 0)
            degradedReasons.Add($"SAM: {completeness.SamFailureFrames} Frames technisch nicht vollständig segmentiert – manuelle Prüfung erforderlich.");
        if (run.SidecarOutage)
            degradedReasons.Add($"Sidecar antwortete ab Frame {run.FrameIndex} nicht mehr – Analyse unvollständig.");
        // Paket 2/A4: VRAM-Mangel ist kein Ausfall, aber ehrlich sichtbar (mit VRAM-Zahlen).
        if (!string.IsNullOrWhiteSpace(vramInsufficientMessage))
            degradedReasons.Add(
                vramInsufficientMessage
                + " Betroffene Frames wurden übersprungen (Skip-Quote) – manuelle Prüfung erforderlich.");
        if (qwenOutage.Noted)
        {
            // NotedErrorCount bleibt auch nach einem spaeteren Erfolg erhalten:
            // die Endmeldung nennt die Folgefehler-Zahl zum Zeitpunkt der Notiz.
            _logger.LogError(
                "Qwen (Ollama) antwortet seit {Count} Frames nicht — VSA-Anreicherung unvollstaendig (Lauf laeuft weiter).",
                qwenOutage.NotedErrorCount);
            degradedReasons.Add(
                $"Qwen/Ollama antwortete bei {qwenOutage.NotedErrorCount} Folgeframes nicht – VSA-Code-Anreicherung unvollständig.");
        }
        else if (completeness.QwenFailureFrames > 0)
            degradedReasons.Add($"Qwen/Ollama fehlgeschlagen bei {completeness.QwenFailureFrames} Frames – VSA-Code-Anreicherung unvollständig.");
        if (!run.DetectorQualified)
        {
            degradedReasons.Add(
                "YOLO-Detektor nicht qualifiziert"
                + (string.IsNullOrWhiteSpace(detectorQualificationReason)
                    ? string.Empty
                    : $": {detectorQualificationReason}")
                + ". DINO/SAM wurden ohne YOLO-Filter ausgeführt; manuelle Prüfung erforderlich.");
        }

        // Skip-Quote: Quote der fehlerbedingt uebersprungenen Frames an den in DIESEM
        // Lauf analysierten Frames (Resume-Frames zaehlen nicht mit). Kein Abbruch.
        var analyzedFrames = run.FrameIndex - run.ResumedFrames;
        var incomplete = completeness.Extraction is { IsComplete: false }
            || (analyzedFrames > 0 && (double)run.OutageGuard.ErrorSkipCount / analyzedFrames > 0.10);

        return new VideoAnalysisResult(run.VideoPath, run.Duration, run.FrameIndex,
            run.Detections.OrderBy(d => d.MeterStart).ToList(), null, summary,
            Degraded: degradedReasons.Count > 0,
            DegradedReason: degradedReasons.Count > 0
                ? string.Join(" ", degradedReasons)
                : null,
            DetectorQualified: run.EffectiveDetectorQualified,
            DetectorQualificationReason: detectorQualificationReason,
            Incomplete: incomplete);
    }
}
