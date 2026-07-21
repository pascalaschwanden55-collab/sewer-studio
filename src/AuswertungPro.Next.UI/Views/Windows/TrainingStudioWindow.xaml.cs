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
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Protocol;            // ProtocolEntry (Codierfenster-Ergebnis)
using AuswertungPro.Next.UI.Ai.Pipeline;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Windows;      // VsaCodeExplorerViewModel

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
    private readonly ServiceProvider? _services;

    private Point _dragStart;
    private bool _dragging;
    private Rectangle? _dragRect;

    /// <summary>Parameterloser Ctor fuer den WPF-/Designer-Rueckfall.</summary>
    public TrainingStudioWindow() : this(services: null) { }

    public TrainingStudioWindow(ServiceProvider? services)
    {
        InitializeComponent();
        WindowStateManager.Track(this);

        _services = services;   // fuer das VSA-Codierfenster (CodeSelectionCatalog)
        var dependencies = TrainingStudioWindowDependencyFactory.CreateDependencies(services);
        _queueService = dependencies.QueueService;
        // Die Review-Warteschlange wird ueber "Warteschlange laden" asynchron geladen (LoadReviewQueue_Click);
        // der synchrone loadQueue-Delegate bleibt leer.
        _vm = new TrainingStudioViewModel(
            dependencies.Workbench,
            () => Array.Empty<WorkbenchItem>(),
            Environment.UserName,
            ensureAiReady: dependencies.EnsureAiReady);
        DataContext = _vm;

        _vm.PropertyChanged += Vm_PropertyChanged;
        OverlayCanvas.SizeChanged += (_, _) => RedrawOverlay();
        Loaded += TrainingStudioWindow_Loaded;
        Closed += (_, _) => _vm.PropertyChanged -= Vm_PropertyChanged;
    }

    private async void TrainingStudioWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= TrainingStudioWindow_Loaded;
        await _vm.StartAiCommand.ExecuteAsync(null);
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
            _vm.StatusText = "Warteschlange konnte nicht geladen werden: "
                + UserError.DescribeAndReport(ex, "Training-Studio Warteschlange laden");
        }
    }

    // ── VSA-Codierfenster (dasselbe wie im Codiermodus) ──────────────────────

    private void OpenCodeExplorer_Click(object sender, RoutedEventArgs e)
    {
        var catalog = _services?.CodeSelectionCatalog;
        if (catalog is null)
        {
            _vm.StatusText = "VSA-Katalog nicht verfuegbar (kein Codier-Kontext).";
            return;
        }

        // Bereits gewaehlten Code als Ausgangswert vorbelegen.
        var seed = string.IsNullOrWhiteSpace(_vm.SelectedCode)
            ? null
            : new ProtocolEntry { Code = _vm.SelectedCode! };

        var explorerVm = new VsaCodeExplorerViewModel(
            existingEntry: seed, presetMeter: null, presetZeit: null, catalog: catalog);

        var dialog = VsaCodeExplorerDialogServiceFactory.Create();
        var result = dialog.Show(
            explorerVm, videoPath: null, currentVideoTime: null, owner: this, liveSnapshotProvider: null);

        if (!result.Accepted || result.SelectedEntry is null)
            return;

        var selection = WorkbenchCodeSelectionMapper.FromProtocolEntry(result.SelectedEntry);
        _vm.ApplyCodeSelection(selection.Code, selection.ClockPosition, selection.Severity);
    }

    // ── Box-Zeichnen (reine Geometrie-Erfassung) ─────────────────────────────

    private void OverlayCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_vm.CurrentItem is null)
            return;

        var imageArea = GetDisplayedImageRect();
        var mousePosition = e.GetPosition(OverlayCanvas);
        if (imageArea.IsEmpty || !imageArea.Contains(mousePosition))
        {
            _vm.StatusText = "Bitte die Box innerhalb des sichtbaren Bildes beginnen.";
            return;
        }

        _dragStart = mousePosition;
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

        var p = TrainingStudioImageGeometryMapper.ClampToImage(
            GetDisplayedImageRect(),
            e.GetPosition(OverlayCanvas));
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
            return Rect.Empty;

        var imageOrigin = FrameImage.TranslatePoint(new Point(0, 0), OverlayCanvas);
        return TrainingStudioImageGeometryMapper.GetDisplayedImageRect(
            FrameImage.RenderSize,
            new Size(src.Width, src.Height),
            imageOrigin);
    }

    private bool TryToNormalizedBox(Point a, Point b, out BoundingBox box)
    {
        return TrainingStudioImageGeometryMapper.TryCreateNormalizedBox(
            GetDisplayedImageRect(),
            a,
            b,
            out box);
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

        // SAM-Maske zuerst zeichnen, damit die rote Auswahl immer oben sichtbar bleibt.
        if (_vm.Segmentation is not null)
        {
            var result = TrainingStudioMaskOverlayRenderer.Render(
                OverlayCanvas,
                _vm.Segmentation,
                area);
            if (!result.Rendered && !string.IsNullOrWhiteSpace(result.ErrorMessage))
                _vm.StatusText = result.ErrorMessage;
        }

        // Gezogene Box immer als oberste Ebene.
        if (_vm.CurrentBox is { } b)
        {
            var bounds = TrainingStudioImageGeometryMapper.ToCanvasRect(area, b);
            var rect = new Rectangle
            {
                Stroke = Brushes.OrangeRed,
                StrokeThickness = 2,
                Fill = new SolidColorBrush(Color.FromArgb(30, 255, 69, 0)),
                IsHitTestVisible = false,
                Width = bounds.Width,
                Height = bounds.Height,
            };
            Canvas.SetLeft(rect, bounds.X);
            Canvas.SetTop(rect, bounds.Y);
            OverlayCanvas.Children.Add(rect);
        }
    }
}
