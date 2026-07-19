using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Workbench;
using AuswertungPro.Next.UI.Ai.Pipeline;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Pruefplatz-Fenster (Etappe 1): Bild links, Codier-Panel rechts. Das Code-behind erfasst
/// ausschliesslich Geometrie (Box ziehen, Maske zeichnen, Foto-Dialog) und delegiert alle
/// Fach-/Speicherlogik an <see cref="TrainingStudioViewModel"/> bzw. den Workbench-Service.
/// </summary>
public partial class TrainingStudioWindow : Window
{
    private readonly TrainingStudioViewModel _vm;
    private readonly WorkbenchQueueService _queueService;

    private Point _dragStart;
    private bool _dragging;
    private Rectangle? _dragRect;

    /// <summary>Parameterloser Ctor fuer den WPF-/Designer-Rueckfall.</summary>
    public TrainingStudioWindow() : this(services: null) { }

    public TrainingStudioWindow(ServiceProvider? services)
    {
        InitializeComponent();
        WindowStateManager.Track(this);

        var workbench = TrainingStudioWindowDependencyFactory.Create(services);
        _queueService = TrainingStudioWindowDependencyFactory.CreateQueueService(services);
        // Die Review-Warteschlange wird ueber "Warteschlange laden" asynchron geladen (LoadReviewQueue_Click);
        // der synchrone loadQueue-Delegate bleibt leer.
        _vm = new TrainingStudioViewModel(workbench, () => Array.Empty<WorkbenchItem>(), Environment.UserName);
        DataContext = _vm;

        _vm.PropertyChanged += Vm_PropertyChanged;
        OverlayCanvas.SizeChanged += (_, _) => RedrawOverlay();
        Closed += (_, _) => _vm.PropertyChanged -= Vm_PropertyChanged;
    }

    // ── Quellen: Fotos + Review-Warteschlange (Logik im WorkbenchQueueService) ──

    private void LoadPhotos_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Fotos fuer den Pruefplatz waehlen",
            Filter = "Bilder (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png",
            Multiselect = true,
        };
        if (dlg.ShowDialog(this) != true)
            return;

        var items = WorkbenchQueueService.BuildPhotoItems(dlg.FileNames, DateTime.Now, _vm.PipeDiameterMm);
        _vm.LoadItems(items);
    }

    private async void LoadReviewQueue_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var items = await _queueService.LoadReviewQueueAsync();
            _vm.LoadItems(items);
            if (items.Count == 0)
                _vm.StatusText = "Keine offenen Review-Faelle (Yellow/Red, noch nicht beurteilt).";
        }
        catch (Exception ex)
        {
            _vm.StatusText = $"Warteschlange konnte nicht geladen werden: {ex.Message}";
        }
    }

    // ── Box-Zeichnen (reine Geometrie-Erfassung) ─────────────────────────────

    private void OverlayCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_vm.CurrentItem is null)
            return;

        _dragStart = e.GetPosition(OverlayCanvas);
        _dragging = true;
        _dragRect = new Rectangle
        {
            Stroke = Brushes.OrangeRed,
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Color.FromArgb(40, 255, 69, 0)),
        };
        Canvas.SetLeft(_dragRect, _dragStart.X);
        Canvas.SetTop(_dragRect, _dragStart.Y);
        OverlayCanvas.Children.Add(_dragRect);
        OverlayCanvas.CaptureMouse();
    }

    private void OverlayCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging || _dragRect is null)
            return;

        var p = e.GetPosition(OverlayCanvas);
        Canvas.SetLeft(_dragRect, Math.Min(p.X, _dragStart.X));
        Canvas.SetTop(_dragRect, Math.Min(p.Y, _dragStart.Y));
        _dragRect.Width = Math.Abs(p.X - _dragStart.X);
        _dragRect.Height = Math.Abs(p.Y - _dragStart.Y);
    }

    private async void OverlayCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging)
            return;

        _dragging = false;
        OverlayCanvas.ReleaseMouseCapture();
        var end = e.GetPosition(OverlayCanvas);

        if (!TryToNormalizedBox(_dragStart, end, out var box))
        {
            _vm.StatusText = "Box zu klein oder ausserhalb des Bildes — bitte erneut ziehen.";
            RedrawOverlay();
            return;
        }

        await _vm.BoxDrawnCommand.ExecuteAsync(box);
        RedrawOverlay();
    }

    // ── Geometrie-Hilfen ─────────────────────────────────────────────────────

    /// <summary>Rechteck der tatsaechlich angezeigten Bildflaeche (Uniform-Stretch, mit Letterbox-Offset).</summary>
    private Rect GetDisplayedImageRect()
    {
        if (FrameImage.Source is not BitmapSource src || FrameImage.ActualWidth <= 0 || FrameImage.ActualHeight <= 0)
            return new Rect(0, 0, OverlayCanvas.ActualWidth, OverlayCanvas.ActualHeight);

        double cw = FrameImage.ActualWidth, ch = FrameImage.ActualHeight;
        double scale = Math.Min(cw / src.PixelWidth, ch / src.PixelHeight);
        double dw = src.PixelWidth * scale, dh = src.PixelHeight * scale;
        return new Rect((cw - dw) / 2, (ch - dh) / 2, dw, dh);
    }

    private bool TryToNormalizedBox(Point a, Point b, out BoundingBox box)
    {
        box = default;
        var area = GetDisplayedImageRect();
        if (area.Width <= 0 || area.Height <= 0)
            return false;

        double x1 = Math.Clamp((Math.Min(a.X, b.X) - area.X) / area.Width, 0, 1);
        double y1 = Math.Clamp((Math.Min(a.Y, b.Y) - area.Y) / area.Height, 0, 1);
        double x2 = Math.Clamp((Math.Max(a.X, b.X) - area.X) / area.Width, 0, 1);
        double y2 = Math.Clamp((Math.Max(a.Y, b.Y) - area.Y) / area.Height, 0, 1);

        double w = x2 - x1, h = y2 - y1;
        if (w < 0.01 || h < 0.01)
            return false;

        return BoundingBox.TryCreate((x1 + x2) / 2, (y1 + y2) / 2, w, h, out box);
    }

    // ── Overlay-Rendering (Box + Maskenkontur) ───────────────────────────────

    private void Vm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TrainingStudioViewModel.CurrentBox)
            or nameof(TrainingStudioViewModel.Segmentation)
            or nameof(TrainingStudioViewModel.CurrentImagePath))
        {
            RedrawOverlay();
        }
    }

    private void RedrawOverlay()
    {
        OverlayCanvas.Children.Clear();
        var area = GetDisplayedImageRect();
        if (area.Width <= 0 || area.Height <= 0)
            return;

        // Gezogene Box.
        if (_vm.CurrentBox is { } b)
        {
            var rect = new Rectangle
            {
                Stroke = Brushes.OrangeRed,
                StrokeThickness = 2,
                Fill = new SolidColorBrush(Color.FromArgb(30, 255, 69, 0)),
                IsHitTestVisible = false,
                Width = b.Width * area.Width,
                Height = b.Height * area.Height,
            };
            Canvas.SetLeft(rect, area.X + (b.XCenter - b.Width / 2) * area.Width);
            Canvas.SetTop(rect, area.Y + (b.YCenter - b.Height / 2) * area.Height);
            OverlayCanvas.Children.Add(rect);
        }

        // SAM-Maskenkontur (gruene Linie), via bestehendem SamMaskRenderer.
        if (_vm.Segmentation is { MaskRle: { Length: > 0 } rle } seg
            && seg.MaskImageWidth > 0 && seg.MaskImageHeight > 0)
        {
            try
            {
                var mask = SamMaskRenderer.DecodeRle(rle, seg.MaskImageWidth, seg.MaskImageHeight);
                var geom = SamMaskRenderer.ExtractContourGeometry(
                    mask, seg.MaskImageWidth, seg.MaskImageHeight, area.Width, area.Height);
                var path = new Path
                {
                    Data = geom,
                    Stroke = new SolidColorBrush(Color.FromArgb(220, 0, 200, 0)),
                    StrokeThickness = 2,
                    IsHitTestVisible = false,
                    RenderTransform = new TranslateTransform(area.X, area.Y),
                };
                OverlayCanvas.Children.Add(path);
            }
            catch
            {
                // Eine defekte Maske darf die Box-Anzeige nicht verhindern.
            }
        }
    }
}
