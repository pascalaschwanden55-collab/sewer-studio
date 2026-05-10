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

    private readonly IDialogService _dialogs = App.Resolve<IDialogService>();

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
            var cfg = AuswertungPro.Next.Application.Ai.Ollama.OllamaConfigProvider.Load();
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
            _dialogs.ShowMessage($"Fehler beim Akzeptieren: {ex.Message}",
                "Review", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>Ablehnen ohne Korrektur — verwirft KI-Vorschlag, entfernt aus Queue.</summary>
    private void ReviewReject_Click(object sender, RoutedEventArgs e)
    {
        if (Vm.SelectedReviewItem is null)
        {
            _dialogs.ShowMessage("Kein Item ausgewaehlt.", "Review",
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
            _dialogs.ShowMessage("Kein Item ausgewaehlt.", "Review",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var item = Vm.SelectedReviewItem;
        var framePath = item.SelfTrainingFramePath;
        if (string.IsNullOrWhiteSpace(framePath) || !System.IO.File.Exists(framePath))
        {
            _dialogs.ShowMessage("Kein Frame-Bild fuer dieses Item vorhanden.", "Review",
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
                var ollamaConfig = AuswertungPro.Next.Application.Ai.Ollama.OllamaConfigProvider.Load();
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
            _dialogs.ShowMessage($"Fehler beim Oeffnen der Korrektur: {ex.Message}",
                "Review", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            // Temp-Ordner nach 1 Stunde aufraeumen — Annotationen werden ohnehin
            // im Knowledge-Ordner persistiert, nicht im Temp.
            if (tempDir is not null)
            {
                try { System.IO.Directory.Delete(tempDir, recursive: true); } catch (Exception _bestEffortEx) { System.Diagnostics.Debug.WriteLine($"[best-effort] {_bestEffortEx.Message}"); }
            }
        }

        await Task.CompletedTask;
    }

    // ── Lehrer-Annotationen Tab Event Handlers ──

    private List<TeacherAnnotation> _allTeacherAnnotations = new();
    private List<TeacherAnnotation> _filteredTeacherAnnotations = new();
    private TeacherAnnotation? _selectedTeacherAnnotation;
    private bool _teacherLoaded;


    // ═══ Batch-Nachtbetrieb + Video-Selbsttraining + Benchmark ═════════

    private void OpenImageAnnotation_Click(object sender, RoutedEventArgs e)
    {
        AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase.KnowledgeBaseManager? kbManager = null;
        AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase.KbDeduplicationService? dedup = null;

        try
        {
            var kbCtx = new AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase.KnowledgeBaseContext();
            var ollamaConfig = AuswertungPro.Next.Application.Ai.Ollama.OllamaConfigProvider.Load();
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
        AuswertungPro.Next.Infrastructure.Ai.Training.Services.PdfProtocolTableParser.PdfToTextExePath = diagnostics.ExplicitPdfToTextPath;

        var cfg = AuswertungPro.Next.Application.Ai.AiRuntimeConfigProvider.Load();
        if (!cfg.Enabled) { _dialogs.ShowMessage("KI ist deaktiviert.", "Video-Blindtest"); return; }

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
        var cfg = AuswertungPro.Next.Application.Ai.AiRuntimeConfigProvider.Load();
        if (!cfg.Enabled)
        {
            _dialogs.ShowMessage("KI ist deaktiviert.", "Eval-Set");
            return;
        }

        var evalDir = System.IO.Path.Combine(@"C:\KI_BRAIN", "eval_set");
        if (!System.IO.Directory.Exists(System.IO.Path.Combine(evalDir, "images")))
        {
            _dialogs.ShowMessage(
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

            _dialogs.ShowMessage(
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
            _dialogs.ShowMessage($"Eval-Set Fehler: {ex.Message}", "Eval-Set",
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

        var cfg = AuswertungPro.Next.Application.Ai.AiRuntimeConfigProvider.Load();
        if (!cfg.Enabled) { _dialogs.ShowMessage("KI ist deaktiviert.", "Benchmark"); return; }

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
            var result = _dialogs.ShowMessage(
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

                _dialogs.ShowMessage(
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
            _dialogs.ShowMessage($"Fehler: {ex.Message}", "Eval-Set", MessageBoxButton.OK, MessageBoxImage.Error);
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

