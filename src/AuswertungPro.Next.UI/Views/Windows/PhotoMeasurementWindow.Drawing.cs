using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AuswertungPro.Next.UI.Views.Windows;

// PhotoMeasurementWindow Drawing-Helpers: Overlay-Zeichnen, Clear-by-Tag,
// Canvas-Labels, Handles, Arcs, Lateral-/Bend-/Pipe-Overlays. Aus dem
// Hauptdatei extrahiert (Slice 5a). Greift via partial class auf
// OverlayCanvas, _calibration, _activeTool und Brush-Felder zu.
public partial class PhotoMeasurementWindow
{
    private void DrawLateralOverlay(Point center, double pipeR, double posRad, double angleDeg)
    {
        // Roter Kreis an Position auf Rohrwand
        double openingR = pipeR * 0.15;
        double openX = center.X + Math.Cos(posRad) * pipeR;
        double openY = center.Y + Math.Sin(posRad) * pipeR;

        var circle = new Ellipse
        {
            Width = openingR * 2, Height = openingR * 2,
            Stroke = Brushes.Red, StrokeThickness = 2,
            Fill = LateralFillBrush,
            Tag = TagOverlay
        };
        Canvas.SetLeft(circle, openX - openingR);
        Canvas.SetTop(circle, openY - openingR);
        OverlayCanvas.Children.Add(circle);

        // Winkelschenkel (gelb)
        double halfAngle = (angleDeg / 2.0) * Math.PI / 180.0;
        double armLen = pipeR * 0.6;

        var arm1End = new Point(
            openX + Math.Cos(posRad - halfAngle) * armLen,
            openY + Math.Sin(posRad - halfAngle) * armLen);
        var arm2End = new Point(
            openX + Math.Cos(posRad + halfAngle) * armLen,
            openY + Math.Sin(posRad + halfAngle) * armLen);

        OverlayCanvas.Children.Add(new Line
        {
            X1 = openX, Y1 = openY, X2 = arm1End.X, Y2 = arm1End.Y,
            Stroke = Brushes.Yellow, StrokeThickness = 2,
            Tag = TagOverlay
        });
        OverlayCanvas.Children.Add(new Line
        {
            X1 = openX, Y1 = openY, X2 = arm2End.X, Y2 = arm2End.Y,
            Stroke = Brushes.Yellow, StrokeThickness = 2,
            Tag = TagOverlay
        });

        // Winkelbogen
        DrawArc(openX, openY, armLen * 0.4, posRad - halfAngle, posRad + halfAngle,
            Brushes.Yellow, 1.5, TagOverlay);

        // Label
        AddCanvasLabel($"{angleDeg:F0}°",
            openX + Math.Cos(posRad) * (armLen * 0.5),
            openY + Math.Sin(posRad) * (armLen * 0.5) - 14, TagOverlay);
    }

    private void DrawBendOverlay(Point center, double pipeR, double posRad, double angleDeg)
    {
        // Muffenringe auf Bogenbahn
        double halfAngle = (angleDeg / 2.0) * Math.PI / 180.0;
        double bogenR = 3.5 * pipeR;

        // Bogenzentrum
        double arcCenterX = center.X + Math.Cos(posRad + Math.PI / 2) * bogenR;
        double arcCenterY = center.Y + Math.Sin(posRad + Math.PI / 2) * bogenR;

        // Clip am Rohrkreis
        var clipGeo = new EllipseGeometry(center, pipeR, pipeR);
        var bendContainer = new Canvas
        {
            Clip = clipGeo,
            Width = OverlayCanvas.ActualWidth,
            Height = OverlayCanvas.ActualHeight,
            Tag = TagOverlay
        };

        int ringCount = 8;
        for (int i = 0; i < ringCount; i++)
        {
            double t = (double)i / (ringCount - 1); // 0..1
            double ringAngle = posRad + Math.PI / 2 - halfAngle + t * 2 * halfAngle;
            double ringX = arcCenterX - Math.Cos(ringAngle) * bogenR;
            double ringY = arcCenterY - Math.Sin(ringAngle) * bogenR;

            // Perspektive: hintere Ringe kleiner + Ellipsen (gekippt)
            double perspScale = 1.0 - 0.3 * Math.Abs(t - 0.5) * 2;
            double rw = pipeR * 0.9 * perspScale;
            double rh = pipeR * 0.3 * perspScale;

            var ring = new Ellipse
            {
                Width = rw * 2, Height = rh * 2,
                Stroke = new SolidColorBrush(Color.FromArgb(
                    (byte)(180 + 75 * perspScale), 255, 165, 0)),
                StrokeThickness = 1.5,
                Fill = Brushes.Transparent
            };
            Canvas.SetLeft(ring, ringX - rw);
            Canvas.SetTop(ring, ringY - rh);
            bendContainer.Children.Add(ring);
        }

        OverlayCanvas.Children.Add(bendContainer);

        // Bogenbahn-Achslinie (gestrichelt)
        var pathFig = new PathFigure();
        for (int i = 0; i <= 20; i++)
        {
            double t = (double)i / 20.0;
            double a = posRad + Math.PI / 2 - halfAngle + t * 2 * halfAngle;
            double px = arcCenterX - Math.Cos(a) * bogenR;
            double py = arcCenterY - Math.Sin(a) * bogenR;
            if (i == 0) pathFig.StartPoint = new Point(px, py);
            else pathFig.Segments.Add(new LineSegment(new Point(px, py), true));
        }

        var pathGeo = new PathGeometry(new[] { pathFig });
        var axisLine = new System.Windows.Shapes.Path
        {
            Data = pathGeo,
            Stroke = Brushes.Orange,
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 4, 2 },
            Tag = TagOverlay
        };
        OverlayCanvas.Children.Add(axisLine);
    }

    // ═══════════════════════════════════════════════
    // Rohrkreis zeichnen
    // ═══════════════════════════════════════════════

    private void DrawPipeCircle()
    {
        ClearByTag(TagPipeCircle);

        // Rohrkreis nur bei Messwerkzeugen die ihn brauchen
        if (_activeTool is PhotoTool.None or PhotoTool.MarkRect
            or PhotoTool.Calibration or PhotoTool.Ruler or PhotoTool.Connection)
            return;

        // Foto-Assistent (PaTool.BendAngle/Lateral/Deformation) bringt seine eigene
        // 3D-Schablone mit - der statische Pipe-Kreis stoert nur (siehe linker
        // Phantom-Kreis im Bend-Tool). Daher unterdruecken wenn PaTool aktiv.
        if (IsPhotoAssistantActive)
            return;

        var r = GetImageRenderedRect(PhotoImage);
        if (r.Width <= 0 || r.Height <= 0) return;

        double refSize = Math.Min(r.Width, r.Height);
        double normDiam = _calibration.NormalizedDiameter;
        double pipeR = (normDiam / 2.0) * refSize;

        var center = NormToCanvas(_calibration.PipeCenter.X, _calibration.PipeCenter.Y);

        var ellipse = new Ellipse
        {
            Width = pipeR * 2, Height = pipeR * 2,
            Stroke = Brushes.Cyan,
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 6, 3 },
            Fill = Brushes.Transparent,
            Tag = TagPipeCircle
        };
        Canvas.SetLeft(ellipse, center.X - pipeR);
        Canvas.SetTop(ellipse, center.Y - pipeR);
        OverlayCanvas.Children.Add(ellipse);

        // Fadenkreuz
        var hLine = new Line
        {
            X1 = center.X - 6, Y1 = center.Y,
            X2 = center.X + 6, Y2 = center.Y,
            Stroke = Brushes.Cyan, StrokeThickness = 1,
            Tag = TagPipeCircle
        };
        var vLine = new Line
        {
            X1 = center.X, Y1 = center.Y - 6,
            X2 = center.X, Y2 = center.Y + 6,
            Stroke = Brushes.Cyan, StrokeThickness = 1,
            Tag = TagPipeCircle
        };
        OverlayCanvas.Children.Add(hLine);
        OverlayCanvas.Children.Add(vLine);
    }

    // ═══════════════════════════════════════════════
    // Canvas-Helfer
    // ═══════════════════════════════════════════════

    private void ClearOverlay()
    {
        ClearByTag(TagPipeCircle);
        ClearByTag(TagOverlay);
        ClearByTag(TagPreview);
        ClearByTag(TagFill);
    }

    private void ClearByTag(string tag)
    {
        var toRemove = OverlayCanvas.Children.OfType<UIElement>()
            .Where(e => (e is FrameworkElement fe && fe.Tag as string == tag)).ToList();
        foreach (var el in toRemove)
            OverlayCanvas.Children.Remove(el);
    }

    private TextBlock AddCanvasLabel(string text, double x, double y, string tag)
    {
        var tb = new TextBlock
        {
            Text = text,
            Foreground = Brushes.White,
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Tag = tag
        };
        // Hintergrund-Border
        var border = new Border
        {
            Background = LabelBgBrush,
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(4, 1, 4, 1),
            Child = tb,
            Tag = tag
        };
        Canvas.SetLeft(border, x);
        Canvas.SetTop(border, y);
        OverlayCanvas.Children.Add(border);
        return tb;
    }

    private void AddHandle(Point center, Brush stroke, string tag)
    {
        var handle = new System.Windows.Shapes.Rectangle
        {
            Width = 8,
            Height = 8,
            Fill = Brushes.White,
            Stroke = stroke,
            StrokeThickness = 1.5,
            Tag = tag
        };
        Canvas.SetLeft(handle, center.X - 4);
        Canvas.SetTop(handle, center.Y - 4);
        OverlayCanvas.Children.Add(handle);
    }

    private void DrawArc(double cx, double cy, double radius,
        double startRad, double endRad, Brush stroke, double thickness, string tag)
    {
        var pathFig = new PathFigure
        {
            StartPoint = new Point(
                cx + Math.Cos(startRad) * radius,
                cy + Math.Sin(startRad) * radius)
        };

        double sweep = endRad - startRad;
        bool isLargeArc = Math.Abs(sweep) > Math.PI;

        pathFig.Segments.Add(new ArcSegment(
            new Point(
                cx + Math.Cos(endRad) * radius,
                cy + Math.Sin(endRad) * radius),
            new Size(radius, radius),
            0,
            isLargeArc,
            sweep > 0 ? SweepDirection.Clockwise : SweepDirection.Counterclockwise,
            true));

        var pathGeo = new PathGeometry(new[] { pathFig });
        var path = new System.Windows.Shapes.Path
        {
            Data = pathGeo,
            Stroke = stroke,
            StrokeThickness = thickness,
            Tag = tag
        };
        OverlayCanvas.Children.Add(path);
    }
}
