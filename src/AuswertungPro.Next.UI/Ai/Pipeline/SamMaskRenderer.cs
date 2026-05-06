using System;
using AuswertungPro.Next.Application.Ai;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.Application.Ai.Pipeline;

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

    // ── Farben pro Schadenstyp ─────────────────────────────────────
    // Jede Schadensgruppe hat eine eigene Farbe damit man sofort sieht
    // was die KI erkannt hat und gezielt korrigieren kann.

    private static readonly Color LabelBg = Color.FromArgb(220, 30, 30, 30);       // Dunkelgrau
    private static readonly Color LabelFg = Color.FromArgb(255, 255, 255, 255);    // Weiss

    /// <summary>
    /// Gibt Fill- und Stroke-Farbe fuer ein DINO-Label zurueck.
    /// Farbschema: Gruen=Bestand, Rot=Strukturell, Orange=Betrieblich, Cyan=Wasser.
    /// </summary>
    private static (Color Fill, Color Stroke) GetMaskColors(string? label)
    {
        var vsaCode = VsaCodeResolver.InferCodeFromLabel(label ?? "");
        var prefix = vsaCode?.Length >= 2 ? vsaCode[..2].ToUpperInvariant() : "";

        return prefix switch
        {
            // BA: Strukturelle Schaeden (Riss, Bruch, Deformation) → Rot
            "BA" => (Color.FromArgb(50, 255, 40, 40), Color.FromArgb(200, 255, 60, 60)),
            // BB: Betriebliche Stoerungen (Wurzeln, Ablagerung) → Orange
            "BB" => (Color.FromArgb(50, 255, 165, 0), Color.FromArgb(200, 255, 180, 30)),
            // BC: Bestandsaufnahme (Anschluss, Bogen) → Gruen
            "BC" => (Color.FromArgb(50, 0, 255, 0), Color.FromArgb(200, 0, 255, 0)),
            // BD: Steuercodes (Wasserspiegel, Abbruch) → Cyan
            "BD" => (Color.FromArgb(50, 0, 200, 255), Color.FromArgb(200, 0, 220, 255)),
            // AE: Aenderungen (Profilwechsel) → Gelb
            "AE" => (Color.FromArgb(50, 255, 255, 0), Color.FromArgb(200, 255, 255, 50)),
            // Unbekannt → Gruen (Standard)
            _ => (Color.FromArgb(64, 0, 255, 0), Color.FromArgb(204, 0, 255, 0)),
        };
    }

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

    // ── Boundary-Tracing (Moore-Neighborhood) ────────────────────────

    /// <summary>
    /// Extrahiert die aeussere Kontur einer Binaermaske als verbundene Polyline-Ketten
    /// (statt achsen-paralleler Rechtecke wie ExtractContourGeometry). Liefert eine
    /// Liste von Polygonzuegen - eine Liste pro zusammenhaengendem Maskenbereich.
    /// Verwendet vereinfachten Moore-Neighborhood-Trace.
    /// </summary>
    public static List<List<Point>> ExtractContourPolylines(
        bool[,] mask, int origWidth, int origHeight,
        double canvasWidth, double canvasHeight)
    {
        int h = mask.GetLength(0);
        int w = mask.GetLength(1);
        var visited = new bool[h, w];
        var result = new List<List<Point>>();
        double sx = canvasWidth / origWidth;
        double sy = canvasHeight / origHeight;

        // 8-Nachbar-Reihenfolge fuer Moore-Trace (im Uhrzeigersinn ab 12 Uhr).
        int[] dx = { 0, 1, 1, 1, 0, -1, -1, -1 };
        int[] dy = { -1, -1, 0, 1, 1, 1, 0, -1 };

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (!mask[y, x] || visited[y, x]) continue;
                // Pruefen ob Boundary-Pixel (mind. ein Nachbar ausserhalb Maske)
                bool isBoundary = false;
                for (int n = 0; n < 8 && !isBoundary; n++)
                {
                    int nx = x + dx[n], ny = y + dy[n];
                    if (nx < 0 || nx >= w || ny < 0 || ny >= h || !mask[ny, nx])
                        isBoundary = true;
                }
                if (!isBoundary) continue;

                // Trace Kontur ab diesem Punkt
                var contour = new List<Point>();
                int cx = x, cy = y;
                int prevDir = 4; // kommt von oben (entspricht index 4 = 6 Uhr)
                int safety = w * h * 2; // verhindert Endlos-Schleife
                while (safety-- > 0)
                {
                    visited[cy, cx] = true;
                    contour.Add(new Point(cx * sx, cy * sy));

                    // Suche naechsten Boundary-Nachbarn (im Uhrzeigersinn nach prev+6)
                    int startDir = (prevDir + 6) % 8;
                    bool found = false;
                    for (int k = 0; k < 8; k++)
                    {
                        int d = (startDir + k) % 8;
                        int nx = cx + dx[d], ny = cy + dy[d];
                        if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                        if (mask[ny, nx])
                        {
                            cx = nx; cy = ny; prevDir = d;
                            found = true;
                            break;
                        }
                    }
                    if (!found) break;
                    if (cx == x && cy == y && contour.Count > 2) break; // Schleife geschlossen
                }
                if (contour.Count >= 4)
                    result.Add(contour);
            }
        }
        return result;
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
    /// Rendert alle SAM-Masken als Konturen + Fuellung auf den Canvas.
    /// Farben werden automatisch nach VSA-Schadenstyp gewaehlt.
    /// </summary>
    /// <param name="onMaskClicked">Optionaler Callback wenn eine Maske angeklickt wird (Index).</param>
    /// <param name="onMaskDeleted">Optionaler Callback wenn Delete auf einer Maske gedrueckt wird (Index).</param>
    /// <param name="previewMode">Wenn true: Masken grau + gedimmt rendern (Vorschau fuer ferne Detektionen).</param>
    /// <param name="indexOffset">Offset fuer Masken-Index im Tag (fuer zweiten RenderMasks-Aufruf).</param>
    public static void RenderMasks(
        Canvas canvas,
        SamResponse samResponse,
        IReadOnlyList<MaskQuantificationService.QuantifiedMask> quantified,
        double canvasWidth,
        double canvasHeight,
        Action<int>? onMaskClicked = null,
        Action<int>? onMaskDeleted = null,
        bool previewMode = false,
        int indexOffset = 0)
    {
        if (samResponse == null || samResponse.Masks.Count == 0) return;

        int imgW = samResponse.ImageWidth;
        int imgH = samResponse.ImageHeight;
        bool interactive = !previewMode && (onMaskClicked is not null || onMaskDeleted is not null);

        for (int i = 0; i < samResponse.Masks.Count; i++)
        {
            var mask = samResponse.Masks[i];
            var quant = i < quantified.Count ? quantified[i] : null;
            var maskIndex = i + indexOffset; // Closure-Kopie (mit Offset fuer zweiten Aufruf)

            // RLE dekodieren
            var decoded = DecodeRle(mask.MaskRle, imgW, imgH);

            // Farbe: normal nach Schadenstyp, oder grau wenn Vorschau (fern)
            var (fillColor, strokeColor) = previewMode
                ? (Color.FromArgb(30, 128, 128, 128), Color.FromArgb(100, 160, 160, 160))
                : GetMaskColors(mask.Label);

            // Gruppen-Tag fuer zusammengehoerige Elemente (Fill + Kontur + Label)
            var groupTag = $"{MaskTag}_{maskIndex}";

            // Fuellung rendern (semi-transparent, klickbar wenn interaktiv)
            var fillGeom = ExtractFillGeometry(decoded, imgW, imgH, canvasWidth, canvasHeight);
            var fillPath = new Path
            {
                Data = fillGeom,
                Fill = new SolidColorBrush(fillColor),
                Tag = groupTag,
                IsHitTestVisible = interactive,
                Cursor = interactive ? System.Windows.Input.Cursors.Hand : null
            };
            if (interactive)
            {
                fillPath.MouseLeftButtonDown += (_, _) => onMaskClicked?.Invoke(maskIndex);
                fillPath.KeyDown += (s, e) =>
                {
                    if (e.Key == System.Windows.Input.Key.Delete)
                    {
                        onMaskDeleted?.Invoke(maskIndex);
                        // Alle Elemente dieser Maske vom Canvas entfernen
                        RemoveMaskGroup(canvas, groupTag);
                        e.Handled = true;
                    }
                };
                // Hover-Effekt: Kontur verdicken
                fillPath.MouseEnter += (s, _) =>
                {
                    foreach (var el in canvas.Children.OfType<Path>()
                        .Where(p => groupTag.Equals(p.Tag as string) && p.Stroke is not null))
                        el.StrokeThickness = 4;
                };
                fillPath.MouseLeave += (s, _) =>
                {
                    foreach (var el in canvas.Children.OfType<Path>()
                        .Where(p => groupTag.Equals(p.Tag as string) && p.Stroke is not null))
                        el.StrokeThickness = 2;
                };
            }
            canvas.Children.Add(fillPath);

            // Kontur rendern (farbig)
            var contourGeom = ExtractContourGeometry(decoded, imgW, imgH, canvasWidth, canvasHeight);
            var contourPath = new Path
            {
                Data = contourGeom,
                Stroke = new SolidColorBrush(strokeColor),
                StrokeThickness = 2,
                Tag = groupTag,
                IsHitTestVisible = false // Kontur nicht klickbar, nur Fill
            };
            canvas.Children.Add(contourPath);

            // Label-Badge positionieren (ueber der BBox)
            if (quant != null && mask.Bbox.Count >= 4)
            {
                double bboxX = mask.Bbox[0] / imgW * canvasWidth;
                double bboxY = mask.Bbox[1] / imgH * canvasHeight;
                RenderMaskLabel(canvas, quant, bboxX, Math.Max(0, bboxY - 40), strokeColor, groupTag);
            }
        }
    }

    /// <summary>Entfernt alle Canvas-Elemente einer Masken-Gruppe.</summary>
    public static void RemoveMaskGroup(Canvas canvas, string groupTag)
    {
        var toRemove = canvas.Children.OfType<UIElement>()
            .Where(e => e is FrameworkElement fe && groupTag.Equals(fe.Tag as string))
            .ToList();
        foreach (var el in toRemove)
            canvas.Children.Remove(el);
    }

    /// <summary>
    /// Rendert ein Label-Badge fuer eine quantifizierte Maske.
    /// Zeigt: Label (VSA-Klartext) + Messungen.
    /// </summary>
    private static void RenderMaskLabel(
        Canvas canvas,
        MaskQuantificationService.QuantifiedMask quant,
        double x, double y,
        Color borderColor,
        string? groupTag = null)
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
            BorderBrush = new SolidColorBrush(borderColor),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(5, 2, 5, 2),
            Child = textBlock,
            Tag = groupTag ?? LabelTag,
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
            .Where(e =>
            {
                var tag = e.Tag as string;
                if (tag == null) return false;
                return tag.StartsWith(MaskTag, StringComparison.Ordinal)
                    || tag.StartsWith(LabelTag, StringComparison.Ordinal);
            })
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
