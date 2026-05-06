// V4.1 Batch-Pipeline: YOLO Batch → Filter → DINO+SAM → Qwen ×6 parallel
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Ai.Pipeline;

/// <summary>
/// Batch-Pipeline: Verarbeitet Video-Frames in Batches statt einzeln.
/// Phase 1: YOLO screent alle Frames im Batch (~5ms pro Frame)
/// Phase 2: Nur relevante Frames an DINO+SAM
/// Phase 3: Qwen ×6 parallel (alle 6 Slots gleichzeitig)
/// Ergebnis: ~2-3 Min pro Haltung statt 30+ Min.
/// </summary>
public sealed class BatchPipelineService
{
    private readonly VisionPipelineClient _sidecar;
    private readonly EnhancedVisionAnalysisService _qwen;
    private readonly PipelineConfig _config;
    private readonly string _ffmpegPath;
    private readonly ILogger _logger;

    /// <summary>Batch-Groesse fuer YOLO (Anzahl Frames pro Batch-Request).</summary>
    public int YoloBatchSize { get; set; } = 6;

    /// <summary>Maximale parallele Qwen-Requests.</summary>
    public int QwenParallelism { get; set; } = 6;

    /// <summary>Frame-Intervall in Sekunden.</summary>
    public double FrameStepSeconds { get; set; } = 2.0;

    public BatchPipelineService(
        VisionPipelineClient sidecar,
        EnhancedVisionAnalysisService qwen,
        PipelineConfig config,
        string ffmpegPath,
        ILogger? logger = null)
    {
        _sidecar = sidecar ?? throw new ArgumentNullException(nameof(sidecar));
        _qwen = qwen ?? throw new ArgumentNullException(nameof(qwen));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _ffmpegPath = ffmpegPath ?? AuswertungPro.Next.Application.Ai.FfmpegLocator.ResolveFfmpeg();
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
    }

    /// <summary>
    /// Analysiert ein Video mit der Batch-Pipeline.
    /// Gibt eine Liste von Frame-Analysen zurueck.
    /// </summary>
    public async Task<BatchPipelineResult> AnalyzeVideoAsync(
        string videoPath,
        int pipeDiameterMm = 300,
        IProgress<BatchPipelineProgress>? progress = null,
        CancellationToken ct = default,
        ProtocolFirstContext? protocolContext = null,
        string? frameOutputDir = null)
    {
        var sw = Stopwatch.StartNew();
        var allAnalyses = new List<BatchFrameAnalysis>();

        // Video-Dauer ermitteln (via ffprobe)
        var duration = await GetDurationAsync(videoPath, ct).ConfigureAwait(false);

        if (duration <= 0)
        {
            _logger.LogWarning("Video-Dauer konnte nicht ermittelt werden: {Path}", videoPath);
            return new BatchPipelineResult([], TimeSpan.Zero, 0, 0, 0);
        }

        var totalFrames = (int)(duration / FrameStepSeconds) + 1;
        _logger.LogInformation(
            "BatchPipeline: {Video}, {Duration:F0}s, {Frames} Frames (Step={Step}s)",
            Path.GetFileName(videoPath), duration, totalFrames, FrameStepSeconds);

        progress?.Report(new BatchPipelineProgress(0, totalFrames, "Frames extrahieren..."));

        // ── Phase 1: Alle Frames extrahieren ──
        var frames = new List<(int Index, double Timestamp, string Base64)>();
        await using var stream = VideoFrameStream.Open(_ffmpegPath, videoPath, FrameStepSeconds, duration, ct);
        int frameIdx = 0;
        await foreach (var frame in stream.ReadFramesAsync(ct).ConfigureAwait(false))
        {
            if (frame.PngBytes is null or { Length: 0 }) continue;
            frames.Add((frameIdx, frame.TimestampSeconds, Convert.ToBase64String(frame.PngBytes)));
            frameIdx++;
        }

        _logger.LogInformation("BatchPipeline: {Count} Frames extrahiert", frames.Count);
        progress?.Report(new BatchPipelineProgress(0, frames.Count, $"{frames.Count} Frames extrahiert, YOLO Batch..."));

        // ── Phase 2: YOLO Batch-Screening ──
        var yoloSw = Stopwatch.StartNew();
        var relevantFrames = new List<(int Index, double Timestamp, string Base64, YoloResponse Yolo)>();
        int skippedFrames = 0;

        for (int batch = 0; batch < frames.Count; batch += YoloBatchSize)
        {
            ct.ThrowIfCancellationRequested();
            var chunk = frames.Skip(batch).Take(YoloBatchSize).ToList();

            var batchRequest = new YoloBatchRequestDto(
                chunk.Select(f => new YoloBatchItemDto(f.Base64, f.Index.ToString())).ToList(),
                _config.YoloConfidence);

            YoloBatchResponseDto batchResult;
            try
            {
                batchResult = await _sidecar.DetectYoloBatchAsync(batchRequest, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "YOLO Batch fehlgeschlagen — Frames einzeln verarbeiten");
                // Fallback: alle Frames als relevant markieren
                foreach (var f in chunk)
                    relevantFrames.Add((f.Index, f.Timestamp, f.Base64,
                        new YoloResponse(true, Array.Empty<YoloDetectionDto>(), "fallback", 0)));
                continue;
            }

            foreach (var item in batchResult.Results)
            {
                var idx = int.Parse(item.FrameId);
                var original = chunk.First(f => f.Index == idx);

                // BCD/BCE-Zonen und periodischer Sweep immer durchlassen
                var estimatedMeter = original.Timestamp / duration *
                    (protocolContext?.InspektionslaengeMeter ?? 100); // besser wenn Laenge bekannt
                bool isBcdZone = original.Timestamp < 10 && original.Index <= 5;
                bool isBceZone = original.Timestamp > duration - FrameStepSeconds * 2;
                bool isPeriodicSweep = original.Index % 5 == 0;

                // V4.2 Phase 2: Wenn Protokoll-Kontext → gezielte Frame-Auswahl.
                // Nur Frames innerhalb Target-Zonen + BCD/BCE werden weitergegeben.
                bool inProtocolWindow = protocolContext is null
                    || protocolContext.ContainsMeter(estimatedMeter);

                if ((item.Result.IsRelevant || isBcdZone || isBceZone || isPeriodicSweep) && inProtocolWindow)
                {
                    relevantFrames.Add((original.Index, original.Timestamp, original.Base64, item.Result));
                }
                else
                {
                    skippedFrames++;
                }
            }

            progress?.Report(new BatchPipelineProgress(
                Math.Min(batch + YoloBatchSize, frames.Count), frames.Count,
                $"YOLO: {relevantFrames.Count} relevant, {skippedFrames} uebersprungen"));
        }

        _logger.LogInformation(
            "BatchPipeline YOLO: {Relevant}/{Total} relevant ({Skipped} uebersprungen) in {Ms}ms",
            relevantFrames.Count, frames.Count, skippedFrames, yoloSw.ElapsedMilliseconds);

        // ── Phase 2.5: DINO + SAM fuer relevante Frames ──
        progress?.Report(new BatchPipelineProgress(
            0, relevantFrames.Count,
            $"DINO+SAM: {relevantFrames.Count} Frames segmentieren..."));

        var dinoSamSw = Stopwatch.StartNew();
        var frameContexts = new Dictionary<int, MultiModelFrameResult>();

        foreach (var frame in relevantFrames)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                // DINO Grounding
                var dinoResp = await _sidecar.DetectDinoAsync(
                    new DinoRequest(frame.Base64, null, _config.DinoBoxThreshold, _config.DinoTextThreshold), ct)
                    .ConfigureAwait(false);

                if (dinoResp.Detections.Count > 0)
                {
                    // SAM Segmentierung (mit Box-Batching)
                    var samBoxes = dinoResp.Detections.Select(d =>
                        new SamBoundingBox(d.X1, d.Y1, d.X2, d.Y2, d.Label, d.Confidence)).ToList();
                    var samResp = await _sidecar.SegmentSamAsync(
                        new SamRequest(frame.Base64, samBoxes,
                            PipeDiameterMm: pipeDiameterMm > 0 ? pipeDiameterMm : null), ct)
                        .ConfigureAwait(false);

                    // Quantifizierung
                    var quantified = MaskQuantificationService.QuantifyAll(samResp, pipeDiameterMm);

                    frameContexts[frame.Index] = new MultiModelFrameResult(
                        frame.Timestamp, null, true,
                        dinoResp.Detections.Select(d => new DinoDetectionDto(
                            d.X1, d.Y1, d.X2, d.Y2, d.Label, d.Confidence, d.Phrase)).ToList(),
                        samResp.Masks,
                        samResp.ImageWidth, samResp.ImageHeight,
                        0, dinoResp.InferenceTimeMs, samResp.InferenceTimeMs);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "DINO/SAM fehlgeschlagen fuer Frame {Frame}", frame.Index);
            }
        }

        _logger.LogInformation("BatchPipeline DINO+SAM: {Count} Frames mit Kontext in {Ms}ms",
            frameContexts.Count, dinoSamSw.ElapsedMilliseconds);

        progress?.Report(new BatchPipelineProgress(
            0, relevantFrames.Count,
            $"Qwen: {relevantFrames.Count} Frames analysieren (2 parallel)..."));

        // ── Phase 3: Qwen 2-fach parallel mit Per-Frame-Timeout ──
        // 6 parallel → Deadlock. 3 parallel = guter Kompromiss.
        var qwenSw = Stopwatch.StartNew();
        var semaphore = new SemaphoreSlim(3, 3);
        int qwenDone = 0;
        int yoloOnlyCount = 0;
        int qwenFallbackCount = 0;
        int yoloZeroDetections = 0;
        int yoloNotRelevant = 0;
        var analysisResults = new BatchFrameAnalysis[relevantFrames.Count];

        // V4.2 Nachbesserung: Pro Target EINMAL verifizieren statt pro naheliegendem Frame.
        // Vorberechnung: Fuer jedes Target den Frame mit dem geringsten Meter-Abstand finden.
        // Nur diese Frame-Indizes werden im Protokoll-First-Modus verifiziert.
        HashSet<int>? verifyFrameIndices = null;
        if (protocolContext is not null && !protocolContext.InverseGapsMode)
        {
            var bestIdxPerTarget = new Dictionary<ProtocolTarget, (int Idx, double Dist)>();
            for (int i = 0; i < relevantFrames.Count; i++)
            {
                var f = relevantFrames[i];
                var em = f.Timestamp / duration *
                    (protocolContext.InspektionslaengeMeter ?? 100);
                var target = protocolContext.FindClosestTarget(em);
                if (target is null) continue;
                var center = (target.MeterStart + target.MeterEnd) / 2.0;
                var dist = Math.Abs(em - center);
                if (!bestIdxPerTarget.TryGetValue(target, out var prev) || dist < prev.Dist)
                    bestIdxPerTarget[target] = (i, dist);
            }
            verifyFrameIndices = new HashSet<int>(bestIdxPerTarget.Values.Select(v => v.Idx));
            _logger.LogInformation(
                "Protokoll-First: {Targets} Targets → {Frames} Verify-Frames (statt {All} roh)",
                bestIdxPerTarget.Count, verifyFrameIndices.Count, relevantFrames.Count);
        }

        var qwenTasks = relevantFrames.Select(async (frame, i) =>
        {
            await semaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                ct.ThrowIfCancellationRequested();
                using var frameCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                frameCts.CancelAfter(TimeSpan.FromSeconds(45));

                // YOLO-first: Wenn YOLO Detektionen hat, Qwen ueberspringen
                // DINO findet bei Kanalbildern nichts → DINO-Context nicht als Bedingung
                EnhancedFrameAnalysis analysis;
                bool hasYoloFindings = frame.Yolo.Detections.Count > 0 && frame.Yolo.IsRelevant;

                // Kennzahlen zaehlen
                if (frame.Yolo.Detections.Count == 0) Interlocked.Increment(ref yoloZeroDetections);
                if (!frame.Yolo.IsRelevant) Interlocked.Increment(ref yoloNotRelevant);
                if (hasYoloFindings) Interlocked.Increment(ref yoloOnlyCount);
                else Interlocked.Increment(ref qwenFallbackCount);

                // V4.2 Phase 2: Protokoll-First-Pfad.
                // Statt Open-Set-Klassifikation fragt Qwen gezielt: "Ist Code X bei Meter Y sichtbar?"
                // Im InverseGapsMode (Ueberraschungsfund-Pass) bleibt der Voll-Open-Set-Pfad aktiv.
                if (protocolContext is not null && !protocolContext.InverseGapsMode)
                {
                    // V4.2 Nachbesserung: Nur bestpassender Frame pro Target wird verifiziert.
                    if (verifyFrameIndices is null || !verifyFrameIndices.Contains(i))
                    {
                        Interlocked.Increment(ref qwenDone);
                        return;
                    }

                    var estimatedMeter = frame.Timestamp / duration *
                        (protocolContext.InspektionslaengeMeter ?? 100);
                    var target = protocolContext.FindClosestTarget(estimatedMeter);
                    if (target is null)
                    {
                        Interlocked.Increment(ref qwenDone);
                        return;
                    }

                    var verify = await _qwen.VerifyCodeAsync(
                        frame.Base64, target.VsaCode, target.MeterStart, target.Description, frameCts.Token)
                        .ConfigureAwait(false);

                    var findings = verify.Visible
                        ? new List<EnhancedFinding>
                        {
                            new(
                                Label: target.VsaCode,
                                VsaCodeHint: target.VsaCode,
                                Severity: verify.Severity ?? 3,
                                PositionClock: target.ClockPosition,
                                ExtentPercent: null,
                                HeightMm: null, WidthMm: null,
                                IntrusionPercent: null,
                                CrossSectionReductionPercent: null,
                                DiameterReductionMm: null,
                                Notes: $"verify:{verify.Confidence:F2} {verify.Notes}")
                        }
                        : new List<EnhancedFinding>();

                    analysis = new EnhancedFrameAnalysis(
                        Meter: estimatedMeter,
                        PipeMaterial: "unbekannt",
                        PipeDiameterMm: null,
                        Findings: findings,
                        ImageQuality: "mittel",
                        IsEmptyFrame: !verify.Visible,
                        Error: null,
                        ViewType: "axial");

                    var verifyDone = Interlocked.Increment(ref qwenDone);
                    progress?.Report(new BatchPipelineProgress(
                        verifyDone, relevantFrames.Count,
                        $"Verify {target.VsaCode}@{target.MeterStart:F1}m: " +
                        (verify.Visible ? "bestaetigt" : "nicht sichtbar")));

                    var persistedPath = PersistFrameIfFindings(
                        frameOutputDir, frame.Index, frame.Base64, analysis);
                    analysisResults[i] = new BatchFrameAnalysis(
                        frame.Index, frame.Timestamp, analysis,
                        frame.Yolo.Detections.Count, persistedPath);
                    return;
                }

                if (hasYoloFindings)
                {
                    // YOLO + DINO/SAM Kontext vorhanden → kein Qwen noetig
                    // Erstelle minimale Analyse aus YOLO-Daten
                    var yoloFindings = frame.Yolo.Detections.Select(d => new EnhancedFinding(
                        Label: d.ClassName,
                        VsaCodeHint: d.ClassName,
                        Severity: d.Confidence > 0.7 ? 2 : 1,
                        PositionClock: null,
                        ExtentPercent: null,
                        HeightMm: null, WidthMm: null,
                        IntrusionPercent: null,
                        CrossSectionReductionPercent: null,
                        DiameterReductionMm: null,
                        Notes: $"YOLO direct (conf={d.Confidence:F2})"
                    )).ToList();

                    analysis = new EnhancedFrameAnalysis(
                        Meter: null,
                        PipeMaterial: "unbekannt",
                        PipeDiameterMm: null,
                        Findings: yoloFindings,
                        ImageQuality: "mittel",
                        IsEmptyFrame: false,
                        Error: null,
                        ViewType: "axial");
                }
                else if (!hasYoloFindings && frameContexts.TryGetValue(frame.Index, out var ctx))
                {
                    // DINO/SAM Kontext aber kein YOLO → Qwen fragen
                    analysis = await _qwen.AnalyzeWithContextAsync(
                        frame.Base64, ctx, pipeDiameterMm, frameCts.Token)
                        .ConfigureAwait(false);
                }
                else
                {
                    // Kein Kontext → Qwen als Fallback
                    analysis = await _qwen.AnalyzeAsync(frame.Base64, frameCts.Token)
                        .ConfigureAwait(false);
                }
                var done = Interlocked.Increment(ref qwenDone);
                progress?.Report(new BatchPipelineProgress(
                    done, relevantFrames.Count,
                    hasYoloFindings
                        ? $"YOLO direct: {done}/{relevantFrames.Count}"
                        : $"Qwen Fallback: {done}/{relevantFrames.Count}"));

                var persistedPath2 = PersistFrameIfFindings(
                    frameOutputDir, frame.Index, frame.Base64, analysis);
                analysisResults[i] = new BatchFrameAnalysis(
                    frame.Index, frame.Timestamp, analysis,
                    frame.Yolo.Detections.Count, persistedPath2);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning("Qwen Timeout fuer Frame {Frame} — uebersprungen", frame.Index);
                Interlocked.Increment(ref qwenDone);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Qwen fehlgeschlagen fuer Frame {Frame}", frame.Index);
                Interlocked.Increment(ref qwenDone);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(qwenTasks).ConfigureAwait(false);

        allAnalyses.AddRange(analysisResults.Where(a => a is not null));
        sw.Stop();

        _logger.LogInformation(
            "BatchPipeline fertig: {Analyses} Analysen, {Duration:F1}s total " +
            "(YOLO={YoloMs}ms, Qwen={QwenMs}ms)",
            allAnalyses.Count, sw.Elapsed.TotalSeconds,
            yoloSw.ElapsedMilliseconds, qwenSw.ElapsedMilliseconds);

        _logger.LogInformation(
            "BatchPipeline Kennzahlen: YOLO-only={YoloOnly}/{Total} | Qwen-Fallback={QwenFB}/{Total} | " +
            "YOLO-zero-detections={ZeroDet}/{Total} | YOLO-not-relevant={NotRel}/{Total}",
            yoloOnlyCount, relevantFrames.Count,
            qwenFallbackCount, relevantFrames.Count,
            yoloZeroDetections, relevantFrames.Count,
            yoloNotRelevant, relevantFrames.Count);

        progress?.Report(new BatchPipelineProgress(
            relevantFrames.Count, relevantFrames.Count,
            $"Fertig: {allAnalyses.Count} Analysen | YOLO-only:{yoloOnlyCount} Qwen:{qwenFallbackCount} in {sw.Elapsed.TotalSeconds:F0}s"));

        return new BatchPipelineResult(
            allAnalyses,
            sw.Elapsed,
            frames.Count,
            relevantFrames.Count,
            skippedFrames);
    }

    /// <summary>
    /// V4.2: Persistiert einen Frame als PNG, wenn Findings vorhanden sind.
    /// Damit bekommt die Review-Queue echte Bilder zum Anzeigen.
    /// </summary>
    private string? PersistFrameIfFindings(
        string? frameOutputDir, int frameIndex, string base64Png,
        EnhancedFrameAnalysis analysis)
    {
        if (string.IsNullOrEmpty(frameOutputDir)) return null;
        if (!analysis.HasFindings) return null;
        if (string.IsNullOrEmpty(base64Png)) return null;
        try
        {
            Directory.CreateDirectory(frameOutputDir);
            var outPath = Path.Combine(frameOutputDir, $"frame_{frameIndex:D6}.png");
            File.WriteAllBytes(outPath, Convert.FromBase64String(base64Png));
            return outPath;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Frame-Persistierung fehlgeschlagen fuer Frame {F}", frameIndex);
            return null;
        }
    }

    private async Task<double> GetDurationAsync(string videoPath, CancellationToken ct)
    {
        try
        {
            var ffprobe = AuswertungPro.Next.Application.Ai.FfmpegLocator.ResolveFfprobe();
            // ArgumentList.Add statt Arguments-String: Command-Injection-Schutz.
            var psi = new System.Diagnostics.ProcessStartInfo
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
            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc is null) return 600;
            var output = await proc.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
            await proc.WaitForExitAsync(ct).ConfigureAwait(false);
            return double.TryParse(output.Trim(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 600;
        }
        catch { return 600; }
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────

/// <summary>Ergebnis einer Batch-Pipeline-Analyse.</summary>
public sealed record BatchPipelineResult(
    List<BatchFrameAnalysis> Analyses,
    TimeSpan Duration,
    int TotalFrames,
    int RelevantFrames,
    int SkippedFrames);

/// <summary>Einzelne Frame-Analyse aus der Batch-Pipeline.</summary>
public sealed record BatchFrameAnalysis(
    int FrameIndex,
    double TimestampSeconds,
    EnhancedFrameAnalysis QwenResult,
    int YoloDetectionCount,
    // V4.2: Persistierter Frame-Pfad (PNG), gesetzt wenn Findings vorhanden und frameOutputDir gegeben.
    string? FramePath = null);

/// <summary>Fortschritt der Batch-Pipeline.</summary>
public sealed record BatchPipelineProgress(
    int Done,
    int Total,
    string Status);

/// <summary>
/// V4.2 Phase 2: Protokoll-First-Kontext — das PDF-Protokoll als Ground-Truth-Fahrplan.
/// Pipeline analysiert nur noch gezielt die im Protokoll genannten Fundstellen,
/// statt blind alle 500 Frames zu raten.
/// </summary>
public sealed class ProtocolFirstContext
{
    /// <summary>Erwartete Fundstellen aus dem Protokoll.</summary>
    public required IReadOnlyList<ProtocolTarget> Targets { get; init; }

    /// <summary>Video-Dauer in Sekunden (fuer Meter-Schaetzung).</summary>
    public required double VideoDurationSeconds { get; init; }

    /// <summary>Geschaetzte Inspektionslaenge in Metern (fuer Linear-Mapping).</summary>
    public double? InspektionslaengeMeter { get; init; }

    /// <summary>Meter-Toleranz um jedes Target (Default 1.0m).</summary>
    public double MeterTolerance { get; init; } = 1.0;

    /// <summary>
    /// V4.2 Phase 2.4: Im Inverse-Modus liefert <see cref="ContainsMeter"/> das Gegenteil —
    /// nur Frames in den Luecken zwischen Protokoll-Zonen werden durchgelassen.
    /// Zusammen mit einem hoeheren FrameStep (z.B. 10s) ergibt das den Ueberraschungsfund-Pass.
    /// </summary>
    public bool InverseGapsMode { get; init; } = false;

    /// <summary>
    /// Prueft ob ein geschaetzter Meterstand in einer der Target-Zonen liegt.
    /// Im InverseGapsMode ist die Logik invertiert (nur Luecken bestehen den Check).
    /// </summary>
    public bool ContainsMeter(double estimatedMeter)
    {
        bool inTarget = false;
        foreach (var t in Targets)
        {
            if (estimatedMeter >= t.MeterStart - MeterTolerance &&
                estimatedMeter <= t.MeterEnd + MeterTolerance)
            {
                inTarget = true;
                break;
            }
        }
        return InverseGapsMode ? !inTarget : inTarget;
    }

    /// <summary>
    /// Findet das Target, das am besten zu einem Meterstand passt (naechster).
    /// </summary>
    public ProtocolTarget? FindClosestTarget(double estimatedMeter)
    {
        ProtocolTarget? best = null;
        double bestDist = double.MaxValue;
        foreach (var t in Targets)
        {
            var center = (t.MeterStart + t.MeterEnd) / 2.0;
            var dist = Math.Abs(center - estimatedMeter);
            if (dist < bestDist && dist <= MeterTolerance)
            {
                bestDist = dist;
                best = t;
            }
        }
        return best;
    }
}

/// <summary>
/// Ein einzelner Protokoll-Eintrag, auf den sich die Pipeline fokussieren soll.
/// </summary>
public sealed record ProtocolTarget(
    string VsaCode,
    double MeterStart,
    double MeterEnd,
    string? Description = null,
    string? ClockPosition = null);

/// <summary>
/// V4.2 Phase 2.3: Ergebnis einer gerichteten Verifikation durch Qwen.
/// "Ist Code X bei Meter Y im Frame sichtbar?" — Ja/Nein + Schweregrad.
/// </summary>
public sealed record DamageVerification(
    bool Visible,
    int? Severity,
    double Confidence,
    string? Notes)
{
    /// <summary>V4.2 Nachbesserung B: Pipeline-Version fuer Ursachenanalyse.</summary>
    public string PipelineVersion { get; init; } = PipelineVersions.Pipeline;

    /// <summary>V4.2 Nachbesserung B: Prompt-Version (VerifyPrompt).</summary>
    public string PromptVersion { get; init; } = PipelineVersions.VerifyPrompt;
}
