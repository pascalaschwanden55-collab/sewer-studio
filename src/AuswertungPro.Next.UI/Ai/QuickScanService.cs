using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.UI.Ai.Shared;

namespace AuswertungPro.Next.UI.Ai;

// ── Public records ───────────────────────────────────────────────────

public sealed record QuickScanSegment(
    double TimestampSeconds,
    bool HasDamage,
    int Severity,
    string? Label,
    string? Clock);

public sealed record QuickScanProgress(
    int FramesDone,
    int FramesTotal,
    string Status,
    QuickScanSegment? LatestSegment);

public sealed record QuickScanResult(
    IReadOnlyList<QuickScanSegment> Segments,
    double VideoDurationSeconds,
    int FramesAnalyzed,
    string? Error);

// ── Service ──────────────────────────────────────────────────────────

public sealed class QuickScanService
{
    private const double FrameStepSeconds = 5.0;
    private const int ScaleWidth = 640;
    private static readonly TimeSpan FrameAiTimeout = TimeSpan.FromSeconds(30);

    private static readonly string Prompt =
        "Kanalinspektion-Frame. Ist ein Schaden sichtbar?\n" +
        "Wenn ja: Kurzbezeichnung (label), Schweregrad 1-5, Uhrzeitposition (clock).\n" +
        "Wenn nein: has_damage=false, severity=0.\n" +
        "Antworte NUR mit JSON.";

    private readonly OllamaClient _client;
    private readonly string _visionModel;
    private readonly string _ffmpegPath;

    public QuickScanService(OllamaClient client, string visionModel, string ffmpegPath)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _visionModel = visionModel;
        _ffmpegPath = ffmpegPath;
    }

    public async Task<QuickScanResult> ScanAsync(
        string videoPath,
        IProgress<QuickScanProgress>? progress,
        CancellationToken ct)
    {
        var duration = await GetVideoDurationAsync(videoPath, ct).ConfigureAwait(false);
        if (duration <= 0)
            return new QuickScanResult(Array.Empty<QuickScanSegment>(), 0, 0,
                "Videodauer konnte nicht ermittelt werden.");

        int totalFrames = (int)Math.Ceiling(duration / FrameStepSeconds);
        var segments = new List<QuickScanSegment>();
        int done = 0;

        progress?.Report(new QuickScanProgress(0, totalFrames, "Starte Schnell-Scan...", null));

        await using var stream = VideoFrameStream.Open(
            _ffmpegPath, videoPath, FrameStepSeconds, duration, ct, scaleWidth: ScaleWidth);

        await foreach (var frame in stream.ReadFramesAsync(ct).ConfigureAwait(false))
        {
            ct.ThrowIfCancellationRequested();

            var segment = await AnalyzeFrameAsync(frame, ct).ConfigureAwait(false);
            segments.Add(segment);
            done++;

            progress?.Report(new QuickScanProgress(done, totalFrames,
                $"Frame {done}/{totalFrames}", segment));
        }

        return new QuickScanResult(segments, duration, done, null);
    }

    private async Task<QuickScanSegment> AnalyzeFrameAsync(FrameData frame, CancellationToken ct)
    {
        try
        {
            using var frameCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            frameCts.CancelAfter(FrameAiTimeout);

            var b64 = Convert.ToBase64String(frame.PngBytes);
            var messages = new[]
            {
                new OllamaClient.ChatMessage("user", Prompt, new[] { b64 })
            };

            // Use plain /api/chat (qwen2.5vl does not support structured format)
            var raw = await _client.ChatAsync(
                _visionModel, messages, frameCts.Token).ConfigureAwait(false);

            var dto = ParseQuickScanResponse(raw);

            return new QuickScanSegment(
                frame.TimestampSeconds,
                dto.HasDamage,
                Math.Clamp(dto.Severity, 0, 5),
                dto.Label,
                dto.Clock);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Per-frame timeout
            return new QuickScanSegment(frame.TimestampSeconds, false, 0, null, null);
        }
        catch (OperationCanceledException)
        {
            throw; // Real cancellation
        }
        catch
        {
            return new QuickScanSegment(frame.TimestampSeconds, false, 0, null, null);
        }
    }

    // ── Duration detection (duplicated from VideoFullAnalysisService) ─

    private async Task<double> GetVideoDurationAsync(string videoPath, CancellationToken ct)
    {
        var ffprobe = FfmpegLocator.ResolveFfprobe();
        var dur = await TryFfprobeAsync(ffprobe, videoPath, ct).ConfigureAwait(false);
        if (dur.HasValue && dur.Value > 0)
            return dur.Value;

        var fallback = await TryFfmpegDurationAsync(_ffmpegPath, videoPath, ct).ConfigureAwait(false);
        return fallback ?? 0;
    }

    private static async Task<double?> TryFfprobeAsync(string ffprobeExe, string videoPath, CancellationToken ct)
    {
        try
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

            var stdoutTask = p.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = p.StandardError.ReadToEndAsync(ct);
            await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
            await p.WaitForExitAsync(ct).ConfigureAwait(false);
            var stdout = stdoutTask.Result;

            if (double.TryParse(stdout.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var dur))
                return dur;
        }
        catch (OperationCanceledException) { throw; }
        catch { /* ffprobe not found or error */ }

        return null;
    }

    private static async Task<double?> TryFfmpegDurationAsync(string ffmpegExe, string videoPath, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ffmpegExe))
            return null;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = ffmpegExe,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            psi.ArgumentList.Add("-i");
            psi.ArgumentList.Add(videoPath);

            using var p = Process.Start(psi);
            if (p is null) return null;

            var text = await p.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
            await p.WaitForExitAsync(ct).ConfigureAwait(false);

            var m = Regex.Match(text, @"Duration:\s*(\d+):(\d+):(\d+(?:\.\d+)?)");
            if (!m.Success) return null;

            var h = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            var min = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            var s = double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
            return h * 3600 + min * 60 + s;
        }
        catch (OperationCanceledException) { throw; }
        catch { return null; }
    }

    // ── DTO + Parsing ─────────────────────────────────────────────────

    private sealed class QuickScanDto
    {
        [JsonPropertyName("has_damage")]
        public bool HasDamage { get; set; }

        [JsonPropertyName("severity")]
        public int Severity { get; set; }

        [JsonPropertyName("label")]
        public string? Label { get; set; }

        [JsonPropertyName("clock")]
        public string? Clock { get; set; }
    }

    private static QuickScanDto ParseQuickScanResponse(string raw)
    {
        var json = ExtractJson(raw);
        if (json is null)
            return new QuickScanDto();

        try
        {
            return JsonSerializer.Deserialize<QuickScanDto>(json,
                Application.Common.JsonDefaults.CaseInsensitive) ?? new QuickScanDto();
        }
        catch
        {
            return new QuickScanDto();
        }
    }

    private static string? ExtractJson(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var m = Regex.Match(raw, @"```(?:json)?\s*(\{[\s\S]*?\})\s*```");
        if (m.Success)
            return m.Groups[1].Value;

        var start = raw.IndexOf('{');
        var end = raw.LastIndexOf('}');
        if (start >= 0 && end > start)
            return raw[start..(end + 1)];

        return null;
    }
}
