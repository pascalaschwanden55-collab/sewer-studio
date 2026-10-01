using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Extrahiert strukturierte Werte aus dem Freitext-Status-String der Pipeline-Progress-Meldungen.
/// Reine, testbare Logik ohne UI-Abhaengigkeit.
/// </summary>
public static class PipelineStatusParser
{
    // Regex-Muster als static readonly, damit sie nur einmal kompiliert werden.
    private static readonly Regex MeterPattern = new(
        @"@\s*(?<meter>\d+(?:[.,]\d+)?)m",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex YoloSkipPattern = new(
        @"(\d+)\s+gesamt\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex FindingsPattern = new(
        @"(?<count>\d+)\s+Befunde",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Versucht den Meterstand aus einem Status-String zu lesen (z.B. "@ 12.5m").
    /// Gibt das formatierte Ergebnis "12.5 m" oder null zurueck.
    /// </summary>
    public static string? TryExtractMeter(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return null;

        var m = MeterPattern.Match(status);
        if (!m.Success)
            return null;

        var raw = m.Groups["meter"].Value.Replace(',', '.');
        return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var meter)
            ? $"{meter:0.0} m"
            : null;
    }

    /// <summary>
    /// Versucht die Anzahl Befunde aus einem Status-String zu lesen (z.B. "5 Befunde").
    /// Gibt die Zahl oder null zurueck.
    /// </summary>
    public static int? TryExtractFindingCount(string? status)
        => TryExtractCount(status, FindingsPattern);

    /// <summary>
    /// Versucht die YOLO-Gesamt-Framezahl aus einem Status-String zu lesen (z.B. "38 gesamt").
    /// Gibt die Zahl oder null zurueck.
    /// </summary>
    public static int? TryExtractYoloTotalFrames(string? status)
        => TryExtractCount(status, YoloSkipPattern);

    /// <summary>
    /// Gemeinsame Zaehlregel: leerer Status, Regex-Treffer, invariant-kulturelle
    /// Ganzzahl-Auswertung ueber die (einzige) Fanggruppe des jeweiligen Musters.
    /// </summary>
    private static int? TryExtractCount(string? status, Regex pattern)
    {
        if (string.IsNullOrWhiteSpace(status))
            return null;

        var m = pattern.Match(status);
        if (!m.Success)
            return null;

        return int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count)
            ? count
            : null;
    }
}
