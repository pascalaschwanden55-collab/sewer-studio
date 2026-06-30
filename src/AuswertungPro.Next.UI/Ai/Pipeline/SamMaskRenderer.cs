using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Ai.Pipeline;

/// <summary>
/// Dekodiert SAM-RLE-Masken und rendert sie als gruene Kontur-Overlays auf einem WPF Canvas.
/// RLE-Format: "start_value,run1,run2,..." (aus sam_wrapper.py _rle_encode).
/// Reine Logik (Dekodierung, Policy, Label-Text) ist nach Infrastructure.Ai.Pipeline
/// ausgelagert: SamMaskDecoder, SamMaskRenderPolicy, MaskLabelTextBuilder.
/// </summary>
public static class SamMaskRenderer
{
    /// <summary>Tag fuer SAM-Masken-Elemente auf dem Canvas.</summary>
    public const string MaskTag = "sam_mask";

    /// <summary>Tag fuer Mess-Label-Elemente.</summary>
    public const string LabelTag = "mm_label";

    // ── Render-Policy (WinCan-Stil) ─────────────────────────────────
    // Typen werden von Infrastructure bereitgestellt und hier weitergereicht,
    // damit alle Aufrufer (inkl. Codex-generierter Code) unveraendert bleiben.

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
            LargeFindingOutlineAreaRatio: SamMaskRenderPolicy.RenderOptions.WinCanStyle.LargeFindingOutlineAreaRatio,
            MinimumVisibleDetectionConfidence: SamMaskRenderPolicy.RenderOptions.WinCanStyle.MinimumVisibleDetectionConfidence,
            MinimumVisibleSamConfidence: SamMaskRenderPolicy.RenderOptions.WinCanStyle.MinimumVisibleSamConfidence,
            MinimumFillDetectionConfidence: SamMaskRenderPolicy.RenderOptions.WinCanStyle.MinimumFillDetectionConfidence,
            FillAlpha: SamMaskRenderPolicy.RenderOptions.WinCanStyle.FillAlpha,
            StrokeAlpha: SamMaskRenderPolicy.RenderOptions.WinCanStyle.StrokeAlpha,
            HiddenLabelTokens: SamMaskRenderPolicy.RenderOptions.WinCanStyle.HiddenLabelTokens);
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
    /// Zusammenfassung eines Render-Durchlaufs: wie viele Masken gezeichnet,
    /// versteckt, nur als Kontur oder mit Fuellung dargestellt wurden, samt
    /// aufgeschluesselter Versteck-Gruende.
    /// </summary>
    public sealed record RenderSummary(
        int Rendered,
        int Hidden,
        int OutlineOnly,
        int SubtleFill,
        IReadOnlyDictionary<string, int> HiddenReasons);

    /// <summary>Bequemer Zugriff auf die WinCan-Voreinstellung.</summary>
    public static RenderOptions WinCanStyleOptions => RenderOptions.WinCanStyle;

    /// <summary>Obergrenze fuer Masken-Pixel (Schutz gegen absurde Dimensionen vom Sidecar). ~50 MB bool.</summary>
    private const long MaxMaskPixels = SamMaskDecoder.MaxMaskPixels;

    // ── Farben ──────────────────────────────────────────────────────

    private static readonly Color MaskStroke = Color.FromArgb(204, 0, 255, 0);     // Gruen, 80% opak (Label-Rahmen)
    private static readonly Color LabelBg = Color.FromArgb(220, 30, 30, 30);       // Dunkelgrau
    private static readonly Color LabelFg = Color.FromArgb(255, 255, 255, 255);    // Weiss

    // ── RLE-Dekodierung ─────────────────────────────────────────────

    /// <summary>
    /// Dekodiert RLE-String zu Masken-Bitmap.
    /// Format: "start_value,run1,run2,..." mit C-order (row-major).
    /// Delegiert an <see cref="SamMaskDecoder.DecodeRle"/> (Infrastructure).
    /// </summary>
    public static bool[,] DecodeRle(string rle, int width, int height)
        => SamMaskDecoder.DecodeRle(rle, width, height);

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

        var ds = SamMaskDecoder.Downsample(mask, maskH, maskW, dsH, dsW);

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
                    if (row == 0 || !SamMaskDecoder.HasOverlap(ds, row - 1, segStart, col))
                    {
                        ctx.BeginFigure(new Point(x1, y), false, false);
                        ctx.LineTo(new Point(x2, y), true, false);
                    }
                    // Untere Kante (wenn Zeile darunter nicht in Maske)
                    if (row == dsH - 1 || !SamMaskDecoder.HasOverlap(ds, row + 1, segStart, col))
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

        var ds = SamMaskDecoder.Downsample(mask, maskH, maskW, dsH, dsW);

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
    /// Entscheidet, wie eine Maske dargestellt wird. Reine, testbare Logik ohne
    /// WPF-Abhaengigkeit. Delegiert an <see cref="SamMaskRenderPolicy.DecideVisualMode"/>
    /// (Infrastructure) und bildet das Ergebnis auf die UI-lokalen Typen ab.
    /// </summary>
    public static RenderDecision DecideVisualMode(MaskRenderCandidate candidate, RenderOptions? options = null)
    {
        // UI-RenderOptions in Infrastructure-RenderOptions mappen (Thin-Delegate).
        var infraOpts = options is null
            ? SamMaskRenderPolicy.RenderOptions.WinCanStyle
            : new SamMaskRenderPolicy.RenderOptions(
                options.LargeFindingOutlineAreaRatio,
                options.MinimumVisibleDetectionConfidence,
                options.MinimumVisibleSamConfidence,
                options.MinimumFillDetectionConfidence,
                options.FillAlpha,
                options.StrokeAlpha,
                options.HiddenLabelTokens);

        var infraCandidate = new SamMaskRenderPolicy.MaskRenderCandidate(
            candidate.Mask, candidate.Quant, candidate.DetectionConfidence);

        var decision = SamMaskRenderPolicy.DecideVisualMode(infraCandidate, infraOpts);

        // Infrastruktur-MaskVisualMode -> UI-MaskVisualMode (beide Enums identisch).
        var uiMode = decision.Mode switch
        {
            SamMaskRenderPolicy.MaskVisualMode.Hidden => MaskVisualMode.Hidden,
            SamMaskRenderPolicy.MaskVisualMode.OutlineOnly => MaskVisualMode.OutlineOnly,
            _ => MaskVisualMode.SubtleFill
        };
        return new RenderDecision(uiMode, decision.Reason);
    }

    /// <summary>
    /// Rendert eine Liste von Kandidaten konturbasiert (WinCan-Stil): bestaetigter
    /// Hintergrund wird versteckt, grosse Befunde nur als Kontur, kleine sichere
    /// Befunde dezent gefuellt. Liefert eine Zusammenfassung zurueck.
    /// </summary>
    public static RenderSummary RenderCandidates(
        Canvas canvas,
        IReadOnlyList<MaskRenderCandidate> candidates,
        int imageWidth,
        int imageHeight,
        double canvasWidth,
        double canvasHeight,
        ILogger? logger = null,
        RenderOptions? options = null,
        double offsetX = 0,
        double offsetY = 0)
    {
        if (candidates.Count == 0)
            return new RenderSummary(0, 0, 0, 0, new Dictionary<string, int>());

        int rendered = 0, hidden = 0, outlineOnly = 0, subtleFill = 0;
        var hiddenReasons = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        options ??= WinCanStyleOptions;

        for (var i = 0; i < candidates.Count; i++)
        {
            try
            {
                var candidate = candidates[i];
                var decision = DecideVisualMode(candidate, options);
                if (decision.Mode == MaskVisualMode.Hidden)
                {
                    hidden++;
                    var reason = decision.Reason ?? "hidden";
                    hiddenReasons[reason] = hiddenReasons.TryGetValue(reason, out var count) ? count + 1 : 1;
                    continue;
                }

                RenderSingleMask(
                    canvas,
                    candidate.Mask,
                    candidate.Quant,
                    imageWidth,
                    imageHeight,
                    canvasWidth,
                    canvasHeight,
                    decision.Mode,
                    options,
                    offsetX,
                    offsetY);
                rendered++;
                if (decision.Mode == MaskVisualMode.OutlineOnly)
                    outlineOnly++;
                else
                    subtleFill++;
            }
            catch (Exception ex)
            {
                // Eine defekte Maske darf das Rendern der uebrigen nicht verhindern.
                logger?.LogWarning(ex, "SamMaskRenderer: Maske {MaskIndex} uebersprungen.", i);
            }
        }

        return new RenderSummary(rendered, hidden, outlineOnly, subtleFill, hiddenReasons);
    }

    /// <summary>
    /// Rueckwaertskompatible Bruecke: nimmt eine vollstaendige SAM-Antwort und
    /// rendert sie ueber <see cref="RenderCandidates"/> konturbasiert.
    /// </summary>
    public static RenderSummary RenderMasks(
        Canvas canvas,
        SamResponse samResponse,
        IReadOnlyList<MaskQuantificationService.QuantifiedMask> quantified,
        double canvasWidth,
        double canvasHeight,
        ILogger? logger = null,
        RenderOptions? options = null,
        double offsetX = 0,
        double offsetY = 0)
    {
        if (samResponse == null || samResponse.Masks.Count == 0)
            return new RenderSummary(0, 0, 0, 0, new Dictionary<string, int>());

        var candidates = samResponse.Masks
            .Select((mask, index) => new MaskRenderCandidate(
                mask,
                index < quantified.Count ? quantified[index] : null,
                DetectionConfidence: null))
            .ToList();

        return RenderCandidates(
            canvas,
            candidates,
            samResponse.ImageWidth,
            samResponse.ImageHeight,
            canvasWidth,
            canvasHeight,
            logger,
            options,
            offsetX,
            offsetY);
    }

    /// <summary>
    /// Rendert eine einzelne SAM-Maske (Fuellung + Kontur + Label) auf den Canvas.
    /// </summary>
    private static void RenderSingleMask(
        Canvas canvas,
        SamMaskResult mask,
        MaskQuantificationService.QuantifiedMask? quant,
        int imgW, int imgH,
        double canvasWidth, double canvasHeight,
        MaskVisualMode visualMode,
        RenderOptions options,
        double offsetX = 0,
        double offsetY = 0)
    {
        // RLE dekodieren
        var decoded = DecodeRle(mask.MaskRle, imgW, imgH);

        // Fuellung nur bei dezenter Fuellung (kleine, sichere Befunde) rendern.
        if (visualMode == MaskVisualMode.SubtleFill)
        {
            var fillGeom = ExtractFillGeometry(decoded, imgW, imgH, canvasWidth, canvasHeight);
            var fillPath = new Path
            {
                Data = fillGeom,
                Fill = new SolidColorBrush(Color.FromArgb(options.FillAlpha, 0, 255, 0)),
                Tag = MaskTag,
                IsHitTestVisible = false,
                RenderTransform = new TranslateTransform(offsetX, offsetY)
            };
            canvas.Children.Add(fillPath);
        }

        // Kontur rendern (gruene Linie)
        var contourGeom = ExtractContourGeometry(decoded, imgW, imgH, canvasWidth, canvasHeight);
        var contourPath = new Path
        {
            Data = contourGeom,
            Stroke = new SolidColorBrush(Color.FromArgb(options.StrokeAlpha, 0, 255, 0)),
            StrokeThickness = 2,
            Tag = MaskTag,
            IsHitTestVisible = false,
            RenderTransform = new TranslateTransform(offsetX, offsetY)
        };
        canvas.Children.Add(contourPath);

        // Label-Badge positionieren (ueber der BBox); null-sicherer Bbox-Zugriff.
        if (quant != null && mask.Bbox is { Count: >= 4 })
        {
            double bboxX = offsetX + mask.Bbox[0] / imgW * canvasWidth;
            double bboxY = offsetY + mask.Bbox[1] / imgH * canvasHeight;
            RenderMaskLabel(canvas, quant, bboxX, Math.Max(0, bboxY - 40));
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
        var label = VsaCodeResolver.LookupLabel(quant.Label) ?? quant.Label;
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
    /// Delegiert an <see cref="MaskLabelTextBuilder.BuildMeasurementText"/> (Infrastructure).
    /// </summary>
    private static string BuildMeasurementText(MaskQuantificationService.QuantifiedMask q)
        => MaskLabelTextBuilder.BuildMeasurementText(q);

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

}
