using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PhotoMeasurementWindow
{
    // ═══════════════════════════════════════════════
    // Level-Werkzeuge (Slider-basiert)
    // ═══════════════════════════════════════════════

    private void SliderFill_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_activeTool is not (PhotoTool.LevelWater or PhotoTool.LevelDeposit or PhotoTool.LevelObstacle))
            return;
        UpdateLevelOverlay();
    }

    private void SliderCamera_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_activeTool is not (PhotoTool.LevelWater or PhotoTool.LevelDeposit or PhotoTool.LevelObstacle))
            return;
        UpdateLevelOverlay();
    }

    private void UpdateLevelOverlay()
    {
        if (_activeTool is not (PhotoTool.LevelWater or PhotoTool.LevelDeposit or PhotoTool.LevelObstacle))
            return;

        double fillPercent = SliderFill.Value;
        var geo = _overlayService.BuildLevelGeometryFromSlider(fillPercent, _activeLevelMode);
        if (geo == null) return;

        _currentGeometry = geo;
        ClearByTag(TagFill);
        ClearByTag(TagOverlay);

        var r = GetImageRenderedRect(PhotoImage);
        if (r.Width <= 0 || r.Height <= 0) return;

        var plan = PhotoMeasurementGeometryService.BuildLevelOverlayPlan(
            geo,
            _calibration,
            r.X,
            r.Y,
            r.Width,
            r.Height,
            CameraHeightPercent);
        if (plan is null) return;

        // Fuellfarbe (statisch gefroren, keine Allokation)
        Brush fillBrush = _activeLevelMode switch
        {
            LevelMode.Water => WaterFillBrush,
            LevelMode.Deposit => DepositFillBrush,
            LevelMode.Obstacle => ObstacleFillBrush,
            _ => Brushes.Transparent
        };

        // CombinedGeometry: Fuellung geclippt am Rohrkreis
        var center = new Point(plan.Center.X, plan.Center.Y);
        var pipeEllipse = new EllipseGeometry(center, plan.PipeRadius, plan.PipeRadius);
        var fillRect = new RectangleGeometry(new Rect(
            plan.FillRect.X,
            plan.FillRect.Y,
            plan.FillRect.Width,
            plan.FillRect.Height));

        var combined = new CombinedGeometry(GeometryCombineMode.Intersect, pipeEllipse, fillRect);
        var fillPath = new System.Windows.Shapes.Path
        {
            Data = combined,
            Fill = fillBrush,
            Tag = TagFill
        };
        OverlayCanvas.Children.Add(fillPath);

        var levelLine = new Line
        {
            X1 = plan.LineStart.X, Y1 = plan.LineStart.Y,
            X2 = plan.LineEnd.X, Y2 = plan.LineEnd.Y,
            Stroke = _activeLevelMode switch
            {
                LevelMode.Water => Brushes.RoyalBlue,
                LevelMode.Deposit => Brushes.Chocolate,
                LevelMode.Obstacle => Brushes.Crimson,
                _ => Brushes.White
            },
            StrokeThickness = 2,
            Tag = TagOverlay
        };
        OverlayCanvas.Children.Add(levelLine);

        // Label
        string labelText = $"{fillPercent:F1}%";
        AddCanvasLabel(labelText, plan.LabelPosition.X, plan.LabelPosition.Y, TagOverlay);

        TxtMeasureInfo.Text = $"{fillPercent:F1}%";
        TxtStatus.Text = $"{_activeLevelMode}: {fillPercent:F1}% | Mausrad: Kreis | Drag: Position";
    }

    // ═══════════════════════════════════════════════
    // Deformation (4-Punkt-Klick)
    // ═══════════════════════════════════════════════

    private void AddDeformationPoint(NormalizedPoint point)
    {
        if (_clickPoints.Count >= 4) return;

        _clickPoints.Add(point);
        int idx = _clickPoints.Count;

        // Marker zeichnen
        var canvasPos = NormToCanvas(point.X, point.Y);
        var marker = new Ellipse
        {
            Width = 10, Height = 10,
            Fill = Brushes.Orange,
            Stroke = Brushes.White, StrokeThickness = 1,
            Tag = TagOverlay
        };
        Canvas.SetLeft(marker, canvasPos.X - 5);
        Canvas.SetTop(marker, canvasPos.Y - 5);
        OverlayCanvas.Children.Add(marker);
        _clickMarkers.Add(marker);

        // Nummer-Label
        AddCanvasLabel($"{idx}", canvasPos.X + 8, canvasPos.Y - 14, TagOverlay);
        // Label ist das letzte Child
        var label = OverlayCanvas.Children[^1];

        // Undo-Frame: Marker + Label zusammen
        _undoFrames.Push(new List<UIElement> { marker, (UIElement)label });

        TxtStatus.Text = $"Deformation: Punkt {idx}/4 gesetzt";

        if (_clickPoints.Count == 4)
            FinalizeDeformation();
    }

    private void FinalizeDeformation()
    {
        var r = GetImageRenderedRect(PhotoImage);
        var deformation = PhotoMeasurementGeometryService.BuildDeformationPlan(
            _clickPoints,
            _calibration,
            _imageAspect,
            r.X,
            r.Y,
            r.Width,
            r.Height);
        if (deformation is null) return;

        _currentGeometry = deformation.Geometry;

        var vLine = new Line
        {
            X1 = deformation.Top.X, Y1 = deformation.Top.Y,
            X2 = deformation.Bottom.X, Y2 = deformation.Bottom.Y,
            Stroke = Brushes.Orange, StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 4, 2 },
            Tag = TagOverlay
        };
        var hLine = new Line
        {
            X1 = deformation.Left.X, Y1 = deformation.Left.Y,
            X2 = deformation.Right.X, Y2 = deformation.Right.Y,
            Stroke = Brushes.Orange, StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 4, 2 },
            Tag = TagOverlay
        };
        OverlayCanvas.Children.Add(vLine);
        OverlayCanvas.Children.Add(hLine);

        AddCanvasLabel(
            $"Deform: {deformation.DeformationPercent:F1}%",
            deformation.LabelPosition.X,
            deformation.LabelPosition.Y,
            TagOverlay);

        TxtMeasureInfo.Text = $"Deform: {deformation.DeformationPercent:F1}%";
        TxtStatus.Text =
            $"Deformation: {deformation.DeformationPercent:F1}% " +
            $"(V={deformation.VerticalDistance:F3}, H={deformation.HorizontalDistance:F3})";
    }

    // ═══════════════════════════════════════════════
    // Querschnittsverminderung (Freihand-Polygon)
    // ═══════════════════════════════════════════════

    private void AddPolygonPoint(NormalizedPoint point)
    {
        _clickPoints.Add(point);

        var canvasPos = NormToCanvas(point.X, point.Y);
        var marker = new Ellipse
        {
            Width = 8, Height = 8,
            Fill = Brushes.MediumPurple,
            Stroke = Brushes.White, StrokeThickness = 1,
            Tag = TagOverlay
        };
        Canvas.SetLeft(marker, canvasPos.X - 4);
        Canvas.SetTop(marker, canvasPos.Y - 4);
        OverlayCanvas.Children.Add(marker);
        _clickMarkers.Add(marker);

        // Undo-Frame: Marker + optional Verbindungslinie
        var frame = new List<UIElement> { marker };

        // Verbindungslinie zum vorherigen Punkt
        if (_clickPoints.Count >= 2)
        {
            var prev = _clickPoints[^2];
            var prevCanvas = NormToCanvas(prev.X, prev.Y);
            var line = new Line
            {
                X1 = prevCanvas.X, Y1 = prevCanvas.Y,
                X2 = canvasPos.X, Y2 = canvasPos.Y,
                Stroke = Brushes.MediumPurple, StrokeThickness = 1.5,
                Tag = TagOverlay
            };
            OverlayCanvas.Children.Add(line);
            frame.Add(line);
        }

        _undoFrames.Push(frame);

        TxtStatus.Text = $"Querschnitt: {_clickPoints.Count} Punkte | Doppelklick = schließen";
    }

    private void ClosePolygon()
    {
        if (_clickPoints.Count < 3) return;
        _polygonClosed = true;

        var r = GetImageRenderedRect(PhotoImage);
        var crossSection = PhotoMeasurementGeometryService.BuildCrossSectionGeometry(
            _clickPoints,
            r.Width,
            r.Height,
            _calibration.NormalizedDiameter);
        if (crossSection is null) return;

        _currentGeometry = crossSection.Geometry;
        double reductionPct = crossSection.ReductionPercent;

        // Polygon zeichnen
        ClearByTag(TagOverlay);
        var polygon = new Polygon
        {
            Fill = PolygonFillBrush,
            Stroke = Brushes.MediumPurple,
            StrokeThickness = 2,
            Tag = TagOverlay
        };
        foreach (var pt in _clickPoints)
        {
            var cp = NormToCanvas(pt.X, pt.Y);
            polygon.Points.Add(cp);
        }
        OverlayCanvas.Children.Add(polygon);

        // Schwerpunkt fuer Label
        var labelPos = NormToCanvas(crossSection.LabelPoint.X, crossSection.LabelPoint.Y);
        AddCanvasLabel($"Quersch: {reductionPct:F1}%", labelPos.X, labelPos.Y - 12, TagOverlay);

        TxtMeasureInfo.Text = $"Quersch: {reductionPct:F1}%";
        TxtStatus.Text = $"Querschnittsverminderung: {reductionPct:F1}%";
    }

    // ═══════════════════════════════════════════════
    // Abzweig / Bogen (Slider-basiert)
    // ═══════════════════════════════════════════════

    private void SliderPosition_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_activeTool is not (PhotoTool.Lateral or PhotoTool.Bend)) return;
        TxtPosition.Text = $"{SliderPosition.Value:F0}°";
        UpdateAngleOverlay();
    }

    private void SliderAngle_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_activeTool is not (PhotoTool.Lateral or PhotoTool.Bend)) return;
        TxtAngle.Text = $"{SliderAngle.Value:F0}°";
        UpdateAngleOverlay();
    }

    private void PositionPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag && double.TryParse(tag, out double deg))
            SliderPosition.Value = deg;
    }

    private void AnglePreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag && double.TryParse(tag, out double deg))
            SliderAngle.Value = deg;
    }

    private void UpdateAngleOverlay()
    {
        ClearByTag(TagOverlay);
        ClearByTag(TagFill);

        var r = GetImageRenderedRect(PhotoImage);
        double positionDeg = SliderPosition.Value;
        double angleDeg = SliderAngle.Value;

        var toolType = _activeTool == PhotoTool.Lateral
            ? OverlayToolType.LateralCircle
            : OverlayToolType.PipeBend;
        var anglePlan = PhotoMeasurementGeometryService.BuildAngleOverlayPlan(
            toolType,
            _calibration,
            positionDeg,
            angleDeg,
            r.X,
            r.Y,
            r.Width,
            r.Height);
        if (anglePlan is null) return;

        if (anglePlan.Lateral is not null)
        {
            DrawLateralOverlay(anglePlan.Lateral, angleDeg);
        }
        else if (anglePlan.Bend is not null)
        {
            DrawBendOverlay(anglePlan.Center, anglePlan.PipeRadius, anglePlan.Bend);
        }

        _currentGeometry = anglePlan.Geometry;

        TxtMeasureInfo.Text = $"{angleDeg:F0}° @ {anglePlan.ClockHour:F1}h";
        TxtStatus.Text = _activeTool == PhotoTool.Lateral
            ? $"Abzweig: {angleDeg:F0}° bei {anglePlan.ClockHour:F1} Uhr"
            : $"Bogen: {angleDeg:F0}° bei {anglePlan.ClockHour:F1} Uhr";
    }

    private void DrawLateralOverlay(PhotoMeasurementLateralOverlayPlan plan, double angleDeg)
    {
        var circle = new Ellipse
        {
            Width = plan.OpeningRadius * 2, Height = plan.OpeningRadius * 2,
            Stroke = Brushes.Red, StrokeThickness = 2,
            Fill = LateralFillBrush,
            Tag = TagOverlay
        };
        Canvas.SetLeft(circle, plan.OpeningCenter.X - plan.OpeningRadius);
        Canvas.SetTop(circle, plan.OpeningCenter.Y - plan.OpeningRadius);
        OverlayCanvas.Children.Add(circle);

        OverlayCanvas.Children.Add(new Line
        {
            X1 = plan.OpeningCenter.X, Y1 = plan.OpeningCenter.Y, X2 = plan.Arm1End.X, Y2 = plan.Arm1End.Y,
            Stroke = Brushes.Yellow, StrokeThickness = 2,
            Tag = TagOverlay
        });
        OverlayCanvas.Children.Add(new Line
        {
            X1 = plan.OpeningCenter.X, Y1 = plan.OpeningCenter.Y, X2 = plan.Arm2End.X, Y2 = plan.Arm2End.Y,
            Stroke = Brushes.Yellow, StrokeThickness = 2,
            Tag = TagOverlay
        });

        // Winkelbogen
        DrawArc(plan.OpeningCenter.X, plan.OpeningCenter.Y, plan.ArcRadius, plan.ArcStartRad, plan.ArcEndRad,
            Brushes.Yellow, 1.5, TagOverlay);

        // Label
        AddCanvasLabel($"{angleDeg:F0}°", plan.LabelPosition.X, plan.LabelPosition.Y, TagOverlay);
    }

    private void DrawBendOverlay(
        PhotoMeasurementCanvasPoint center,
        double pipeR,
        PhotoMeasurementBendOverlayPlan plan)
    {
        // Clip am Rohrkreis
        var clipCenter = new Point(center.X, center.Y);
        var clipGeo = new EllipseGeometry(clipCenter, pipeR, pipeR);
        var bendContainer = new Canvas
        {
            Clip = clipGeo,
            Width = OverlayCanvas.ActualWidth,
            Height = OverlayCanvas.ActualHeight,
            Tag = TagOverlay
        };

        foreach (var ringPlan in plan.Rings)
        {
            var ring = new Ellipse
            {
                Width = ringPlan.RadiusX * 2, Height = ringPlan.RadiusY * 2,
                Stroke = new SolidColorBrush(Color.FromArgb(
                    (byte)(180 + 75 * ringPlan.PerspectiveScale), 255, 165, 0)),
                StrokeThickness = 1.5,
                Fill = Brushes.Transparent
            };
            Canvas.SetLeft(ring, ringPlan.Center.X - ringPlan.RadiusX);
            Canvas.SetTop(ring, ringPlan.Center.Y - ringPlan.RadiusY);
            bendContainer.Children.Add(ring);
        }

        OverlayCanvas.Children.Add(bendContainer);

        // Bogenbahn-Achslinie (gestrichelt)
        var pathFig = new PathFigure();
        for (int i = 0; i < plan.AxisPoints.Count; i++)
        {
            var point = plan.AxisPoints[i];
            if (i == 0) pathFig.StartPoint = new Point(point.X, point.Y);
            else pathFig.Segments.Add(new LineSegment(new Point(point.X, point.Y), true));
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

        var r = GetImageRenderedRect(PhotoImage);
        if (r.Width <= 0 || r.Height <= 0) return;

        var plan = PhotoMeasurementGeometryService.BuildPipeCirclePlan(
            _calibration,
            r.X,
            r.Y,
            r.Width,
            r.Height);
        if (plan is null) return;

        var ellipse = new Ellipse
        {
            Width = plan.Radius * 2, Height = plan.Radius * 2,
            Stroke = Brushes.Cyan,
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 6, 3 },
            Fill = Brushes.Transparent,
            Tag = TagPipeCircle
        };
        Canvas.SetLeft(ellipse, plan.Center.X - plan.Radius);
        Canvas.SetTop(ellipse, plan.Center.Y - plan.Radius);
        OverlayCanvas.Children.Add(ellipse);

        // Fadenkreuz
        var hLine = new Line
        {
            X1 = plan.HorizontalStart.X, Y1 = plan.HorizontalStart.Y,
            X2 = plan.HorizontalEnd.X, Y2 = plan.HorizontalEnd.Y,
            Stroke = Brushes.Cyan, StrokeThickness = 1,
            Tag = TagPipeCircle
        };
        var vLine = new Line
        {
            X1 = plan.VerticalStart.X, Y1 = plan.VerticalStart.Y,
            X2 = plan.VerticalEnd.X, Y2 = plan.VerticalEnd.Y,
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

    private void DrawArc(double cx, double cy, double radius,
        double startRad, double endRad, Brush stroke, double thickness, string tag)
    {
        var plan = PhotoMeasurementGeometryService.BuildArcPlan(cx, cy, radius, startRad, endRad);
        var pathFig = new PathFigure
        {
            StartPoint = new Point(plan.Start.X, plan.Start.Y)
        };

        pathFig.Segments.Add(new ArcSegment(
            new Point(plan.End.X, plan.End.Y),
            new Size(plan.Radius, plan.Radius),
            0,
            plan.IsLargeArc,
            plan.IsClockwise ? SweepDirection.Clockwise : SweepDirection.Counterclockwise,
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

    // ═══════════════════════════════════════════════
    // Overlay ins Foto einbrennen (DPI-korrekt)
    // ═══════════════════════════════════════════════

    private string? BurnOverlayToPhoto()
    {
        if (PhotoImage.Source is not BitmapSource bmpSrc) return null;

        var r = GetImageRenderedRect(PhotoImage);
        if (r.Width <= 0 || r.Height <= 0) return null;

        // In ORIGINALAUFLOESUNG rendern (nicht Display-Groesse)
        int outW = bmpSrc.PixelWidth;
        int outH = bmpSrc.PixelHeight;
        if (outW <= 0 || outH <= 0) return null; // Bild hat keine gueltige Groesse

        var rtb = new RenderTargetBitmap(outW, outH, 96, 96, PixelFormats.Pbgra32);

        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            // 1. Original-Foto in voller Aufloesung
            dc.DrawImage(bmpSrc, new Rect(0, 0, outW, outH));

            // 2. Canvas-Overlay hochskalieren: Display-Bereich → Originalaufloesung
            double scaleX = outW / r.Width;
            double scaleY = outH / r.Height;

            // Nur den gerenderten Bildbereich des Canvas nehmen (Letterbox-Offset abziehen)
            var vb = new VisualBrush(OverlayCanvas)
            {
                Viewbox = new Rect(r.X, r.Y, r.Width, r.Height),
                ViewboxUnits = BrushMappingMode.Absolute,
                Stretch = Stretch.Fill
            };
            dc.DrawRectangle(vb, null, new Rect(0, 0, outW, outH));
        }
        rtb.Render(dv);

        // PNG speichern
        var outPath = System.IO.Path.ChangeExtension(_photoPath, null) + "_overlay.png";
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        using var fs = File.Create(outPath);
        enc.Save(fs);
        return outPath;
    }
}
