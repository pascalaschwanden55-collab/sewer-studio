using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Ai.Teacher;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Windows;
using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class TrainingCenterWindow : Window
{
    public TrainingCenterViewModel Vm { get; }

    private readonly ServiceProvider? _services;
    private readonly IDialogService _dialogs;
    private readonly TrainingCenterLazyServices _trainingServices;
    private readonly TrainingCenterWindowLifetime _lifetime = new();

    private IVsaCodeSelectionCatalog? CodeSelectionCatalog
        => _services?.CodeSelectionCatalog;

    private IDialogService Dialogs => _dialogs;

    // Pipeline-Dots und Service-Indikatoren fuer Animation
    private Ellipse[] _pipelineDots = Array.Empty<Ellipse>();
    private Border[] _serviceDots = Array.Empty<Border>();

    // ── Box-Zeichnen auf Review-Karte (B5) ──────────────────────────────
    private Rectangle? _boxPreview;
    private Point _boxStart;
    private bool _drawing;
    public TrainingCenterWindow()
        : this(
            services: null,
            TrainingCenterWindowFallbackDependencies.Dialogs,
            TrainingCenterWindowFallbackDependencies.Store,
            TrainingCenterWindowFallbackDependencies.Import,
            TrainingCenterWindowFallbackDependencies.KnowledgeBaseDiagnostics,
            () => TrainingCenterWindowFallbackDependencies.TrainingReviewQueue,
            TrainingCenterWindowFallbackDependencies.CreateTrainingReviewSam,
            TrainingCenterWindowFallbackDependencies.CreateFewShotStore)
    {
    }

    public TrainingCenterWindow(ServiceProvider? services)
        : this(
            services,
            services?.Dialogs ?? TrainingCenterWindowFallbackDependencies.Dialogs,
            services?.TrainingCenterStore ?? TrainingCenterWindowFallbackDependencies.Store,
            services?.TrainingCenterImport ?? TrainingCenterWindowFallbackDependencies.Import,
            services?.KnowledgeBaseDiagnostics ?? TrainingCenterWindowFallbackDependencies.KnowledgeBaseDiagnostics,
            () => services?.TrainingReviewQueue ?? TrainingCenterWindowFallbackDependencies.TrainingReviewQueue,
            () => services?.CreateTrainingReviewSam() ?? TrainingCenterWindowFallbackDependencies.CreateTrainingReviewSam(),
            () => services?.CreateFewShotStore() ?? TrainingCenterWindowFallbackDependencies.CreateFewShotStore())
    {
    }

    public TrainingCenterWindow(
        ServiceProvider? services,
        IDialogService dialogs,
        TrainingCenterStore store,
        TrainingCenterImportService import,
        IKnowledgeBaseDiagnosticsRunner knowledgeBaseDiagnostics)
        : this(
            services,
            dialogs,
            store,
            import,
            knowledgeBaseDiagnostics,
            () => services?.TrainingReviewQueue ?? TrainingCenterWindowFallbackDependencies.TrainingReviewQueue,
            () => services?.CreateTrainingReviewSam() ?? TrainingCenterWindowFallbackDependencies.CreateTrainingReviewSam(),
            () => services?.CreateFewShotStore() ?? TrainingCenterWindowFallbackDependencies.CreateFewShotStore())
    {
    }

    public TrainingCenterWindow(
        ServiceProvider? services,
        IDialogService dialogs,
        TrainingCenterStore store,
        TrainingCenterImportService import,
        IKnowledgeBaseDiagnosticsRunner knowledgeBaseDiagnostics,
        Func<InfraSelfImproving.ReviewQueueService> createReviewQueue,
        Func<TrainingReviewSamSegmentationService> createReviewSam,
        Func<FewShotExampleStore> createFewShotStore)
    {
        _services = services;
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(import);
        ArgumentNullException.ThrowIfNull(knowledgeBaseDiagnostics);
        _trainingServices = new TrainingCenterLazyServices(
            createReviewQueue,
            createReviewSam,
            createFewShotStore);

        InitializeComponent();
        WindowStateManager.Track(this);

        // Hover-Foto-Vorschau: Frame-Pfade sind absolut (kein Projekt-Root noetig).
        Behaviors.PhotoHoverPreviewBehavior.SetPhotoPathsSelector(
            SamplesGrid, Behaviors.PhotoHoverPreviewSelectors.TrainingSamplePhotos);
        Behaviors.PhotoHoverPreviewBehavior.SetPhotoPathsSelector(
            ReviewQueueList, Behaviors.PhotoHoverPreviewSelectors.ReviewQueueItemPhotos);
        Behaviors.PhotoHoverPreviewBehavior.SetPhotoPathsSelector(
            TeacherGallery, Behaviors.PhotoHoverPreviewSelectors.TeacherAnnotationPhotos);

        var codeCatalog = services?.CodeCatalog;
        Vm = new TrainingCenterViewModel(
            store,
            import,
            codeCatalog,
            knowledgeBaseDiagnostics,
            services?.Settings,
            uiThread: null,
            knowledgeBackup: services?.KnowledgeBackup ?? new KnowledgeBackupTransferService(),
            trainingSamples: services?.TrainingSamples ?? TrainingSamplesStore.Current,
            trainingPreviewFrames: services?.TrainingPreviewFrames ?? TrainingPreviewFrameExtractor.Current);

        DataContext = Vm;

        Loaded += async (_, __) =>
        {
            await Vm.LoadAsync();
            SetupPipelineElements();
            SetupAutoScroll();

            // Review-Queue laden (falls KB vorhanden) - persistent, ueberlebt Neustart
            var reviewQueue = _trainingServices.GetReviewQueue();
            Vm.ReviewQueueServiceRef = reviewQueue;
            Vm.LoadReviewQueue(reviewQueue);

            // Lehrer-Annotationen laden
            await LoadTeacherAnnotationsAsync();
        };

        Vm.PropertyChanged += OnVmPropertyChanged;
        Closing += TrainingCenterWindow_Closing;
        Closed += (_, _) => Vm.PropertyChanged -= OnVmPropertyChanged;
    }

    private void TrainingCenterWindow_Closing(object? sender, CancelEventArgs e)
    {
        _lifetime.Dispose();
        Vm.CancelOutstandingOperations();
    }

    private void SetupPipelineElements()
    {
        _pipelineDots = new[] { Dot0, Dot1, Dot2, Dot3, Dot4, Dot5 };
        _serviceDots = new[] { SvcOsd, SvcFrame, SvcQwen, SvcCompare, SvcTech };
    }

    // Debounce-Timer fuer Auto-Scroll (verhindert Layout-Kollaps bei schnellen Batch-Updates)
    private System.Windows.Threading.DispatcherTimer? _scrollDebounceResults;
    private System.Windows.Threading.DispatcherTimer? _scrollDebounceLog;

    private void SetupAutoScroll()
    {
        // Debounce: ScrollIntoView erst nach 200ms Ruhe (statt bei jedem CollectionChanged)
        _scrollDebounceResults = new System.Windows.Threading.DispatcherTimer
            { Interval = TimeSpan.FromMilliseconds(200) };
        _scrollDebounceResults.Tick += (_, _) =>
        {
            _scrollDebounceResults.Stop();
            if (ResultsListBox.Items.Count > 0)
                ResultsListBox.ScrollIntoView(ResultsListBox.Items[^1]);
        };

        ((System.Collections.Specialized.INotifyCollectionChanged)Vm.SelfTrainingResults)
            .CollectionChanged += (_, _) =>
        {
            _scrollDebounceResults.Stop();
            _scrollDebounceResults.Start();
        };

        // Gleiches Debounce fuer Echtzeit-Log
        _scrollDebounceLog = new System.Windows.Threading.DispatcherTimer
            { Interval = TimeSpan.FromMilliseconds(200) };
        _scrollDebounceLog.Tick += (_, _) =>
        {
            _scrollDebounceLog.Stop();
            if (SelfTrainingLogList.Items.Count > 0)
                SelfTrainingLogList.ScrollIntoView(SelfTrainingLogList.Items[^1]);
        };

        ((System.Collections.Specialized.INotifyCollectionChanged)Vm.SelfTrainingLogEntries)
            .CollectionChanged += (_, _) =>
        {
            _scrollDebounceLog.Stop();
            _scrollDebounceLog.Start();
        };
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TrainingCenterViewModel.LogText))
            LogTextBox?.ScrollToEnd();

        if (e.PropertyName == nameof(TrainingCenterViewModel.PipelineActiveStep))
            UpdatePipelineVisuals(Vm.PipelineActiveStep);

        if (e.PropertyName == nameof(TrainingCenterViewModel.IsModelActive))
            UpdateModelIndicator(Vm.IsModelActive, Vm.ActiveModelName);

        if (e.PropertyName is nameof(TrainingCenterViewModel.ExactPercent)
            or nameof(TrainingCenterViewModel.PartialPercent)
            or nameof(TrainingCenterViewModel.MismatchPercent)
            or nameof(TrainingCenterViewModel.NoFindingsPercent))
            UpdateMatchRateBar();

        // Box loeschen wenn Kandidat wechselt (B5)
        if (e.PropertyName == nameof(TrainingCenterViewModel.SelectedReviewItem))
            ClearBox();
    }

    private void UpdatePipelineVisuals(int activeStep)
    {
        if (_pipelineDots.Length == 0) return;

        var green = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0x4A, 0xDE, 0x80));
        var amber = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0xFB, 0xBF, 0x24));
        var gray = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0x47, 0x55, 0x69));

        for (var i = 0; i < _pipelineDots.Length; i++)
        {
            _pipelineDots[i].Fill = i < activeStep ? green : i == activeStep ? amber : gray;
        }

        // Service-Dots: 0=OSD(Stage0), 1=Frame(Stage1), 2=Qwen(Stage2), 3=Compare(Stage3), 4=Tech(Stage4)
        if (_serviceDots.Length >= 5)
        {
            for (var i = 0; i < _serviceDots.Length; i++)
            {
                _serviceDots[i].Background = i == activeStep ? amber : gray;
            }
        }
    }

    private void UpdateModelIndicator(bool isActive, string modelName)
    {
        var gpuGreen = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0x4A, 0xDE, 0x80));
        var cpuAmber = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0xFB, 0xBF, 0x24));
        var gray = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0x47, 0x55, 0x69));

        if (!isActive)
        {
            ModelPulse.Fill = gray;
            ModelNameText.Foreground = gray;
            ActiveModelBorder.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(0x0F, 0x17, 0x2A));
            return;
        }

        // GPU-Modelle gruen, CPU-Prozesse amber
        bool isGpu = modelName.Contains("GPU", StringComparison.OrdinalIgnoreCase);
        var color = isGpu ? gpuGreen : cpuAmber;
        ModelPulse.Fill = color;
        ModelNameText.Foreground = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0xF1, 0xF5, 0xF9));
        ActiveModelBorder.Background = new System.Windows.Media.SolidColorBrush(
            isGpu ? System.Windows.Media.Color.FromArgb(0x30, 0x4A, 0xDE, 0x80)
                  : System.Windows.Media.Color.FromArgb(0x30, 0xFB, 0xBF, 0x24));
    }

    private void UpdateMatchRateBar()
    {
        var total = Vm.ExactPercent + Vm.PartialPercent + Vm.MismatchPercent + Vm.NoFindingsPercent;
        if (total <= 0) return;

        // Grid-Spalten proportional setzen
        ExactCol.Width = new GridLength(Vm.ExactPercent, GridUnitType.Star);
        PartialCol.Width = new GridLength(Vm.PartialPercent, GridUnitType.Star);
        MismatchCol.Width = new GridLength(Vm.MismatchPercent, GridUnitType.Star);
        NoFindingsCol.Width = new GridLength(Vm.NoFindingsPercent, GridUnitType.Star);

        // Restliche Spalte auf 0 wenn Daten da sind
        MatchRateBar.ColumnDefinitions[4].Width = new GridLength(
            total >= 0.99 ? 0 : 1 - total, GridUnitType.Star);
    }

    // ── Box-Zeichnen auf der Review-Karte (B5) ──────────────────────────

    /// <summary>
    /// Entfernt die gezeichnete Vorschau-Box und setzt PendingBox im ViewModel zurueck.
    /// </summary>
    private void ClearBox()
    {
        if (BoxCanvas is not null)
            SamMaskRenderer.ClearMasks(BoxCanvas);
        if (ReviewSamStatusText is not null)
            ReviewSamStatusText.Text = "";

        if (_boxPreview is not null)
        {
            BoxCanvas?.Children.Remove(_boxPreview);
            _boxPreview = null;
        }
        _drawing = false;
        Vm.PendingBox = null;
        Vm.PendingSamMask = null;
    }

    private async void ReviewSegmentSam_Click(object sender, RoutedEventArgs e)
    {
        var card = Vm.SelectedReviewCard;
        if (card is null)
        {
            Dialogs.Info("Bitte zuerst einen Review-Kandidaten waehlen.", "SAM");
            return;
        }

        if (Vm.PendingBox is not { } box)
        {
            Dialogs.Info("Bitte zuerst eine Box um den Schaden ziehen.", "SAM");
            return;
        }

        if (string.IsNullOrWhiteSpace(card.FramePath) || !File.Exists(card.FramePath))
        {
            Dialogs.Warn("Der Review-Frame ist nicht verfuegbar.", "SAM");
            return;
        }

        var ct = _lifetime.Token;
        try
        {
            BtnReviewSegmentSam.IsEnabled = false;
            ReviewSamStatusText.Text = "SAM läuft...";
            SamMaskRenderer.ClearMasks(BoxCanvas);
            Vm.PendingSamMask = null;

            var result = await _trainingServices.GetReviewSam().SegmentFrameFileAsync(
                card.FramePath,
                box,
                card.ProtocolCode,
                ResolveReviewPipeDiameterMm(),
                ct);

            SamMaskRenderer.RenderMasks(
                BoxCanvas,
                result.Response,
                result.QuantifiedMasks,
                BoxCanvas.ActualWidth,
                BoxCanvas.ActualHeight,
                _services?.LoggerFactory.CreateLogger("TrainingReviewSam"));

            Vm.PendingSamMask = CreateTrainingSegmentationMask(result.Response);
            ReviewSamStatusText.Text = result.Response.Masks.Count == 0
                ? BuildSamStatus(result.Response)
                : $"SAM: {result.Response.Masks.Count} Maske(n) - wird mit Akzeptieren gespeichert";
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Das Fenster wird geschlossen; keine Meldung mehr anzeigen.
        }
        catch (Exception ex)
        {
            ReviewSamStatusText.Text = "SAM Fehler";
            var userMessage = UserError.DescribeAndReport(ex, "SAM-Segmentierung");
            Dialogs.Warn($"SAM-Segmentierung fehlgeschlagen:\n{userMessage}", "SAM");
        }
        finally
        {
            if (!ct.IsCancellationRequested)
                BtnReviewSegmentSam.IsEnabled = true;
        }
    }

    private static TrainingSegmentationMask? CreateTrainingSegmentationMask(SamResponse response)
    {
        var mask = response.Masks.FirstOrDefault(m => !string.IsNullOrWhiteSpace(m.MaskRle));
        if (mask is null)
            return null;

        return new TrainingSegmentationMask(
            mask.MaskRle,
            response.ImageWidth,
            response.ImageHeight,
            mask.MaskAreaPixels,
            mask.Confidence,
            mask.Label);
    }

    private static string BuildSamStatus(SamResponse response)
    {
        if (!string.IsNullOrWhiteSpace(response.Error))
            return $"SAM: keine Maske ({response.Error})";

        if (response.SkippedBoxes > 0)
            return $"SAM: keine Maske ({response.SkippedBoxes}/{response.RequestedBoxes} Box(en) uebersprungen)";

        return "SAM: keine Maske";
    }

    private int? ResolveReviewPipeDiameterMm()
    {
        var pipelineCfg = _services is not null
            ? _services.PipelineCfg
            : new AppSettingsAiSettingsProvider().Load().ToPipelineConfig();

        return pipelineCfg.PipeDiameterMmOverride ?? 300;
    }

    private void BoxCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (BoxCanvas is null) return;

        // Bestehende Box loeschen bevor neue gezeichnet wird
        ClearBox();

        _drawing = true;
        _boxStart = e.GetPosition(BoxCanvas);

        // Vorschau-Rechteck anlegen
        _boxPreview = new Rectangle
        {
            Stroke = new SolidColorBrush(Color.FromRgb(0xFB, 0xBF, 0x24)), // Amber #FBBF24
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Color.FromArgb(0x33, 0xFB, 0xBF, 0x24)),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(_boxPreview, _boxStart.X);
        Canvas.SetTop(_boxPreview, _boxStart.Y);
        BoxCanvas.Children.Add(_boxPreview);

        BoxCanvas.CaptureMouse();
    }

    private void BoxCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_drawing || _boxPreview is null || BoxCanvas is null) return;

        var current = e.GetPosition(BoxCanvas);

        // Rechteck aus beliebiger Richtung zeichnen
        var left = Math.Min(_boxStart.X, current.X);
        var top = Math.Min(_boxStart.Y, current.Y);
        var width = Math.Abs(current.X - _boxStart.X);
        var height = Math.Abs(current.Y - _boxStart.Y);

        Canvas.SetLeft(_boxPreview, left);
        Canvas.SetTop(_boxPreview, top);
        _boxPreview.Width = width;
        _boxPreview.Height = height;
    }

    private void BoxCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_drawing || _boxPreview is null || BoxCanvas is null)
        {
            _drawing = false;
            BoxCanvas?.ReleaseMouseCapture();
            return;
        }

        _drawing = false;
        BoxCanvas.ReleaseMouseCapture();

        // Normierte Koordinaten berechnen (0-1, YOLO-Format)
        var canvasW = BoxCanvas.ActualWidth;
        var canvasH = BoxCanvas.ActualHeight;
        if (canvasW <= 0 || canvasH <= 0)
        {
            ClearBox();
            return;
        }

        var left = Canvas.GetLeft(_boxPreview);
        var top = Canvas.GetTop(_boxPreview);
        var w = _boxPreview.Width;
        var h = _boxPreview.Height;

        // Mindestgroesse 2% in beiden Achsen — kleiner = Versehen, nicht speichern
        if (w / canvasW < 0.02 || h / canvasH < 0.02)
        {
            ClearBox();
            return;
        }

        var normW = Math.Clamp(w / canvasW, 0.0, 1.0);
        var normH = Math.Clamp(h / canvasH, 0.0, 1.0);
        var normXc = Math.Clamp((left + w / 2.0) / canvasW, 0.0, 1.0);
        var normYc = Math.Clamp((top + h / 2.0) / canvasH, 0.0, 1.0);

        if (BoundingBox.TryCreate(normXc, normYc, normW, normH, out var box))
        {
            Vm.PendingBox = box;
        }
        else
        {
            // Ungueltige Box (z.B. ausserhalb Bild) → verwerfen
            ClearBox();
        }
    }

    /// <summary>Esc loescht die gezeichnete Box (KeyDown auf Review-Grid).</summary>
    private void ReviewGrid_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            ClearBox();
            e.Handled = true;
        }
    }

    // ── Protokoll-Startdaten Sammel-Freigabe (B6) ───────────────────────

    /// <summary>
    /// Sammel-Freigabe aller Protokoll-Startdaten-Kandidaten nach expliziter Bestaetigung.
    /// Alle Freigaben laufen ueber ApproveReviewItemAsync (kein direkter KB-Write).
    /// </summary>
    private async void ReleaseStartdata_Click(object sender, RoutedEventArgs e)
    {
        var n = Vm.StartdataCandidateCount;
        if (n == 0)
        {
            Dialogs.Info("Keine Protokoll-Startdaten in der Queue.", "Sammel-Freigabe");
            return;
        }
        if (!Dialogs.ConfirmWarn(
            $"{n} Protokoll-Startdaten freigeben?\n\nDas schreibt {n} gepruefte Eintraege in die Knowledge Base (ueber Review, kein Auto-Index).",
            "Sammel-Freigabe")) return;
        try { await Vm.ApproveAllStartdataAsync(); }
        catch (Exception ex)
        {
            var userMessage = UserError.DescribeAndReport(ex, "Sammel-Freigabe");
            Dialogs.Warn($"Fehler bei der Sammel-Freigabe: {userMessage}", "Sammel-Freigabe");
        }
    }

    // ── Review-Korrektur ─────────────────────────────────────────────────

    private async void ReviewCorrect_Click(object sender, RoutedEventArgs e)
    {
        var dialogService = VsaCodeExplorerDialogServiceFactory.Create();
        await TrainingCenterReviewCorrectionWorkflow.ExecuteAsync(
            new TrainingCenterReviewCorrectionRequest(Vm.SelectedReviewItem, CodeSelectionCatalog),
            new TrainingCenterReviewCorrectionActions(
                ShowCodeExplorer: explorerVm => dialogService.Show(explorerVm, null, null, this),
                ApplyCorrectionAsync: (item, code, description) =>
                    Vm.ApplyReviewCorrectionAsync(item, code, correctedDescription: description),
                Warn: Dialogs.Warn));
    }

    // ── Lehrer-Annotationen Tab Event Handlers ──

    private List<TeacherAnnotation> _allTeacherAnnotations = new();
    private List<TeacherAnnotation> _filteredTeacherAnnotations = new();
    private TeacherAnnotation? _selectedTeacherAnnotation;
    private bool _teacherLoaded;
    private readonly TeacherAnnotationGalleryService _teacherGalleryService = new();
    private readonly VideoLabelToolLauncher _videoLabelToolLauncher = new();

    private void VideoLabelToolOpen_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            BtnOpenVideoLabelTool.IsEnabled = false;
            var result = _videoLabelToolLauncher.Launch(new VideoLabelToolLaunchOptions());
            VideoLabelToolStatusText.Text = result.ServerStarted
                ? $"Gold-Label-Tool gestartet: {result.Url}"
                : $"Gold-Label-Tool geöffnet: {result.Url}";
        }
        catch (Exception ex)
        {
            var userMessage = UserError.DescribeAndReport(ex, "VideoLabelTool starten");
            Dialogs.Warn(
                $"VideoLabelTool konnte nicht gestartet werden:\n{userMessage}",
                "Gold-Label-Tool");
        }
        finally
        {
            BtnOpenVideoLabelTool.IsEnabled = true;
        }
    }

    private async void TeacherRefresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadTeacherAnnotationsAsync();
    }

    private async Task LoadTeacherAnnotationsAsync()
    {
        try
        {
            var snapshot = await _teacherGalleryService.LoadPendingAsync();
            _allTeacherAnnotations = snapshot.PendingAnnotations.ToList();

            TeacherFilterCombo.Items.Clear();
            TeacherFilterCombo.Items.Add(new ComboBoxItem { Content = "Alle", IsSelected = true });
            foreach (var code in snapshot.FilterCodes)
                TeacherFilterCombo.Items.Add(new ComboBoxItem { Content = code });

            TeacherFilterCombo.SelectedIndex = 0;
            ApplyTeacherFilter();
            _teacherLoaded = true;
        }
        catch (Exception ex)
        {
            var userMessage = UserError.DescribeAndReport(ex, "Lehrer-Annotationen laden");
            Dialogs.Warn($"Fehler beim Laden der Lehrer-Annotationen:\n{userMessage}", "Lehrer");
        }
    }

    private void TeacherFilter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_teacherLoaded) return;
        ApplyTeacherFilter();
    }

    private void ApplyTeacherFilter()
    {
        var selectedItem = TeacherFilterCombo.SelectedItem as ComboBoxItem;
        var filterCode = selectedItem?.Content?.ToString();

        _filteredTeacherAnnotations = TeacherAnnotationGalleryService
            .FilterByCode(_allTeacherAnnotations, filterCode)
            .ToList();

        TeacherGallery.ItemsSource = _filteredTeacherAnnotations;
        TeacherCountText.Text = $"{_filteredTeacherAnnotations.Count} Annotationen";

        // Selection zuruecksetzen
        _selectedTeacherAnnotation = null;
        TeacherDetailPanel.Visibility = Visibility.Collapsed;
        BtnTeacherAddFewShot.IsEnabled = false;
        BtnTeacherDelete.IsEnabled = false;
    }

    private void TeacherThumb_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not TeacherAnnotation annotation)
            return;

        _selectedTeacherAnnotation = annotation;
        BtnTeacherAddFewShot.IsEnabled = true;
        BtnTeacherDelete.IsEnabled = true;

        // Detail-Ansicht fuellen
        TeacherDetailPanel.Visibility = Visibility.Visible;
        TeacherDetailCode.Text = annotation.VsaCode;
        TeacherDetailBeschreibung.Text = annotation.Beschreibung;
        TeacherDetailMeter.Text = $"Meter: {annotation.MeterPosition:F2}m";
        TeacherDetailClock.Text = annotation.ClockPosition.HasValue
            ? $"Uhr: {annotation.ClockPosition.Value:F1}"
            : "Uhr: –";
        TeacherDetailTool.Text = $"Tool: {annotation.ToolType}";
        TeacherDetailDate.Text = $"Erstellt: {annotation.CreatedUtc.LocalDateTime:yyyy-MM-dd HH:mm}";
        TeacherDetailId.Text = $"ID: {annotation.AnnotationId}";

        // Volles Frame laden
        var framePath = annotation.FullFramePath;
        if (!string.IsNullOrEmpty(framePath) && File.Exists(framePath))
        {
            try
            {
                var converter = new FileToImageConverter();
                TeacherDetailImage.Source = converter.Convert(framePath, typeof(BitmapImage), null,
                    CultureInfo.InvariantCulture) as BitmapImage;
            }
            catch { TeacherDetailImage.Source = null; }
        }
        else
        {
            TeacherDetailImage.Source = null;
        }
    }

    private async void TeacherAddToFewShot_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTeacherAnnotation is null) return;

        var imagePath = _selectedTeacherAnnotation.CroppedRegionPath;
        if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
            imagePath = _selectedTeacherAnnotation.FullFramePath;

        if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
        {
            Dialogs.Warn("Kein Bild fuer diese Annotation verfuegbar.", "FewShot");
            return;
        }

        try
        {
            var store = _trainingServices.CreateFewShotStore();
            await store.LoadAsync();

            var imageBytes = await File.ReadAllBytesAsync(imagePath);
            var ext = System.IO.Path.GetExtension(imagePath).ToLowerInvariant();
            var clockStr = _selectedTeacherAnnotation.ClockPosition.HasValue
                ? $"{_selectedTeacherAnnotation.ClockPosition.Value:F0} Uhr"
                : null;

            await store.AddExampleAsync(
                imageBytes, ext,
                _selectedTeacherAnnotation.VsaCode,
                _selectedTeacherAnnotation.Beschreibung,
                clockStr,
                _selectedTeacherAnnotation.MeterPosition,
                null, null,
                $"teacher:{_selectedTeacherAnnotation.AnnotationId}",
                1.0);

            Dialogs.Info(
                $"Annotation '{_selectedTeacherAnnotation.VsaCode}' als FewShot-Beispiel hinzugefuegt (quality=1.0).",
                "FewShot");
        }
        catch (Exception ex)
        {
            var userMessage = UserError.DescribeAndReport(ex, "FewShot-Lernen");
            Dialogs.Error($"Fehler: {userMessage}", "FewShot");
        }
    }

    private async void TeacherDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTeacherAnnotation is null) return;

        if (!Dialogs.ConfirmWarn(
            $"Annotation '{_selectedTeacherAnnotation.VsaCode}' bei {_selectedTeacherAnnotation.MeterPosition:F1}m wirklich loeschen?\n\n" +
            "Zugehoerige Dateien (Frame, Crop, YOLO-Label) werden ebenfalls entfernt.",
            "Annotation loeschen")) return;

        try
        {
            await _teacherGalleryService.DeleteAsync(_selectedTeacherAnnotation);

            // Galerie neu laden
            await LoadTeacherAnnotationsAsync();
        }
        catch (Exception ex)
        {
            var userMessage = UserError.DescribeAndReport(ex, "Lehrer-Annotation loeschen");
            Dialogs.Error($"Fehler beim Loeschen: {userMessage}", "Lehrer");
        }
    }

}

/// <summary>Converter: non-null → true, null → false.</summary>
public sealed class NotNullToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not null;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Wandelt einen VSA-Code in "CODE — Klartext" um (Klartext aus dem Katalog via VsaCodeResolver).
/// Ist der Wert kein bekannter Code (z.B. "nichts erkannt"), bleibt er unveraendert.
/// </summary>
public sealed class VsaCodeToTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string code || string.IsNullOrWhiteSpace(code))
            return value;
        var label = AuswertungPro.Next.Infrastructure.Ai.VsaCodeResolver.LookupLabel(code);
        return string.IsNullOrWhiteSpace(label) ? code : $"{code} — {label}";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}

/// <summary>
/// Laedt ein Bild aus dem Dateipfad in den Speicher, ohne die Datei zu sperren.
/// Verhindert File-Locking und ermoeglicht Echtzeit-Updates waehrend Self-Training.
/// </summary>
public sealed class FileToImageConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrEmpty(path))
            return null;

        if (!System.IO.File.Exists(path))
        {
            BestEffort.ReportWarning($"[FileToImage] Datei nicht gefunden: {path}");
            return null;
        }

        try
        {
            var bmp = new System.Windows.Media.Imaging.BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bmp.CreateOptions = System.Windows.Media.Imaging.BitmapCreateOptions.IgnoreImageCache;
            bmp.DecodePixelWidth = ResolveDecodePixelWidth(parameter);
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch (Exception ex)
        {
            BestEffort.ReportWarning($"[FileToImage] Fehler beim Laden: {path} → {ex.Message}");
            return null;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static int ResolveDecodePixelWidth(object? parameter)
    {
        if (parameter is int width && width > 0)
            return width;

        if (parameter is string text
            && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            && parsed > 0)
        {
            return parsed;
        }

        return 480;
    }
}
