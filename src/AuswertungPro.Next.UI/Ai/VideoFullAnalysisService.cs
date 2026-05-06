using System;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Vision;
using AuswertungPro.Next.Domain.Ai.Vision;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using AuswertungPro.Next.Application.Ai.QualityGate;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.UI.Ai;

/// <summary>
/// Kompletter Video-Workflow: Video -> alle Schäden in einem Durchgang.
///
/// FIXES in dieser Version:
/// - Bug 1.2: AdvanceActiveFindings nutzt jetzt DedupWindowFrames (nicht hardcoded 3)
/// - Bug 1.4: EnhancedVisionAnalysisService eingebunden (detaillierterer Prompt,
///            Uhrzeitlage, Rohrmaterial, vsa_code_hint direkt aus Vision)
/// </summary>
public sealed class VideoFullAnalysisService
{
    // BUG 1.4 FIX: EnhancedVisionAnalysisService statt OllamaVisionFindingsService
    private readonly EnhancedVisionAnalysisService _vision;
    private readonly string _ffmpegPath;
    private readonly string _ffprobePath;

    /// <summary>Optional: Knick-Erkennung (BAG) via Fluchtpunkt-Tracking.</summary>
    public KnickDetectionService? KnickDetection { get; set; }

    public double FrameStepSeconds { get; set; } = 1.5;
    public int DedupWindowFrames { get; set; } = 3;
    public int MinSeverity { get; set; } = 1;
    public TimeSpan VisionFrameTimeout { get; set; } = TimeSpan.FromSeconds(300);

    public VideoFullAnalysisService(
        EnhancedVisionAnalysisService vision,
        string ffmpegPath = "ffmpeg",
        string? ffprobePath = null)
    {
        _vision = vision;
        _ffmpegPath = ffmpegPath;
        _ffprobePath = ffprobePath ?? DeriveFFprobePath(ffmpegPath);
    }

    /// <summary>
    /// Rückwärtskompatibel: Erstellt aus OllamaClient direkt (kein separater Service nötig).
    /// </summary>
    public static VideoFullAnalysisService Create(
        OllamaClient client,
        string visionModel,
        string? referenceModel = null,
        string ffmpegPath = "ffmpeg")
        // Phase 0.4: Batch/Video nutzt vollen Damage-Prompt (mit Aufnahmetechnik).
        => new(new EnhancedVisionAnalysisService(client, visionModel, referenceModel, useFullDamagePrompt: true), ffmpegPath);

    public async Task<VideoAnalysisResult> AnalyzeAsync(
        string videoPath,
        IProgress<VideoAnalysisProgress>? progress = null,
        CancellationToken ct = default)
    {
        // Pfad normalisieren
        videoPath = videoPath.Trim();
        if (videoPath.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            videoPath = new Uri(videoPath).LocalPath;
        videoPath = Path.GetFullPath(videoPath);

        if (!File.Exists(videoPath))
            return VideoAnalysisResult.Failed($"Video nicht gefunden: {videoPath}");

        _lastKnownMeter = 0;
        KnickDetection?.Reset();
        progress?.Report(new VideoAnalysisProgress(0, 0, "Videodauer wird ermittelt..."));

        var (duration, probeError) = await GetVideoDurationWithErrorAsync(videoPath, ct).ConfigureAwait(false);
        if (duration <= 0)
            return VideoAnalysisResult.Failed($"Videodauer konnte nicht ermittelt werden (ffprobe): {probeError}");

        var totalFrames = (int)Math.Ceiling(duration / FrameStepSeconds);
        var detections = new List<RawVideoDetection>();
        var active = new Dictionary<string, ActiveFinding>(StringComparer.OrdinalIgnoreCase);
        var frameIndex = 0;

        progress?.Report(new VideoAnalysisProgress(0, totalFrames, "Analyse gestartet..."));

        var telemetry = new AuswertungPro.Next.Application.Ai.Pipeline.PipelineTelemetry();

        await using var frameStream = VideoFrameStream.Open(
            _ffmpegPath, videoPath, FrameStepSeconds, duration, ct);

        await foreach (var frame in frameStream.ReadFramesAsync(ct).ConfigureAwait(false))
        {
            ct.ThrowIfCancellationRequested();
            var frameSw = System.Diagnostics.Stopwatch.StartNew();
            frameIndex++;
            var t = frame.TimestampSeconds;

            var extractionMs = frameSw.ElapsedMilliseconds;
            var frameBytes = frame.PngBytes;

            if (frameBytes is null or { Length: 0 })
            {
                telemetry.RecordFrame(new AuswertungPro.Next.Application.Ai.Vision.FrameTiming(frameIndex, t, extractionMs, 0, 0, 0, 0, frameSw.ElapsedMilliseconds, Skipped: true));
                AdvanceAll(active, detections, DedupWindowFrames);
                progress?.Report(new VideoAnalysisProgress(frameIndex, totalFrames,
                    $"Frame {frameIndex}/{totalFrames} – kein Bild"));
                continue;
            }

            progress?.Report(new VideoAnalysisProgress(
                frameIndex,
                totalFrames,
                $"Frame {frameIndex}/{totalFrames} – Bild extrahiert",
                FramePreviewPng: frameBytes));

            EnhancedFrameAnalysis analysis;
            var visionSw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                progress?.Report(new VideoAnalysisProgress(
                    frameIndex,
                    totalFrames,
                    $"Frame {frameIndex}/{totalFrames} – KI analysiert Bild...",
                    FramePreviewPng: frameBytes));

                using var visionCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                visionCts.CancelAfter(VisionFrameTimeout);
                analysis = await _vision.AnalyzeAsync(
                    Convert.ToBase64String(frameBytes), visionCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                progress?.Report(new VideoAnalysisProgress(frameIndex, totalFrames,
                    $"Frame {frameIndex}/{totalFrames} – Timeout bei KI-Analyse ({VisionFrameTimeout.TotalSeconds:0}s)"));
                telemetry.RecordFrame(new AuswertungPro.Next.Application.Ai.Vision.FrameTiming(frameIndex, t, extractionMs, 0, 0, 0, visionSw.ElapsedMilliseconds, frameSw.ElapsedMilliseconds, Skipped: true));
                AdvanceAll(active, detections, DedupWindowFrames);
                continue;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                progress?.Report(new VideoAnalysisProgress(frameIndex, totalFrames,
                    $"Frame {frameIndex}/{totalFrames} – Fehler: {ex.Message}"));
                telemetry.RecordFrame(new AuswertungPro.Next.Application.Ai.Vision.FrameTiming(frameIndex, t, extractionMs, 0, 0, 0, visionSw.ElapsedMilliseconds, frameSw.ElapsedMilliseconds, Skipped: true));
                AdvanceAll(active, detections, DedupWindowFrames);
                continue;
            }
            var qwenMs = visionSw.ElapsedMilliseconds;

            telemetry.RecordFrame(new AuswertungPro.Next.Application.Ai.Vision.FrameTiming(frameIndex, t, extractionMs, 0, 0, 0, qwenMs, frameSw.ElapsedMilliseconds, Skipped: false));

            var meter = analysis.Meter ?? EstimateMeter(t, duration);
            // Always update _lastKnownMeter so EstimateMeter doesn't stagnate at 0.01
            _lastKnownMeter = meter;

            var current = (analysis.Findings ?? Array.Empty<EnhancedFinding>())
                .Where(f => !string.IsNullOrWhiteSpace(f.Label) && f.Severity >= MinSeverity)
                .ToList();

            var liveFindings = current
                .Select(f => new LiveFrameFinding(
                    Label: f.Label.Trim(),
                    Severity: f.Severity,
                    PositionClock: f.PositionClock,
                    ExtentPercent: f.ExtentPercent,
                    VsaCodeHint: f.VsaCodeHint,
                    HeightMm: f.HeightMm,
                    WidthMm: f.WidthMm,
                    IntrusionPercent: f.IntrusionPercent,
                    CrossSectionReductionPercent: f.CrossSectionReductionPercent,
                    DiameterReductionMm: f.DiameterReductionMm))
                .ToList();

            // Knick-Erkennung: Fluchtpunkt-Tracking parallel (~5ms, blockiert nicht)
            if (KnickDetection is not null && frameBytes is { Length: > 0 })
            {
                try
                {
                    var b64 = Convert.ToBase64String(frameBytes);
                    var knick = await KnickDetection.ProcessFrameAsync(b64, meter, frameIndex, ct: ct)
                        .ConfigureAwait(false);
                    if (knick != null)
                    {
                        // Knick als Finding hinzufuegen (BAG = Lageabweichung/Knick)
                        int knickSeverity = knick.AngleDeg >= 30 ? 4 : knick.AngleDeg >= 15 ? 3 : 2;
                        var knickLabel = $"Knick {knick.AngleDeg:F0}°";
                        liveFindings.Add(new LiveFrameFinding(
                            Label: knickLabel,
                            Severity: knickSeverity,
                            PositionClock: null,
                            ExtentPercent: null,
                            VsaCodeHint: "BAG",
                            HeightMm: null, WidthMm: null,
                            IntrusionPercent: null,
                            CrossSectionReductionPercent: null,
                            DiameterReductionMm: null));

                        current.Add(new EnhancedFinding(
                            Label: knickLabel,
                            VsaCodeHint: "BAG",
                            Severity: knickSeverity,
                            PositionClock: null,
                            ExtentPercent: null,
                            HeightMm: null, WidthMm: null,
                            IntrusionPercent: null,
                            CrossSectionReductionPercent: null,
                            DiameterReductionMm: null,
                            Notes: $"Lageabweichung {knick.AngleDeg:F1}° an Rohrverbindung (Konf: {knick.Confidence:F0}%)"
                        ));
                    }
                }
                catch
                {
                    // Knick-Erkennung darf Pipeline nicht blockieren
                }
            }

            UpdateActive(active, current, meter, detections);

            progress?.Report(new VideoAnalysisProgress(
                frameIndex,
                totalFrames,
                $"Frame {frameIndex}/{totalFrames} @ {meter:0.0}m – {current.Count} Befunde",
                FramePreviewPng: frameBytes,
                LiveFindings: liveFindings));
        }

        foreach (var a in active.Values)
            detections.Add(a.ToDetection());

        progress?.Report(new VideoAnalysisProgress(totalFrames, totalFrames,
            $"Fertig – {detections.Count} Schäden erkannt."));

        return new VideoAnalysisResult(videoPath, duration, frameIndex,
            detections.OrderBy(d => d.MeterStart).ToList(), null, telemetry.GetSummary());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void UpdateActive(
        Dictionary<string, ActiveFinding> active,
        List<EnhancedFinding> current,
        double meter,
        List<RawVideoDetection> completed)
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
                active[key].Update(meter, finding.Severity, finding.VsaCodeHint, finding.PositionClock, finding.ExtentPercent,
                    finding.HeightMm, finding.WidthMm, finding.IntrusionPercent, finding.CrossSectionReductionPercent, finding.DiameterReductionMm,
                    finding.BboxX1Norm, finding.BboxY1Norm, finding.BboxX2Norm, finding.BboxY2Norm);
            }
            else
            {
                active[key].MissedFrames++;
                // BUG 1.2 FIX: DedupWindowFrames
                if (active[key].MissedFrames >= DedupWindowFrames)
                {
                    completed.Add(active[key].ToDetection());
                    active.Remove(key);
                }
            }
        }

        foreach (var pair in currentMap)
        {
            if (!active.ContainsKey(pair.Key))
            {
                var f = pair.Value;
                active[pair.Key] = new ActiveFinding(
                    f.Label.Trim(),
                    meter,
                    f.Severity,
                    f.VsaCodeHint,
                    f.PositionClock,
                    f.ExtentPercent,
                    f.HeightMm,
                    f.WidthMm,
                    f.IntrusionPercent,
                    f.CrossSectionReductionPercent,
                    f.DiameterReductionMm,
                    f.BboxX1Norm, f.BboxY1Norm, f.BboxX2Norm, f.BboxY2Norm);
            }
        }
    }

    // BUG 1.2 FIX: dedupWindow als Parameter statt hardcoded 3
    private static void AdvanceAll(
        Dictionary<string, ActiveFinding> active,
        List<RawVideoDetection> completed,
        int dedupWindow)
    {
        foreach (var key in active.Keys.ToList())
        {
            active[key].MissedFrames++;
            if (active[key].MissedFrames >= dedupWindow)
            {
                completed.Add(active[key].ToDetection());
                active.Remove(key);
            }
        }
    }

    // Dauer + Fehler
    private async Task<(double duration, string error)> GetVideoDurationWithErrorAsync(string videoPath, CancellationToken ct)
    {
        var probe = ResolveFfprobe(_ffmpegPath, _ffprobePath);
        if (probe is not null)
        {
            try
            {
                var (dur, err) = await TryWithFfprobeWithErrorAsync(probe, videoPath, ct);
                if (dur is not null && dur > 0)
                    return (dur.Value, "");
                // ffprobe gestartet aber kein Ergebnis → ffmpeg-Fallback versuchen
            }
            catch (OperationCanceledException) { throw; }
            catch { /* ffprobe nicht gefunden oder Fehler → ffmpeg-Fallback */ }
        }

        var fallback = await TryWithFfmpegAsync(_ffmpegPath, videoPath, ct);
        return (fallback ?? 0, fallback == null
            ? $"Videodauer konnte nicht ermittelt werden. Bitte ffmpeg/ffprobe im PATH oder per Env SEWERSTUDIO_FFMPEG konfigurieren."
            : "");
    }

    // Neue Methode: ffprobe mit Fehlerausgabe
    private static async Task<(double? duration, string error)> TryWithFfprobeWithErrorAsync(string ffprobeExe, string videoPath, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = ffprobeExe,
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
            if (p is null) return (null, "Process.Start failed");

            // stdout und stderr parallel lesen um Pipe-Deadlock zu vermeiden
            var stdoutTask = p.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = p.StandardError.ReadToEndAsync(ct);
            await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
            await p.WaitForExitAsync(ct).ConfigureAwait(false);

            var stdout = stdoutTask.Result;
            var stderr = stderrTask.Result;

            if (p.ExitCode != 0)
                return (null, string.IsNullOrWhiteSpace(stderr) ? $"ExitCode {p.ExitCode}" : stderr);

            if (double.TryParse(stdout.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var dur))
                return (dur, "");

            return (null, $"stdout: '{stdout.Trim()}', stderr: '{stderr.Trim()}'");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return (null, ex.Message); }
    }

    private static string? ResolveFfprobe(string ffmpegPath, string? ffprobePath)
    {
        // Absoluter Pfad zu ffprobe → direkt nutzen
        if (!string.IsNullOrWhiteSpace(ffprobePath) && File.Exists(ffprobePath))
            return ffprobePath;

        // Absoluter Pfad zu ffmpeg → ffprobe.exe daneben suchen
        if (!string.IsNullOrWhiteSpace(ffmpegPath) && File.Exists(ffmpegPath))
        {
            var dir = Path.GetDirectoryName(ffmpegPath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                var candidate = Path.Combine(dir, "ffprobe.exe");
                if (File.Exists(candidate)) return candidate;
            }
        }

        // PATH-basierter Name (z.B. "ffprobe" oder "ffmpeg") → als Fallback direkt verwenden
        if (!string.IsNullOrWhiteSpace(ffprobePath))
            return ffprobePath;

        // Aus ffmpeg-Name "ffprobe" ableiten (z.B. "ffmpeg" → "ffprobe")
        if (!string.IsNullOrWhiteSpace(ffmpegPath))
        {
            var derived = DeriveFFprobePath(ffmpegPath);
            if (!string.IsNullOrWhiteSpace(derived))
                return derived;
        }

        return null;
    }

    private static async Task<double?> TryWithFfmpegAsync(string ffmpegExe, string videoPath, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ffmpegExe))
            return null;
        // Nur bei absolutem Pfad File.Exists prüfen; PATH-Namen ("ffmpeg") direkt verwenden
        if (Path.IsPathRooted(ffmpegExe) && !File.Exists(ffmpegExe))
            return null;

        var psi = new ProcessStartInfo
        {
            FileName = ffmpegExe,
            UseShellExecute = false,
            RedirectStandardError = true,  // Duration steht in stderr
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add(videoPath);

        try
        {
            using var p = Process.Start(psi);
            if (p is null) return null;

            var text = await p.StandardError.ReadToEndAsync();
            await p.WaitForExitAsync(ct);

            var m = System.Text.RegularExpressions.Regex.Match(text, @"Duration:\s*(\d+):(\d+):(\d+(?:\.\d+)?)");
            if (!m.Success) return null;

            var h = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            var min = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            var s = double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
            return h * 3600 + min * 60 + s;
        }
        catch (OperationCanceledException) { throw; }
        catch { return null; }
    }

    private double _lastKnownMeter;

    private double EstimateMeter(double t, double dur)
    {
        // Schätze Meter-Inkrement basierend auf Zeitfortschritt.
        // Annahme: konstante Kamerageschwindigkeit über die gesamte Haltung.
        if (dur <= 0)
            return Math.Round(_lastKnownMeter + 0.01, 2);

        // Wenn noch kein Meter bekannt, schätze ~0.1m/s Kamerageschwindigkeit als Default
        var estimatedPipeLength = _lastKnownMeter > 0 ? _lastKnownMeter * (dur / Math.Max(t, 1.0)) : dur * 0.1;
        var step = FrameStepSeconds / dur * estimatedPipeLength;
        return Math.Round(_lastKnownMeter + Math.Max(step, 0.01), 2);
    }

    private static string BuildFindingKey(EnhancedFinding finding)
    {
        var keyBase = VsaCodeResolver.NormalizeFindingCode(finding.VsaCodeHint)
            ?? VsaCodeResolver.InferCodeFromLabel(finding.Label)
            ?? finding.Label.Trim();
        var clock = NormalizeClock(finding.PositionClock);
        return string.IsNullOrWhiteSpace(clock) ? keyBase : $"{keyBase}|{clock}";
    }

    private static int? TryParseClockHour(string? raw)
    {
        var normalized = VsaCodeResolver.NormalizeClock(raw);
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        var m = Regex.Match(normalized, @"\b(?<h>1[0-2]|0?[1-9])\b");
        if (!m.Success)
            return null;

        return int.TryParse(m.Groups["h"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var h)
            ? h
            : null;
    }

    private static string? NormalizeClock(string? raw)
    {
        return VsaCodeResolver.NormalizeClock(raw);
    }

    private static string DeriveFFprobePath(string ffmpegPath)
    {
        if (string.IsNullOrWhiteSpace(ffmpegPath) ||
            string.Equals(ffmpegPath, "ffmpeg", StringComparison.OrdinalIgnoreCase))
            return "ffprobe";
        var dir = Path.GetDirectoryName(ffmpegPath);
        var ext = Path.GetExtension(ffmpegPath);
        return string.IsNullOrWhiteSpace(dir) ? "ffprobe" + ext : Path.Combine(dir, "ffprobe" + ext);
    }

    private sealed class ActiveFinding
    {
        public string Name { get; }
        public double MeterStart { get; }
        public double MeterEnd { get; private set; }
        public int MaxSeverity { get; private set; }
        public string? VsaCodeHint { get; private set; }
        public string? PositionClock { get; private set; }
        public int? ExtentPercent { get; private set; }
        public int? HeightMm { get; private set; }
        public int? WidthMm { get; private set; }
        public int? IntrusionPercent { get; private set; }
        public int? CrossSectionReductionPercent { get; private set; }
        public int? DiameterReductionMm { get; private set; }
        public int MissedFrames { get; set; }

        // BoundingBox (normiert 0-1) — vom Frame mit hoechster Severity
        public double? BboxX1 { get; private set; }
        public double? BboxY1 { get; private set; }
        public double? BboxX2 { get; private set; }
        public double? BboxY2 { get; private set; }

        public ActiveFinding(
            string name,
            double start,
            int severity,
            string? hint,
            string? positionClock,
            int? extentPercent,
            int? heightMm = null,
            int? widthMm = null,
            int? intrusionPercent = null,
            int? crossSectionReductionPercent = null,
            int? diameterReductionMm = null,
            double? bboxX1 = null,
            double? bboxY1 = null,
            double? bboxX2 = null,
            double? bboxY2 = null)
        {
            Name = name; MeterStart = start; MeterEnd = start;
            MaxSeverity = severity; VsaCodeHint = hint;
            PositionClock = NormalizeClock(positionClock);
            ExtentPercent = extentPercent is null ? null : Math.Clamp(extentPercent.Value, 1, 100);
            HeightMm = heightMm;
            WidthMm = widthMm;
            IntrusionPercent = intrusionPercent;
            CrossSectionReductionPercent = crossSectionReductionPercent;
            DiameterReductionMm = diameterReductionMm;
            BboxX1 = bboxX1; BboxY1 = bboxY1; BboxX2 = bboxX2; BboxY2 = bboxY2;
        }

        public void Update(
            double meter,
            int severity,
            string? hint,
            string? positionClock,
            int? extentPercent,
            int? heightMm = null,
            int? widthMm = null,
            int? intrusionPercent = null,
            int? crossSectionReductionPercent = null,
            int? diameterReductionMm = null,
            double? bboxX1 = null,
            double? bboxY1 = null,
            double? bboxX2 = null,
            double? bboxY2 = null)
        {
            MeterEnd = meter;
            MissedFrames = 0;
            if (severity > MaxSeverity)
            {
                MaxSeverity = severity;
                // BBox vom Frame mit hoechster Severity uebernehmen
                if (bboxX1 is not null) { BboxX1 = bboxX1; BboxY1 = bboxY1; BboxX2 = bboxX2; BboxY2 = bboxY2; }
            }
            if (!string.IsNullOrWhiteSpace(hint)) VsaCodeHint = hint;
            if (!string.IsNullOrWhiteSpace(positionClock))
                PositionClock = NormalizeClock(positionClock);
            if (extentPercent is { } e)
                ExtentPercent = Math.Max(ExtentPercent ?? 0, Math.Clamp(e, 1, 100));
            if (heightMm is { } h)
                HeightMm = Math.Max(HeightMm ?? 0, h);
            if (widthMm is { } w)
                WidthMm = Math.Max(WidthMm ?? 0, w);
            if (intrusionPercent is { } ip)
                IntrusionPercent = Math.Max(IntrusionPercent ?? 0, ip);
            if (crossSectionReductionPercent is { } csr)
                CrossSectionReductionPercent = Math.Max(CrossSectionReductionPercent ?? 0, csr);
            if (diameterReductionMm is { } dr)
                DiameterReductionMm = Math.Max(DiameterReductionMm ?? 0, dr);
            // Falls noch keine BBox gesetzt, erste verfuegbare uebernehmen
            if (BboxX1 is null && bboxX1 is not null)
            { BboxX1 = bboxX1; BboxY1 = bboxY1; BboxX2 = bboxX2; BboxY2 = bboxY2; }
        }

        public RawVideoDetection ToDetection() =>
            new(Name, MeterStart, MeterEnd, SeverityLabel(MaxSeverity), VsaCodeHint, PositionClock, ExtentPercent,
                HeightMm, WidthMm, IntrusionPercent, CrossSectionReductionPercent, DiameterReductionMm,
                BboxX1: BboxX1, BboxY1: BboxY1, BboxX2: BboxX2, BboxY2: BboxY2);

        private static string SeverityLabel(int s) => s >= 4 ? "high" : s == 3 ? "mid" : "low";
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────────
// Phase 5.3 vorbereitend: VideoAnalysisResult / VideoAnalysisProgress /
// LiveFrameFinding / RawVideoDetection nach Domain/Ai/Vision/VideoAnalysisModels.cs.
