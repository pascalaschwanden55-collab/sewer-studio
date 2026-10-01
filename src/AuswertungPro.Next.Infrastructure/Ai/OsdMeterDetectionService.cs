using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Infrastructure.Ai;

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
}

public sealed record MeterReadResult(double Value, MeterSource Source);

public enum MeterSource
{
    OsdVision,      // Ollama Vision hat OSD direkt gelesen
    OcrText,        // OCR-Engine hat Text aus Bild gelesen
    LinearEstimate, // Lineare Schätzung aus Zeit/Dauer
    Unknown
}
