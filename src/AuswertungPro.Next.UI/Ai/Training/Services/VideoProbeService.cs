// AuswertungPro – KI Videoanalyse Modul
using System;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.UI.Ai.Training.Services;

/// <summary>
/// Ermittelt Video-Metadaten (Dauer, Auflösung, Codec) via ffprobe.
/// Fehlermeldung ist immer als <see cref="VideoProbeResult.Error"/> sichtbar.
/// </summary>
public sealed class VideoProbeService
{
    private readonly string _ffprobe;
    private readonly string _ffmpeg;

    public VideoProbeService(string? ffprobePath = null, string? ffmpegPath = null)
    {
        _ffprobe = ffprobePath ?? FfmpegLocator.ResolveFfprobe();
        _ffmpeg  = ffmpegPath  ?? FfmpegLocator.ResolveFfmpeg();
    }

    /// <summary>
    /// Gibt Dauer und Metadaten des Videos zurück.
    /// Versucht zuerst ffprobe, dann ffmpeg-Fallback.
    /// </summary>
    public async Task<VideoProbeResult> ProbeAsync(string videoPath, CancellationToken ct = default)
    {
        // Stufe 1: ffprobe JSON
        var ffprobeResult = await TryFfprobeAsync(videoPath, ct).ConfigureAwait(false);
        if (ffprobeResult.Success)
            return ffprobeResult;

        // Stufe 2: ffmpeg stderr Duration-Zeile
        var ffmpegResult = await TryFfmpegDurationAsync(videoPath, ct).ConfigureAwait(false);
        if (ffmpegResult.Success)
            return ffmpegResult;

        return VideoProbeResult.Fail(
            $"Videodauer konnte nicht ermittelt werden. " +
            $"Bitte ffmpeg/ffprobe im PATH oder per Env {FfmpegLocator.EnvKey} konfigurieren. " +
            $"Letzter Fehler: {ffmpegResult.Error}");
    }

    // ── Intern ────────────────────────────────────────────────────────────

    private async Task<VideoProbeResult> TryFfprobeAsync(string videoPath, CancellationToken ct)
    {
        // Phase D2.3: ProcessRunner — sicherer ArgumentList + asynchroner Drain + Timeout.
        var result = await ProcessRunner.RunAsync(
            fileName: _ffprobe,
            arguments: ["-v", "error",
                        "-show_entries", "format=duration",
                        "-of", "default=noprint_wrappers=1:nokey=1",
                        videoPath],
            timeout: TimeSpan.FromSeconds(30),
            ct: ct).ConfigureAwait(false);

        if (result.StartFailed)
            return VideoProbeResult.Fail($"ffprobe: {result.Stderr}");
        if (result.TimedOut)
            return VideoProbeResult.Fail("ffprobe: Timeout");
        if (!result.IsSuccess)
            return VideoProbeResult.Fail($"ffprobe ExitCode {result.ExitCode}: {result.Stderr.Trim()}");

        if (double.TryParse(result.Stdout.Trim(), NumberStyles.Float,
                CultureInfo.InvariantCulture, out var dur) && dur > 0)
            return VideoProbeResult.Ok(dur);

        return VideoProbeResult.Fail($"ffprobe: ungültige Ausgabe '{result.Stdout.Trim()}'");
    }

    private async Task<VideoProbeResult> TryFfmpegDurationAsync(string videoPath, CancellationToken ct)
    {
        // Phase D2.3: ProcessRunner. ffmpeg -i <video> ohne Output-Datei beendet
        // mit ExitCode != 0, Duration-Zeile steht aber bereits in stderr.
        var result = await ProcessRunner.RunAsync(
            fileName: _ffmpeg,
            arguments: ["-i", videoPath],
            timeout: TimeSpan.FromSeconds(30),
            ct: ct).ConfigureAwait(false);

        if (result.StartFailed)
            return VideoProbeResult.Fail($"ffmpeg: {result.Stderr}");
        if (result.TimedOut)
            return VideoProbeResult.Fail("ffmpeg: Timeout");

        var m = Regex.Match(result.Stderr, @"Duration:\s*(\d+):(\d{2}):(\d{2}(?:\.\d+)?)");
        if (m.Success
            && int.TryParse(m.Groups[1].Value, out var h)
            && int.TryParse(m.Groups[2].Value, out var min)
            && double.TryParse(m.Groups[3].Value, NumberStyles.Float,
                   CultureInfo.InvariantCulture, out var sec))
        {
            return VideoProbeResult.Ok(h * 3600 + min * 60 + sec);
        }

        return VideoProbeResult.Fail(
            $"ffmpeg: Duration-Zeile nicht gefunden. stderr: " +
            $"{result.Stderr[..Math.Min(200, result.Stderr.Length)]}");
    }
}

/// <summary>Ergebnis eines Video-Probe-Aufrufs.</summary>
public sealed record VideoProbeResult
{
    public bool   Success         { get; private init; }
    public double DurationSeconds { get; private init; }
    public string Error           { get; private init; } = "";

    public static VideoProbeResult Ok(double duration)
        => new() { Success = true, DurationSeconds = duration };

    public static VideoProbeResult Fail(string error)
        => new() { Success = false, Error = error };
}
