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
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await using var stream = VideoFrameStream.Open(ffmpegPath, videoPath, stepSeconds, duration, ct);
        await foreach (var frame in stream.ReadFramesAsync(ct).ConfigureAwait(false))
            yield return frame;
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

    /// <summary>
    /// Normalisiert Clock-Positionen — delegiert an kanonische Implementierung in VsaCodeResolver.
    /// </summary>
    private static string? NormalizeClockPosition(string? clock) =>
        VsaCodeResolver.NormalizeClock(clock);

    private static bool CanUseClassifierDecision(YoloClassifyResponse cls)
        => cls.ClassifierLoaded && !cls.BendVetoFailed;

    private static void MarkTraceDegraded(PipelineFrameTrace trace, string reason)
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
    private static string? ClassifierModelTag(YoloClassifyResponse? cls)
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

    private double EstimateMeter(double t, double duration, ref double lastMeter)
    {
        // Lineare Schaetzung basierend auf geschaetzter Haltungslaenge (wird durch Qwen OSD korrigiert)
        var estimated = t / Math.Max(duration, 1.0) * EstimatedReachLengthM;
        lastMeter = Math.Max(lastMeter, estimated);
        return Math.Round(lastMeter, 2);
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
    /// Resume-Punkt neu (nicht journaliert); ffmpeg dekodiert weiter ab Anfang, die
    /// journalierten Frames werden nur dekodiert, nicht erneut inferiert (spart die
    /// teure GPU-Inferenz; bewusster v1-Kompromiss).
    /// </summary>
    private async Task<(int LastFrameIndex, double LastMeter)> RestoreCheckpointAsync(
        string videoPath,
        List<RawVideoDetection> detections,
        TemporalFindingDeduplicator deduplicator,
        int totalFrames,
        double lastMeter,
        IProgress<VideoAnalysisProgress>? progress,
        CancellationToken ct)
    {
        if (_checkpointJournal is null)
            return (0, lastMeter);

        var state = await _checkpointJournal.OpenAsync(videoPath, FrameStepSeconds, ct).ConfigureAwait(false);
        if (!state.HasResume)
            return (0, lastMeter);

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
            lastMeter = Math.Max(lastMeter, frame.Meter);
        }

        _logger.LogInformation(
            "Checkpoint-Journal: Fortsetzung ab Frame {Frame} ({Count} Frames aus Journal uebernommen).",
            state.LastFrameIndex + 1, state.Frames.Count);
        progress?.Report(new VideoAnalysisProgress(state.LastFrameIndex, totalFrames,
            $"Checkpoint: Fortsetzung ab Frame {state.LastFrameIndex + 1} ({state.Frames.Count} Frames uebernommen)."));
        return (state.LastFrameIndex, lastMeter);
    }

    /// <summary>Frame-Record ans Checkpoint-Journal anhaengen (No-op ohne Journal).</summary>
    private Task AppendCheckpointAsync(AnalysisCheckpointFrame frame, CancellationToken ct)
        => _checkpointJournal?.AppendFrameAsync(frame, ct) ?? Task.CompletedTask;

    /// <summary>
    /// Baut das Abschluss-Ergebnis: Degraded-Gruende (Sidecar-Ausfall, Qwen-Serie,
    /// Detektor-Qualifikation, VRAM-Kapazitaetsmangel) und die Unvollstaendigkeits-
    /// Kennzeichnung aus der Skip-Quote (mehr als 10 % fehlerbedingt uebersprungene
    /// Frames des Laufs).
    /// </summary>
    private VideoAnalysisResult BuildResult(
        string videoPath,
        double duration,
        int frameIndex,
        int resumedFrames,
        List<RawVideoDetection> detections,
        TelemetrySummary summary,
        bool sidecarOutage,
        bool detectorQualified,
        bool? effectiveDetectorQualified,
        string? detectorQualificationReason,
        SidecarOutageGuard outageGuard,
        QwenOutageTracker qwenOutage,
        string? vramInsufficientMessage)
    {
        var degradedReasons = new List<string>();
        if (sidecarOutage)
            degradedReasons.Add($"Sidecar antwortete ab Frame {frameIndex} nicht mehr – Analyse unvollstaendig.");
        // Paket 2/A4: VRAM-Mangel ist kein Ausfall, aber ehrlich sichtbar (mit VRAM-Zahlen).
        if (!string.IsNullOrWhiteSpace(vramInsufficientMessage))
            degradedReasons.Add(
                vramInsufficientMessage
                + " Betroffene Frames wurden uebersprungen (Skip-Quote) – manuelle Pruefung erforderlich.");
        if (qwenOutage.Noted)
        {
            // NotedErrorCount bleibt auch nach einem spaeteren Erfolg erhalten:
            // die Endmeldung nennt die Folgefehler-Zahl zum Zeitpunkt der Notiz.
            _logger.LogError(
                "Qwen (Ollama) antwortet seit {Count} Frames nicht — VSA-Anreicherung unvollstaendig (Lauf laeuft weiter).",
                qwenOutage.NotedErrorCount);
            degradedReasons.Add(
                $"Qwen/Ollama antwortete bei {qwenOutage.NotedErrorCount} Folgeframes nicht – VSA-Code-Anreicherung unvollstaendig.");
        }
        if (!detectorQualified)
        {
            degradedReasons.Add(
                "YOLO-Detektor nicht qualifiziert"
                + (string.IsNullOrWhiteSpace(detectorQualificationReason)
                    ? string.Empty
                    : $": {detectorQualificationReason}")
                + ". DINO/SAM wurden ohne YOLO-Filter ausgefuehrt; manuelle Pruefung erforderlich.");
        }

        // Skip-Quote: Quote der fehlerbedingt uebersprungenen Frames an den in DIESEM
        // Lauf analysierten Frames (Resume-Frames zaehlen nicht mit). Kein Abbruch.
        var analyzedFrames = frameIndex - resumedFrames;
        var incomplete = analyzedFrames > 0
            && (double)outageGuard.ErrorSkipCount / analyzedFrames > 0.10;

        return new VideoAnalysisResult(videoPath, duration, frameIndex,
            detections.OrderBy(d => d.MeterStart).ToList(), null, summary,
            Degraded: degradedReasons.Count > 0,
            DegradedReason: degradedReasons.Count > 0
                ? string.Join(" ", degradedReasons)
                : null,
            DetectorQualified: effectiveDetectorQualified,
            DetectorQualificationReason: detectorQualificationReason,
            Incomplete: incomplete);
    }

    /// <summary>Mutebarer Uebergabestand des Qwen-Blocks (Meter darf durch OSD korrigiert werden).</summary>
    private sealed class QwenFrameContext
    {
        public QwenFrameContext(double meter, double lastMeter)
        {
            Meter = meter;
            LastMeter = lastMeter;
        }

        public double Meter { get; set; }
        public double LastMeter { get; set; }
        public bool MeterAccepted { get; set; }
    }

    /// <summary>
    /// Step 5 des Frame-Loops: Qwen VSA-Code-Anreicherung (reiner Move aus der
    /// Hauptdatei, verhaltensneutral — haelt sie unter dem 1000-Zeilen-Deckel).
    /// </summary>
    private async Task<long> EnrichFindingsWithQwenAsync(
        QwenFrameContext context,
        List<EnhancedFinding> findings,
        string? classifierCode,
        int frameIndex,
        double t,
        byte[] frameBytes,
        string frameBase64,
        DinoResponse dinoResult,
        SamResponse samResult,
        YoloResponse yoloResult,
        int pipeDiameterMm,
        int totalFrames,
        PipelineFrameTrace trace,
        QwenOutageTracker qwenOutage,
        IProgress<VideoAnalysisProgress>? progress,
        CancellationToken ct)
    {
        var qwenVision = _qwenVision
            ?? throw new InvalidOperationException("Qwen-Anreicherung ohne Qwen-Dienst aufgerufen.");
        var meter = context.Meter;
        var lastMeter = context.LastMeter;
        var qwenMeterAccepted = false;
        var phaseSw = Stopwatch.StartNew();
        long qwenMs;


        trace.QwenCalled = true;
        progress?.Report(new VideoAnalysisProgress(frameIndex, totalFrames,
            $"Frame {frameIndex}/{totalFrames} – Qwen VSA-Code-Mapping...",
            FramePreviewPng: frameBytes));

        phaseSw.Restart();
        try
        {
            var multiModelContext = new MultiModelFrameResult(
                TimestampSec: t,
                Meter: meter,
                IsRelevant: true,
                DinoDetections: dinoResult.Detections,
                SamMasks: samResult.Masks,
                ImageWidth: samResult.ImageWidth,
                ImageHeight: samResult.ImageHeight,
                YoloTimeMs: yoloResult.InferenceTimeMs,
                DinoTimeMs: dinoResult.InferenceTimeMs,
                SamTimeMs: samResult.InferenceTimeMs);

            using var qwenCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            qwenCts.CancelAfter(QwenFrameTimeout);
            // Vorherigen Befund als Kontext uebergeben (nur wenn < 1m entfernt)
            var prevCtx = _lastFinding is var (pc, pd, pm, pconf) && Math.Abs(meter - pm) < 1.0
                ? _lastFinding : null;
            var qwenResult = await qwenVision.AnalyzeWithContextAsync(
                frameBase64, multiModelContext, pipeDiameterMm, qwenCts.Token,
                previousFinding: prevCtx).ConfigureAwait(false);

            trace.QwenImageQuality = qwenResult.ImageQuality;
            trace.QwenRawFindingCount = qwenResult.Findings.Count;
            qwenOutage.RegisterSuccess();

            var badQuality = string.Equals(qwenResult.ImageQuality, "schlecht", StringComparison.OrdinalIgnoreCase);

            // OSD-Meter nur uebernehmen, wenn plausibel (0..500 m) UND nicht aus einem schlechten
            // Bild — sonst vergiftet ein halluzinierter/fehlgelesener Meter die fortlaufende
            // Timeline (lastMeter). Bei schlechtem Bild ist auch das OSD-Lesen unzuverlaessig. (Audit R7)
            if (qwenResult.Meter.HasValue && !badQuality
                && AuswertungPro.Next.Infrastructure.Ai.MeterPlausibility.IsPlausible(qwenResult.Meter.Value))
            {
                meter = qwenResult.Meter.Value;
                lastMeter = meter;
                qwenMeterAccepted = true;
            }
            else if (qwenResult.Meter.HasValue)
            {
                _logger.LogDebug("Frame {Frame}: OSD-Meter {Meter} verworfen ({Reason})",
                    frameIndex, qwenResult.Meter.Value, badQuality ? "schlechtes Bild" : "unplausibel");
            }

            // ImageQuality-Gate: Bei schlechter Bildqualitaet Findings verwerfen
            if (badQuality)
            {
                _logger.LogDebug("Frame {Frame}: ImageQuality=schlecht, {Count} Findings verworfen",
                    frameIndex, findings.Count);
                trace.DropReason = "image_quality_bad";
                findings.Clear();
            }

            if (qwenResult.HasFindings)
            {
                // Match Qwen findings to our quantified findings by label similarity
                foreach (var qf in qwenResult.Findings)
                {
                    var match = findings.FirstOrDefault(f =>
                        f.Label.Equals(qf.Label, StringComparison.OrdinalIgnoreCase) ||
                        qf.Label.Contains(f.Label, StringComparison.OrdinalIgnoreCase) ||
                        f.Label.Contains(qf.Label, StringComparison.OrdinalIgnoreCase));

                    // Klassifikator fuehrt (Paket 2): bestaetigte Codes darf Qwen
                    // nicht ueberschreiben — nur noch leere Hints fuellen.
                    if (match is not null && !string.IsNullOrWhiteSpace(qf.VsaCodeHint)
                        && (classifierCode is null || string.IsNullOrWhiteSpace(match.VsaCodeHint)))
                    {
                        var idx = findings.IndexOf(match);
                        // Replace with enriched finding (keep SAM quantification, add Qwen VSA code)
                        findings[idx] = match with { VsaCodeHint = qf.VsaCodeHint };
                    }
                }

                // Letzten Befund merken fuer Qwen-Kontext beim naechsten Frame
                var topFinding = qwenResult.Findings
                    .Where(f => !string.IsNullOrEmpty(f.VsaCodeHint))
                    .OrderByDescending(f => f.Severity)
                    .FirstOrDefault();
                if (topFinding != null)
                {
                    _lastFinding = (
                        topFinding.VsaCodeHint ?? topFinding.Label,
                        topFinding.Label,
                        meter,
                        topFinding.Severity / 5.0); // Severity 1-5 → Confidence 0.2-1.0
                }

                _logger.LogDebug("Frame {Frame}: Qwen enriched {Count} findings with VSA codes",
                    frameIndex, qwenResult.Findings.Count(f => !string.IsNullOrWhiteSpace(f.VsaCodeHint)));
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Nutzerabbruch: sofort weiterwerfen, nie als Qwen-Ausfall zaehlen.
            throw;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            trace.DropReason = "qwen_timeout";
            qwenOutage.RegisterFailure();
            _logger.LogWarning("Frame {Frame}: Qwen VSA-Code-Mapping timeout ({Timeout}s)",
                frameIndex, QwenFrameTimeout.TotalSeconds);
        }
        catch (Exception ex)
        {
            trace.DropReason = "qwen_error";
            qwenOutage.RegisterFailure();
            _logger.LogWarning(ex, "Frame {Frame}: Qwen VSA-Code-Mapping fehlgeschlagen", frameIndex);
        }
        qwenMs = phaseSw.ElapsedMilliseconds;
        context.Meter = meter;
        context.LastMeter = lastMeter;
        context.MeterAccepted = qwenMeterAccepted;
        return qwenMs;
    }
}
