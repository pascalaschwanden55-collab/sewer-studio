using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>
/// Qwen-Schritt der Mehrmodell-Analyse (Qwen/Ollama VSA-Code-Anreicherung), ausgelagert aus
/// <see cref="MultiModelAnalysisService"/> (AP05). Eine Instanz lebt genau einen Analyselauf:
/// Sie haelt den letzten Qwen-Befund als Kontext fuer das naechste Bild und zaehlt die
/// Qwen-Folgefehler im gemeinsamen <see cref="QwenOutageTracker"/> des Laufs.
/// Eigene Regeln dieses Modells (bewusst nicht mit YOLO/DINO/SAM gleichgesetzt):
/// Ein als Wert zurueckgegebenes Fehlerergebnis ist kein Erfolg; ein gueltiges leeres Ergebnis
/// ist einer. Ein interner Zeitablauf ist ein Qwen-Fehler, ein Nutzerabbruch wird weitergereicht.
/// Qwen ist ein eigener Prozess: Folgefehler fuehren nur zu einer Degraded-Notiz, nie zu einem
/// Sidecar-Neustart oder Laufabbruch. Bestaetigte Klassifikator-Codes ueberschreibt Qwen nicht.
/// </summary>
internal sealed class MultiModelQwenSchritt
{
    private readonly EnhancedVisionAnalysisService _qwenVision;
    private readonly QwenOutageTracker _outage;
    private readonly ILogger _logger;

    // Letzter Befund fuer Qwen-Kontext (Frame-uebergreifende Kohaerenz); endet mit dem Lauf.
    private (string Code, string Description, double Meter, double Confidence)? _lastFinding;

    public MultiModelQwenSchritt(EnhancedVisionAnalysisService qwenVision, QwenOutageTracker outage, ILogger logger)
    {
        _qwenVision = qwenVision ?? throw new ArgumentNullException(nameof(qwenVision));
        _outage = outage ?? throw new ArgumentNullException(nameof(outage));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Mutebarer Uebergabestand des Qwen-Blocks (Meter darf durch OSD korrigiert werden).</summary>
    internal sealed class QwenFrameContext
    {
        public QwenFrameContext(double meter, double lastMeter)
        {
            Meter = meter;
            LastMeter = lastMeter;
        }

        public double Meter { get; set; }
        public double LastMeter { get; set; }
        public bool MeterAccepted { get; set; }
        public bool RequiresRetry { get; set; }
    }

    /// <summary>
    /// Step 5 des Frame-Loops: Qwen VSA-Code-Anreicherung. Liefert die Qwen-Dauer; Meter,
    /// OSD-Uebernahme und Wiederholungsbedarf stehen danach im <paramref name="context"/>.
    /// </summary>
    public async Task<long> EnrichAsync(
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
        TimeSpan frameTimeout,
        IProgress<VideoAnalysisProgress>? progress,
        CancellationToken ct)
    {
        var qwenOutage = _outage;
        var qwenVision = _qwenVision;
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
            qwenCts.CancelAfter(frameTimeout);
            // Vorherigen Befund als Kontext uebergeben (nur wenn < 1m entfernt)
            var prevCtx = _lastFinding is var (pc, pd, pm, pconf) && Math.Abs(meter - pm) < 1.0
                ? _lastFinding : null;
            var qwenResult = await qwenVision.AnalyzeWithContextAsync(
                frameBase64, multiModelContext, pipeDiameterMm, qwenCts.Token,
                previousFinding: prevCtx).ConfigureAwait(false);

            trace.QwenImageQuality = qwenResult.ImageQuality;
            trace.QwenRawFindingCount = qwenResult.Findings.Count;
            if (qwenResult.Outcome is not (AnalysisOutcome.Ok or AnalysisOutcome.NoFinding)
                || !string.IsNullOrWhiteSpace(qwenResult.Error))
            {
                var reason = qwenResult.Outcome == AnalysisOutcome.Timeout ? "qwen_timeout" : "qwen_error";
                RecordQwenFailure(context, trace, qwenOutage, reason);
                _logger.LogWarning("Frame {Frame}: Qwen VSA-Code-Mapping fehlgeschlagen ({Outcome}): {Error}",
                    frameIndex, qwenResult.Outcome, qwenResult.Error);
                return phaseSw.ElapsedMilliseconds;
            }
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
            RecordQwenFailure(context, trace, qwenOutage, "qwen_timeout");
            _logger.LogWarning("Frame {Frame}: Qwen VSA-Code-Mapping timeout ({Timeout}s)",
                frameIndex, frameTimeout.TotalSeconds);
        }
        catch (Exception ex)
        {
            RecordQwenFailure(context, trace, qwenOutage, "qwen_error");
            _logger.LogWarning(ex, "Frame {Frame}: Qwen VSA-Code-Mapping fehlgeschlagen", frameIndex);
        }
        qwenMs = phaseSw.ElapsedMilliseconds;
        context.Meter = meter;
        context.LastMeter = lastMeter;
        context.MeterAccepted = qwenMeterAccepted;
        return qwenMs;
    }

    private static void RecordQwenFailure(QwenFrameContext context, PipelineFrameTrace trace,
        QwenOutageTracker outage, string reason)
    {
        context.RequiresRetry = true;
        trace.DropReason = reason;
        MultiModelAnalysisService.MarkTraceDegraded(trace, reason);
        outage.RegisterFailure();
    }
}
