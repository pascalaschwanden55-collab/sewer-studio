using System.Collections.Generic;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Formatiert Telemetrie-Zusammenfassungen der Videoanalyse-Pipeline als lesbaren String.
/// Reine, testbare Logik ohne UI-Abhaengigkeit.
/// </summary>
public static class PipelineTelemetryFormatter
{
    /// <summary>
    /// Formatiert eine <see cref="TelemetrySummary"/> als einzeiligen Pipe-getrennten Text.
    /// Gibt einen Leerstring zurueck, wenn keine Telemetrie vorhanden.
    /// </summary>
    public static string Format(TelemetrySummary? t)
    {
        if (t is null)
            return "";

        var parts = new List<string>
        {
            $"Wall: {t.WallClockMs / 1000.0:F1}s",
            $"Frames: {t.TotalFrames} ({t.SkippedFrames} skipped)",
            $"Extraction: {FormatStat(t.Extraction)}"
        };

        if (t.Yolo.TotalMs > 0) parts.Add($"YOLO: {FormatStat(t.Yolo)}");
        if (t.Dino.TotalMs > 0) parts.Add($"DINO: {FormatStat(t.Dino)}");
        if (t.Sam.TotalMs > 0) parts.Add($"SAM: {FormatStat(t.Sam)}");
        if (t.Qwen.TotalMs > 0) parts.Add($"Vision: {FormatStat(t.Qwen)}");
        parts.Add($"Total/Frame: {FormatStat(t.Total)}");

        return string.Join("  |  ", parts);
    }

    private static string FormatStat(PhaseStat s) => s.TotalMs > 0
        ? $"Mean={s.MeanMs:F0}ms  P95={s.P95Ms:F0}ms"
        : "—";
}
