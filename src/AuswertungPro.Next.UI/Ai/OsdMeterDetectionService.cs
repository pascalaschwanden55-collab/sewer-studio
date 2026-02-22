using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.UI.Ai;

/// <summary>
/// Liest den Meterstand aus dem OSD (On-Screen Display) eines Kanalinspektion-Frames.
///
/// Strategie 1 (Vision-LLM): Ollama liest den Wert direkt aus dem Bild.
///   → genaueste Methode, benötigt VisionModel
///
/// Strategie 2 (ffmpeg OCR / Regex): Schnelles, modell-loses Fallback –
///   extrahiert alle Zahlenfolgen im Bild und wählt den plausibelsten Meterstand.
///   Erfordert ffmpeg+tesseract oder liefert nur einen "Linear"-Schätzwert.
///
/// Strategie 3 (Lineare Schätzung): Wenn alle anderen Methoden scheitern,
///   wird der Meterstand linear aus Zeitposition / Videodauer interpoliert.
/// </summary>
public sealed class OsdMeterDetectionService
{
    private readonly OllamaVisionFindingsService _vision;

    // Muster für typische OSD-Meterstände in Kanalvideos:
    // "18.40 m", "18,40m", "18.4", "018.40", "+18.40"
    private static readonly Regex MeterPattern = new(
        @"(?<!\d)([\+\-]?\d{1,4}[.,]\d{1,3})\s*m?\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Typische Meterbereich-Grenzen für Plausibilitätsprüfung
    private const double MeterMin = 0.0;
    private const double MeterMax = 500.0;

    public OsdMeterDetectionService(OllamaVisionFindingsService vision)
    {
        _vision = vision;
    }

    /// <summary>
    /// Versucht den Meterstand aus einem Frame zu lesen.
    /// Gibt null zurück, wenn kein plausibler Wert erkannt wurde.
    /// </summary>
    public async Task<MeterReadResult> ReadMeterAsync(
        string framePngBase64,
        double? linearFallback,
        CancellationToken ct = default)
    {
        // Strategie 1: Vision-LLM liest OSD
        try
        {
            var visionResult = await ReadMeterViaVisionAsync(framePngBase64, ct)
                .ConfigureAwait(false);
            if (visionResult is not null && IsPlausible(visionResult.Value))
                return new MeterReadResult(visionResult.Value, MeterSource.OsdVision);
        }
        catch (OperationCanceledException) { throw; }
        catch { /* Vision nicht verfügbar, weiter */ }

        // Strategie 2: Regex auf bekanntem OSD-Textlayout
        // (Hier könnten Tesseract/Tesseract.NET eingebunden werden;
        //  ohne OCR-Lib geben wir direkt den Fallback zurück)

        // Strategie 3: Lineare Schätzung
        if (linearFallback is not null && IsPlausible(linearFallback.Value))
            return new MeterReadResult(linearFallback.Value, MeterSource.LinearEstimate);

        return new MeterReadResult(0, MeterSource.Unknown);
    }

    /// <summary>
    /// Liest Meterstand aus mehreren Frames und gibt den Median-Wert zurück.
    /// Robuster als ein einzelner Frame bei verwackelten oder unklaren OSD-Darstellungen.
    /// </summary>
    public async Task<MeterReadResult> ReadMeterFromSequenceAsync(
        IReadOnlyList<string> framesBase64,
        double? linearFallback,
        CancellationToken ct = default)
    {
        if (framesBase64.Count == 0)
            return new MeterReadResult(linearFallback ?? 0, MeterSource.LinearEstimate);

        var results = new List<double>();
        foreach (var frame in framesBase64)
        {
            ct.ThrowIfCancellationRequested();
            var r = await ReadMeterAsync(frame, linearFallback: null, ct).ConfigureAwait(false);
            if (r.Source == MeterSource.OsdVision && IsPlausible(r.Value))
                results.Add(r.Value);
        }

        if (results.Count > 0)
        {
            var sorted = results.OrderBy(v => v).ToList();
            var median = sorted[sorted.Count / 2];
            return new MeterReadResult(median, MeterSource.OsdVision);
        }

        if (linearFallback is not null)
            return new MeterReadResult(linearFallback.Value, MeterSource.LinearEstimate);

        return new MeterReadResult(0, MeterSource.Unknown);
    }

    /// <summary>
    /// Dedupliziert eine Sequenz von Meterwerten:
    /// Filtert Ausreißer heraus und interpoliert fehlende Werte linear.
    /// Nützlich um OSD-Lesefehler (z.B. "108" statt "18") zu korrigieren.
    /// </summary>
    public static IReadOnlyList<(double TimeSeconds, double Meter)> SmoothMeterTimeline(
        IReadOnlyList<(double TimeSeconds, double? Meter)> rawTimeline,
        double maxJumpPerSecond = 5.0)
    {
        if (rawTimeline.Count == 0)
            return Array.Empty<(double, double)>();

        var smoothed = new List<(double t, double m)>(rawTimeline.Count);
        double? lastGood = null;

        foreach (var (t, rawMeter) in rawTimeline)
        {
            if (rawMeter is null)
            {
                // Lücke → wird später interpoliert
                smoothed.Add((t, double.NaN));
                continue;
            }

            if (lastGood is not null)
            {
                var lastT = smoothed.LastOrDefault(x => !double.IsNaN(x.m));
                var dt = t - lastT.t;
                var jump = Math.Abs(rawMeter.Value - lastGood.Value);
                if (dt > 0 && jump / dt > maxJumpPerSecond)
                {
                    // Ausreißer → interpolieren
                    smoothed.Add((t, double.NaN));
                    continue;
                }
            }

            lastGood = rawMeter.Value;
            smoothed.Add((t, rawMeter.Value));
        }

        // Lücken interpolieren
        return InterpolateMissing(smoothed);
    }

    // ── Private ──────────────────────────────────────────────────────────────

    private async Task<double?> ReadMeterViaVisionAsync(string base64, CancellationToken ct)
    {
        // Nutzt bestehenden VisionFindingsService – das Meter-Feld des FrameFinding
        var finding = await _vision.AnalyzeAsync(base64, ct).ConfigureAwait(false);
        return finding.Meter;
    }

    private static bool IsPlausible(double value)
        => value >= MeterMin && value <= MeterMax;

    /// <summary>Versucht einen Meterstand aus rohem Text per Regex zu extrahieren.</summary>
    public static double? TryParseMeterFromText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var matches = MeterPattern.Matches(text);
        var candidates = matches
            .Select(m =>
            {
                var raw = m.Groups[1].Value.Replace(',', '.');
                return double.TryParse(raw, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var v) ? v : (double?)null;
            })
            .Where(v => v is not null && v >= MeterMin && v <= MeterMax)
            .Select(v => v!.Value)
            .Distinct()
            .ToList();

        if (candidates.Count == 0) return null;
        if (candidates.Count == 1) return candidates[0];

        // Mehrere Kandidaten: wähle den kleinsten plausiblen (OSD ist meist oben links)
        return candidates.Min();
    }

    private static IReadOnlyList<(double, double)> InterpolateMissing(
        List<(double t, double m)> data)
    {
        var result = new List<(double, double)>(data.Count);
        for (var i = 0; i < data.Count; i++)
        {
            var (t, m) = data[i];
            if (!double.IsNaN(m))
            {
                result.Add((t, m));
                continue;
            }

            // Suche nächsten gültigen Wert vor und nach
            var prevIdx = i - 1;
            while (prevIdx >= 0 && double.IsNaN(data[prevIdx].m)) prevIdx--;

            var nextIdx = i + 1;
            while (nextIdx < data.Count && double.IsNaN(data[nextIdx].m)) nextIdx++;

            if (prevIdx < 0 && nextIdx >= data.Count)
                result.Add((t, 0));
            else if (prevIdx < 0)
                result.Add((t, data[nextIdx].m));
            else if (nextIdx >= data.Count)
                result.Add((t, data[prevIdx].m));
            else
            {
                // Lineare Interpolation
                var (t0, m0) = data[prevIdx];
                var (t1, m1) = data[nextIdx];
                var frac = (t - t0) / (t1 - t0);
                result.Add((t, m0 + frac * (m1 - m0)));
            }
        }
        return result;
    }
}

public sealed record MeterReadResult(double Value, MeterSource Source);

public enum MeterSource
{
    OsdVision,      // Ollama Vision hat OSD direkt gelesen
    OcrText,        // OCR-Engine hat Text aus Bild gelesen
    LinearEstimate, // Lineare Schätzung aus Zeit/Dauer
    Unknown
}
