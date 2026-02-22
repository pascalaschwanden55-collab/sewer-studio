using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.UI.Ai;

/// <summary>
/// Pallon-ähnlicher Workflow: Video -> alle Schäden in einem Durchgang.
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

    public double FrameStepSeconds { get; set; } = 2.0;
    public int DedupWindowFrames { get; set; } = 3;
    public int MinSeverity { get; set; } = 1;

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
        string ffmpegPath = "ffmpeg")
        => new(new EnhancedVisionAnalysisService(client, visionModel), ffmpegPath);

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

        var (duration, probeError) = await GetVideoDurationWithErrorAsync(videoPath, ct).ConfigureAwait(false);
        if (duration <= 0)
            return VideoAnalysisResult.Failed($"Videodauer konnte nicht ermittelt werden (ffprobe): {probeError}");

        var totalFrames = (int)Math.Ceiling(duration / FrameStepSeconds);
        var detections = new List<RawVideoDetection>();
        var active = new Dictionary<string, ActiveFinding>(StringComparer.OrdinalIgnoreCase);
        var frameIndex = 0;

        progress?.Report(new VideoAnalysisProgress(0, totalFrames, "Analyse gestartet..."));

        for (var t = 0.0; t < duration; t += FrameStepSeconds)
        {
            ct.ThrowIfCancellationRequested();
            frameIndex++;

            var frameBytes = await VideoFrameExtractor.TryExtractFramePngAsync(
                _ffmpegPath, videoPath, TimeSpan.FromSeconds(t), ct).ConfigureAwait(false);

            if (frameBytes is null or { Length: 0 })
            {
                // BUG 1.2 FIX: DedupWindowFrames als Parameter
                AdvanceAll(active, detections, DedupWindowFrames);
                progress?.Report(new VideoAnalysisProgress(frameIndex, totalFrames,
                    $"Frame {frameIndex}/{totalFrames} – kein Bild"));
                continue;
            }

            EnhancedFrameAnalysis analysis;
            try
            {
                // BUG 1.4 FIX: EnhancedVisionAnalysisService
                analysis = await _vision.AnalyzeAsync(
                    Convert.ToBase64String(frameBytes), ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                progress?.Report(new VideoAnalysisProgress(frameIndex, totalFrames,
                    $"Frame {frameIndex}/{totalFrames} – Fehler: {ex.Message}"));
                AdvanceAll(active, detections, DedupWindowFrames);
                continue;
            }

            var meter = analysis.Meter ?? EstimateMeter(t, duration);

            var current = (analysis.Findings ?? Array.Empty<EnhancedFinding>())
                .Where(f => !string.IsNullOrWhiteSpace(f.Label) && f.Severity >= MinSeverity)
                .ToList();

            UpdateActive(active, current, meter, detections);

            progress?.Report(new VideoAnalysisProgress(frameIndex, totalFrames,
                $"Frame {frameIndex}/{totalFrames} @ {meter:0.0}m – {current.Count} Befunde"));
        }

        foreach (var a in active.Values)
            detections.Add(a.ToDetection());

        progress?.Report(new VideoAnalysisProgress(totalFrames, totalFrames,
            $"Fertig – {detections.Count} Schäden erkannt."));

        return new VideoAnalysisResult(videoPath, duration, frameIndex,
            detections.OrderBy(d => d.MeterStart).ToList(), null);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void UpdateActive(
        Dictionary<string, ActiveFinding> active,
        List<EnhancedFinding> current,
        double meter,
        List<RawVideoDetection> completed)
    {
        var currentKeys = current
            .Select(f => f.Label.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var key in active.Keys.ToList())
        {
            if (currentKeys.Contains(key))
            {
                var f = current.First(x => string.Equals(x.Label.Trim(), key, StringComparison.OrdinalIgnoreCase));
                active[key].Update(meter, f.Severity, f.VsaCodeHint);
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

        foreach (var f in current)
        {
            var key = f.Label.Trim();
            if (!active.ContainsKey(key))
                active[key] = new ActiveFinding(key, meter, f.Severity, f.VsaCodeHint);
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

    private async Task<double> GetVideoDurationAsync(string videoPath, CancellationToken ct)
    {
        // Diese Methode ist jetzt nur noch ein Wrapper für die neue Methode
        var (duration, _) = await GetVideoDurationWithErrorAsync(videoPath, ct);
        return duration;
    }

    // Neue Methode: Dauer + Fehler
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
            ? $"Videodauer konnte nicht ermittelt werden. Bitte ffmpeg/ffprobe im PATH oder per Env AUSWERTUNGPRO_FFMPEG konfigurieren."
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

            var stdout = await p.StandardOutput.ReadToEndAsync();
            var stderr = await p.StandardError.ReadToEndAsync();
            await p.WaitForExitAsync(ct);

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

    private static async Task<double?> TryWithFfprobeAsync(string ffprobeExe, string videoPath, CancellationToken ct)
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

        using var p = Process.Start(psi);
        if (p is null) return null;

        var stdout = await p.StandardOutput.ReadToEndAsync();
        var stderr = await p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync(ct);

        if (p.ExitCode != 0) return null;

        if (double.TryParse(stdout.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var dur))
            return dur;

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

    private static double EstimateMeter(double t, double dur)
        => dur > 0 ? Math.Round(t / dur * 100.0, 2) : t;

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
        public int MissedFrames { get; set; }

        public ActiveFinding(string name, double start, int severity, string? hint)
        {
            Name = name; MeterStart = start; MeterEnd = start;
            MaxSeverity = severity; VsaCodeHint = hint;
        }

        public void Update(double meter, int severity, string? hint)
        {
            MeterEnd = meter;
            MissedFrames = 0;
            if (severity > MaxSeverity) MaxSeverity = severity;
            if (!string.IsNullOrWhiteSpace(hint)) VsaCodeHint = hint;
        }

        public RawVideoDetection ToDetection() =>
            new(Name, MeterStart, MeterEnd, SeverityLabel(MaxSeverity), VsaCodeHint);

        private static string SeverityLabel(int s) => s >= 4 ? "high" : s == 3 ? "mid" : "low";
    }
}

// ── DTOs ──────────────────────────────────────────────────────────────────────


public sealed record VideoAnalysisResult(
    string VideoPath,
    double DurationSeconds,
    int FramesAnalyzed,
    IReadOnlyList<RawVideoDetection> Detections,
    string? Error)
{
    public bool IsSuccess => Error is null;
    public static VideoAnalysisResult Failed(string error) =>
        new(string.Empty, 0, 0, Array.Empty<RawVideoDetection>(), error);
}

public sealed record VideoAnalysisProgress(int FramesDone, int FramesTotal, string Status)
{
    public double Percent => FramesTotal > 0 ? (double)FramesDone / FramesTotal * 100.0 : 0;
}

public sealed record RawVideoDetection(
    string FindingLabel,
    double MeterStart,
    double MeterEnd,
    string Severity,
    string? VsaCodeHint = null  // NEU: direkt aus EnhancedVisionAnalysisService
) 
{
    // Für UI-Bindings / Mapping
    public string Code => VsaCodeHint ?? "";
    public string Label => FindingLabel;

    // Simple Heuristik (Severity kommt i.d.R. als "high/mid/low")
    public double Confidence => Severity?.ToLowerInvariant() switch
    {
        "high" => 0.90,
        "mid"  => 0.70,
        "low"  => 0.50,
        _      => 0.60
    };
}
