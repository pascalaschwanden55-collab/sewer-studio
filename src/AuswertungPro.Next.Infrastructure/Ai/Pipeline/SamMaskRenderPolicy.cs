using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>
/// Bestimmt den visuellen Darstellungsmodus einer SAM-Maske (Policy ohne WPF-Abhaengigkeit).
/// </summary>
public static class SamMaskRenderPolicy
{
    /// <summary>Wie eine Maske visuell dargestellt wird.</summary>
    public enum MaskVisualMode
    {
        /// <summary>Gar nicht zeichnen (z. B. bestaetigter Hintergrund wie Wasserwand).</summary>
        Hidden,
        /// <summary>Nur Kontur, keine Fuellung (z. B. grossflaechige Befunde).</summary>
        OutlineOnly,
        /// <summary>Dezente Fuellung plus Kontur (kleine, sichere Befunde).</summary>
        SubtleFill
    }

    /// <summary>
    /// Parametersatz fuer die Render-Policy. Steuert ab wann eine Maske als
    /// Hintergrund versteckt, als grosser Befund nur als Kontur oder als kleiner
    /// sicherer Befund mit Fuellung gezeichnet wird.
    /// </summary>
    public sealed record RenderOptions(
        double LargeFindingOutlineAreaRatio,
        double MinimumVisibleDetectionConfidence,
        double MinimumVisibleSamConfidence,
        double MinimumFillDetectionConfidence,
        byte FillAlpha,
        byte StrokeAlpha,
        IReadOnlySet<string> HiddenLabelTokens)
    {
        /// <summary>Voreinstellung im WinCan-Stil: grosse Befunde bleiben als Kontur erhalten.</summary>
        public static RenderOptions WinCanStyle { get; } = new(
            LargeFindingOutlineAreaRatio: 0.30,
            MinimumVisibleDetectionConfidence: 0.25,
            MinimumVisibleSamConfidence: 0.25,
            MinimumFillDetectionConfidence: 0.60,
            FillAlpha: 24,
            StrokeAlpha: 230,
            HiddenLabelTokens: new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "water wall",
                "structure water wall",
                "pipe wall",
                "black border",
                "osd"
            });
    }

    /// <summary>
    /// Eine zu rendernde Maske samt optionaler Quantifizierung und optionaler
    /// Detektor-Konfidenz (z. B. aus Grounding DINO).
    /// </summary>
    public sealed record MaskRenderCandidate(
        SamMaskResult Mask,
        MaskQuantificationService.QuantifiedMask? Quant,
        double? DetectionConfidence = null);

    /// <summary>Ergebnis der Render-Policy: Darstellungsmodus und Begruendung.</summary>
    public sealed record RenderDecision(MaskVisualMode Mode, string? Reason);

    /// <summary>
    /// Entscheidet, wie eine Maske dargestellt wird. Reine, testbare Logik ohne
    /// WPF-Abhaengigkeit. Reihenfolge: Hintergrund-Label verstecken, dann zu
    /// niedrige Konfidenz verstecken, dann grosse Befunde als Kontur, dann
    /// kleine sichere Befunde mit Fuellung, sonst nur Kontur.
    /// </summary>
    public static RenderDecision DecideVisualMode(MaskRenderCandidate candidate, RenderOptions? options = null)
    {
        options ??= RenderOptions.WinCanStyle;

        var mask = candidate.Mask;
        var label = NormalizeLabel(mask.Label ?? candidate.Quant?.Label ?? "");
        if (options.HiddenLabelTokens.Any(token => label.Contains(NormalizeLabel(token), StringComparison.Ordinal)))
            return new RenderDecision(MaskVisualMode.Hidden, "background_label");

        var detectionConfidence = candidate.DetectionConfidence;
        var samConfidence = Math.Max(mask.Confidence, candidate.Quant?.Confidence ?? 0);
        if ((detectionConfidence ?? samConfidence) < options.MinimumVisibleDetectionConfidence
            && samConfidence < options.MinimumVisibleSamConfidence)
            return new RenderDecision(MaskVisualMode.Hidden, "confidence_too_low");

        // Backup-Verhalten (11.06, gute Segmentierung): jede sichtbare Maske wird gefuellt +
        // Kontur gezeichnet. Das fruehere OutlineOnly-Strippen (grosse Flaeche bzw. fehlende
        // DINO-Confidence im manuellen Mark-Pfad) liess nur eine duenne Scanline-Kontur uebrig
        // und wirkte "verzerrt/falsch". Hintergrund (Label/Confidence) bleibt weiter Hidden.
        return new RenderDecision(MaskVisualMode.SubtleFill, null);
    }

    /// <summary>
    /// Berechnet den Flaechenanteil einer Maske am Gesamtbild (0..1).
    /// </summary>
    public static double GetAreaRatio(SamMaskResult mask)
    {
        if (mask.ImageAreaPixels > 0 && mask.MaskAreaPixels >= 0)
            return mask.MaskAreaPixels / (double)mask.ImageAreaPixels;
        return 0;
    }

    /// <summary>
    /// Normalisiert ein Label fuer den Vergleich: Trim, Unterstriche/Bindestriche
    /// zu Leerzeichen, Kleinbuchstaben.
    /// </summary>
    public static string NormalizeLabel(string label)
    {
        return label
            .Trim()
            .Replace('_', ' ')
            .Replace('-', ' ')
            .ToLowerInvariant();
    }
}
