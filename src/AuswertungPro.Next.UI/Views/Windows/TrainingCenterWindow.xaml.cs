using System;
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
using AuswertungPro.Next.UI.Ai.Teacher;
using AuswertungPro.Next.UI.Ai.Training;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class TrainingCenterWindow : Window
{
    public TrainingCenterViewModel Vm { get; }

    // Pipeline-Dots und Service-Indikatoren fuer Animation
    private Ellipse[] _pipelineDots = Array.Empty<Ellipse>();
    private Border[] _serviceDots = Array.Empty<Border>();

    // Review-Services (lazy, erst bei erster Review-Aktion)
    private Ai.SelfImproving.ReviewQueueService? _reviewQueueService;

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
            _reviewQueueService = new Ai.SelfImproving.ReviewQueueService();
            Vm.ReviewQueueServiceRef = _reviewQueueService;
            Vm.LoadReviewQueue(_reviewQueueService);

            // Lehrer-Annotationen laden
            await LoadTeacherAnnotationsAsync();
        };

        Vm.PropertyChanged += OnVmPropertyChanged;
        Closed += (_, _) => Vm.PropertyChanged -= OnVmPropertyChanged;
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
            SelfTrainingLogList.ScrollToEnd();
        };

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

    /// <summary>Erzeugt FeedbackIngestionService mit optionalem KbManager fuer KB-Re-Indexierung.</summary>
    private static Ai.SelfImproving.FeedbackIngestionService CreateFeedbackService(
        Ai.KnowledgeBase.KnowledgeBaseContext db)
    {
        var logger = new Ai.QualityGate.ValidationLogger(db.Connection);
        var weights = new Ai.QualityGate.WeightLearningService(db.Connection);

        // KbManager optional — wenn Ollama offline, wird nur geloggt
        Ai.KnowledgeBase.KnowledgeBaseManager? kbManager = null;
        try
        {
            var cfg = Ai.Ollama.OllamaConfig.Load();
            var http = new System.Net.Http.HttpClient { Timeout = cfg.RequestTimeout };
            var embedder = new Ai.KnowledgeBase.EmbeddingService(http, cfg);
            kbManager = new Ai.KnowledgeBase.KnowledgeBaseManager(db, embedder);
        }
        catch { /* Ollama nicht verfuegbar — Feedback wird geloggt, KB-Update uebersprungen */ }

        return new Ai.SelfImproving.FeedbackIngestionService(logger, weights, kbManager);
    }

    private async void ReviewApprove_Click(object sender, RoutedEventArgs e)
    {
        if (Vm.SelectedReviewItem is null) return;
        _reviewQueueService ??= new Ai.SelfImproving.ReviewQueueService();
        try
        {
            using var db = new Ai.KnowledgeBase.KnowledgeBaseContext();
            var feedback = CreateFeedbackService(db);
            await Vm.ApproveReviewItemAsync(Vm.SelectedReviewItem, feedback, _reviewQueueService);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Fehler beim Akzeptieren: {ex.Message}",
                "Review", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void ReviewReject_Click(object sender, RoutedEventArgs e)
    {
        if (Vm.SelectedReviewItem is null) return;
        var code = Microsoft.VisualBasic.Interaction.InputBox(
            "Korrekter VSA-Code:", "Korrektur",
            Vm.SelectedReviewItem.SuggestedCode ?? "");
        if (string.IsNullOrWhiteSpace(code)) return;
        _reviewQueueService ??= new Ai.SelfImproving.ReviewQueueService();
        try
        {
            using var db = new Ai.KnowledgeBase.KnowledgeBaseContext();
            var feedback = CreateFeedbackService(db);
            await Vm.RejectReviewItemAsync(Vm.SelectedReviewItem, code, feedback, _reviewQueueService);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Fehler beim Ablehnen: {ex.Message}",
                "Review", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
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
            var store = new AuswertungPro.Next.UI.Ai.Training.FewShotExampleStore();
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
        var selected = CasesGrid.SelectedItems.Cast<TrainingCase>().ToList();
        if (selected.Count == 0) return;
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

    private void OpenVideoTrainingReview_Click(object sender, RoutedEventArgs e)
    {
        if (App.Services is not ServiceProvider sp) return;

        // pdftotext-Pfad aus den App-Einstellungen setzen
        Ai.Training.Services.PdfProtocolTableParser.PdfToTextExePath = sp.Diagnostics.ExplicitPdfToTextPath;

        var cfg = Ai.AiRuntimeConfig.Load();
        if (!cfg.Enabled) { MessageBox.Show("KI ist deaktiviert.", "Video-Blindtest"); return; }

        // Factory: Erstellt pro Analyse-Durchlauf frischen HttpClient + Pipeline
        // (HttpClient kann nicht wiederverwendet werden nachdem Headers geaendert wurden)
        Func<Ai.Training.VideoSelfTrainingOrchestrator> orchestratorFactory = () =>
        {
            var allowedSet = new System.Collections.Generic.HashSet<string>(
                sp.CodeCatalog.AllowedCodes(), StringComparer.OrdinalIgnoreCase);
            var plausibility = new Ai.RuleBasedAiSuggestionPlausibilityService(allowedSet);
            var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(30) };
            var pipeline = sp.CreateVideoAnalysisPipeline(cfg, plausibility, http);
            var meterTimeline = new Ai.Training.MeterTimelineService(cfg);
            return new Ai.Training.VideoSelfTrainingOrchestrator(pipeline, meterTimeline);
        };

        var vm = new VideoTrainingReviewViewModel(orchestratorFactory, sp.WinCanImport, sp.IbakImport);
        new VideoTrainingReviewWindow(vm) { Owner = this }.Show();
    }

    private void OpenBenchmark_Click(object sender, RoutedEventArgs e)
    {
        if (App.Services is not ServiceProvider sp) return;
        var cfg = Ai.AiRuntimeConfig.Load();
        if (!cfg.Enabled) { MessageBox.Show("KI ist deaktiviert.", "Benchmark"); return; }

        var allowedSet = new System.Collections.Generic.HashSet<string>(
            sp.CodeCatalog.AllowedCodes(), StringComparer.OrdinalIgnoreCase);
        var plausibility = new Ai.RuleBasedAiSuggestionPlausibilityService(allowedSet);
        var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        var pipeline = sp.CreateVideoAnalysisPipeline(cfg, plausibility, http);
        var protocolLoader = new Ai.Training.Services.ProtocolLoaderFactory(sp.WinCanImport, sp.IbakImport);
        var setStore = new BenchmarkSetStore();
        var metricsStore = new BenchmarkMetricsStore();
        var meterTimeline = new Ai.Training.MeterTimelineService(cfg);
        var orchestrator = new Ai.Training.VideoSelfTrainingOrchestrator(pipeline, meterTimeline);
        var runner = new BenchmarkRunner(setStore, metricsStore, orchestrator, protocolLoader.LoadProtocolAsync);
        var vm = new BenchmarkViewModel(setStore, runner, metricsStore, protocolLoader);
        new BenchmarkWindow(vm) { Owner = this }.Show();
    }

    private async void StartBatchNightRun_Click(object sender, RoutedEventArgs e)
    {
        if (App.Services is not ServiceProvider sp) return;

        // pdftotext-Pfad aus den App-Einstellungen setzen
        Ai.Training.Services.PdfProtocolTableParser.PdfToTextExePath = sp.Diagnostics.ExplicitPdfToTextPath;

        var cfg = Ai.AiRuntimeConfig.Load();
        if (!cfg.Enabled) { MessageBox.Show("KI ist deaktiviert.", "Batch-Nachtbetrieb"); return; }

        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Ordner mit Haltungen waehlen" };
        if (dlg.ShowDialog() != true) return;

        var confirm = MessageBox.Show(
            $"Batch-Nachtbetrieb starten?\n\nOrdner: {dlg.FolderName}\n\nAlle Haltungen mit Video + PDF werden automatisch verarbeitet.\nDas kann mehrere Stunden dauern.",
            "Batch-Nachtbetrieb", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.OK) return;

        var allowedSet = new System.Collections.Generic.HashSet<string>(
            sp.CodeCatalog.AllowedCodes(), StringComparer.OrdinalIgnoreCase);
        var plausibility = new Ai.RuleBasedAiSuggestionPlausibilityService(allowedSet);
        var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        var pipeline = sp.CreateVideoAnalysisPipeline(cfg, plausibility, http);
        var protocolLoader = new Ai.Training.Services.ProtocolLoaderFactory(sp.WinCanImport, sp.IbakImport);
        var meterTimeline = new Ai.Training.MeterTimelineService(cfg);
        var videoOrch = new Ai.Training.VideoSelfTrainingOrchestrator(pipeline, meterTimeline);

        Ai.KnowledgeBase.KbEnrichmentService enrichment;
        try
        {
            var kbHttp = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            var ollamaConfig = Ai.Ollama.OllamaConfig.Load();
            var kbCtx = new Ai.KnowledgeBase.KnowledgeBaseContext();
            var embedder = new Ai.KnowledgeBase.EmbeddingService(kbHttp, ollamaConfig);
            var retrieval = new Ai.KnowledgeBase.RetrievalService(kbCtx, embedder);
            var kbManager = new Ai.KnowledgeBase.KnowledgeBaseManager(kbCtx, embedder);
            var dedup = new Ai.KnowledgeBase.KbDeduplicationService(embedder, retrieval);
            enrichment = new Ai.KnowledgeBase.KbEnrichmentService(kbManager, dedup);
        }
        catch (Exception ex) { MessageBox.Show($"KB-Fehler: {ex.Message}"); return; }

        var batchOrch = new Ai.Training.BatchSelfTrainingOrchestrator(videoOrch, protocolLoader, enrichment);
        var request = new Ai.Training.Models.BatchSelfTrainingRequest { ExportRootPath = dlg.FolderName };

        BtnBatchNight.IsEnabled = false;
        BtnBatchNight.Content = "🌙 Batch laeuft...";

        var progress = new Progress<Ai.Training.Models.BatchSelfTrainingProgress>(p =>
        {
            Vm.StatusText = p.Status;
            if (p.EstimatedRemaining.HasValue)
                Title = $"Training Center — Batch {p.CurrentIndex}/{p.TotalHaltungen} — {p.EstimatedRemaining.Value.Hours}h {p.EstimatedRemaining.Value.Minutes}min";
        });

        try
        {
            var result = await Task.Run(() => batchOrch.RunAsync(request, progress));
            var s = result.FinalStats;
            MessageBox.Show(
                $"Batch fertig: {result.Processed}/{result.TotalHaltungen} Haltungen in {result.TotalDuration.TotalMinutes:F0} Min\n\n" +
                $"TP:{s.TruePositives} FN:{s.FalseNegatives} FP:{s.FalsePositives} MM:{s.CodeMismatches}\n" +
                $"F1: {s.F1:P1}\n\nKB: +{s.KbIndexed} neu, {s.KbDeduplicated} Duplikate",
                "Batch-Nachtbetrieb", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { MessageBox.Show($"Batch-Fehler: {ex.Message}"); }
        finally { BtnBatchNight.IsEnabled = true; BtnBatchNight.Content = "🌙 Batch-Nachtbetrieb"; Title = "Training Center"; }
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
