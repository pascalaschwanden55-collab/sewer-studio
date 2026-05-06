// AuswertungPro – Video-Selbsttraining Phase 2 — Review-Window
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.ViewModels.Windows;
using AuswertungPro.Next.Application.Ai.Teacher;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class VideoTrainingReviewWindow : Window
{
    // Zustand fuer Rechteck-Zeichnung
    private Point? _drawStart;
    private Rectangle? _previewRect;

    public VideoTrainingReviewWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        FrameImage.SizeChanged += (_, _) => UpdateCanvasViewport();
        Loaded += (_, _) => UpdateCanvasViewport();
    }

    public VideoTrainingReviewWindow(VideoTrainingReviewViewModel vm) : this()
    {
        DataContext = vm;
    }

    /// <summary>Ordner-Auswahl Dialog.</summary>
    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Haltungsordner waehlen (mit Video + PDF)"
        };

        if (dlg.ShowDialog() == true && DataContext is VideoTrainingReviewViewModel vm)
        {
            vm.SetFolder(dlg.FolderName);
        }
    }

    /// <summary>Tastaturkuerzel fuer schnelles Review.</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (DataContext is not VideoTrainingReviewViewModel vm) return;

        switch (e.Key)
        {
            case Key.K:
                vm.ApproveKiCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.P:
                vm.ApproveProtocolCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.I:
                vm.IgnoreEntryCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.M:
                vm.IsAnnotating = !vm.IsAnnotating;
                e.Handled = true;
                break;
            case Key.Escape when vm.IsAnnotating:
                vm.IsAnnotating = false;
                CancelDraw();
                e.Handled = true;
                break;
        }
    }

    // --- ViewModel-Wechsel: SelectedEntry beobachten ---

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is VideoTrainingReviewViewModel oldVm)
            oldVm.PropertyChanged -= OnViewModelPropertyChanged;

        if (e.NewValue is VideoTrainingReviewViewModel newVm)
            newVm.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(VideoTrainingReviewViewModel.SelectedEntry))
        {
            // Bei Zeilenwechsel: Alle Overlays neu zeichnen
            ClearDisplayCanvas();
            if (DataContext is VideoTrainingReviewViewModel vm && vm.SelectedEntry is { } entry)
            {
                // 1. KI-Detection-Box rendern (wenn BBox vorhanden)
                if (entry.HasKiBbox)
                    RenderKiDetectionBox(entry);

                // 2. Bestehende Annotation rendern (wenn vorhanden)
                if (entry.AnnotationBbox is { } bbox)
                    RenderAnnotationRect(bbox);
            }
        }
    }

    // --- Maus-Handler fuer Rechteck-Zeichnung ---

    private void AnnotationCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not VideoTrainingReviewViewModel { SelectedEntry: not null })
            return;

        _drawStart = e.GetPosition(AnnotationCanvas);
        AnnotationCanvas.CaptureMouse();

        // Vorschau-Rechteck erstellen
        _previewRect = new Rectangle
        {
            Stroke = Brushes.Cyan,
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection([4, 2]),
            Fill = new SolidColorBrush(Color.FromArgb(30, 0, 255, 255))
        };
        Canvas.SetLeft(_previewRect, _drawStart.Value.X);
        Canvas.SetTop(_previewRect, _drawStart.Value.Y);
        AnnotationCanvas.Children.Add(_previewRect);
    }

    private void AnnotationCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_drawStart is null || _previewRect is null) return;

        var pos = e.GetPosition(AnnotationCanvas);
        var x = Math.Min(_drawStart.Value.X, pos.X);
        var y = Math.Min(_drawStart.Value.Y, pos.Y);
        var w = Math.Abs(pos.X - _drawStart.Value.X);
        var h = Math.Abs(pos.Y - _drawStart.Value.Y);

        Canvas.SetLeft(_previewRect, x);
        Canvas.SetTop(_previewRect, y);
        _previewRect.Width = w;
        _previewRect.Height = h;
    }

    private void AnnotationCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        AnnotationCanvas.ReleaseMouseCapture();

        if (_drawStart is null || _previewRect is null)
            return;

        var end = e.GetPosition(AnnotationCanvas);
        var canvasW = AnnotationCanvas.ActualWidth;
        var canvasH = AnnotationCanvas.ActualHeight;

        // Vorschau-Rechteck entfernen
        AnnotationCanvas.Children.Remove(_previewRect);
        _previewRect = null;

        if (canvasW <= 0 || canvasH <= 0)
        {
            _drawStart = null;
            return;
        }

        // Normalisierte Koordinaten berechnen
        var x1 = Math.Clamp(_drawStart.Value.X / canvasW, 0, 1);
        var y1 = Math.Clamp(_drawStart.Value.Y / canvasH, 0, 1);
        var x2 = Math.Clamp(end.X / canvasW, 0, 1);
        var y2 = Math.Clamp(end.Y / canvasH, 0, 1);

        var left = Math.Min(x1, x2);
        var top = Math.Min(y1, y2);
        var width = Math.Abs(x2 - x1);
        var height = Math.Abs(y2 - y1);

        _drawStart = null;

        // Zu kleines Rechteck ignorieren (Fehlklick)
        if (width < 0.01 || height < 0.01)
            return;

        var bbox = new NormalizedBoundingBox
        {
            XCenter = left + width / 2,
            YCenter = top + height / 2,
            Width = width,
            Height = height
        };

        // Auf ViewModel setzen und sofort als TeacherAnnotation speichern
        if (DataContext is VideoTrainingReviewViewModel vm && vm.SelectedEntry is { } entry)
        {
            entry.AnnotationBbox = bbox;
            ClearDisplayCanvas();
            if (entry.HasKiBbox)
                RenderKiDetectionBox(entry);
            RenderAnnotationRect(bbox);

            // Sofort speichern mit dem Protokoll-Code (asynchron im Hintergrund)
            _ = SaveAnnotationImmediatelyAsync(vm, entry, bbox);
        }
    }

    /// <summary>
    /// Speichert die Markierung sofort als TeacherAnnotation + YOLO-Label.
    /// Der Protokoll-Code wird automatisch uebernommen.
    /// </summary>
    private async Task SaveAnnotationImmediatelyAsync(
        VideoTrainingReviewViewModel vm,
        DifferenceEntryViewModel entry,
        NormalizedBoundingBox bbox)
    {
        var vsaCode = entry.ProtocolCode ?? entry.KiCode ?? "";
        if (string.IsNullOrWhiteSpace(vsaCode) || entry.FramePath is null) return;

        try
        {
            var annotation = new AuswertungPro.Next.Application.Ai.Teacher.TeacherAnnotation
            {
                VsaCode = vsaCode,
                Beschreibung = entry.Explanation ?? vsaCode,
                Severity = entry.KiSeverity > 0 ? entry.KiSeverity : 3,
                MeterPosition = entry.ProtocolMeter ?? entry.KiMeter ?? 0,
                HaltungName = vm.HaltungId,
                VideoPath = vm.VideoPath,
                ToolType = Domain.Models.OverlayToolType.Rectangle,
                BoundingBox = bbox,
                FullFramePath = entry.FramePath
            };

            // YOLO-Export
            var exportService = new Ai.Teacher.TrainingAnnotationExportService();
            var classId = Ai.Teacher.VsaYoloClassMap.GetClassId(vsaCode);
            var baseName = $"review_{annotation.AnnotationId}";
            var exportResult = await exportService.ExportAsync(
                entry.FramePath, bbox, vsaCode, classId, baseName);

            if (exportResult.Success)
            {
                annotation.FullFramePath = exportResult.FullFramePath;
                annotation.CroppedRegionPath = exportResult.CroppedRegionPath;
                annotation.YoloAnnotationPath = exportResult.YoloAnnotationPath;
            }

            await Ai.Teacher.TeacherAnnotationStore.AppendAsync(annotation);

            // Entscheidung automatisch auf "Protokoll korrekt" setzen
            if (entry.Decision == Ai.Training.Models.ReviewDecision.Pending)
                entry.Decision = Ai.Training.Models.ReviewDecision.ProtocolCorrect;

            vm.StatusText = $"Markierung {vsaCode} gespeichert → teacher_annotations.json";
        }
        catch (Exception ex)
        {
            vm.StatusText = $"Speichern fehlgeschlagen: {ex.Message}";
        }
    }

    // --- Anzeige-Hilfsmethoden ---

    /// <summary>Zeichnet ein Cyan-gestricheltes Rechteck auf dem DisplayCanvas.</summary>
    private void RenderAnnotationRect(NormalizedBoundingBox bbox)
    {
        var w = DisplayCanvas.ActualWidth;
        var h = DisplayCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        var left = (bbox.XCenter - bbox.Width / 2) * w;
        var top = (bbox.YCenter - bbox.Height / 2) * h;
        var rectW = bbox.Width * w;
        var rectH = bbox.Height * h;

        var rect = new Rectangle
        {
            Stroke = Brushes.Cyan,
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection([6, 3]),
            Fill = new SolidColorBrush(Color.FromArgb(25, 0, 255, 255)),
            Width = rectW,
            Height = rectH
        };

        Canvas.SetLeft(rect, left);
        Canvas.SetTop(rect, top);
        DisplayCanvas.Children.Add(rect);
    }

    /// <summary>Zeichnet die KI-Detection-BoundingBox mit Severity-Farbe und Label.</summary>
    private void RenderKiDetectionBox(DifferenceEntryViewModel entry)
    {
        var w = DisplayCanvas.ActualWidth;
        var h = DisplayCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        var x1 = entry.KiBboxX1!.Value * w;
        var y1 = entry.KiBboxY1!.Value * h;
        var x2 = entry.KiBboxX2!.Value * w;
        var y2 = entry.KiBboxY2!.Value * h;
        var rectW = x2 - x1;
        var rectH = y2 - y1;
        if (rectW <= 0 || rectH <= 0) return;

        var color = MapSeverityColor(entry.KiSeverity);

        // BoundingBox-Rechteck
        var rect = new Rectangle
        {
            Width = rectW,
            Height = rectH,
            Stroke = new SolidColorBrush(Color.FromArgb(220, color.R, color.G, color.B)),
            StrokeThickness = 2.5,
            Fill = new SolidColorBrush(Color.FromArgb(35, color.R, color.G, color.B)),
            RadiusX = 4,
            RadiusY = 4,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(rect, x1);
        Canvas.SetTop(rect, y1);
        DisplayCanvas.Children.Add(rect);

        // Label-Badge
        var label = entry.KiCode ?? "?";
        var conf = entry.KiConfidence;
        var labelText = conf.HasValue ? $"{label} ({conf:P0})" : label;

        var badge = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(200, color.R, color.G, color.B)),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(5, 2, 5, 2),
            IsHitTestVisible = false,
            Child = new TextBlock
            {
                Text = labelText,
                Foreground = Brushes.White,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold
            }
        };

        // Badge oberhalb der Box positionieren
        Canvas.SetLeft(badge, x1);
        Canvas.SetTop(badge, Math.Max(0, y1 - 22));
        DisplayCanvas.Children.Add(badge);
    }

    /// <summary>Severity (1-5) zu Farbe — gleiche Skala wie PlayerWindow.</summary>
    private static Color MapSeverityColor(int severity) => Math.Clamp(severity, 1, 5) switch
    {
        >= 5 => Color.FromRgb(239, 68, 68),    // Rot
        4 => Color.FromRgb(249, 115, 22),       // Orange
        3 => Color.FromRgb(245, 158, 11),       // Amber
        2 => Color.FromRgb(132, 204, 22),       // Hellgruen
        _ => Color.FromRgb(34, 197, 94)          // Gruen
    };

    private void ClearDisplayCanvas()
    {
        DisplayCanvas.Children.Clear();
    }

    /// <summary>
    /// Passt Canvas-Groesse an die tatsaechlich gerenderte Bildflaeche an.
    /// Bei Stretch=Uniform belegt das Bild nicht die gesamte Grid-Zelle —
    /// ohne diesen Fix waeren die Overlays oval statt rund (Seitenverhaeltnis-Verzerrung).
    /// </summary>
    private void UpdateCanvasViewport()
    {
        if (FrameImage.Source is not System.Windows.Media.Imaging.BitmapSource bmp)
            return;

        double containerW = FrameImage.ActualWidth;
        double containerH = FrameImage.ActualHeight;
        if (containerW <= 0 || containerH <= 0) return;

        double imgAspect = (double)bmp.PixelWidth / bmp.PixelHeight;
        double containerAspect = containerW / containerH;

        double renderW, renderH;
        if (imgAspect > containerAspect)
        {
            // Bild ist breiter → volle Breite, Hoehe angepasst
            renderW = containerW;
            renderH = containerW / imgAspect;
        }
        else
        {
            // Bild ist hoeher → volle Hoehe, Breite angepasst
            renderH = containerH;
            renderW = containerH * imgAspect;
        }

        // Canvas auf gerenderte Bildgroesse setzen und zentrieren
        DisplayCanvas.Width = renderW;
        DisplayCanvas.Height = renderH;
        AnnotationCanvas.Width = renderW;
        AnnotationCanvas.Height = renderH;

        // Overlays neu zeichnen bei Groessenaenderung
        ClearDisplayCanvas();
        if (DataContext is VideoTrainingReviewViewModel vm && vm.SelectedEntry is { } entry)
        {
            if (entry.HasKiBbox)
                RenderKiDetectionBox(entry);
            if (entry.AnnotationBbox is { } bbox)
                RenderAnnotationRect(bbox);
        }
    }

    private void CancelDraw()
    {
        if (_previewRect is not null)
        {
            AnnotationCanvas.Children.Remove(_previewRect);
            _previewRect = null;
        }
        _drawStart = null;
        AnnotationCanvas.ReleaseMouseCapture();
    }
}
