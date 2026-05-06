// AuswertungPro – KI Videoanalyse Modul
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.UI.Ai.Training.Services;

/// <summary>
/// Erkennt relevante Frame-Wechsel in Kanalinspektion-Videos via ffmpeg scene-detect.
/// Vermeidet blindes Sampling und konzentriert sich auf tatsächliche Szenenänderungen.
/// </summary>
public sealed class SceneChangeDetector
{
    private readonly string _ffmpeg;

    /// <summary>
    /// Schwellwert für Szenenänderung (0.0–1.0).
    /// 0.3 = moderate Änderung; höher = weniger Szenen erkannt.
    /// </summary>
    public double Threshold { get; set; } = 0.3;

    /// <summary>Mindestabstand zwischen zwei Szenen in Sekunden.</summary>
    public double MinIntervalSeconds { get; set; } = 1.0;

    public SceneChangeDetector(string? ffmpegPath = null)
    {
        _ffmpeg = ffmpegPath ?? FfmpegLocator.ResolveFfmpeg();
    }

    /// <summary>
    /// Erkennt Szenenänderungen und gibt Zeitstempel (in Sekunden) zurück.
    /// Gibt immer t=0 als ersten Eintrag zurück (erster Frame ist immer relevant).
    /// </summary>
    public async Task<IReadOnlyList<double>> DetectAsync(
        string videoPath,
        CancellationToken ct = default)
    {
        var raw = await RunFfmpegSceneDetectAsync(videoPath, ct).ConfigureAwait(false);
        return BuildTimestampList(raw);
    }

    // ── Intern ────────────────────────────────────────────────────────────

    private async Task<string> RunFfmpegSceneDetectAsync(string videoPath, CancellationToken ct)
    {
        // Phase D2.3: ProcessRunner. Scene-Detect kann lange dauern (lange Videos),
        // daher kein harter Timeout — externer ct fuer Batch-Abbruch reicht.
        var result = await ProcessRunner.RunAsync(
            fileName: _ffmpeg,
            arguments: ["-i", videoPath,
                        "-vf", $"select='gt(scene,{Threshold.ToString(CultureInfo.InvariantCulture)})',showinfo",
                        "-vsync", "vfr",
                        "-f", "null", "-"],
            ct: ct).ConfigureAwait(false);

        // showinfo schreibt nach stderr — auch bei ExitCode != 0 nutzbar.
        return result.Stderr ?? "";
    }

    private IReadOnlyList<double> BuildTimestampList(string ffmpegOutput)
    {
        var times = new List<double> { 0.0 };   // t=0 ist immer dabei

        // showinfo gibt u.a. "pts_time:12.345" aus
        var pattern = new Regex(@"pts_time:(\d+(?:\.\d+)?)", RegexOptions.Compiled);

        double lastAdded = 0.0;
        foreach (Match m in pattern.Matches(ffmpegOutput))
        {
            if (!double.TryParse(m.Groups[1].Value, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var t))
                continue;

            if (t - lastAdded < MinIntervalSeconds)
                continue;

            times.Add(t);
            lastAdded = t;
        }

        times.Sort();
        return times;
    }
}
