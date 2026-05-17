using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Diagnostics;
using AuswertungPro.Next.Application.Ai.Pipeline;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Application.Ai.Training.Models;
using AuswertungPro.Next.Application.Ai.Vision;
using AuswertungPro.Next.Domain.Ai.Vision;
using AuswertungPro.Next.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Ai.Pipeline;

// Audit 2026-04-23 ARCH-H5: MultiModelAnalysisService war 2185 LOC. Pure
// Helper, Plausibility-/Consensus-Filter und der ProtocolMerger sind hier
// extrahiert. Hauptdatei behaelt nur den GPU-State-Automaten (AnalyzeAsync,
// AnalyzeWithNvdecAsync, UpdateActive/FinalizeOrDiscard, GetVideoDuration).
public sealed partial class MultiModelAnalysisService
{
    // ── Conversion helper ──────────────────────────────────────────────

    /// <summary>
    /// Convert a MultiModelFrameResult to EnhancedFrameAnalysis
    /// (for compatibility with the existing pipeline).
    /// </summary>
    public static EnhancedFrameAnalysis ToEnhancedAnalysis(
        MultiModelFrameResult result,
        int pipeDiameterMm,
        Domain.Models.PipeCalibration? calibration = null)
    {
        if (!result.IsRelevant)
            return EnhancedFrameAnalysis.Empty();

        // K3: optionale Kalibrierung — falls uebergeben, nutzt QuantifyWithRatio statt 0.70.
        var quantified = new List<MaskQuantificationService.QuantifiedMask>();
        foreach (var mask in result.SamMasks)
        {
            quantified.Add(calibration != null
                ? MaskQuantificationService.Quantify(
                    mask, result.ImageWidth, result.ImageHeight, pipeDiameterMm, calibration)
                : MaskQuantificationService.Quantify(
                    mask, result.ImageWidth, result.ImageHeight, pipeDiameterMm));
        }

        var findings = new List<EnhancedFinding>(quantified.Count);
        for (var i = 0; i < quantified.Count; i++)
        {
            var q = quantified[i];
            if (string.IsNullOrWhiteSpace(q.Label))
                continue;

            var bbox = i < result.SamMasks.Count ? GetNormalizedBbox(result.SamMasks[i], result.ImageWidth, result.ImageHeight) : default;
            findings.Add(new EnhancedFinding(
                Label: q.Label,
                VsaCodeHint: VsaCodeResolver.InferCodeFromLabel(q.Label),
                Severity: EstimateSeverity(q),
                PositionClock: NormalizeClockPosition(q.ClockPosition),
                ExtentPercent: q.ExtentPercent,
                HeightMm: q.HeightMm,
                WidthMm: q.WidthMm,
                IntrusionPercent: q.IntrusionPercent,
                CrossSectionReductionPercent: q.CrossSectionReductionPercent,
                DiameterReductionMm: null,
                BboxX1Norm: bbox.X1,
                BboxY1Norm: bbox.Y1,
                BboxX2Norm: bbox.X2,
                BboxY2Norm: bbox.Y2,
                Notes: null
            ));
        }

        return new EnhancedFrameAnalysis(
            Meter: result.Meter,
            PipeMaterial: "unbekannt",
            PipeDiameterMm: pipeDiameterMm,
            Findings: findings,
            ImageQuality: "gut",
            IsEmptyFrame: false,
            Error: null);
    }

    // ── Private helpers ────────────────────────────────────────────────

    // Audit 2026-05-13 M1: Fachlogik nach Application/Ai/Pipeline/SeverityEstimator verschoben.
    private static int EstimateSeverity(MaskQuantificationService.QuantifiedMask q)
        => SeverityEstimator.Estimate(q);

    private static (double? X1, double? Y1, double? X2, double? Y2) GetNormalizedBbox(
        SamMaskResult mask,
        int imageWidth,
        int imageHeight)
    {
        if (mask.Bbox == null || mask.Bbox.Count < 4 || imageWidth <= 0 || imageHeight <= 0)
            return default;

        return (
            Clamp01(mask.Bbox[0] / imageWidth),
            Clamp01(mask.Bbox[1] / imageHeight),
            Clamp01(mask.Bbox[2] / imageWidth),
            Clamp01(mask.Bbox[3] / imageHeight));
    }

    private static double Clamp01(double value) => Math.Clamp(value, 0.0, 1.0);

    private async Task<List<EnhancedFinding>> AnalyzePipeAxisGeometryAsync(
        string frameBase64,
        double position,
        int pipeDiameterMm,
        Queue<(PipeAxisResult Axis, double Position)> history,
        PipeAxisBendDetector detector,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(frameBase64))
            return new List<EnhancedFinding>();

        PipeAxisResult axis;
        try
        {
            axis = await _client.AnalyzePipeAxisAsync(
                new PipeAxisRequest(frameBase64, pipeDiameterMm), ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "PipeAxis geometry failed at position {Position:F2}", position);
            return new List<EnhancedFinding>();
        }

        history.Enqueue((axis, position));
        while (history.Count > 8)
            history.Dequeue();

        if (history.Count < detector.MinWindowSize)
            return new List<EnhancedFinding>();

        var bend = detector.Detect(history.ToList());
        if (string.IsNullOrWhiteSpace(bend.RecommendedCode) || bend.Confidence < 0.12)
            return new List<EnhancedFinding>();

        return new List<EnhancedFinding>
        {
            new(
                Label: $"Bogen {FormatBendDirection(bend.Direction)}",
                VsaCodeHint: bend.RecommendedCode,
                Severity: 1,
                PositionClock: null,
                ExtentPercent: null,
                HeightMm: null,
                WidthMm: null,
                IntrusionPercent: null,
                CrossSectionReductionPercent: null,
                DiameterReductionMm: null,
                Notes: bend.DiagnosticText)
        };
    }

    private bool TryEmitGeometryOnlyFrame(
        IReadOnlyList<EnhancedFinding> geometryFindings,
        Dictionary<string, ActiveFindingState> active,
        List<RawVideoDetection> detections,
        double meter,
        double timestampSeconds,
        int frameIndex,
        int totalFrames,
        byte[]? frameBytes,
        IProgress<VideoAnalysisProgress>? progress,
        string statusSuffix)
    {
        if (geometryFindings.Count == 0)
            return false;

        var findings = geometryFindings.ToList();
        UpdateActive(active, findings, meter, timestampSeconds, detections, evidence: null);

        progress?.Report(new VideoAnalysisProgress(
            frameIndex,
            totalFrames,
            $"Frame {frameIndex}/{totalFrames} @ {meter:0.0}m - Geometrie: {findings.Count} Befund(e) ({statusSuffix})",
            FramePreviewPng: frameBytes,
            LiveFindings: ToLiveFindings(findings),
            TimestampSeconds: timestampSeconds));

        return true;
    }

    private static IReadOnlyList<LiveFrameFinding> ToLiveFindings(IEnumerable<EnhancedFinding> findings)
        => findings.Select(f => new LiveFrameFinding(
            Label: f.Label,
            Severity: f.Severity,
            PositionClock: f.PositionClock,
            ExtentPercent: f.ExtentPercent,
            VsaCodeHint: f.VsaCodeHint,
            HeightMm: f.HeightMm,
            WidthMm: f.WidthMm,
            IntrusionPercent: f.IntrusionPercent,
            CrossSectionReductionPercent: f.CrossSectionReductionPercent,
            DiameterReductionMm: f.DiameterReductionMm)).ToList();

    private static string FormatBendDirection(PipeAxisBendDetector.BendDirection direction)
        => direction switch
        {
            PipeAxisBendDetector.BendDirection.Left => "links",
            PipeAxisBendDetector.BendDirection.Right => "rechts",
            PipeAxisBendDetector.BendDirection.Up => "oben",
            PipeAxisBendDetector.BendDirection.Down => "unten",
            PipeAxisBendDetector.BendDirection.Straight => "gerade",
            _ => "unbekannt"
        };

    private static bool ShouldSuppressByImageQuality(string? imageQuality, double dinoConf)
    {
        if (!string.Equals(imageQuality, "schlecht", StringComparison.OrdinalIgnoreCase))
            return false;

        // Niedrige DINO-Konfidenz + schlechte Bildqualitaet => likely false positive.
        // Bei starken DINO-Hinweisen behalten wir Findings fuer bessere Recall.
        return dinoConf < 0.35;
    }

    private static void RecordYoloRawDiagnostics(
        int frameIndex,
        double timestampSeconds,
        double meter,
        YoloResponse response,
        double latencyMs,
        string source)
    {
        AiDiagnosticsRecorderProvider.Current.Record(new AiDiagnosticEvent
        {
            Stage = AiDiagnosticStage.YoloRaw,
            Source = source,
            Summary = $"Frame {frameIndex}: YOLO relevant={response.IsRelevant}, boxes={response.Detections.Count}, class={response.FrameClass ?? "-"}",
            RawOutput = FormatYoloDetections(response.Detections),
            LatencyMs = latencyMs,
            Metadata = new Dictionary<string, string>
            {
                ["frame_index"] = frameIndex.ToString(CultureInfo.InvariantCulture),
                ["timestamp_seconds"] = timestampSeconds.ToString("F2", CultureInfo.InvariantCulture),
                ["meter"] = meter.ToString("F2", CultureInfo.InvariantCulture),
                ["boxes"] = response.Detections.Count.ToString(CultureInfo.InvariantCulture),
                ["is_relevant"] = response.IsRelevant.ToString(),
                ["frame_class"] = response.FrameClass ?? ""
            }
        });
    }

    private static void RecordDinoRawDiagnostics(
        int frameIndex,
        double timestampSeconds,
        double meter,
        DinoResponse response,
        IReadOnlyList<DinoDetectionDto> filtered,
        double latencyMs,
        string source)
    {
        AiDiagnosticsRecorderProvider.Current.Record(new AiDiagnosticEvent
        {
            Stage = AiDiagnosticStage.MultiModelRaw,
            Source = source,
            Summary = $"Frame {frameIndex}: DINO raw={response.Detections.Count}, filtered={filtered.Count}",
            RawOutput = FormatDinoDetections(response.Detections),
            LatencyMs = latencyMs,
            Metadata = new Dictionary<string, string>
            {
                ["stage"] = "dino",
                ["frame_index"] = frameIndex.ToString(CultureInfo.InvariantCulture),
                ["timestamp_seconds"] = timestampSeconds.ToString("F2", CultureInfo.InvariantCulture),
                ["meter"] = meter.ToString("F2", CultureInfo.InvariantCulture),
                ["raw_boxes"] = response.Detections.Count.ToString(CultureInfo.InvariantCulture),
                ["filtered_boxes"] = filtered.Count.ToString(CultureInfo.InvariantCulture)
            }
        });
    }

    private static void RecordSamRawDiagnostics(
        int frameIndex,
        double timestampSeconds,
        double meter,
        SamResponse response,
        int promptBoxes,
        bool usedRingScan,
        double latencyMs,
        string source)
    {
        AiDiagnosticsRecorderProvider.Current.Record(new AiDiagnosticEvent
        {
            Stage = AiDiagnosticStage.MultiModelRaw,
            Source = source,
            Summary = $"Frame {frameIndex}: SAM masks={response.Masks.Count}, boxes={promptBoxes}, ring_scan={usedRingScan}",
            RawOutput = FormatSamMasks(response.Masks),
            LatencyMs = latencyMs,
            Metadata = new Dictionary<string, string>
            {
                ["stage"] = "sam",
                ["frame_index"] = frameIndex.ToString(CultureInfo.InvariantCulture),
                ["timestamp_seconds"] = timestampSeconds.ToString("F2", CultureInfo.InvariantCulture),
                ["meter"] = meter.ToString("F2", CultureInfo.InvariantCulture),
                ["prompt_boxes"] = promptBoxes.ToString(CultureInfo.InvariantCulture),
                ["masks"] = response.Masks.Count.ToString(CultureInfo.InvariantCulture),
                ["ring_scan"] = usedRingScan.ToString(),
                ["image_size"] = $"{response.ImageWidth}x{response.ImageHeight}"
            }
        });
    }

    private static string FormatYoloDetections(IReadOnlyList<YoloDetectionDto> detections)
        => string.Join(Environment.NewLine, detections.Select(d =>
            $"{d.ClassName} conf={d.Confidence:F2} box=[{d.X1:F0},{d.Y1:F0},{d.X2:F0},{d.Y2:F0}]"));

    private static string FormatDinoDetections(IReadOnlyList<DinoDetectionDto> detections)
        => string.Join(Environment.NewLine, detections.Select(d =>
            $"{d.Label} conf={d.Confidence:F2} fallback={d.IsFallbackFromYolo} phrase={d.Phrase ?? ""} box=[{d.X1:F0},{d.Y1:F0},{d.X2:F0},{d.Y2:F0}]"));

    private static string FormatSamMasks(IReadOnlyList<SamMaskResult> masks)
        => string.Join(Environment.NewLine, masks.Select(m =>
            $"{m.Label} conf={m.Confidence:F2} area={m.MaskAreaPixels} centroid=({m.CentroidX:F0},{m.CentroidY:F0})"));

    private static double EstimateQwenVisionConfidence(string? imageQuality, bool hasFindings)
    {
        var baseConf = imageQuality?.ToLowerInvariant() switch
        {
            "gut" => 0.85,
            "mittel" => 0.65,
            "schlecht" => 0.35,
            _ => 0.55
        };

        if (hasFindings)
            baseConf += 0.05;

        return Math.Clamp(baseConf, 0.0, 1.0);
    }

    /// <summary>Geschaetzte Haltungslaenge in Metern (wird durch OSD-Korrektur von Qwen ueberschrieben).</summary>
    public double EstimatedReachLengthM { get; set; } = 50.0; // Typisch 15-80m, Fallback 50m

    private double EstimateMeter(double t, double duration, ref double lastMeter)
    {
        // Lineare Schaetzung basierend auf geschaetzter Haltungslaenge (wird durch Qwen OSD korrigiert)
        var estimated = t / Math.Max(duration, 1.0) * EstimatedReachLengthM;
        lastMeter = Math.Max(lastMeter, estimated);
        return Math.Round(lastMeter, 2);
    }

    private static string NormalizePath(string path)
    {
        path = path.Trim();
        if (path.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            path = new Uri(path).LocalPath;
        return Path.GetFullPath(path);
    }

    private static (int Width, int Height) ReadPngDimensions(byte[]? pngBytes)
    {
        if (pngBytes is null || pngBytes.Length < 24)
            return (720, 576);

        try
        {
            int w = (pngBytes[16] << 24) | (pngBytes[17] << 16) | (pngBytes[18] << 8) | pngBytes[19];
            int h = (pngBytes[20] << 24) | (pngBytes[21] << 16) | (pngBytes[22] << 8) | pngBytes[23];
            return (w > 0 && h > 0) ? (w, h) : (720, 576);
        }
        catch
        {
            return (720, 576);
        }
    }

    private void UpdateActive(
        Dictionary<string, ActiveFindingState> active,
        List<EnhancedFinding> current,
        double meter,
        double timestampSeconds,
        List<RawVideoDetection> completed,
        AuswertungPro.Next.Application.Ai.QualityGate.EvidenceVector? evidence = null)
    {
        var currentMap = new Dictionary<string, EnhancedFinding>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in current)
        {
            var key = BuildFindingKey(f);
            if (!currentMap.ContainsKey(key))
                currentMap[key] = f;
        }

        foreach (var key in active.Keys.ToList())
        {
            if (currentMap.TryGetValue(key, out var finding))
            {
                // BBox Y-Zentrum fuer Bestaetigungs-Tracking berechnen.
                // Klammern um die Null-Coalescing-Ausdruecke sind ZWINGEND noetig, da `??`
                // schwaecher bindet als `+` -> ohne Klammern wuerde Y2 ignoriert sobald Y1 gesetzt ist.
                double yCenter = ((finding.BboxY1Norm ?? 0) + (finding.BboxY2Norm ?? 1)) / 2.0;
                active[key].Update(meter, timestampSeconds, finding.Severity, finding.VsaCodeHint, finding.PositionClock,
                    finding.ExtentPercent, finding.HeightMm, finding.WidthMm,
                    finding.IntrusionPercent, finding.CrossSectionReductionPercent, finding.DiameterReductionMm,
                    evidence, yCenter);
            }
            else
            {
                active[key].MissedFrames++;
                if (active[key].MissedFrames >= DedupWindowFrames)
                {
                    FinalizeOrDiscard(active, key, completed);
                }
            }
        }

        foreach (var pair in currentMap)
        {
            if (!active.ContainsKey(pair.Key))
            {
                var f = pair.Value;
                double yCenter = ((f.BboxY1Norm ?? 0) + (f.BboxY2Norm ?? 1)) / 2.0;
                active[pair.Key] = new ActiveFindingState(
                    f.Label.Trim(), meter, timestampSeconds, f.Severity, f.VsaCodeHint, f.PositionClock,
                    f.ExtentPercent, f.HeightMm, f.WidthMm,
                    f.IntrusionPercent, f.CrossSectionReductionPercent, f.DiameterReductionMm,
                    evidence, yCenter);
            }
        }
    }

    /// <summary>
    /// Finalisiert oder verwirft einen Befund basierend auf Bestaetigung.
    /// Unbestaetigte Ferndetektionen werden still verworfen (Selbstkorrektur).
    /// </summary>
    private void FinalizeOrDiscard(
        Dictionary<string, ActiveFindingState> active,
        string key,
        List<RawVideoDetection> completed)
    {
        var state = active[key];
        if (state.ShouldFinalize)
        {
            completed.Add(state.ToDetection());
        }
        else
        {
            _logger.LogDebug(
                "Selbstkorrektur: '{Name}' verworfen — {Frames} Frames, MaxY={Y:F2}, bestaetigt={Confirmed}",
                state.Name, state.FrameCount, state.MaxYCenter, state.IsConfirmed);
        }
        active.Remove(key);
    }

    private static void AdvanceAll(
        Dictionary<string, ActiveFindingState> active,
        List<RawVideoDetection> completed,
        int dedupWindow)
    {
        foreach (var key in active.Keys.ToList())
        {
            active[key].MissedFrames++;
            if (active[key].MissedFrames >= dedupWindow)
            {
                // Nur bestaetigte Befunde finalisieren
                if (active[key].ShouldFinalize)
                    completed.Add(active[key].ToDetection());
                active.Remove(key);
            }
        }
    }

    private async Task<double> GetVideoDurationAsync(string videoPath, CancellationToken ct)
    {
        var probePath = DeriveFfprobePath(_ffmpegPath);
        var psi = new ProcessStartInfo
        {
            FileName = probePath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("-v");
        psi.ArgumentList.Add("error");
        psi.ArgumentList.Add("-show_entries");
        psi.ArgumentList.Add("format=duration");
        psi.ArgumentList.Add("-of");
        psi.ArgumentList.Add("default=noprint_wrappers=1:nokey=1");
        psi.ArgumentList.Add(videoPath);

        try
        {
            using var p = Process.Start(psi);
            if (p is null) return 0;
            var stdout = await p.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
            await p.WaitForExitAsync(ct).ConfigureAwait(false);
            if (double.TryParse(stdout.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var dur))
                return dur;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MultiModelAnalysis] ffprobe fehlgeschlagen: {ex.Message}");
        }
        return 0;
    }

    private static string DeriveFfprobePath(string ffmpegPath)
    {
        if (string.IsNullOrWhiteSpace(ffmpegPath) ||
            string.Equals(ffmpegPath, "ffmpeg", StringComparison.OrdinalIgnoreCase))
            return "ffprobe";
        var dir = Path.GetDirectoryName(ffmpegPath);
        var ext = Path.GetExtension(ffmpegPath);
        return string.IsNullOrWhiteSpace(dir) ? "ffprobe" + ext : Path.Combine(dir, "ffprobe" + ext);
    }

    /// <summary>
    /// Baut einen stabilen Dedup-Key fuer ein Finding.
    /// Normalisiert Labels gegen DINO-Phrasen-Drift (crack/fracture/break → gleicher Key)
    /// und Clock-Positionen (3:00/3/rechts → normalisierte Stunde).
    /// </summary>
    private static string BuildFindingKey(EnhancedFinding f)
    {
        var label = VsaCodeResolver.NormalizeFindingCode(f.VsaCodeHint)
            ?? VsaCodeResolver.InferCodeFromLabel(f.Label)
            ?? NormalizeFindingLabel(f.Label.Trim());
        var clock = NormalizeClockPosition(f.PositionClock);
        // Keine Dimensionen im Key — wachsende Schaeden (z.B. 5x3 → 8x5mm)
        // wuerden sonst neue Keys erzeugen und die Dedup brechen.
        // Maximalwerte werden stattdessen in UpdateActive() aktualisiert.
        if (string.IsNullOrEmpty(clock))
            return label;
        return $"{label}|{clock}";
    }

    /// <summary>
    /// Normalisiert DINO-Labels auf kanonische Gruppen.
    /// "crack", "fracture", "break" → "crack"
    /// "root intrusion", "roots" → "roots"
    /// Reduziert Label-Drift zwischen Frames.
    /// </summary>
    private static string NormalizeFindingLabel(string label)
    {
        var lower = label.ToLowerInvariant();

        // Risse/Brueche
        if (lower.Contains("crack") || lower.Contains("fracture") || lower.Contains("riss"))
            return "crack";
        if (lower.Contains("break") || lower.Contains("bruch") || lower.Contains("collapse") || lower.Contains("einsturz"))
            return "break";

        // Deformation
        if (lower.Contains("deform") || lower.Contains("verform") || lower.Contains("dent") || lower.Contains("oval"))
            return "deformation";

        // Wurzeln
        if (lower.Contains("root") || lower.Contains("wurzel"))
            return "roots";

        // Korrosion / Oberflaechenschaden
        if (lower.Contains("corros") || lower.Contains("erosion") || lower.Contains("surface damage") || lower.Contains("abplatz"))
            return "corrosion";

        // Ablagerung
        if (lower.Contains("deposit") || lower.Contains("sediment") || lower.Contains("buildup")
            || lower.Contains("ablagerung") || lower.Contains("inkrust"))
            return "deposit";

        // Infiltration
        if (lower.Contains("infiltrat") || lower.Contains("ingress") || lower.Contains("leak")
            || lower.Contains("undicht") || lower.Contains("fremdwasser"))
            return "infiltration";

        // Versatz
        if (lower.Contains("displace") || lower.Contains("offset") || lower.Contains("versatz") || lower.Contains("joint"))
            return "displacement";

        // Hindernis
        if (lower.Contains("obstacle") || lower.Contains("blockage") || lower.Contains("obstruct") || lower.Contains("hindernis"))
            return "obstacle";

        // Anschluss
        if (lower.Contains("connection") || lower.Contains("anschluss") || lower.Contains("intrud") || lower.Contains("protrud"))
            return "connection";

        return lower;
    }

    /// <summary>
    /// Normalisiert Clock-Positionen auf ganzzahlige Stunden.
    /// "3:00" → "3", "12" → "12", "Scheitel" → "12", "Sohle" → "6", "rechts" → "3", "links" → "9".
    /// </summary>
    private static string? NormalizeClockPosition(string? clock)
    {
        var normalized = VsaCodeResolver.NormalizeClock(clock);
        if (string.IsNullOrWhiteSpace(normalized))
            return null;
        return normalized;
    }

    // ── Materialplausibilitaet ─────────────────────────────────────────────

    // Audit 2026-05-13 M1: Fachlogik nach Application/Ai/Pipeline/MaterialPlausibilityFilter
    // verschoben. Hier nur noch der Dispatcher mit Bezug zu Service-State (_config, _logger).

    private void ApplyMaterialPlausibilityFilter(List<RawVideoDetection> detections, string? materialOverride = null)
        => MaterialPlausibilityFilter.Apply(detections, materialOverride ?? _config.PipeMaterial, _logger);

    private static string MapMaterialToAedCode(string material)
        => MaterialPlausibilityFilter.MapMaterialToAedCode(material);

    private static bool IsKunststoffMaterial(string material)
        => MaterialPlausibilityFilter.IsKunststoff(material);

    // ── ActiveFindingState (mirrors VideoFullAnalysisService.ActiveFinding) ──

    // Audit 2026-05-13 M1: Fachlogik nach Application/Ai/Pipeline/ConsensusQualityFilter verschoben.
    private void ApplyConsensusAndQualityFilter(List<RawVideoDetection> detections)
        => ConsensusQualityFilter.Apply(detections, _logger);

    private sealed class ActiveFindingState
    {
        public string Name { get; }
        public double MeterStart { get; private set; }
        public double MeterEnd { get; private set; }
        public double TimestampStartSeconds { get; private set; }
        public double TimestampEndSeconds { get; private set; }
        public double? ConfirmedTimestampSeconds { get; private set; }
        public int MaxSeverity { get; private set; }
        public string? VsaCodeHint { get; private set; }
        public string? PositionClock { get; private set; }
        public int? ExtentPercent { get; private set; }
        public int? HeightMm { get; private set; }
        public int? WidthMm { get; private set; }
        public int? IntrusionPercent { get; private set; }
        public int? CrossSectionReductionPercent { get; private set; }
        public int? DiameterReductionMm { get; private set; }
        public AuswertungPro.Next.Application.Ai.QualityGate.EvidenceVector? Evidence { get; private set; }
        public int FrameCount { get; private set; } = 1;
        public int MissedFrames { get; set; }

        // ── Bestaetigungs-Tracking ──────────────────────────────────
        // Ein Befund gilt erst als bestaetigt wenn er mindestens einmal
        // auf Kamerahoehe (Y >= 0.30) gesehen wurde. Ferndetektionen
        // (oberes Bilddrittel) allein reichen nicht.

        /// <summary>True wenn die Detection mindestens einmal auf Kamerahoehe bestaetigt wurde.</summary>
        public bool IsConfirmed { get; private set; }

        /// <summary>Naechste Y-Position (normiert) an der die Detection gesehen wurde. Hoeher = naeher.</summary>
        public double MaxYCenter { get; private set; }

        /// <summary>Meterstand bei Bestaetigung (naeher = genauer).</summary>
        public double? ConfirmedMeter { get; private set; }

        /// <summary>Mindestanzahl Frames bevor ein Befund finalisiert wird.</summary>
        public const int MinConfirmationFrames = 2;

        /// <summary>Y-Schwelle ab der eine Detection als "auf Kamerahoehe" gilt (normiert).</summary>
        private const double ConfirmationYThreshold = 0.30;

        public ActiveFindingState(
            string name, double start, double timestampSeconds, int severity, string? hint, string? clock,
            int? extent, int? height, int? width, int? intrusion, int? crossSection, int? diameterReduction,
            AuswertungPro.Next.Application.Ai.QualityGate.EvidenceVector? evidence = null,
            double bboxYCenterNorm = 0.5)
        {
            Name = name; MeterStart = start; MeterEnd = start;
            TimestampStartSeconds = timestampSeconds;
            TimestampEndSeconds = timestampSeconds;
            MaxSeverity = severity; VsaCodeHint = hint; PositionClock = clock;
            ExtentPercent = extent; HeightMm = height; WidthMm = width;
            IntrusionPercent = intrusion; CrossSectionReductionPercent = crossSection;
            DiameterReductionMm = diameterReduction;
            Evidence = evidence;
            MaxYCenter = bboxYCenterNorm;
            if (bboxYCenterNorm >= ConfirmationYThreshold)
            {
                IsConfirmed = true;
                ConfirmedMeter = start;
                ConfirmedTimestampSeconds = timestampSeconds;
            }
        }

        public void Update(double meter, double timestampSeconds, int severity, string? hint, string? clock,
            int? extent, int? height, int? width, int? intrusion, int? crossSection, int? diameterReduction,
            AuswertungPro.Next.Application.Ai.QualityGate.EvidenceVector? evidence = null,
            double bboxYCenterNorm = 0.5)
        {
            MeterEnd = meter;
            TimestampEndSeconds = timestampSeconds;
            MissedFrames = 0;
            FrameCount++;
            if (severity > MaxSeverity) MaxSeverity = severity;
            if (!string.IsNullOrWhiteSpace(hint)) VsaCodeHint = hint;
            if (!string.IsNullOrWhiteSpace(clock)) PositionClock = clock;
            if (extent is { } e) ExtentPercent = Math.Max(ExtentPercent ?? 0, Math.Clamp(e, 1, 100));
            if (height is { } h) HeightMm = Math.Max(HeightMm ?? 0, h);
            if (width is { } w) WidthMm = Math.Max(WidthMm ?? 0, w);
            if (intrusion is { } ip) IntrusionPercent = Math.Max(IntrusionPercent ?? 0, ip);
            if (crossSection is { } csr) CrossSectionReductionPercent = Math.Max(CrossSectionReductionPercent ?? 0, csr);
            if (diameterReduction is { } dr) DiameterReductionMm = Math.Max(DiameterReductionMm ?? 0, dr);
            if (evidence is not null)
            {
                Evidence = Evidence is null ? evidence : MergeEvidence(Evidence, evidence);
            }

            // Bestaetigungs-Tracking: wenn naeher gesehen → Meter korrigieren
            if (bboxYCenterNorm > MaxYCenter)
            {
                MaxYCenter = bboxYCenterNorm;
                if (bboxYCenterNorm >= ConfirmationYThreshold && !IsConfirmed)
                {
                    IsConfirmed = true;
                    ConfirmedMeter = meter;
                    ConfirmedTimestampSeconds = timestampSeconds;
                    // Meter korrigieren: Bestaetigung auf Kamerahoehe ist genauer
                    MeterStart = meter;
                }
            }
        }

        /// <summary>
        /// True wenn der Befund finalisiert werden soll (genug Frames + bestaetigt).
        /// Unbestaetigte Ferndetektionen werden still verworfen.
        /// </summary>
        public bool ShouldFinalize => IsConfirmed && (FrameCount >= MinConfirmationFrames || IsSingleFrameEventCode);

        private bool IsSingleFrameEventCode
        {
            get
            {
                var code = VsaCodeResolver.NormalizeFindingCode(VsaCodeHint) ?? VsaCodeHint;
                if (string.IsNullOrWhiteSpace(code) || code.Length < 2)
                    return false;

                var prefix2 = code[..2].ToUpperInvariant();
                var prefix3 = code.Length >= 3 ? code[..3].ToUpperInvariant() : prefix2;
                return prefix2 is "BC" or "BD" or "AE"
                    || prefix3 is "BCA" or "BCC" or "BCD" or "BCE";
            }
        }

        public RawVideoDetection ToDetection() =>
            new(Name,
                ConfirmedMeter ?? MeterStart, // Bestaetigung-Meter hat Vorrang
                MeterEnd,
                SeverityLabel(MaxSeverity), VsaCodeHint, PositionClock,
                ExtentPercent, HeightMm, WidthMm, IntrusionPercent, CrossSectionReductionPercent, DiameterReductionMm,
                Evidence: Evidence is not null ? Evidence with { FrameCount = FrameCount } : null,
                TimestampSeconds: ConfirmedTimestampSeconds ?? TimestampStartSeconds);

        private static string SeverityLabel(int s) => s >= 4 ? "high" : s == 3 ? "mid" : "low";

        private static AuswertungPro.Next.Application.Ai.QualityGate.EvidenceVector MergeEvidence(AuswertungPro.Next.Application.Ai.QualityGate.EvidenceVector a, AuswertungPro.Next.Application.Ai.QualityGate.EvidenceVector b) =>
            new(
                YoloConf: Max(a.YoloConf, b.YoloConf),
                DinoConf: Max(a.DinoConf, b.DinoConf),
                SamMaskStability: Max(a.SamMaskStability, b.SamMaskStability),
                QwenVisionConf: Max(a.QwenVisionConf, b.QwenVisionConf),
                LlmCodeConf: Max(a.LlmCodeConf, b.LlmCodeConf),
                KbSimilarity: Max(a.KbSimilarity, b.KbSimilarity),
                KbCodeAgreement: a.KbCodeAgreement ?? b.KbCodeAgreement,
                PlausibilityScore: Max(a.PlausibilityScore, b.PlausibilityScore),
                DamageCategory: a.DamageCategory ?? b.DamageCategory,
                FrameCount: (a.FrameCount ?? 0) + (b.FrameCount ?? 0)
            );

        private static double? Max(double? a, double? b) =>
            a.HasValue && b.HasValue ? Math.Max(a.Value, b.Value)
            : a ?? b;
    }
}
