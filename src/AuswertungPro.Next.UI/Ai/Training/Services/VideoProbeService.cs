// AuswertungPro – KI Videoanalyse Modul
using System;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.UI.Ai.Shared;

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
        var psi = new ProcessStartInfo
        {
            FileName = _ffprobe,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("-v");           psi.ArgumentList.Add("error");
        psi.ArgumentList.Add("-show_entries"); psi.ArgumentList.Add("format=duration");
        psi.ArgumentList.Add("-of");           psi.ArgumentList.Add("default=noprint_wrappers=1:nokey=1");
        psi.ArgumentList.Add(videoPath);

        try
        {
            using var p = Process.Start(psi);
            if (p is null) return VideoProbeResult.Fail("ffprobe: Process.Start returned null");

            var stdout = await p.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
            var stderr = await p.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
            await p.WaitForExitAsync(ct).ConfigureAwait(false);

            if (p.ExitCode != 0)
                return VideoProbeResult.Fail($"ffprobe ExitCode {p.ExitCode}: {stderr.Trim()}");

            if (double.TryParse(stdout.Trim(), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var dur) && dur > 0)
                return VideoProbeResult.Ok(dur);

            return VideoProbeResult.Fail($"ffprobe: ungültige Ausgabe '{stdout.Trim()}'");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return VideoProbeResult.Fail($"ffprobe: {ex.Message}"); }
    }

    private async Task<VideoProbeResult> TryFfmpegDurationAsync(string videoPath, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _ffmpeg,
            UseShellExecute = false,
            RedirectStandardError  = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("-i");
        psi.ArgumentList.Add(videoPath);

        try
        {
            using var p = Process.Start(psi);
            if (p is null) return VideoProbeResult.Fail("ffmpeg: Process.Start returned null");

            var stderr = await p.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
            await p.WaitForExitAsync(ct).ConfigureAwait(false);

            var m = Regex.Match(stderr, @"Duration:\s*(\d+):(\d{2}):(\d{2}(?:\.\d+)?)");
            if (m.Success
                && int.TryParse(m.Groups[1].Value, out var h)
                && int.TryParse(m.Groups[2].Value, out var min)
                && double.TryParse(m.Groups[3].Value, NumberStyles.Float,
                       CultureInfo.InvariantCulture, out var sec))
            {
                return VideoProbeResult.Ok(h * 3600 + min * 60 + sec);
            }

            return VideoProbeResult.Fail($"ffmpeg: Duration-Zeile nicht gefunden. stderr: {stderr[..Math.Min(200, stderr.Length)]}");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return VideoProbeResult.Fail($"ffmpeg: {ex.Message}"); }
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
