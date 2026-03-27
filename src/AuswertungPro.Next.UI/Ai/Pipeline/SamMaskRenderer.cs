using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AuswertungPro.Next.UI.Ai.Pipeline;

/// <summary>
/// Dekodiert SAM-RLE-Masken und rendert sie als gruene Kontur-Overlays auf einem WPF Canvas.
/// RLE-Format: "start_value,run1,run2,..." (aus sam_wrapper.py _rle_encode).
/// </summary>
public static class SamMaskRenderer
{
    /// <summary>Tag fuer SAM-Masken-Elemente auf dem Canvas.</summary>
    public const string MaskTag = "sam_mask";

    /// <summary>Tag fuer Mess-Label-Elemente.</summary>
    public const string LabelTag = "mm_label";

    // ── Farben ──────────────────────────────────────────────────────

    private static readonly Color MaskFill = Color.FromArgb(64, 0, 255, 0);       // Gruen, 25% opak
    private static readonly Color MaskStroke = Color.FromArgb(204, 0, 255, 0);     // Gruen, 80% opak
    private static readonly Color LabelBg = Color.FromArgb(220, 30, 30, 30);       // Dunkelgrau
    private static readonly Color LabelFg = Color.FromArgb(255, 255, 255, 255);    // Weiss

    // ── RLE-Dekodierung ─────────────────────────────────────────────

    /// <summary>
    /// Dekodiert RLE-String zu Masken-Bitmap.
    /// Format: "start_value,run1,run2,..." mit C-order (row-major).
    /// </summary>
    public static bool[,] DecodeRle(string rle, int width, int height)
    {
        var mask = new bool[height, width];
        if (string.IsNullOrWhiteSpace(rle)) return mask;

        var parts = rle.Split(',');
        if (parts.Length < 2) return mask;

        int startVal = int.Parse(parts[0]);
        bool currentVal = startVal != 0;
        int pos = 0;
        int totalPixels = width * height;

        for (int i = 1; i < parts.Length && pos < totalPixels; i++)
        {
            int runLength = int.Parse(parts[i]);
            if (currentVal)
            {
                int end = Math.Min(pos + runLength, totalPixels);
                for (int p = pos; p < end; p++)
                {
                    int row = p / width;
                    int col = p % width;
                    mask[row, col] = true;
                }
            }
            pos += runLength;
            currentVal = !currentVal;
        }

        return mask;
    }

    // ── Kontur-Extraktion ───────────────────────────────────────────

    /// <summary>
    /// Extrahiert die aeussere Kontur einer Binaermaske als WPF StreamGeometry.
    /// Verwendet horizontales Scanline-Verfahren fuer Kontur-Segmente.
    /// Die Maske wird auf targetWidth herunterskaliert fuer Performance.
    /// </summary>
    public static StreamGeometry ExtractContourGeometry(
        bool[,] mask, int origWidth, int origHeight,
        double canvasWidth, double canvasHeight,
        int targetWidth = 480)
    {
        int maskH = mask.GetLength(0);
        int maskW = mask.GetLength(1);

        // Downsample fuer Performance (nur fuer Konturberechnung)
        double scale = Math.Min(1.0, (double)targetWidth / maskW);
        int dsW = (int)(maskW * scale);
        int dsH = (int)(maskH * scale);

        var ds = Downsample(mask, maskH, maskW, dsH, dsW);

        // Canvas-Skalierungsfaktoren
        double scaleX = canvasWidth / origWidth;
        double scaleY = canvasHeight / origHeight;

        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();

        // Horizontale Kontur-Segmente: Finde Uebergaenge in jeder Zeile
        for (int row = 0; row < dsH; row++)
        {
            bool inMask = false;
            int segStart = 0;

            for (int col = 0; col <= dsW; col++)
            {
                bool val = col < dsW && ds[row, col];
                if (val && !inMask)
                {
                    segStart = col;
                    inMask = true;
                }
                else if (!val && inMask)
                {
                    // Segment Ende: obere und untere Kante zeichnen
                    double x1 = (segStart / scale) * scaleX;
                    double x2 = (col / scale) * scaleX;
                    double y = (row / scale) * scaleY;
                    double yNext = ((row + 1) / scale) * scaleY;

                    // Obere Kante (wenn Zeile darueber nicht in Maske)
                    if (row == 0 || !HasOverlap(ds, row - 1, segStart, col))
                    {
                        ctx.BeginFigure(new Point(x1, y), false, false);
                        ctx.LineTo(new Point(x2, y), true, false);
                    }
                    // Untere Kante (wenn Zeile darunter nicht in Maske)
                    if (row == dsH - 1 || !HasOverlap(ds, row + 1, segStart, col))
                    {
                        ctx.BeginFigure(new Point(x1, yNext), false, false);
                        ctx.LineTo(new Point(x2, yNext), true, false);
                    }
                    // Linke Kante
                    if (segStart == 0 || !ds[row, segStart - 1])
                    {
                        ctx.BeginFigure(new Point(x1, y), false, false);
                        ctx.LineTo(new Point(x1, yNext), true, false);
                    }
                    // Rechte Kante
                    if (col >= dsW || !ds[row, col])
                    {
                        ctx.BeginFigure(new Point(x2, y), false, false);
                        ctx.LineTo(new Point(x2, yNext), true, false);
                    }

                    inMask = false;
                }
            }
        }

        geometry.Freeze();
        return geometry;
    }

    /// <summary>
    /// Erzeugt eine gefuellte Geometrie (fuer semi-transparente Maskenfuellung).
    /// Verwendet Rechteck-Approximation pro Scanline-Segment.
    /// </summary>
    public static StreamGeometry ExtractFillGeometry(
        bool[,] mask, int origWidth, int origHeight,
        double canvasWidth, double canvasHeight,
        int targetWidth = 480)
    {
        int maskH = mask.GetLength(0);
        int maskW = mask.GetLength(1);

        double scale = Math.Min(1.0, (double)targetWidth / maskW);
        int dsW = (int)(maskW * scale);
        int dsH = (int)(maskH * scale);

        var ds = Downsample(mask, maskH, maskW, dsH, dsW);

        double scaleX = canvasWidth / origWidth;
        double scaleY = canvasHeight / origHeight;

        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();

        // Gefuellte Rechtecke pro Scanline-Segment
        for (int row = 0; row < dsH; row++)
        {
            bool inMask = false;
            int segStart = 0;

            for (int col = 0; col <= dsW; col++)
            {
                bool val = col < dsW && ds[row, col];
                if (val && !inMask)
                {
                    segStart = col;
                    inMask = true;
                }
                else if (!val && inMask)
                {
                    double x1 = (segStart / scale) * scaleX;
                    double x2 = (col / scale) * scaleX;
                    double y1 = (row / scale) * scaleY;
                    double y2 = ((row + 1) / scale) * scaleY;

                    ctx.BeginFigure(new Point(x1, y1), true, true);
                    ctx.LineTo(new Point(x2, y1), false, false);
                    ctx.LineTo(new Point(x2, y2), false, false);
                    ctx.LineTo(new Point(x1, y2), false, false);

                    inMask = false;
                }
            }
        }

        geometry.Freeze();
        return geometry;
    }

    // ── Canvas-Rendering ────────────────────────────────────────────

    /// <summary>
    /// Rendert alle SAM-Masken als gruene Konturen + Fuellung auf den Canvas.
    /// </summary>
    public static void RenderMasks(
        Canvas canvas,
        SamResponse samResponse,
        IReadOnlyList<MaskQuantificationService.QuantifiedMask> quantified,
        double canvasWidth,
        double canvasHeight)
    {
        if (samResponse == null || samResponse.Masks.Count == 0) return;

        int imgW = samResponse.ImageWidth;
        int imgH = samResponse.ImageHeight;

        for (int i = 0; i < samResponse.Masks.Count; i++)
        {
            var mask = samResponse.Masks[i];
            var quant = i < quantified.Count ? quantified[i] : null;

            // RLE dekodieren
            var decoded = DecodeRle(mask.MaskRle, imgW, imgH);

            // Fuellung rendern (semi-transparent gruen)
            var fillGeom = ExtractFillGeometry(decoded, imgW, imgH, canvasWidth, canvasHeight);
            var fillPath = new Path
            {
                Data = fillGeom,
                Fill = new SolidColorBrush(MaskFill),
                Tag = MaskTag,
                IsHitTestVisible = false
            };
            canvas.Children.Add(fillPath);

            // Kontur rendern (gruene Linie)
            var contourGeom = ExtractContourGeometry(decoded, imgW, imgH, canvasWidth, canvasHeight);
            var contourPath = new Path
            {
                Data = contourGeom,
                Stroke = new SolidColorBrush(MaskStroke),
                StrokeThickness = 2,
                Tag = MaskTag,
                IsHitTestVisible = false
            };
            canvas.Children.Add(contourPath);

            // Label-Badge positionieren (ueber der BBox)
            if (quant != null && mask.Bbox.Count >= 4)
            {
                double bboxX = mask.Bbox[0] / imgW * canvasWidth;
                double bboxY = mask.Bbox[1] / imgH * canvasHeight;
                RenderMaskLabel(canvas, quant, bboxX, Math.Max(0, bboxY - 40));
            }
        }
    }

    /// <summary>
    /// Rendert ein Label-Badge fuer eine quantifizierte Maske.
    /// Zeigt: Label (VSA-Klartext) + Messungen.
    /// </summary>
    private static void RenderMaskLabel(
        Canvas canvas,
        MaskQuantificationService.QuantifiedMask quant,
        double x, double y)
    {
        // Klartext-Label bauen
        var label = Services.CodeCatalog.VsaCodeTree.LookupLabel(quant.Label) ?? quant.Label;
        var measurements = BuildMeasurementText(quant);

        var textBlock = new TextBlock
        {
            FontSize = 10,
            Foreground = new SolidColorBrush(LabelFg),
            TextWrapping = TextWrapping.NoWrap
        };
        textBlock.Inlines.Add(new System.Windows.Documents.Run(label) { FontWeight = FontWeights.Bold });
        if (!string.IsNullOrEmpty(measurements))
        {
            textBlock.Inlines.Add(new System.Windows.Documents.LineBreak());
            textBlock.Inlines.Add(new System.Windows.Documents.Run(measurements) { FontSize = 9 });
        }

        var border = new Border
        {
            Background = new SolidColorBrush(LabelBg),
            BorderBrush = new SolidColorBrush(MaskStroke),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(5, 2, 5, 2),
            Child = textBlock,
            Tag = LabelTag,
            IsHitTestVisible = false
        };

        Canvas.SetLeft(border, x);
        Canvas.SetTop(border, y);
        canvas.Children.Add(border);
    }

    /// <summary>
    /// Baut den Mess-Text fuer ein Label-Badge.
    /// Format: "H:45mm W:2mm | 3:00 | 15%"
    /// </summary>
    private static string BuildMeasurementText(MaskQuantificationService.QuantifiedMask q)
    {
        var parts = new List<string>();

        if (q.HeightMm.HasValue && q.WidthMm.HasValue)
            parts.Add($"H:{q.HeightMm}mm W:{q.WidthMm}mm");
        else if (q.HeightMm.HasValue)
            parts.Add($"H:{q.HeightMm}mm");

        if (!string.IsNullOrEmpty(q.ClockPosition))
            parts.Add(q.ClockPosition);

        if (q.ExtentPercent is > 0)
            parts.Add($"{q.ExtentPercent}%");
        else if (q.CrossSectionReductionPercent is > 0)
            parts.Add($"QR:{q.CrossSectionReductionPercent}%");
        else if (q.IntrusionPercent is > 0)
            parts.Add($"Einr:{q.IntrusionPercent}%");

        return string.Join(" | ", parts);
    }

    /// <summary>
    /// Entfernt alle SAM-Masken und Labels vom Canvas.
    /// </summary>
    public static void ClearMasks(Canvas canvas)
    {
        var toRemove = canvas.Children.OfType<FrameworkElement>()
            .Where(e => MaskTag.Equals(e.Tag) || LabelTag.Equals(e.Tag))
            .ToList();
        foreach (var el in toRemove)
            canvas.Children.Remove(el);
    }

    // ── Hilfsfunktionen ─────────────────────────────────────────────

    private static bool[,] Downsample(bool[,] src, int srcH, int srcW, int dstH, int dstW)
    {
        if (dstH >= srcH && dstW >= srcW) return src;

        var dst = new bool[dstH, dstW];
        double yScale = (double)srcH / dstH;
        double xScale = (double)srcW / dstW;

        for (int r = 0; r < dstH; r++)
        {
            int srcR = Math.Min((int)(r * yScale), srcH - 1);
            for (int c = 0; c < dstW; c++)
            {
                int srcC = Math.Min((int)(c * xScale), srcW - 1);
                dst[r, c] = src[srcR, srcC];
            }
        }
        return dst;
    }

    private static bool HasOverlap(bool[,] ds, int row, int colStart, int colEnd)
    {
        int w = ds.GetLength(1);
        if (row < 0 || row >= ds.GetLength(0)) return false;
        for (int c = Math.Max(0, colStart); c < Math.Min(w, colEnd); c++)
            if (ds[row, c]) return true;
        return false;
    }
}
