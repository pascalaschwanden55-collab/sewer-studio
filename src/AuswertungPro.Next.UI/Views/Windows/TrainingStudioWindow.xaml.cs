using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Workbench;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.UseCases.PdfTrainingReview;
using AuswertungPro.Next.Application.UseCases.TrainingStudioSegmentation;
using AuswertungPro.Next.Domain.Protocol;            // ProtocolEntry (Codierfenster-Ergebnis)
using AuswertungPro.Next.UI.Ai.Pipeline;
using AuswertungPro.Next.UI.Helpers;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.BendSuggestions;  // BendSuggestionListViewModel
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
    private readonly BendSuggestionListViewModel _bendVm;
    private readonly WorkbenchQueueService _queueService;
    private readonly ITrainingPdfReviewImportService _pdfReviewImport;
    private readonly ITrainingPdfReviewBatchImportUseCase _pdfReviewBatchImport;
    private readonly IPersonalGoldAlbumService _goldAlbumService;
    private readonly IPersonalGoldInboxService _goldInboxService;
    private readonly IFolderOpenService? _folderOpen;
    private readonly ServiceProvider? _services;

    private Point _dragStart;
    private bool _dragging;
    private Rectangle? _dragRect;
    private bool _pdfImportInProgress;
    private bool _syncingThumbnailSelection;
    private CancellationTokenSource? _pdfImportCts;
    private bool _isClosed;

    /// <summary>Parameterloser Ctor fuer den WPF-/Designer-Rueckfall.</summary>
    public TrainingStudioWindow() : this(services: null) { }

    public TrainingStudioWindow(ServiceProvider? services)
    {
        InitializeComponent();
        WindowStateManager.Track(this);

        _services = services;   // fuer das VSA-Codierfenster (CodeSelectionCatalog)
        // Der Bogen-Vorschlags-Workflow meldet von Threadpool-Threads — alles Gebundene
        // laeuft ueber den Dispatcher zurueck auf den UI-Thread.
        var dependencies = TrainingStudioWindowDependencyFactory.CreateDependencies(
            services,
            aktion => Dispatcher.Invoke(aktion));
        _queueService = dependencies.QueueService;
        _pdfReviewImport = dependencies.PdfReviewImport;
        _pdfReviewBatchImport = dependencies.PdfReviewBatchImport;
        _goldAlbumService = dependencies.GoldAlbum;
        _goldInboxService = dependencies.GoldInbox;
        _folderOpen = dependencies.FolderOpen;
        _bendVm = dependencies.BendSuggestions;
        BendSuggestionSection.DataContext = _bendVm;
        // Die Review-Warteschlange wird ueber "Warteschlange laden" asynchron geladen (LoadReviewQueue_Click);
        // der synchrone loadQueue-Delegate bleibt leer.
        _vm = new TrainingStudioViewModel(
            dependencies.Workbench,
            () => Array.Empty<WorkbenchItem>(),
            Environment.UserName,
            ensureAiReady: dependencies.EnsureAiReady,
            loadGoldProgress: dependencies.LoadGoldProgress,
            previewDetection: dependencies.PreviewDetection,
            goldQualityReview: dependencies.GoldQualityReview);
        DataContext = _vm;

        _vm.PropertyChanged += Vm_PropertyChanged;
        OverlayCanvas.SizeChanged += (_, _) => RedrawOverlay();
        Loaded += TrainingStudioWindow_Loaded;
        Closed += (_, _) =>
        {
            _isClosed = true;
            _pdfImportCts?.Cancel();
            _vm.PropertyChanged -= Vm_PropertyChanged;
            // Bricht Scan- und Vorschaularbeit des Bogen-Bereichs ab.
            _bendVm.Dispose();
            // Gibt Workbench-SAM-Service + Vision-Client (eigener HttpClient) frei.
            _vm.Dispose();
        };
    }

    // ── Bogen-Vorschläge: nur die Dateiwahl, die Fachlogik liegt im ViewModel ──

    private void ChooseBendSuggestionVideo_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Video einer Haltung für die Bogen-Vorschläge wählen",
            Filter = "Videos (*.mpg;*.mpeg;*.mp4;*.avi;*.mov;*.mkv)|*.mpg;*.mpeg;*.mp4;*.avi;*.mov;*.mkv|Alle Dateien (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
        };
        const string suggestedRoot = @"D:\Haltungen";
        if (Directory.Exists(suggestedRoot))
            dlg.InitialDirectory = suggestedRoot;
        if (dlg.ShowDialog(this) != true)
            return;

        _bendVm.SetVideo(dlg.FileName);
    }

    private void OpenBendSuggestionPreview_Click(object sender, RoutedEventArgs e)
        => OpenBendSuggestionPreview();

    private void BendSuggestionGrid_MouseDoubleClick(
        object sender, System.Windows.Input.MouseButtonEventArgs e)
        => OpenBendSuggestionPreview();

    /// <summary>
    /// Grossansicht des gewaehlten Vorschlags: Der Daumen im Bereich taugt nicht
    /// zur Beurteilung (Sichtpruefung 2026-08-09). Reine Anzeige, kein Player-Sprung.
    /// </summary>
    private void OpenBendSuggestionPreview()
    {
        var auswahl = _bendVm.SelectedSuggestion;
        if (auswahl is null || _bendVm.PeakImage is null)
            return;

        var fenster = new BendSuggestionPreviewWindow { Owner = this };
        fenster.SetContent(auswahl.OrtText, _bendVm.PeakImage, _bendVm.ClipPath);
        fenster.Show();
    }

    private async void TrainingStudioWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= TrainingStudioWindow_Loaded;
        await _vm.RefreshGoldProgressCommand.ExecuteAsync(null);
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

    private async void LoadPdfProtocol_Click(object sender, RoutedEventArgs e)
    {
        if (_pdfImportInProgress)
        {
            _vm.StatusText = "Das PDF-Protokoll wird bereits gelesen.";
            return;
        }

        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "PDF-Protokoll einer Haltung für den Prüfplatz wählen",
            Filter = "PDF-Protokolle (*.pdf)|*.pdf",
            CheckFileExists = true,
            Multiselect = false,
        };
        const string suggestedRoot = @"D:\Haltungen";
        if (Directory.Exists(suggestedRoot))
            dlg.InitialDirectory = suggestedRoot;
        if (dlg.ShowDialog(this) != true)
            return;

        var importCts = new CancellationTokenSource();
        BeginPdfImport(
            importCts,
            "PDF-Fotos und Operateurbefunde werden sicher zugeordnet …");
        try
        {
            var result = await _pdfReviewImport.ImportAsync(
                new TrainingPdfReviewImportRequest(
                    dlg.FileName,
                    _vm.PipeDiameterMm),
                importCts.Token);
            if (!_vm.LoadItems(result.Items))
                return;
            _vm.StatusText = TrainingStudioPdfImportPresentation.FormatSingle(result);
        }
        catch (OperationCanceledException) when (importCts.IsCancellationRequested)
        {
            if (!_isClosed)
                _vm.StatusText = "PDF-Import abgebrochen.";
        }
        catch (Exception ex)
        {
            _vm.StatusText = "PDF-Protokoll konnte nicht für den Prüfplatz geladen werden: "
                + UserError.DescribeAndReport(
                    ex,
                    $"Training-Studio PDF-Protokoll laden ({System.IO.Path.GetFileName(dlg.FileName)})");
        }
        finally
        {
            EndPdfImport(importCts);
        }
    }

    private async void LoadPdfFolders_Click(object sender, RoutedEventArgs e)
    {
        if (_pdfImportInProgress)
        {
            _vm.StatusText = "PDF-Protokolle werden bereits gelesen.";
            return;
        }

        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Ordner mit PDF-Protokollen wählen (Mehrfachauswahl möglich)",
            Multiselect = true,
        };
        const string suggestedRoot = @"D:\Haltungen";
        if (Directory.Exists(suggestedRoot))
            dlg.InitialDirectory = suggestedRoot;
        if (dlg.ShowDialog(this) != true)
            return;

        var folders = dlg.FolderNames
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToArray();
        if (folders.Length == 0)
            return;

        var importCts = new CancellationTokenSource();
        BeginPdfImport(
            importCts,
            "Die gewählten Ordner werden nach PDF-Protokollen durchsucht …");
        var progress = new Progress<TrainingPdfReviewBatchProgress>(value =>
        {
            if (!_isClosed)
            {
                _vm.StatusText =
                    $"PDF {value.CurrentPdfNumber} von {value.DiscoveredPdfCount} wird gelesen: " +
                    value.SourceDocumentName;
            }
        });

        try
        {
            var result = await _pdfReviewBatchImport.ImportFoldersAsync(
                new TrainingPdfReviewBatchImportRequest(
                    folders,
                    _vm.PipeDiameterMm),
                progress,
                importCts.Token);
            if (!_vm.LoadItems(result.Items))
                return;
            _vm.StatusText = TrainingStudioPdfImportPresentation.FormatBatch(result);
        }
        catch (OperationCanceledException) when (importCts.IsCancellationRequested)
        {
            if (!_isClosed)
                _vm.StatusText = "PDF-Ordnerimport abgebrochen.";
        }
        catch (Exception ex)
        {
            _vm.StatusText = "PDF-Ordner konnten nicht für den Prüfplatz geladen werden: "
                + UserError.DescribeAndReport(
                    ex,
                    "Training-Studio PDF-Ordner laden");
        }
        finally
        {
            EndPdfImport(importCts);
        }
    }

    private void BeginPdfImport(
        CancellationTokenSource importCts,
        string statusText)
    {
        _pdfImportCts = importCts;
        _pdfImportInProgress = true;
        SetPdfImportUiBusy(true);
        _vm.StatusText = statusText;
    }

    private void EndPdfImport(CancellationTokenSource importCts)
    {
        if (ReferenceEquals(_pdfImportCts, importCts))
        {
            _pdfImportCts = null;
            _pdfImportInProgress = false;
            if (!_isClosed)
                SetPdfImportUiBusy(false);
        }

        importCts.Dispose();
    }

    private void SetPdfImportUiBusy(bool isBusy)
    {
        PdfSourceToolbar.IsEnabled = !isBusy;
        PdfReviewArea.IsEnabled = !isBusy;
        PdfThumbnailQueue.IsEnabled = !isBusy;
        PdfImportProgressPanel.Visibility = isBusy
            ? Visibility.Visible
            : Visibility.Collapsed;
        PdfImportCancelButton.IsEnabled = isBusy;
    }

    private void CancelPdfImport_Click(object sender, RoutedEventArgs e)
    {
        if (_pdfImportCts is null || _pdfImportCts.IsCancellationRequested)
            return;

        _pdfImportCts.Cancel();
        PdfImportCancelButton.IsEnabled = false;
        _vm.StatusText = "PDF-Import wird abgebrochen …";
    }

    private void PreviewModelBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!TrainingStudioModelSelectionFocusPolicy.ShouldReleaseFocus(
                hasSelection: PreviewModelBox.SelectedItem is not null,
                listHasFocus: PreviewModelBox.IsKeyboardFocusWithin))
            return;

        // Fokus ans Fenster zurueck, damit A/K/V und die Pfeiltasten sofort wieder
        // greifen. Nicht auf das Bild: ein Image ist in WPF nicht fokussierbar,
        // der Fokus bliebe still auf der Auswahlliste stehen.
        Keyboard.Focus(this);
    }

    private void TrainingStudioWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var decision = TrainingStudioKeyboardShortcutPolicy.Resolve(
            e.Key,
            Keyboard.Modifiers,
            KeyboardTextInputFocusGuard.IsTextInputFocused(),
            _pdfImportInProgress);

        if (!decision.ShouldHandle)
            return;

        e.Handled = true;

        ICommand? command = decision.Action switch
        {
            TrainingStudioKeyboardShortcutAction.Accept => _vm.AcceptCommand,
            TrainingStudioKeyboardShortcutAction.Correct => _vm.CorrectCommand,
            TrainingStudioKeyboardShortcutAction.Discard => _vm.DiscardCommand,
            TrainingStudioKeyboardShortcutAction.NextItem => _vm.NextItemCommand,
            TrainingStudioKeyboardShortcutAction.PreviousItem => _vm.PreviousItemCommand,
            _ => null
        };

        if (command?.CanExecute(null) == true)
            command.Execute(null);
    }

    private void OpenGoldInbox_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = _goldInboxService.EnsureFolders();
            if (_folderOpen is null)
            {
                _vm.StatusText = $"Gold-Eingang: {path}";
                return;
            }

            var result = _folderOpen.EnsureAndOpen(path);
            _vm.StatusText = result.Success
                ? $"Gold-Eingang geöffnet: {path}"
                : $"Gold-Eingang konnte nicht geöffnet werden: {result.Error}";
        }
        catch (Exception ex)
        {
            _vm.StatusText = "Gold-Eingang konnte nicht vorbereitet werden: "
                + UserError.DescribeAndReport(ex, "Training-Studio Gold-Eingang öffnen");
        }
    }

    private async void LoadGoldInbox_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var snapshot = await _goldInboxService.LoadAsync();
            var items = WorkbenchQueueService.BuildGoldInboxItems(
                snapshot.Images,
                _vm.PipeDiameterMm);
            if (!_vm.LoadItems(items))
                return;
            var issueText = snapshot.Issues.Count == 0
                ? string.Empty
                : $" · {snapshot.Issues.Count} Hinweise";
            _vm.StatusText = items.Count == 0
                ? $"Gold-Eingang ist leer: {snapshot.RootPath}{issueText}"
                : $"{items.Count} Bilder aus dem Gold-Eingang geladen{issueText}.";
        }
        catch (Exception ex)
        {
            _vm.StatusText = "Gold-Eingang konnte nicht geladen werden: "
                + UserError.DescribeAndReport(ex, "Training-Studio Gold-Eingang laden");
        }
    }

    private async void LoadReviewQueue_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var items = await _queueService.LoadReviewQueueAsync();
            if (!_vm.LoadItems(items))
                return;
            if (items.Count == 0)
                _vm.StatusText = "Keine offenen Review-Faelle (Yellow/Red, noch nicht beurteilt).";
        }
        catch (Exception ex)
        {
            _vm.StatusText = "Warteschlange konnte nicht geladen werden: "
                + UserError.DescribeAndReport(ex, "Training-Studio Warteschlange laden");
        }
    }

    private async void PdfThumbnailQueue_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_syncingThumbnailSelection || _vm is null)
            return;

        var requestedIndex = PdfThumbnailQueue.SelectedIndex;
        if (requestedIndex < 0 || requestedIndex == _vm.CurrentIndex)
            return;

        var selected = await _vm.SelectQueueItemAsync(requestedIndex);
        if (selected || _isClosed)
            return;

        _syncingThumbnailSelection = true;
        try
        {
            PdfThumbnailQueue.SelectedIndex = _vm.CurrentIndex;
        }
        finally
        {
            _syncingThumbnailSelection = false;
        }
    }

    private async void LoadIncompleteGoldQueue_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _vm.StatusText = "Bilder ohne gültige Segmentierung werden geprüft …";
            var items = await _queueService.LoadSegmentationRepairQueueAsync(Environment.UserName);
            if (!await _vm.LoadSegmentationRepairItemsAsync(items))
                return;
            if (items.Count == 0)
                _vm.StatusText = "Keine lesbaren persönlichen Bilder ohne gültige Segmentierung gefunden.";
        }
        catch (Exception ex)
        {
            _vm.StatusText = "Segmentierungs-Warteschlange konnte nicht geladen werden: "
                + UserError.DescribeAndReport(ex, "Training-Studio Segmentierungs-Warteschlange");
        }
    }

    private async void LoadAllIncompleteGoldQueue_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var items = await _queueService
                .LoadIncompletePersonalGoldQueueAsync(Environment.UserName);
            if (!_vm.LoadItems(items))
                return;
            _vm.StatusText = items.Count == 0
                ? "Keine persönlichen Gold-Reparaturfälle gefunden."
                : $"{items.Count} Gold-Reparaturfälle geladen. Nicht lesbare Bilder dienen nur der Diagnose.";
        }
        catch (Exception ex)
        {
            _vm.StatusText = "Gold-Reparaturfälle konnten nicht geladen werden: "
                + UserError.DescribeAndReport(ex, "Training-Studio alle Gold-Reparaturfälle");
        }
    }

    private void OpenGoldAlbum_Click(object sender, RoutedEventArgs e)
    {
        var window = new PersonalGoldAlbumWindow(
            _goldAlbumService,
            Environment.UserName)
        {
            Owner = this
        };
        window.Show();
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
        _vm.ApplyCodeSelection(
            selection.Code,
            selection.ClockPosition,
            selection.Severity,
            selection.Beschreibung);
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
            or nameof(TrainingStudioViewModel.PreviewDetections)
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
            var maskValidation = TrainingStudioBoxAnalysisUseCase.ValidateSegmentation(
                _vm.CurrentBox,
                _vm.Segmentation);
            var result = TrainingStudioMaskOverlayRenderer.Render(
                OverlayCanvas,
                _vm.Segmentation,
                area,
                maskValidation.IsValid);
            if (!result.Rendered && !string.IsNullOrWhiteSpace(result.ErrorMessage))
                _vm.StatusText = result.ErrorMessage;
        }

        // Automatische Modelltreffer bleiben blau und getrennt von der roten Hand-Box.
        if (FrameImage.Source is BitmapSource source)
            DrawPreviewDetections(area, source);

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

    private void DrawPreviewDetections(Rect area, BitmapSource source)
    {
        if (source.PixelWidth <= 0 || source.PixelHeight <= 0)
            return;

        foreach (var detection in _vm.PreviewDetections)
        {
            var x1 = Math.Clamp(detection.X1 / source.PixelWidth, 0, 1);
            var y1 = Math.Clamp(detection.Y1 / source.PixelHeight, 0, 1);
            var x2 = Math.Clamp(detection.X2 / source.PixelWidth, 0, 1);
            var y2 = Math.Clamp(detection.Y2 / source.PixelHeight, 0, 1);
            var left = area.X + Math.Min(x1, x2) * area.Width;
            var top = area.Y + Math.Min(y1, y2) * area.Height;
            var width = Math.Abs(x2 - x1) * area.Width;
            var height = Math.Abs(y2 - y1) * area.Height;
            if (width < 1 || height < 1)
                continue;

            var rectangle = new Rectangle
            {
                Stroke = Brushes.DeepSkyBlue,
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 5, 3 },
                Fill = new SolidColorBrush(Color.FromArgb(20, 0, 191, 255)),
                IsHitTestVisible = false,
                Width = width,
                Height = height,
            };
            Canvas.SetLeft(rectangle, left);
            Canvas.SetTop(rectangle, top);
            OverlayCanvas.Children.Add(rectangle);

            var label = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(215, 0, 105, 148)),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(4, 2, 4, 2),
                IsHitTestVisible = false,
                Child = new TextBlock
                {
                    Text = $"{detection.DisplayText} {detection.Confidence:P0}",
                    Foreground = Brushes.White,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                },
            };
            Canvas.SetLeft(label, left);
            Canvas.SetTop(label, Math.Max(area.Y, top - 22));
            OverlayCanvas.Children.Add(label);
        }
    }
}
