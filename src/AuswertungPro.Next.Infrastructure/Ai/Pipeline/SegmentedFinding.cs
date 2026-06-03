using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>
/// Feste Kopplungseinheit eines segmentierten Befunds. Ersetzt die fruehere lose
/// Index-Kopplung von DINO/SAM/Quantifizierung (fragil bei SAM-skipped-boxes).
/// </summary>
public sealed record SegmentedFinding(
    DinoDetectionDto? Dino,
    SamMaskResult Mask,
    MaskQuantificationService.QuantifiedMask Quant,
    MetrierungProximityResult Proximity);

/// <summary>
/// Baut SegmentedFindings masken-basiert. Iteriert ueber die SAM-Masken (uebersprungene
/// Boxen existieren dort nicht), paart Mask/Quant per Index INNERHALB der Maskenliste
/// und ordnet DINO ueber bbox-IoU + Label zu (kein Listen-Index ueber Listen hinweg).
/// </summary>
public static class SegmentedFindingBuilder
{
    public static IReadOnlyList<SegmentedFinding> Build(
        SamResponse sam,
        IReadOnlyList<DinoDetectionDto> dinoDetections,
        IReadOnlyList<MaskQuantificationService.QuantifiedMask> quantified,
        double vanishX, double vanishY, double pipeRadiusNorm,
        MetrierungProximityThresholds thresholds)
    {
        var result = new List<SegmentedFinding>(sam.Masks.Count);
        int w = sam.ImageWidth > 0 ? sam.ImageWidth : 1;
        int h = sam.ImageHeight > 0 ? sam.ImageHeight : 1;
        double aspect = (double)w / h;
        double pipeR = pipeRadiusNorm > 0 ? pipeRadiusNorm : 0.5;

        for (int m = 0; m < sam.Masks.Count; m++)
        {
            var mask = sam.Masks[m];
            var quant = m < quantified.Count ? quantified[m] : null;
            if (quant is null) continue; // QuantifyAll ist 1:1; defensiv

            var dino = MatchDino(mask, dinoDetections);

            // Box normiert aus der Masken-bbox (traegt die geclampte Input-Box).
            double x1 = 0, y1 = 0, x2 = 1, y2 = 1;
            if (mask.Bbox.Count >= 4)
            {
                x1 = mask.Bbox[0] / w; y1 = mask.Bbox[1] / h;
                x2 = mask.Bbox[2] / w; y2 = mask.Bbox[3] / h;
            }

            var input = new MetrierungProximityInput(x1, y1, x2, y2, vanishX, vanishY, aspect, pipeR);
            var prox = MetrierungProximityEvaluator.Evaluate(input, thresholds);

            result.Add(new SegmentedFinding(dino, mask, quant, prox));
        }
        return result;
    }

    /// <summary>
    /// DINO-Detection mit gleichem Label und hoechstem Containment der Masken-bbox; null wenn keine plausibel.
    /// Containment (Schnitt / Masken-Flaeche) statt klassischer IoU, weil die SAM-Masken-bbox die
    /// GECLAMPTE Input-Box ist und damit Teilmenge der (ungeclampten) DINO-Box. Klassische IoU fiele
    /// fuer Boxen, die weit aus dem Bild ragen, faelschlich unter die Schwelle und liefe ins Dino=null.
    /// </summary>
    private static DinoDetectionDto? MatchDino(SamMaskResult mask, IReadOnlyList<DinoDetectionDto> dinos)
    {
        if (mask.Bbox.Count < 4 || dinos.Count == 0) return null;
        double mx1 = mask.Bbox[0], my1 = mask.Bbox[1], mx2 = mask.Bbox[2], my2 = mask.Bbox[3];
        double maskArea = Math.Max(0, mx2 - mx1) * Math.Max(0, my2 - my1);
        if (maskArea <= 0) return null;

        DinoDetectionDto? best = null;
        double bestScore = 0.0;
        foreach (var d in dinos)
        {
            if (!string.Equals(d.Label, mask.Label, StringComparison.OrdinalIgnoreCase)) continue;
            double containment = Intersection(mx1, my1, mx2, my2, d.X1, d.Y1, d.X2, d.Y2) / maskArea;
            if (containment > bestScore) { bestScore = containment; best = d; }
        }
        return bestScore >= 0.5 ? best : null; // konservativer Mindest-Overlap
    }

    private static double Intersection(double ax1, double ay1, double ax2, double ay2,
                                       double bx1, double by1, double bx2, double by2)
    {
        double ix1 = Math.Max(ax1, bx1), iy1 = Math.Max(ay1, by1);
        double ix2 = Math.Min(ax2, bx2), iy2 = Math.Min(ay2, by2);
        double iw = Math.Max(0, ix2 - ix1), ih = Math.Max(0, iy2 - iy1);
        return iw * ih;
    }
}
