using System;
using AuswertungPro.Next.Domain.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using AuswertungPro.Next.Application.Ai.Ollama;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Application.Ai;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Teacher;
using AuswertungPro.Next.UI.Ai.Training;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Windows;
using Infrastructure = AuswertungPro.Next.Infrastructure;
using AuswertungPro.Next.Application.Ai.Pipeline;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.Application.Ai.SelfImproving;
using AuswertungPro.Next.UI.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class TrainingCenterWindow : Window
{
    public TrainingCenterViewModel Vm { get; }

    // Pipeline-Dots und Service-Indikatoren fuer Animation
    private Ellipse[] _pipelineDots = Array.Empty<Ellipse>();
    private Border[] _serviceDots = Array.Empty<Border>();

    // Review-Services (lazy, erst bei erster Review-Aktion)
    private AuswertungPro.Next.Application.Ai.SelfImproving.ReviewQueueService? _reviewQueueService;

    // Batch-Nachtbetrieb Abbruch
    private System.Threading.CancellationTokenSource? _batchCts;

    public TrainingCenterWindow()
    {
        InitializeComponent();
        WindowStateManager.Track(this);

        Vm = new TrainingCenterViewModel(
            new TrainingCenterStore(),
            new TrainingCenterImportService());

        DataContext = Vm;

        Loaded += async (_, __) =>
        {
            await Vm.LoadAsync();
            SetupPipelineElements();
            SetupAutoScroll();

            // Review-Queue laden (falls KB vorhanden)
            _reviewQueueService = new AuswertungPro.Next.Application.Ai.SelfImproving.ReviewQueueService();
            Vm.ReviewQueueServiceRef = _reviewQueueService;
            Vm.LoadReviewQueue(_reviewQueueService);

            // Lehrer-Annotationen laden
            await LoadTeacherAnnotationsAsync();
        };

        Vm.PropertyChanged += OnVmPropertyChanged;
        // Audit R-H3 2026-04-25: Batch-CTS muss beim Schliessen gecancelt
        // werden, sonst arbeitet die Pipeline mit Refs auf disposed UI weiter
        // (Dispatcher.Invoke auf nicht mehr gebundene Vm-Properties → spaeter
        // Race-Crash). Plus Debounce-Timer abschalten, sonst feuern sie auf
        // disposed Listen.
        Closed += (_, _) =>
        {
            void Safe(string step, Action a)
            {
                try { a(); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[TrainingCenterWindow.Closed] {step}: {ex.Message}"); }
            }
            Safe("BatchCts-Cancel", () => _batchCts?.Cancel());
            Safe("ScrollDebounce-Results", () => _scrollDebounceResults?.Stop());
            Safe("ScrollDebounce-Log", () => _scrollDebounceLog?.Stop());
            Safe("Vm-Unsubscribe", () => Vm.PropertyChanged -= OnVmPropertyChanged);
        };
    }

    private void SetupPipelineElements()
    {
        _pipelineDots = new[] { Dot0, Dot1, Dot2, Dot3, Dot4, Dot5 };
        _serviceDots = new[] { SvcOsd, SvcFrame, SvcQwen, SvcCompare, SvcTech };
    }

    // Debounce-Timer fuer Auto-Scroll (verhindert Layout-Kollaps bei schnellen Batch-Updates)
    private System.Windows.Threading.DispatcherTimer? _scrollDebounceResults;
    private System.Windows.Threading.DispatcherTimer? _scrollDebounceLog;
    private bool _logAutoScrollEnabled = true;

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
            // Nur auto-scrollen wenn User am Ende ist (nicht wenn manuell hochgescrollt)
            if (_logAutoScrollEnabled)
                SelfTrainingLogList.ScrollToEnd();
        };
        // Auto-Scroll deaktivieren wenn User manuell scrollt
        SelfTrainingLogList.AddHandler(System.Windows.Controls.Primitives.ScrollBar.ScrollEvent,
            new System.Windows.Controls.Primitives.ScrollEventHandler((_, se) =>
            {
                // Wenn User manuell scrollt → Auto-Scroll aus
                // Wenn User ganz nach unten scrollt → Auto-Scroll wieder an
                if (SelfTrainingLogList is System.Windows.Controls.TextBox tb)
                {
                    _logAutoScrollEnabled = tb.VerticalOffset >= tb.ExtentHeight - tb.ViewportHeight - 20;
                }
            }), handledEventsToo: true);

        // EchtzeitLogText wird vom ViewModel aktualisiert — Auto-Scroll via PropertyChanged
        Vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TrainingCenterViewModel.EchtzeitLogText))
            {
                _scrollDebounceLog.Stop();
                _scrollDebounceLog.Start();
            }
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

    // ── Review Queue Event Handlers ──

    // Shared HttpClient — verhindert Socket-Leak bei vielen Review-Aktionen (siehe
    // denselben Fix in PlayerWindow.xaml.cs).
    private static readonly System.Net.Http.HttpClient _feedbackHttpClient =
        new() { Timeout = TimeSpan.FromMinutes(2) };

    /// <summary>Erzeugt FeedbackIngestionService mit optionalem KbManager fuer KB-Re-Indexierung.</summary>
    private static AuswertungPro.Next.Infrastructure.Ai.SelfImproving.FeedbackIngestionService CreateFeedbackService(
        AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase.KnowledgeBaseContext db)
    {
        var logger = new AuswertungPro.Next.Infrastructure.Ai.QualityGate.ValidationLogger(db.Connection);
        var weights = new AuswertungPro.Next.Infrastructure.Ai.QualityGate.WeightLearningService(db.Connection);

        // KbManager optional — wenn Ollama offline, wird nur geloggt
        AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase.KnowledgeBaseManager? kbManager = null;
        try
        {
            var cfg = Ai.Ollama.OllamaConfigExtensions.Load();
            var embedder = new AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase.EmbeddingService(_feedbackHttpClient, cfg);
            kbManager = new AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase.KnowledgeBaseManager(db, embedder);
        }
        catch { /* Ollama nicht verfuegbar — Feedback wird geloggt, KB-Update uebersprungen */ }

        return new AuswertungPro.Next.Infrastructure.Ai.SelfImproving.FeedbackIngestionService(logger, weights, kbManager);
    }

    private async void ReviewApprove_Click(object sender, RoutedEventArgs e)
    {
        if (Vm.SelectedReviewItem is null) return;
        _reviewQueueService ??= new AuswertungPro.Next.Application.Ai.SelfImproving.ReviewQueueService();
        try
        {
            using var db = new AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase.KnowledgeBaseContext();
            var feedback = CreateFeedbackService(db);
            await Vm.ApproveReviewItemAsync(Vm.SelectedReviewItem, feedback, _reviewQueueService);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Fehler beim Akzeptieren: {ex.Message}",
                "Review", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>Ablehnen ohne Korrektur — verwirft KI-Vorschlag, entfernt aus Queue.</summary>
    private void ReviewReject_Click(object sender, RoutedEventArgs e)
    {
        if (Vm.SelectedReviewItem is null)
        {
            MessageBox.Show("Kein Item ausgewaehlt.", "Review",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var item = Vm.SelectedReviewItem;
        _reviewQueueService ??= new AuswertungPro.Next.Application.Ai.SelfImproving.ReviewQueueService();
        _reviewQueueService.Remove(item.Id);
        Vm.ReviewQueue.Remove(item);
        Vm.ReviewQueueCount = Vm.ReviewQueue.Count;
        Vm.ReviewStatusText = $"Abgelehnt: {item.SuggestedCode} | {Vm.ReviewQueueCount} verbleibend";
        Vm.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] Review abgelehnt: {item.Label}\n");
    }

    /// <summary>Korrigieren + Speichern — Bildtraining-Fenster mit vorgeladenem Frame.</summary>
    private async void ReviewCorrect_Click(object sender, RoutedEventArgs e)
    {
        if (Vm.SelectedReviewItem is null)
        {
            MessageBox.Show("Kein Item ausgewaehlt.", "Review",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var item = Vm.SelectedReviewItem;
        var framePath = item.SelfTrainingFramePath;
        if (string.IsNullOrWhiteSpace(framePath) || !System.IO.File.Exists(framePath))
        {
            MessageBox.Show("Kein Frame-Bild fuer dieses Item vorhanden.", "Review",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // V4.2 Nachbesserung: Korrektur im ImageAnnotationWindow (analog Bildtraining).
        // Pascal kann dort Code waehlen + BBox zeichnen wie beim manuellen Annotieren.
        // Trick: Wir kopieren den Frame in einen temporaeren 1-Bild-Ordner und laden
        // das ImageAnnotationViewModel mit diesem Ordner. Nach Fenster-Close wird das
        // Review-Item aus der Queue entfernt.
        string? tempDir = null;
        try
        {
            tempDir = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "SewerStudio", "review_correction",
                Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(tempDir);
            var copyPath = System.IO.Path.Combine(tempDir, System.IO.Path.GetFileName(framePath));
            System.IO.File.Copy(framePath, copyPath, true);

            AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase.KnowledgeBaseManager? kbManager = null;
            AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase.KbDeduplicationService? dedup = null;
            try
            {
                var kbCtx = new AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase.KnowledgeBaseContext();
                var ollamaConfig = Ai.Ollama.OllamaConfigExtensions.Load();
                var http = new System.Net.Http.HttpClient { Timeout = ollamaConfig.RequestTimeout };
                var embedder = new AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase.EmbeddingService(http, ollamaConfig);
                kbManager = new AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase.KnowledgeBaseManager(kbCtx, embedder);
                var retrieval = new AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase.RetrievalService(kbCtx, embedder);
                dedup = new AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase.KbDeduplicationService(embedder, retrieval);
            }
            catch { /* KB optional — Annotation wird trotzdem gespeichert */ }

            AuswertungPro.Next.Infrastructure.Ai.Pipeline.VisionPipelineClient? sidecar = null;
            // Phase 5.1.B Etappe 3.J: via DI-Container.
            try
            {
                var sidecarSvc = App.Resolve<Ai.PythonSidecarService>();
                var pipelineCfg = App.Resolve<AuswertungPro.Next.Application.Ai.PipelineConfig>();
                if (sidecarSvc.IsAvailable)
                {
                    var sidecarHttp = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(10) };
                    sidecar = new AuswertungPro.Next.Infrastructure.Ai.Pipeline.VisionPipelineClient(pipelineCfg.SidecarUrl, sidecarHttp);
                }
            }
            catch { /* Sidecar optional */ }

            var vm = new ImageAnnotationViewModel(kbManager, dedup, sidecar);
            vm.LoadFolder(tempDir);
            if (!string.IsNullOrWhiteSpace(item.SuggestedCode))
                vm.VsaCode = item.SuggestedCode;
            vm.HaltungName = item.SelfTrainingCaseId ?? "Review";

            var win = new ImageAnnotationWindow(vm) { Owner = this };
            win.ShowDialog();

            // Nach Annotation: Item aus Queue entfernen (als reviewed).
            _reviewQueueService ??= new AuswertungPro.Next.Application.Ai.SelfImproving.ReviewQueueService();
            _reviewQueueService.Remove(item.Id);
            Vm.ReviewQueue.Remove(item);
            Vm.ReviewQueueCount = Vm.ReviewQueue.Count;
            Vm.ReviewStatusText = $"Korrigiert im Bildtraining | {Vm.ReviewQueueCount} verbleibend";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Fehler beim Oeffnen der Korrektur: {ex.Message}",
                "Review", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            // Temp-Ordner nach 1 Stunde aufraeumen — Annotationen werden ohnehin
            // im Knowledge-Ordner persistiert, nicht im Temp.
            if (tempDir is not null)
            {
                try { System.IO.Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }

        await Task.CompletedTask;
    }

    // ── Lehrer-Annotationen Tab Event Handlers ──

    private List<TeacherAnnotation> _allTeacherAnnotations = new();
    private List<TeacherAnnotation> _filteredTeacherAnnotations = new();
    private TeacherAnnotation? _selectedTeacherAnnotation;
    private bool _teacherLoaded;

    private async void TeacherRefresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadTeacherAnnotationsAsync();
    }

    private async Task LoadTeacherAnnotationsAsync()
    {
        try
        {
            var all = await TeacherAnnotationStore.LoadAsync();

            // Bereits als FewShot uebernommene Annotationen ausfiltern
            var trainedIds = await GetTrainedAnnotationIdsAsync();
            _allTeacherAnnotations = trainedIds.Count > 0
                ? all.Where(a => !trainedIds.Contains(a.AnnotationId)).ToList()
                : all;

            // Filter-ComboBox mit vorhandenen VSA-Codes fuellen
            var codes = _allTeacherAnnotations
                .Select(a => a.VsaCode)
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c)
                .ToList();

            TeacherFilterCombo.Items.Clear();
            TeacherFilterCombo.Items.Add(new ComboBoxItem { Content = "Alle", IsSelected = true });
            foreach (var code in codes)
                TeacherFilterCombo.Items.Add(new ComboBoxItem { Content = code });

            TeacherFilterCombo.SelectedIndex = 0;
            ApplyTeacherFilter();
            _teacherLoaded = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Fehler beim Laden der Lehrer-Annotationen:\n{ex.Message}",
                "Lehrer", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>
    /// Laedt die IDs aller Lehrer-Annotationen die bereits als FewShot-Beispiel uebernommen wurden.
    /// Format im FewShot-Store: Source = "teacher:{annotationId}"
    /// </summary>
    private static async Task<HashSet<string>> GetTrainedAnnotationIdsAsync()
    {
        try
        {
            var store = new AuswertungPro.Next.Application.Ai.Training.FewShotExampleStore();
            await store.LoadAsync();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var ex in store.Examples)
            {
                if (ex.Source is not null && ex.Source.StartsWith("teacher:", StringComparison.Ordinal))
                    ids.Add(ex.Source.Substring("teacher:".Length));
            }
            return ids;
        }
        catch
        {
            return new HashSet<string>();
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

        _filteredTeacherAnnotations = (filterCode == "Alle" || string.IsNullOrEmpty(filterCode))
            ? new List<TeacherAnnotation>(_allTeacherAnnotations)
            : _allTeacherAnnotations
                .Where(a => a.VsaCode.Equals(filterCode, StringComparison.OrdinalIgnoreCase))
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
            MessageBox.Show("Kein Bild fuer diese Annotation verfuegbar.",
                "FewShot", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var store = new FewShotExampleStore();
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

            MessageBox.Show(
                $"Annotation '{_selectedTeacherAnnotation.VsaCode}' als FewShot-Beispiel hinzugefuegt (quality=1.0).",
                "FewShot", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Fehler: {ex.Message}", "FewShot", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void TeacherDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedTeacherAnnotation is null) return;

        var result = MessageBox.Show(
            $"Annotation '{_selectedTeacherAnnotation.VsaCode}' bei {_selectedTeacherAnnotation.MeterPosition:F1}m wirklich loeschen?\n\n" +
            "Zugehoerige Dateien (Frame, Crop, YOLO-Label) werden ebenfalls entfernt.",
            "Annotation loeschen",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        try
        {
            // Dateien loeschen (best effort)
            TryDeleteFile(_selectedTeacherAnnotation.FullFramePath);
            TryDeleteFile(_selectedTeacherAnnotation.CroppedRegionPath);
            TryDeleteFile(_selectedTeacherAnnotation.YoloAnnotationPath);

            // Aus Store entfernen (neu laden, filtern, speichern)
            var all = await TeacherAnnotationStore.LoadAsync();
            var remaining = all.Where(a => a.AnnotationId != _selectedTeacherAnnotation.AnnotationId).ToList();

            // Direkt in JSON schreiben (Store hat keine Delete-Methode — Append-only umgehen)
            var storePath = System.IO.Path.Combine(Ai.KnowledgeRoot.GetRoot(), "teacher_annotations.json");
            var json = System.Text.Json.JsonSerializer.Serialize(remaining,
                new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                });
            await File.WriteAllTextAsync(storePath, json);

            // Galerie neu laden
            await LoadTeacherAnnotationsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Fehler beim Loeschen: {ex.Message}",
                "Lehrer", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void RemoveSelectedCases_Click(object sender, RoutedEventArgs e)
    {
        // CasesGrid wird dynamisch aus dem XAML-Baum gesucht (x:Name noch nicht definiert)
        var grid = this.FindName("CasesGrid") as System.Windows.Controls.DataGrid;
        var selected = grid?.SelectedItems.Cast<TrainingCase>().ToList();
        if (selected is null or { Count: 0 }) return;
        await Vm.RemoveSelectedCasesCommand.ExecuteAsync(selected);
    }

    private async void RemoveAllCases_Click(object sender, RoutedEventArgs e)
    {
        await Vm.RemoveAllCasesCommand.ExecuteAsync(null);
    }

    private static void TryDeleteFile(string? path)
    {
        try
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                File.Delete(path);
        }
        catch { /* best effort */ }
    }

    // ═══ Batch-Nachtbetrieb + Video-Selbsttraining + Benchmark ═════════

    private void OpenImageAnnotation_Click(object sender, RoutedEventArgs e)
    {
        AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase.KnowledgeBaseManager? kbManager = null;
        AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase.KbDeduplicationService? dedup = null;

        try
        {
            var kbCtx = new AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase.KnowledgeBaseContext();
            var ollamaConfig = Ai.Ollama.OllamaConfigExtensions.Load();
            var http = new System.Net.Http.HttpClient { Timeout = ollamaConfig.RequestTimeout };
            var embedder = new AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase.EmbeddingService(http, ollamaConfig);
            kbManager = new AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase.KnowledgeBaseManager(kbCtx, embedder);
            var retrieval = new AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase.RetrievalService(kbCtx, embedder);
            dedup = new AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase.KbDeduplicationService(embedder, retrieval);
        }
        catch { /* KB nicht verfuegbar — Annotationen werden trotzdem als TeacherAnnotation gespeichert */ }

        // Sidecar-Client fuer SAM-Segmentierung — Phase 5.1.B Etappe 3.J: via DI.
        AuswertungPro.Next.Infrastructure.Ai.Pipeline.VisionPipelineClient? sidecar = null;
        try
        {
            var pipelineCfg = App.Resolve<AuswertungPro.Next.Application.Ai.PipelineConfig>();
            sidecar = new AuswertungPro.Next.Infrastructure.Ai.Pipeline.VisionPipelineClient(pipelineCfg.SidecarUrl);
        }
        catch { /* Sidecar optional */ }

        var vm = new ImageAnnotationViewModel(kbManager, dedup, sidecar);
        new ImageAnnotationWindow(vm) { Owner = this }.Show();
    }

    private void OpenVideoTrainingReview_Click(object sender, RoutedEventArgs e)
    {
        // Phase 5.1.B Etappe 3.K: via DI-Container.
        var diagnostics = App.Resolve<AuswertungPro.Next.Application.Diagnostics.DiagnosticsOptions>();
        var codeCatalog = App.Resolve<AuswertungPro.Next.Application.Protocol.ICodeCatalogProvider>();
        var winCanImport = App.Resolve<AuswertungPro.Next.Application.Import.IWinCanDbImportService>();
        var ibakImport = App.Resolve<AuswertungPro.Next.Application.Import.IIbakImportService>();

        // pdftotext-Pfad aus den App-Einstellungen setzen
        Ai.Training.Services.PdfProtocolTableParser.PdfToTextExePath = diagnostics.ExplicitPdfToTextPath;

        var cfg = Ai.AiRuntimeConfigLoader.Load();
        if (!cfg.Enabled) { MessageBox.Show("KI ist deaktiviert.", "Video-Blindtest"); return; }

        // Factory: Erstellt pro Analyse-Durchlauf frischen HttpClient + Pipeline
        // (HttpClient kann nicht wiederverwendet werden nachdem Headers geaendert wurden)
        Func<AuswertungPro.Next.Infrastructure.Ai.Training.VideoSelfTrainingOrchestrator> orchestratorFactory = () =>
        {
            var allowedSet = new System.Collections.Generic.HashSet<string>(
                codeCatalog.AllowedCodes(), StringComparer.OrdinalIgnoreCase);
            var plausibility = new AuswertungPro.Next.Application.Ai.RuleBasedAiSuggestionPlausibilityService(allowedSet);
            var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(30) };
            var pipeline = new Ai.VideoAnalysisPipelineService(cfg, plausibility, http);
            var meterTimeline = new AuswertungPro.Next.Infrastructure.Ai.Training.MeterTimelineService(cfg);
            return new AuswertungPro.Next.Infrastructure.Ai.Training.VideoSelfTrainingOrchestrator(pipeline, meterTimeline);
        };

        var vm = new VideoTrainingReviewViewModel(orchestratorFactory, winCanImport, ibakImport);
        new VideoTrainingReviewWindow(vm) { Owner = this }.Show();
    }

    /// <summary>V4.2 Qualitaetshebel: Frozen Eval-Set durch Qwen laufen, F1/Precision/Recall pro Code loggen.</summary>
    private async void RunEvalSet_Click(object sender, RoutedEventArgs e)
    {
        // Phase 5.1.B Etappe 3.K: kein ServiceProvider-Zugriff mehr noetig — dieser Block
        // hat sp gar nicht benutzt; nur die Eingangs-Pruefung war dafuer da.
        var cfg = Ai.AiRuntimeConfigLoader.Load();
        if (!cfg.Enabled)
        {
            MessageBox.Show("KI ist deaktiviert.", "Eval-Set");
            return;
        }

        var evalDir = System.IO.Path.Combine(@"C:\KI_BRAIN", "eval_set");
        if (!System.IO.Directory.Exists(System.IO.Path.Combine(evalDir, "images")))
        {
            MessageBox.Show(
                $"Eval-Set nicht gefunden: {evalDir}\n\n" +
                "Erzeuge es zuerst via Profile extrahieren → Eval-Set.",
                "Eval-Set", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var btn = sender as System.Windows.Controls.Button;
        if (btn is not null) { btn.IsEnabled = false; btn.Content = "\U0001F4CF Eval laeuft..."; }

        try
        {
            var ollamaClient = cfg.CreateOllamaClient();
            var qwen = new AuswertungPro.Next.Infrastructure.Ai.EnhancedVisionAnalysisService(
                ollamaClient, cfg.VisionModel, cfg.ReferenceVisionModel, cfg.OllamaNumCtx);
            var runner = new AuswertungPro.Next.Infrastructure.Ai.Training.EvalRunnerService(qwen);

            Vm.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] [Eval] Start Eval-Set-Lauf...\n");

            var result = await Task.Run(() => runner.RunAsync(evalDir,
                progress: (done, total, msg) =>
                {
                    // UI-Thread: Log jeder 5. Schritt
                    if (done % 5 == 0 || done == total)
                    {
                        Dispatcher.Invoke(() =>
                            Vm.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] [Eval] {msg}\n"));
                    }
                }));

            Vm.AppendToLogText(
                $"[{DateTime.Now:HH:mm:ss}] [Eval] Fertig — " +
                $"{result.CorrectPredictions}/{result.TotalFrames} richtig | " +
                $"Precision={result.OverallPrecision:P1} " +
                $"Recall={result.OverallRecall:P1} " +
                $"F1={result.OverallF1:P1}\n" +
                $"[{DateTime.Now:HH:mm:ss}] [Eval] CSV: {result.CsvPath}\n");

            MessageBox.Show(
                $"Eval-Set fertig\n\n" +
                $"Frames: {result.TotalFrames}\n" +
                $"Richtig: {result.CorrectPredictions}\n" +
                $"F1: {result.OverallF1:P1}\n" +
                $"Precision: {result.OverallPrecision:P1}\n" +
                $"Recall: {result.OverallRecall:P1}\n\n" +
                $"CSV: {result.CsvPath}",
                "Eval-Set", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Eval-Set Fehler: {ex.Message}", "Eval-Set",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            Vm.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] [Eval] Fehler: {ex.Message}\n");
        }
        finally
        {
            if (btn is not null) { btn.IsEnabled = true; btn.Content = "\U0001F4CF Eval-Set laufen"; }
        }
    }

    private void OpenBenchmark_Click(object sender, RoutedEventArgs e)
    {
        // Phase 5.1.B Etappe 3.K: via DI-Container.
        var codeCatalog = App.Resolve<AuswertungPro.Next.Application.Protocol.ICodeCatalogProvider>();
        var winCanImport = App.Resolve<AuswertungPro.Next.Application.Import.IWinCanDbImportService>();
        var ibakImport = App.Resolve<AuswertungPro.Next.Application.Import.IIbakImportService>();
        var sidecarSvc = App.Resolve<Ai.PythonSidecarService>();
        var pipelineCfg = App.Resolve<AuswertungPro.Next.Application.Ai.PipelineConfig>();

        var cfg = Ai.AiRuntimeConfigLoader.Load();
        if (!cfg.Enabled) { MessageBox.Show("KI ist deaktiviert.", "Benchmark"); return; }

        var allowedSet = new System.Collections.Generic.HashSet<string>(
            codeCatalog.AllowedCodes(), StringComparer.OrdinalIgnoreCase);
        var plausibility = new AuswertungPro.Next.Application.Ai.RuleBasedAiSuggestionPlausibilityService(allowedSet);
        var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        var pipeline = new Ai.VideoAnalysisPipelineService(cfg, plausibility, http);
        var protocolLoader = new Ai.Training.Services.ProtocolLoaderFactory(winCanImport, ibakImport);
        var setStore = new BenchmarkSetStore();
        var metricsStore = new BenchmarkMetricsStore();
        var meterTimeline = new AuswertungPro.Next.Infrastructure.Ai.Training.MeterTimelineService(cfg);
        var orchestrator = new AuswertungPro.Next.Infrastructure.Ai.Training.VideoSelfTrainingOrchestrator(pipeline, meterTimeline);

        // V4.1: Batch-Pipeline (YOLO Batch → Filter → Qwen ×6 parallel)
        if (sidecarSvc.IsAvailable)
        {
            var sidecarHttp = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            var sidecarClient = new AuswertungPro.Next.Infrastructure.Ai.Pipeline.VisionPipelineClient(pipelineCfg.SidecarUrl, sidecarHttp);
            var ollamaClient = cfg.CreateOllamaClient();
            var qwenVision = new AuswertungPro.Next.Infrastructure.Ai.EnhancedVisionAnalysisService(ollamaClient, cfg.VisionModel, cfg.ReferenceVisionModel, cfg.OllamaNumCtx);
            orchestrator.BatchPipeline = new AuswertungPro.Next.Infrastructure.Ai.Pipeline.BatchPipelineService(
                sidecarClient, qwenVision, pipelineCfg,
                cfg.FfmpegPath ?? AuswertungPro.Next.Application.Ai.FfmpegLocator.ResolveFfmpeg());
        }

        var runner = new BenchmarkRunner(setStore, metricsStore, orchestrator, protocolLoader.LoadProtocolAsync);
        var vm = new BenchmarkViewModel(setStore, runner, metricsStore, protocolLoader);
        new BenchmarkWindow(vm) { Owner = this }.Show();
    }

    private async void StartBatchNightRun_Click(object sender, RoutedEventArgs e)
    {
        // Phase 5.1.B Etappe 3.K: via DI-Container.
        var diagnostics = App.Resolve<AuswertungPro.Next.Application.Diagnostics.DiagnosticsOptions>();
        var codeCatalog = App.Resolve<AuswertungPro.Next.Application.Protocol.ICodeCatalogProvider>();
        var winCanImport = App.Resolve<AuswertungPro.Next.Application.Import.IWinCanDbImportService>();
        var ibakImport = App.Resolve<AuswertungPro.Next.Application.Import.IIbakImportService>();
        var sidecarSvc = App.Resolve<Ai.PythonSidecarService>();
        var pipelineCfg = App.Resolve<AuswertungPro.Next.Application.Ai.PipelineConfig>();

        // pdftotext-Pfad aus den App-Einstellungen setzen
        Ai.Training.Services.PdfProtocolTableParser.PdfToTextExePath = diagnostics.ExplicitPdfToTextPath;

        var cfg = Ai.AiRuntimeConfigLoader.Load();
        if (!cfg.Enabled) { MessageBox.Show("KI ist deaktiviert.", "Batch-Nachtbetrieb"); return; }

        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Ordner mit Haltungen waehlen" };
        if (dlg.ShowDialog() != true) return;

        var confirm = MessageBox.Show(
            $"Batch-Nachtbetrieb starten?\n\nOrdner: {dlg.FolderName}\n\nAlle Haltungen mit Video + PDF werden automatisch verarbeitet.\nDas kann mehrere Stunden dauern.",
            "Batch-Nachtbetrieb", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.OK) return;

        // V4.2 Nachbesserung: Test-Cap fuer begrenzte Laeufe.
        var limitInput = Microsoft.VisualBasic.Interaction.InputBox(
            "Wie viele Haltungen verarbeiten?\n\n" +
            "  0 = alle Haltungen im Ordner (voller Batch)\n" +
            "  2 = empfohlen fuer ersten V4.2-Testlauf\n",
            "Batch-Umfang",
            "2");
        if (string.IsNullOrWhiteSpace(limitInput)) return;
        if (!int.TryParse(limitInput, out var maxHaltungen) || maxHaltungen < 0)
        {
            MessageBox.Show("Ungueltige Zahl — Abbruch.", "Batch-Nachtbetrieb");
            return;
        }

        var allowedSet = new System.Collections.Generic.HashSet<string>(
            codeCatalog.AllowedCodes(), StringComparer.OrdinalIgnoreCase);
        var plausibility = new AuswertungPro.Next.Application.Ai.RuleBasedAiSuggestionPlausibilityService(allowedSet);
        var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        var pipeline = new Ai.VideoAnalysisPipelineService(cfg, plausibility, http);
        var protocolLoader = new Ai.Training.Services.ProtocolLoaderFactory(winCanImport, ibakImport);
        var meterTimeline = new AuswertungPro.Next.Infrastructure.Ai.Training.MeterTimelineService(cfg);
        var videoOrch = new AuswertungPro.Next.Infrastructure.Ai.Training.VideoSelfTrainingOrchestrator(pipeline, meterTimeline);

        // V4.1: Batch-Pipeline fuer den initialen Orchestrator
        if (sidecarSvc.IsAvailable)
        {
            var sidecarHttp = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            var sidecarClient = new AuswertungPro.Next.Infrastructure.Ai.Pipeline.VisionPipelineClient(pipelineCfg.SidecarUrl, sidecarHttp);
            var ollamaClient = cfg.CreateOllamaClient();
            var qwenVision = new AuswertungPro.Next.Infrastructure.Ai.EnhancedVisionAnalysisService(ollamaClient, cfg.VisionModel, cfg.ReferenceVisionModel, cfg.OllamaNumCtx);
            videoOrch.BatchPipeline = new AuswertungPro.Next.Infrastructure.Ai.Pipeline.BatchPipelineService(
                sidecarClient, qwenVision, pipelineCfg,
                cfg.FfmpegPath ?? AuswertungPro.Next.Application.Ai.FfmpegLocator.ResolveFfmpeg());
        }

        Ai.KnowledgeBase.KbEnrichmentService enrichment;
        try
        {
            var kbHttp = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            var ollamaConfig = Ai.Ollama.OllamaConfigExtensions.Load();
            var kbCtx = new AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase.KnowledgeBaseContext();
            var embedder = new AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase.EmbeddingService(kbHttp, ollamaConfig);
            var retrieval = new AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase.RetrievalService(kbCtx, embedder);
            var kbManager = new AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase.KnowledgeBaseManager(kbCtx, embedder);
            var dedup = new AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase.KbDeduplicationService(embedder, retrieval);
            // H3: Review-Queue fuer Mittel-Confidence-Samples (0.65 <= conf < 0.85).
            _reviewQueueService ??= new AuswertungPro.Next.Application.Ai.SelfImproving.ReviewQueueService();
            enrichment = new Ai.KnowledgeBase.KbEnrichmentService(
                kbManager, dedup, log: null, reviewQueue: _reviewQueueService);
        }
        catch (Exception ex) { MessageBox.Show($"KB-Fehler: {ex.Message}"); return; }

        // Sidecar-Pfad fuer Auto-Restart bei Crash
        var sidecarDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "sidecar");
        if (!System.IO.Directory.Exists(sidecarDir))
            sidecarDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "sidecar");
        // Factory fuer parallele Pipeline-Instanzen: Jede Haltung bekommt ihren eigenen
        // HttpClient + Pipeline + Orchestrator (VideoSelfTrainingOrchestrator hat internen State)
        Func<AuswertungPro.Next.Infrastructure.Ai.Training.VideoSelfTrainingOrchestrator> orchestratorFactory = () =>
        {
            var pHttp = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(30) };
            var pPipeline = new Ai.VideoAnalysisPipelineService(cfg, plausibility, pHttp);
            var pTimeline = new AuswertungPro.Next.Infrastructure.Ai.Training.MeterTimelineService(cfg);
            var orch = new AuswertungPro.Next.Infrastructure.Ai.Training.VideoSelfTrainingOrchestrator(pPipeline, pTimeline);

            // V4.1: Batch-Pipeline (YOLO Batch → Filter → Qwen ×6 parallel)
            if (sidecarSvc.IsAvailable)
            {
                var sidecarHttp = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(10) };
                var sidecarClient = new AuswertungPro.Next.Infrastructure.Ai.Pipeline.VisionPipelineClient(pipelineCfg.SidecarUrl, sidecarHttp);
                var ollamaClient = cfg.CreateOllamaClient();
                var qwenVision = new AuswertungPro.Next.Infrastructure.Ai.EnhancedVisionAnalysisService(ollamaClient, cfg.VisionModel, cfg.ReferenceVisionModel, cfg.OllamaNumCtx);
                orch.BatchPipeline = new AuswertungPro.Next.Infrastructure.Ai.Pipeline.BatchPipelineService(
                    sidecarClient, qwenVision, pipelineCfg,
                    cfg.FfmpegPath ?? AuswertungPro.Next.Application.Ai.FfmpegLocator.ResolveFfmpeg());
            }

            return orch;
        };

        // V4.2 Phase 1.4: UncertaintySamplingService auf den Fenster-lokalen ReviewQueueService mappen.
        _reviewQueueService ??= new AuswertungPro.Next.Application.Ai.SelfImproving.ReviewQueueService();
        var uncertaintySampler = new AuswertungPro.Next.Application.Ai.SelfImproving.UncertaintySamplingService(_reviewQueueService);

        var batchOrch = new Ai.Training.BatchSelfTrainingOrchestrator(
            videoOrch, protocolLoader, enrichment, sidecarDir: sidecarDir,
            orchestratorFactory: orchestratorFactory,
            uncertaintySampler: uncertaintySampler,
            sidecarUrl: pipelineCfg.SidecarUrl.ToString());
        var request = new Ai.Training.Models.BatchSelfTrainingRequest
        {
            ExportRootPath = dlg.FolderName,
            MaxHaltungen = maxHaltungen,
            // V4.3: Checkbox "Alle erneut durchlaufen" (oben beim Selbsttraining-Button) gilt auch hier.
            // Wenn aktiv → bereits verarbeitete Haltungen nochmals durchlaufen.
            SkipAlreadyProcessed = !Vm.ForceRerunAll
        };

        var btnBatch = this.FindName("BtnBatchNight") as System.Windows.Controls.Button;
        if (btnBatch is not null) { btnBatch.IsEnabled = false; btnBatch.Content = "Batch laeuft..."; }
        BtnBatchCancel.Visibility = Visibility.Visible;

        _batchCts?.Dispose();
        _batchCts = new System.Threading.CancellationTokenSource();

        if (TryCreateKnowledgeBaseSnapshot(out var snapshotInfo))
            Vm.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] [KB] Snapshot: {snapshotInfo}\n");
        else
            Vm.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] [KB] Snapshot uebersprungen: {snapshotInfo}\n");

        var sampleIdsBefore = new HashSet<string>(StringComparer.Ordinal);
        var trainingCountBefore = 0;
        var trainingBboxBefore = 0;
        try
        {
            var before = await TrainingSamplesStore.LoadAsync();
            trainingCountBefore = before.Count;
            trainingBboxBefore = before.Count(s => s.HasBbox);
            foreach (var sample in before)
            {
                if (!string.IsNullOrWhiteSpace(sample.SampleId))
                    sampleIdsBefore.Add(sample.SampleId);
            }
        }
        catch
        {
            // Training-Sample-Statistik ist optional.
        }

        Vm.LogText = ""; // Log leeren vor Batch-Start
        var progress = new Progress<Ai.Training.Models.BatchSelfTrainingProgress>(p =>
        {
            Vm.StatusText = p.Status;

            // Alles ins Live-Log schreiben damit der User sieht was passiert
            var ts = $"[{DateTime.Now:HH:mm:ss}]";
            var phase = p.Phase ?? "";
            Vm.AppendToLogText($"{ts} [{p.CurrentIndex}/{p.TotalHaltungen}] {p.Status}\n");

            // Gesamtstatistik nach Ergebnis-Zeilen
            if (p.RunningStats is { } s && phase == "Ergebnis")
            {
                Vm.AppendToLogText($"    Gesamt: F1={s.F1:P0} | TP:{s.TruePositives} FN:{s.FalseNegatives} FP:{s.FalsePositives} | KB:+{s.KbIndexed}\n");
            }

            // Log-Textbox automatisch nach unten scrollen
            LogTextBox?.ScrollToEnd();

            if (p.EstimatedRemaining.HasValue)
                Title = $"Training Center — Batch {p.CurrentIndex}/{p.TotalHaltungen} — {p.EstimatedRemaining.Value.Hours}h {p.EstimatedRemaining.Value.Minutes}min";
        });

        try
        {
            var result = await Task.Run(() => batchOrch.RunAsync(request, progress, _batchCts!.Token));

            // V4.2 Nachbesserung: Review-Queue nach Batch neu laden + Counter-Log ins UI.
            var queueCountBefore = Vm.ReviewQueueCount;
            Vm.LoadReviewQueue(_reviewQueueService);
            var queueDelta = Vm.ReviewQueueCount - queueCountBefore;
            Vm.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] [V4.2] Review-Queue: {queueDelta:+#;-#;0} Items, total {Vm.ReviewQueueCount}\n");

            var s = result.FinalStats;
            string retrainSummary;
            var trainingCountAfter = trainingCountBefore;
            var trainingBboxAfter = trainingBboxBefore;
            var newSampleCount = 0;
            var newVideoTimestampCount = 0;
            var newBboxCount = 0;
            double? newTimeMin = null;
            double? newTimeMax = null;
            var timeExamples = "n/a";

            try
            {
                Vm.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] [YOLO] Auto-Retrain Pruefung gestartet...\n");

                using var retrainHttp = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(90) };
                var retrainClient = new AuswertungPro.Next.Infrastructure.Ai.Pipeline.VisionPipelineClient(pipelineCfg.SidecarUrl, retrainHttp);
                var benchmarkSetStore = new AuswertungPro.Next.Application.Ai.Training.BenchmarkSetStore();
                var benchmarkMetricsStore = new AuswertungPro.Next.Application.Ai.Training.BenchmarkMetricsStore();
                var benchmarkRunner = new AuswertungPro.Next.Infrastructure.Ai.Training.BenchmarkRunner(
                    benchmarkSetStore,
                    benchmarkMetricsStore,
                    videoOrch,
                    protocolLoader.LoadProtocolAsync);

                var retrainOrchestrator = new AuswertungPro.Next.Infrastructure.Ai.Training.YoloRetrainOrchestrator(
                    retrainClient,
                    new AuswertungPro.Next.Infrastructure.Ai.Training.YoloDatasetExportService(),
                    benchmarkRunner,
                    benchmarkMetricsStore,
                    sidecarDir,
                    msg => Vm.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] [YOLO] {msg}\n"));

                var retrain = await retrainOrchestrator.RunIfEligibleAsync(ct: _batchCts!.Token);
                retrainSummary = retrain.Deployed
                    ? $"Deploy OK ({System.IO.Path.GetFileName(retrain.ActiveModelPath)}, F1={retrain.BenchmarkF1:P1})"
                    : retrain.StatusText;
            }
            catch (Exception retrainEx)
            {
                retrainSummary = $"Auto-Retrain Fehler: {retrainEx.Message}";
                Vm.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] [YOLO] {retrainSummary}\n");
            }

            // ── Phase 3: LoRA-Training (nach YOLO-Retrain) ──
            // V4.2: LoRA-Auto-Training default DEAKTIVIERT wegen OOM-Problem bei >9000 Samples.
            // Opt-in via Umgebungsvariable SEWERSTUDIO_ENABLE_LORA_AUTOTRAIN=1.
            string loraSummary;
            var loraEnabled = Environment.GetEnvironmentVariable("SEWERSTUDIO_ENABLE_LORA_AUTOTRAIN") == "1";
            if (!loraEnabled)
            {
                loraSummary = "LoRA-Auto-Training deaktiviert (SEWERSTUDIO_ENABLE_LORA_AUTOTRAIN nicht gesetzt)";
                Vm.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] [LoRA] {loraSummary}\n");
            }
            else
            {
                try
                {
                    Vm.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] [LoRA] Qwen LoRA-Training Pruefung...\n");

                    using var loraHttp = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(120) };
                    var loraClient = new AuswertungPro.Next.Infrastructure.Ai.Pipeline.VisionPipelineClient(pipelineCfg.SidecarUrl, loraHttp);
                    var ollamaConfig = Ai.Ollama.OllamaConfigExtensions.Load();

                    var loraBenchmarkSetStore = new AuswertungPro.Next.Application.Ai.Training.BenchmarkSetStore();
                    var loraBenchmarkMetricsStore = new AuswertungPro.Next.Application.Ai.Training.BenchmarkMetricsStore();
                    var loraBenchmarkRunner = new AuswertungPro.Next.Infrastructure.Ai.Training.BenchmarkRunner(
                        loraBenchmarkSetStore,
                        loraBenchmarkMetricsStore,
                        videoOrch,
                        protocolLoader.LoadProtocolAsync);

                    using var kbCtx = new AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase.KnowledgeBaseContext();
                    var loraOrchestrator = new AuswertungPro.Next.Infrastructure.Ai.Training.QwenLoraOrchestrator(
                        loraClient,
                        kbCtx,
                        ollamaConfig,
                        loraBenchmarkRunner,
                        loraBenchmarkMetricsStore,
                        msg => Vm.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] [LoRA] {msg}\n"));

                    var loraResult = await loraOrchestrator.RunIfEligibleAsync(ct: _batchCts!.Token);
                    loraSummary = loraResult.Deployed
                        ? $"Deploy OK ({loraResult.ActiveModelName}, F1={loraResult.BenchmarkF1:P1})"
                        : loraResult.StatusText;
                }
                catch (Exception loraEx)
                {
                    loraSummary = $"LoRA Fehler: {loraEx.Message}";
                    Vm.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] [LoRA] {loraSummary}\n");
                }
            }

            try
            {
                var after = await TrainingSamplesStore.LoadAsync();
                trainingCountAfter = after.Count;
                trainingBboxAfter = after.Count(s => s.HasBbox);

                var newSamples = after
                    .Where(sample => !string.IsNullOrWhiteSpace(sample.SampleId) && !sampleIdsBefore.Contains(sample.SampleId))
                    .ToList();

                newSampleCount = newSamples.Count;
                newVideoTimestampCount = newSamples.Count(sample =>
                    string.Equals(sample.SourceType, SourceTypeNames.VideoTimestamp, StringComparison.OrdinalIgnoreCase));
                newBboxCount = newSamples.Count(sample => sample.HasBbox);

                var times = newSamples
                    .Where(sample => sample.TimeSeconds > 0)
                    .Select(sample => sample.TimeSeconds)
                    .OrderBy(seconds => seconds)
                    .ToList();

                if (times.Count > 0)
                {
                    newTimeMin = times[0];
                    newTimeMax = times[^1];
                    var median = times[times.Count / 2];
                    timeExamples = $"{times[0]:F1}s, {median:F1}s, {times[^1]:F1}s";
                }

                var videoShare = newSampleCount > 0
                    ? (double)newVideoTimestampCount / newSampleCount
                    : 0.0;
                var timeRangeText = newTimeMin.HasValue
                    ? $"{newTimeMin.Value:F1}s..{newTimeMax!.Value:F1}s"
                    : "keine >0s";
                Vm.AppendToLogText(
                    $"[{DateTime.Now:HH:mm:ss}] [QA] Neu: {newSampleCount}, VideoTimestamp: {newVideoTimestampCount} ({videoShare:P1}), " +
                    $"BBox: {newBboxCount}, TimeSeconds: {timeRangeText}, Beispiele: {timeExamples}\n");
            }
            catch
            {
                // Training-Sample-Statistik ist optional.
            }

            var trainingDelta = Math.Max(0, trainingCountAfter - trainingCountBefore);
            var trainingBboxDelta = Math.Max(0, trainingBboxAfter - trainingBboxBefore);

            MessageBox.Show(
                $"Batch fertig: {result.Processed}/{result.TotalHaltungen} Haltungen in {result.TotalDuration.TotalMinutes:F0} Min\n\n" +
                $"TP:{s.TruePositives} FN:{s.FalseNegatives} FP:{s.FalsePositives} MM:{s.CodeMismatches}\n" +
                $"F1: {s.F1:P1}\n\nKB: +{s.KbIndexed} neu, {s.KbDeduplicated} Duplikate\n" +
                $"Training-Samples: +{trainingDelta} neu, davon +{trainingBboxDelta} mit BBox\n" +
                $"Neu seit Start: {newSampleCount}, VideoTimestamp: {newVideoTimestampCount}, BBox: {newBboxCount}\n" +
                $"TimeSeconds > 0: {(newTimeMin.HasValue ? $"{newTimeMin.Value:F1}s..{newTimeMax!.Value:F1}s ({timeExamples})" : "keine")}\n\n" +
                $"YOLO Auto-Retrain: {retrainSummary}\n" +
                $"Qwen LoRA: {loraSummary}",
                "Batch-Nachtbetrieb", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (OperationCanceledException)
        {
            Vm.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] Batch abgebrochen.\n");
            MessageBox.Show("Batch-Nachtbetrieb wurde abgebrochen.", "Abgebrochen", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { MessageBox.Show($"Batch-Fehler: {ex.Message}"); }
        finally
        {
            var btnRestore = this.FindName("BtnBatchNight") as System.Windows.Controls.Button;
            if (btnRestore is not null) { btnRestore.IsEnabled = true; btnRestore.Content = "\U0001f319 Batch-Nachtbetrieb"; }
            BtnBatchCancel.Visibility = Visibility.Collapsed;
            BtnBatchCancel.IsEnabled = true;
            BtnBatchCancel.Content = "\u26d4 Abbrechen";
            _batchCts?.Dispose();
            _batchCts = null;
            Title = "Training Center";
        }
    }

    private void CancelBatchNightRun_Click(object sender, RoutedEventArgs e)
    {
        if (_batchCts == null || _batchCts.IsCancellationRequested) return;

        var confirm = MessageBox.Show(
            "Batch-Nachtbetrieb wirklich abbrechen?\n\nDie aktuelle Haltung wird noch fertig verarbeitet.",
            "Abbrechen?", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        _batchCts.Cancel();
        BtnBatchCancel.IsEnabled = false;
        BtnBatchCancel.Content = "Wird abgebrochen...";
        Vm.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] Abbruch angefordert — aktuelle Haltung wird fertig verarbeitet...\n");
    }

    private static bool TryCreateKnowledgeBaseSnapshot(out string info)
    {
        try
        {
            var dbPath = Ai.KnowledgeRoot.GetKnowledgeDbPath();
            if (!File.Exists(dbPath))
            {
                info = "KnowledgeBase.db nicht gefunden.";
                return false;
            }

            var rootDir = System.IO.Path.GetDirectoryName(dbPath) ?? Ai.KnowledgeRoot.GetRoot();
            var snapshotDir = System.IO.Path.Combine(rootDir, "snapshots");
            Directory.CreateDirectory(snapshotDir);

            var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var targetDbPath = System.IO.Path.Combine(snapshotDir, $"KnowledgeBase_{stamp}.db");
            File.Copy(dbPath, targetDbPath, overwrite: true);

            var walPath = dbPath + "-wal";
            if (File.Exists(walPath))
                File.Copy(walPath, targetDbPath + "-wal", overwrite: true);

            var shmPath = dbPath + "-shm";
            if (File.Exists(shmPath))
                File.Copy(shmPath, targetDbPath + "-shm", overwrite: true);

            info = targetDbPath;
            return true;
        }
        catch (Exception ex)
        {
            info = ex.Message;
            return false;
        }
    }

    // ── Inspektions-Profile extrahieren ──────────────────────────────────

    private async void ExtractProfiles_Click(object sender, RoutedEventArgs e)
    {
        // Sofort sichtbares Feedback - sonst denkt der User nichts passiere.
        try { Vm?.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] Profile-Extraktion: Datei waehlen...\n"); } catch { }

        // Quelle waehlen:
        //   - WinCan: DB3 (SQLite) / SDF (SQL Server Compact) / SQLite
        //   - KIAS/IBAK: Arizona.fdb (Firebird), Daten.txt (IBAK-Beobachtungen), *.xtf (ISYBAU)
        // ACHTUNG: "Daten.txt" als Pattern wird vom Windows-File-Picker nur erkannt
        // wenn ein Wildcard davorsteht. Daher "*Daten.txt" + zusaetzlich "*.txt".
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Datenquelle waehlen (WinCan DB3/SDF/SQLite oder KIAS/IBAK FDB/Daten.txt/XTF)",
            Filter = "Alle unterstuetzten|*.db3;*.sdf;*.sqlite;*.fdb;*Daten.txt;*.xtf|"
                   + "WinCan DB3 (SQLite)|*.db3|"
                   + "WinCan SDF (SQL Server Compact)|*.sdf|"
                   + "SQLite|*.sqlite|"
                   + "KIAS Arizona.fdb (Firebird)|*.fdb|"
                   + "IBAK Daten.txt|*Daten.txt;*.txt|"
                   + "ISYBAU XTF|*.xtf|"
                   + "Alle Dateien|*.*",
            Multiselect = false
        };

        bool? dlgResult;
        try { dlgResult = dlg.ShowDialog(); }
        catch (Exception dex)
        {
            MessageBox.Show($"Dialog konnte nicht geoeffnet werden:\n{dex.Message}",
                "Profile extrahieren", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        if (dlgResult != true)
        {
            try { Vm?.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] Auswahl abgebrochen.\n"); } catch { }
            return;
        }

        var selectedPath = dlg.FileName;
        string db3Path;

        try { Vm?.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] Gewaehlt: {selectedPath}\n"); } catch { }

        // KIAS/IBAK-Pfad: wenn Arizona.fdb, Daten.txt oder *.xtf gewaehlt -> IBAK-Profile
        // direkt aus Daten.txt + Stammdaten-Aggregator (XTF/PDF/FDB) extrahieren.
        var ext = System.IO.Path.GetExtension(selectedPath);
        var fileName = System.IO.Path.GetFileName(selectedPath);
        var isKiasSource = string.Equals(ext, ".fdb", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(ext, ".xtf", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(fileName, "Daten.txt", StringComparison.OrdinalIgnoreCase);
        if (isKiasSource)
        {
            try
            {
                await ExtractProfilesFromKiasAsync(selectedPath);
            }
            catch (Exception kex)
            {
                try { Vm?.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] KIAS-FEHLER (vor Try): {kex.Message}\n"); } catch { }
                MessageBox.Show($"KIAS/IBAK-Extraktion fehlgeschlagen:\n{kex}", "Profile extrahieren",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return;
        }

        // Bei SDF automatisch in SQLite konvertieren bevor der Extractor drauf zugreift.
        if (selectedPath.EndsWith(".sdf", StringComparison.OrdinalIgnoreCase))
        {
            if (!Infrastructure.Import.WinCan.SdfToSqliteConverter.IsSsceAvailable())
            {
                MessageBox.Show(
                    "SDF-Konvertierung nicht moeglich: SQL Server Compact 4.0 Runtime fehlt.\n\n" +
                    "Installieren via 'Microsoft SQL Server Compact 4.0 SP1' " +
                    "(download.microsoft.com, SSCERuntime_x64-ENU.exe).",
                    "SDF nicht unterstuetzt",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            BtnExtractProfiles.IsEnabled = false;
            try { Vm?.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] SDF wird nach SQLite konvertiert: {System.IO.Path.GetFileName(selectedPath)}...\n"); } catch { }
            try
            {
                db3Path = await System.Threading.Tasks.Task.Run(() =>
                    Infrastructure.Import.WinCan.SdfToSqliteConverter.Convert(selectedPath));
                try { Vm?.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] Konvertierung fertig: {db3Path}\n"); } catch { }
            }
            catch (Exception ex)
            {
                try { Vm?.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] SDF-Konvertierung FEHLGESCHLAGEN: {ex.Message}\n"); } catch { }
                MessageBox.Show($"SDF-Konvertierung fehlgeschlagen:\n\n{ex.Message}",
                    "SDF-Konvertierung", MessageBoxButton.OK, MessageBoxImage.Error);
                BtnExtractProfiles.IsEnabled = true;
                return;
            }
        }
        else
        {
            db3Path = selectedPath;
        }
        var outputDir = System.IO.Path.Combine(@"C:\KI_BRAIN", "inspection_profiles");
        var patternsPath = System.IO.Path.Combine(@"C:\KI_BRAIN", "inspection_patterns.json");

        BtnExtractProfiles.IsEnabled = false;
        try { Vm?.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] Profile extrahieren: {System.IO.Path.GetFileName(db3Path)}...\n"); } catch { }

        try
        {
            var profiles = await System.Threading.Tasks.Task.Run(() =>
                Infrastructure.Import.WinCan.InspectionProfileExtractor.ExtractFromDb3(db3Path));

            try { Vm?.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] {profiles.Count} Profile extrahiert\n"); } catch { }

            // Profile speichern
            await System.Threading.Tasks.Task.Run(() =>
                Infrastructure.Import.WinCan.InspectionProfileExtractor.SaveProfiles(profiles, outputDir));

            try { Vm?.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] Profile gespeichert: {outputDir}\n"); } catch { }

            // Muster aggregieren
            var patterns = Infrastructure.Import.WinCan.InspectionPatternAggregator.Aggregate(profiles);
            await System.Threading.Tasks.Task.Run(() =>
                Infrastructure.Import.WinCan.InspectionPatternAggregator.SavePatterns(patterns, patternsPath));

            try { Vm?.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] Muster aggregiert:\n"); } catch { }
            try { Vm?.AppendToLogText($"  Haltungen: {patterns.AnzahlHaltungen}, Beobachtungen: {patterns.AnzahlBeobachtungen}\n"); } catch { }
            try { Vm?.AppendToLogText($"  Median Geschwindigkeit: {patterns.MedianFahrgeschwindigkeit:F3} m/s\n"); } catch { }
            try { Vm?.AppendToLogText($"  Median Codierungen/m: {patterns.MedianCodierungenProMeter:F2}\n"); } catch { }
            try { Vm?.AppendToLogText($"  Median Luecke: {patterns.MedianLueckeMeter:F1}m\n"); } catch { }
            foreach (var r in patterns.SequenzRegeln)
                try { Vm?.AppendToLogText($"  Regel: {r.Regel} (Support: {r.Support:P0}, Ausnahmen: {r.Ausnahmen})\n"); } catch { }

            // QualityFlags zusammenfassen
            int warnCount = profiles.Count(p => p.QualityFlags.Warnings.Count > 0);
            int noBcd = profiles.Count(p => p.QualityFlags.MissingBcd);
            int noBce = profiles.Count(p => p.QualityFlags.MissingBce);
            try { Vm?.AppendToLogText($"  Warnungen: {warnCount} Profile, fehlendes BCD: {noBcd}, fehlendes BCE: {noBce}\n"); } catch { }

            try { Vm?.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] Profile fertig. Gespeichert nach {outputDir}\n"); } catch { }

            // Fragen ob Frames extrahiert werden sollen.
            // Bei SDF-Quelle verwenden wir den Original-SDF-Pfad fuer die Video-Suche —
            // die konvertierte .db3 liegt in C:\KI_BRAIN\sdf_converted\, dort sind keine Videos.
            var rootBaseForVideos = selectedPath;
            var exportRoot = System.IO.Path.GetDirectoryName(rootBaseForVideos) ?? "";
            // WinCan-Export-Root: 2 Ebenen hoch von DB-Ordner (DB → Project → DISK1)
            var disk1Root = exportRoot;
            for (int up = 0; up < 3; up++)
            {
                var parent = System.IO.Path.GetDirectoryName(disk1Root);
                if (parent != null) disk1Root = parent;
            }

            // Profile mit Video zaehlen
            int mitVideo = 0;
            foreach (var p in profiles)
            {
                if (string.IsNullOrEmpty(p.VideoPfad)) continue;
                var resolved = Infrastructure.Import.WinCan.VideoResolver.Resolve(
                    p.HaltungKey, disk1Root,
                    string.IsNullOrEmpty(p.VideoPfad) ? null : new List<string> { p.VideoPfad });
                if (resolved != null) mitVideo++;
            }

            var framesDir = System.IO.Path.Combine(@"C:\KI_BRAIN", "training_frames");
            int geschaetzteFrames = profiles.Sum(p => p.Ereignisse.Count * 5 + p.Luecken.Count(l => (l.DistanzM ?? 0) > 3) + 2);

            var extractFrames = MessageBox.Show(
                $"{profiles.Count} Profile extrahiert.\n" +
                $"{mitVideo} davon haben ein Video.\n\n" +
                $"Geschwindigkeit: {patterns.MedianFahrgeschwindigkeit:F3} m/s\n" +
                $"Codierungen/m: {patterns.MedianCodierungenProMeter:F2}\n\n" +
                $"Jetzt ~{geschaetzteFrames} Trainings-Frames aus den Videos extrahieren?\n" +
                $"(5 Frames pro Codierung + Negativ-Beispiele + Aufnahmetechnik)\n" +
                $"Geschaetzte Dauer: ~{mitVideo * 30 / 60} Minuten",
                "Frames extrahieren?", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (extractFrames == MessageBoxResult.Yes && mitVideo > 0)
            {
                await ExtractFramesFromProfiles(profiles, disk1Root, framesDir);
            }
        }
        catch (Exception ex)
        {
            try { Vm?.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] FEHLER: {ex.Message}\n"); } catch { }
            MessageBox.Show($"Fehler: {ex.Message}", "Profile extrahieren", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnExtractProfiles.IsEnabled = true;
        }
    }

    /// <summary>
    /// Extrahiert Inspektionsprofile aus einem KIAS/IBAK-Export. Akzeptiert die
    /// Auswahl von Arizona.fdb, Daten.txt oder *.xtf - in allen Faellen wird der
    /// daruebergeordnete Export-Ordner als Wurzel genutzt und der
    /// IbakInspectionProfileExtractor (Daten.txt + StammdatenAggregator) angewendet.
    /// </summary>
    private async System.Threading.Tasks.Task ExtractProfilesFromKiasAsync(string selectedFile)
    {
        try { BtnExtractProfiles.IsEnabled = false; } catch { }
        // Sofort sichtbares Feedback bevor irgendetwas crashen kann.
        try { Vm?.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] KIAS-Extraktion startet fuer {System.IO.Path.GetFileName(selectedFile)}...\n"); } catch { }
        try
        {
            // Export-Wurzel aus Datei-Pfad ableiten:
            //   - Arizona.fdb liegt in <root>/Data/Arizona.fdb -> root = parent von Data
            //   - Daten.txt   liegt in <root>/Film/Daten.txt    -> root = parent von Film
            //   - *.xtf       liegt typisch in <root> selbst    -> root = Verzeichnis der xtf
            var dir = System.IO.Path.GetDirectoryName(selectedFile) ?? "";
            var parent = System.IO.Path.GetDirectoryName(dir);
            var folderName = System.IO.Path.GetFileName(dir);
            var exportRoot = (string.Equals(folderName, "Data", StringComparison.OrdinalIgnoreCase)
                          ||  string.Equals(folderName, "Film", StringComparison.OrdinalIgnoreCase))
                          && !string.IsNullOrWhiteSpace(parent)
                ? parent!
                : dir;

            try { Vm?.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] KIAS/IBAK-Quelle erkannt: {System.IO.Path.GetFileName(selectedFile)}\n"); } catch { }
            try { Vm?.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] Export-Wurzel: {exportRoot}\n"); } catch { }

            var pattern = Infrastructure.Import.Ibak.KiasExportPattern.Detect(exportRoot);
            try { Vm?.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] KIAS-Pattern: {(pattern.IsKias ? "ja" : "nein")} ({pattern.Reason})\n"); } catch { }

            var profiles = await System.Threading.Tasks.Task.Run(() =>
                Infrastructure.Import.Ibak.IbakInspectionProfileExtractor.ExtractFromExportRoot(exportRoot));

            try { Vm?.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] {profiles.Count} Profile aus IBAK Daten.txt extrahiert\n"); } catch { }
            if (profiles.Count == 0)
            {
                MessageBox.Show($"Keine Inspektionsprofile gefunden.\nExport-Wurzel: {exportRoot}\n\n"
                    + "Pruefe ob Film/Daten.txt vorhanden ist.",
                    "Profile extrahieren", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var outputDir = System.IO.Path.Combine(@"C:\KI_BRAIN", "inspection_profiles");
            var patternsPath = System.IO.Path.Combine(@"C:\KI_BRAIN", "inspection_patterns.json");

            await System.Threading.Tasks.Task.Run(() =>
                Infrastructure.Import.WinCan.InspectionProfileExtractor.SaveProfiles(profiles, outputDir));
            try { Vm?.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] Profile gespeichert: {outputDir}\n"); } catch { }

            var aggPatterns = Infrastructure.Import.WinCan.InspectionPatternAggregator.Aggregate(profiles);
            await System.Threading.Tasks.Task.Run(() =>
                Infrastructure.Import.WinCan.InspectionPatternAggregator.SavePatterns(aggPatterns, patternsPath));

            try { Vm?.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] Muster aggregiert:\n"); } catch { }
            try { Vm?.AppendToLogText($"  Haltungen: {aggPatterns.AnzahlHaltungen}, Beobachtungen: {aggPatterns.AnzahlBeobachtungen}\n"); } catch { }
            try { Vm?.AppendToLogText($"  Median Geschwindigkeit: {aggPatterns.MedianFahrgeschwindigkeit:F3} m/s\n"); } catch { }
            try { Vm?.AppendToLogText($"  Median Codierungen/m: {aggPatterns.MedianCodierungenProMeter:F2}\n"); } catch { }

            int mitVideo = profiles.Count(p => !string.IsNullOrEmpty(p.VideoPfad));
            int ohneLaenge = profiles.Count(p => p.LaengeM is null);
            int totalEvents = profiles.Sum(p => p.Ereignisse.Count);
            try { Vm?.AppendToLogText($"  Profile mit Video: {mitVideo}/{profiles.Count}, ohne Laenge: {ohneLaenge}\n"); } catch { }
            try { Vm?.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] KIAS/IBAK-Profile fertig.\n"); } catch { }

            MessageBox.Show(
                $"KIAS/IBAK-Extraktion erfolgreich:\n\n"
                + $"  - {profiles.Count} Inspektionsprofile\n"
                + $"  - {totalEvents} Beobachtungen\n"
                + $"  - {mitVideo}/{profiles.Count} mit Video-Zuordnung\n"
                + $"  - {profiles.Count - ohneLaenge}/{profiles.Count} mit Haltungslaenge\n\n"
                + $"Profile gespeichert nach:\n{outputDir}\n\n"
                + $"Muster-JSON:\n{patternsPath}",
                "Profile extrahiert",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            try { Vm?.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] FEHLER: {ex.Message}\n"); } catch { }
            MessageBox.Show($"Fehler: {ex.Message}", "Profile extrahieren", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnExtractProfiles.IsEnabled = true;
        }
    }

    /// <summary>Extrahiert Trainings-Frames aus Videos fuer alle Profile mit Video-Zuordnung.</summary>
    private async System.Threading.Tasks.Task ExtractFramesFromProfiles(
        List<Infrastructure.Import.WinCan.InspectionProfile> profiles,
        string exportRoot,
        string framesDir)
    {
        var allFrames = new List<Infrastructure.Import.WinCan.ExtractedFrame>();
        int done = 0;
        int total = profiles.Count;
        var cts = new System.Threading.CancellationTokenSource();

        Vm.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] Frame-Extraktion gestartet ({total} Haltungen)...\n");

        foreach (var profile in profiles)
        {
            done++;

            // Video finden
            var dbFiles = string.IsNullOrEmpty(profile.VideoPfad) ? null : new List<string> { profile.VideoPfad };
            var videoMatch = Infrastructure.Import.WinCan.VideoResolver.Resolve(
                profile.HaltungKey, exportRoot, dbFiles);

            if (videoMatch == null)
            {
                Vm.AppendToLogText($"  [{done}/{total}] {profile.HaltungKey}: Kein Video gefunden — uebersprungen\n");
                continue;
            }

            Vm.AppendToLogText($"  [{done}/{total}] {profile.HaltungKey}: {profile.Ereignisse.Count} Events, Video: {System.IO.Path.GetFileName(videoMatch.FilePath)} (Conf: {videoMatch.Confidence:F2})\n");

            try
            {
                var frames = await Infrastructure.Import.WinCan.InspectionFrameExtractor.ExtractFramesAsync(
                    profile, videoMatch.FilePath, framesDir, cts.Token);

                allFrames.AddRange(frames);
                Vm.AppendToLogText($"    → {frames.Count} Frames extrahiert\n");
            }
            catch (Exception ex)
            {
                Vm.AppendToLogText($"    → FEHLER: {ex.Message}\n");
            }
        }

        // Frame-Index kumulieren (bestehende laden + neue dazufuegen)
        var indexPath = System.IO.Path.Combine(framesDir, "_frame_index.json");
        var existingFrames = new List<Infrastructure.Import.WinCan.ExtractedFrame>();
        if (System.IO.File.Exists(indexPath))
        {
            try
            {
                var json = await System.IO.File.ReadAllTextAsync(indexPath);
                var opts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                existingFrames = System.Text.Json.JsonSerializer.Deserialize<List<Infrastructure.Import.WinCan.ExtractedFrame>>(json, opts)
                    ?? new();
            }
            catch { /* Korrupter Index → neu erstellen */ }
        }

        // Duplikate entfernen (gleicher Pfad = gleicher Frame)
        var existingPaths = new HashSet<string>(existingFrames.Select(f => f.PngPfad), StringComparer.OrdinalIgnoreCase);
        var newFrames = allFrames.Where(f => !existingPaths.Contains(f.PngPfad)).ToList();
        existingFrames.AddRange(newFrames);

        Vm.AppendToLogText($"  Index: {newFrames.Count} neue + {existingPaths.Count} bestehende = {existingFrames.Count} total\n");

        await System.Threading.Tasks.Task.Run(() =>
            Infrastructure.Import.WinCan.InspectionFrameExtractor.SaveFrameIndex(existingFrames, indexPath));

        allFrames = existingFrames;

        // Zusammenfassung
        int refFrames = allFrames.Count(f => f.IsReferenceFrame);
        int negFrames = allFrames.Count(f => f.FrameTyp.Contains("negativ"));
        int techFrames = allFrames.Count(f => f.Quelle == "aufnahmetechnik");

        Vm.AppendToLogText($"\n[{DateTime.Now:HH:mm:ss}] Frame-Extraktion abgeschlossen:\n");
        Vm.AppendToLogText($"  Total: {allFrames.Count} Frames\n");
        Vm.AppendToLogText($"  Referenz-Frames (Codierungen): {refFrames}\n");
        Vm.AppendToLogText($"  Negativ-Beispiele: {negFrames}\n");
        Vm.AppendToLogText($"  Aufnahmetechnik: {techFrames}\n");
        Vm.AppendToLogText($"  Gespeichert: {framesDir}\n");
        Vm.AppendToLogText($"  Index: {indexPath}\n");

        MessageBox.Show(
            $"{allFrames.Count} Frames extrahiert!\n\n" +
            $"Referenz: {refFrames}\n" +
            $"Negativ: {negFrames}\n" +
            $"Aufnahmetechnik: {techFrames}\n\n" +
            $"Gespeichert: {framesDir}",
            "Frame-Extraktion", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ── Eval-Set generieren ──────────────────────────────────────────

    private async void GenerateEvalSet_Click(object sender, RoutedEventArgs e)
    {
        // DB3-Datei waehlen (gleich wie Profile extrahieren)
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "WinCan DB3 waehlen fuer Eval-Set",
            Filter = "WinCan DB3|*.db3|Alle Dateien|*.*",
            Multiselect = false
        };
        if (dlg.ShowDialog() != true) return;

        var db3Path = dlg.FileName;
        var framesDir = System.IO.Path.Combine(@"C:\KI_BRAIN", "training_frames");
        var evalDir = System.IO.Path.Combine(@"C:\KI_BRAIN", "eval_set");
        var candidatesPath = System.IO.Path.Combine(evalDir, "_candidates.json");

        BtnGenerateEvalSet.IsEnabled = false;
        Vm.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] Eval-Set generieren aus {System.IO.Path.GetFileName(db3Path)}...\n");

        try
        {
            // Profile extrahieren
            var profiles = await System.Threading.Tasks.Task.Run(() =>
                Infrastructure.Import.WinCan.InspectionProfileExtractor.ExtractFromDb3(db3Path));

            Vm.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] {profiles.Count} Profile geladen\n");

            // Eval-Kandidaten generieren (120 diverse Frames)
            var candidates = Infrastructure.Import.WinCan.EvalSetGenerator.GenerateCandidates(
                profiles, framesDir, targetCount: 120);

            Vm.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] {candidates.Count} Eval-Kandidaten generiert:\n");

            // Statistik
            var byKategorie = candidates.GroupBy(c => c.Kategorie)
                .Select(g => $"  {g.Key}: {g.Count()}")
                .ToList();
            foreach (var line in byKategorie)
                Vm.AppendToLogText($"{line}\n");

            var byCodes = candidates.GroupBy(c => c.CodeMain)
                .OrderByDescending(g => g.Count())
                .Select(g => $"  {g.Key}: {g.Count()}")
                .ToList();
            Vm.AppendToLogText($"  Codes:\n");
            foreach (var line in byCodes)
                Vm.AppendToLogText($"  {line}\n");

            // Speichern
            Infrastructure.Import.WinCan.EvalSetGenerator.SaveCandidates(candidates, candidatesPath);
            Vm.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] Kandidaten gespeichert: {candidatesPath}\n");

            // Fragen ob sofort exportieren (alle als approved)
            var result = MessageBox.Show(
                $"{candidates.Count} Eval-Kandidaten generiert.\n\n" +
                $"Du kannst jetzt:\n" +
                $"• JA: Alle als 'approved' markieren und Eval-Set exportieren\n" +
                $"  (Schnell, aber ohne manuelle Pruefung)\n\n" +
                $"• NEIN: Kandidaten-Datei manuell pruefen und spaeter exportieren\n" +
                $"  (Sauberer, du pruefst jeden Frame)\n\n" +
                $"Kandidaten: {candidatesPath}",
                "Eval-Set exportieren?", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                // Alle als approved markieren
                var approved = candidates.Select(c => c with { Status = "approved" }).ToList();
                Infrastructure.Import.WinCan.EvalSetGenerator.SaveCandidates(approved, candidatesPath);

                var exported = Infrastructure.Import.WinCan.EvalSetGenerator.ExportFrozenEvalSet(
                    approved, evalDir);

                Vm.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] Eval-Set exportiert: {exported} Frames\n");
                Vm.AppendToLogText($"  Pfad: {evalDir}\n");
                Vm.AppendToLogText($"  WICHTIG: Dieses Set wird NIE vom Training beruehrt!\n");

                MessageBox.Show(
                    $"{exported} Frames als Eval-Set exportiert.\n\n" +
                    $"Pfad: {evalDir}\n\n" +
                    $"WICHTIG: Dieses Set ist eingefroren.\n" +
                    $"Die KI darf daraus nie lernen — nur geprueft werden.",
                    "Eval-Set bereit", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                Vm.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] Kandidaten gespeichert. Bitte manuell pruefen:\n");
                Vm.AppendToLogText($"  {candidatesPath}\n");
                Vm.AppendToLogText($"  Status pro Frame auf 'approved' / 'rejected' / 'corrected' setzen\n");
                Vm.AppendToLogText($"  Dann 'Eval-Set' nochmals klicken zum Exportieren\n");
            }
        }
        catch (Exception ex)
        {
            Vm.AppendToLogText($"[{DateTime.Now:HH:mm:ss}] FEHLER: {ex.Message}\n");
            MessageBox.Show($"Fehler: {ex.Message}", "Eval-Set", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnGenerateEvalSet.IsEnabled = true;
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
            System.Diagnostics.Debug.WriteLine($"[FileToImage] Datei nicht gefunden: {path}");
            return null;
        }

        try
        {
            var bmp = new System.Windows.Media.Imaging.BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bmp.CreateOptions = System.Windows.Media.Imaging.BitmapCreateOptions.IgnoreImageCache;
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FileToImage] Fehler beim Laden: {path} → {ex.Message}");
            return null;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

