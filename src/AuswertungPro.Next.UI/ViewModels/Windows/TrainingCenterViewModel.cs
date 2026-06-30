using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Ai.Teacher;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training.Services;
using AuswertungPro.Next.UI.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai.Training;
using AuswertungPro.Next.UI.Services;
using AiTrack = AuswertungPro.Next.UI.Services.AiActivityTracker;
using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

public partial class TrainingCenterViewModel : ObservableObject
{
    private readonly TrainingCenterStore _store;
    private readonly TrainingCenterImportService _import;
    private readonly ICodeCatalogProvider? _codeCatalog;
    private readonly IKnowledgeBaseDiagnosticsRunner _kbDiagnostics;
    private readonly AppSettings? _settings;

    /// <summary>Wiederverwendbarer HttpClient fuer KB-Operationen (Embedding-Requests).</summary>
    private System.Net.Http.HttpClient? _kbHttpClient;

    /// <summary>Optionale Referenz auf die Review Queue (gesetzt von Window).</summary>
    public InfraSelfImproving.ReviewQueueService? ReviewQueueServiceRef { get; set; }

    public ObservableCollection<TrainingCase> Cases { get; } = new();
    public ObservableCollection<TrainingSample> Samples { get; } = new();

    [ObservableProperty] private TrainingCase? _selectedCase;
    [ObservableProperty] private TrainingSample? _selectedSample;
    [ObservableProperty] private string _rootFolder = "";
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _logText = "";
    [ObservableProperty] private int _progressValue;
    [ObservableProperty] private int _progressMax = 1;

    // Live-Vorschau während Batch-Import
    [ObservableProperty] private string _liveFramePath = "";
    [ObservableProperty] private string _liveCaseInfo = "";
    [ObservableProperty] private string _liveCodeInfo = "";
    [ObservableProperty] private string _liveMeterInfo = "";
    private DateTime _lastLiveFrameUpdate = DateTime.MinValue;

    /// <summary>Setzt LiveFramePath mit Throttling (~5 fps max), um UI-Thread nicht zu ueberlasten.</summary>
    private void SetLiveFrameThrottled(string? path)
    {
        if (string.IsNullOrEmpty(path)) { LiveFramePath = ""; return; }
        if ((DateTime.UtcNow - _lastLiveFrameUpdate).TotalMilliseconds < 180) return;
        LiveFramePath = path;
        _lastLiveFrameUpdate = DateTime.UtcNow;
    }

    // KB-Trainingsstand
    [ObservableProperty] private int _kbSampleCount;
    [ObservableProperty] private int _kbErrorCount;   // Approved aber Quality-Gate fehlgeschlagen
    [ObservableProperty] private int _kbNewCount;      // Nicht-approved (Status=New)
    [ObservableProperty] private int _kbEmbeddingCount;
    [ObservableProperty] private int _kbCodesCovered;
    [ObservableProperty] private string _kbReadinessLabel = "Unbekannt";
    [ObservableProperty] private System.Windows.Media.Brush _kbReadinessBrush
        = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x94, 0xA3, 0xB8));
    [ObservableProperty] private string _kbLastUpdate = "\u2014";
    [ObservableProperty] private string _kbTopCodesText = "";

    // KB-Qualitaet Dashboard
    [ObservableProperty] private string _kbCoverageGapsText = "";
    [ObservableProperty] private int _kbCoverageGapsCount;
    [ObservableProperty] private string _kbAccuracyText = "";
    [ObservableProperty] private int _kbStaleSampleCount;
    [ObservableProperty] private string _kbTrendText = "";
    [ObservableProperty] private string _kbTrendDirection = "";

    // Review Queue (Self-Improving Loop)
    public ObservableCollection<InfraSelfImproving.ReviewQueueItem> ReviewQueue { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedReviewCard))]
    [NotifyCanExecuteChangedFor(nameof(ApproveSelectedReviewCommand))]
    [NotifyCanExecuteChangedFor(nameof(RejectSelectedReviewCommand))]
    private InfraSelfImproving.ReviewQueueItem? _selectedReviewItem;

    /// <summary>
    /// Optionale YOLO-Box die der Reviewer auf dem Review-Karten-Bild gezeichnet hat.
    /// Wird beim Approve an ApproveSelfTrainingAsync weitergereicht (B5).
    /// </summary>
    public BoundingBox? PendingBox { get; set; }

    /// <summary>
    /// Optionale SAM-Maske zur gezeichneten Review-Box. Wird beim Approve gespeichert.
    /// </summary>
    public TrainingSegmentationMask? PendingSamMask { get; set; }

    [ObservableProperty] private int _reviewQueueCount;
    [ObservableProperty] private string _reviewStatusText = "";

    /// <summary>Projektion des aktuell gewaehlten Kandidaten auf die Karte (null = nichts gewaehlt).</summary>
    public ReviewCardViewModel? SelectedReviewCard
        => SelectedReviewItem is null ? null : new ReviewCardViewModel(SelectedReviewItem);

    // ── Selbsttraining-Visualisierungen ──
    public ObservableCollection<SelfTrainingEntryResult> SelfTrainingResults { get; } = new();
    public ObservableCollection<CodeDistributionEntry> CodeDistribution { get; } = new();
    public ObservableCollection<string> SelfTrainingLogEntries { get; } = new();

    [ObservableProperty] private int _pipelineActiveStep; // 0-5 (BuildingTimeline..Completed)
    [ObservableProperty] private string _currentEntryCode = "";
    [ObservableProperty] private double _currentEntryMeter;
    [ObservableProperty] private string _currentComparisonText = "";
    [ObservableProperty] private string _currentTechniqueGrade = "";
    [ObservableProperty] private string _currentTechniqueDetails = "";

    // Aktives KI-Modell Anzeige
    [ObservableProperty] private string _activeModelName = "";
    [ObservableProperty] private bool _isModelActive;

    // Match-Rate Prozentsaetze
    [ObservableProperty] private double _exactPercent;
    [ObservableProperty] private double _partialPercent;
    [ObservableProperty] private double _mismatchPercent;
    [ObservableProperty] private double _noFindingsPercent;
    private int _totalExact, _totalPartial, _totalMismatch, _totalNoFindings;

    private void RefreshMatchRatePercents()
    {
        var p = SelfTrainingStatusCalculator.ComputeMatchRatePercents(
            _totalExact, _totalPartial, _totalMismatch, _totalNoFindings);
        ExactPercent = p.Exact;
        PartialPercent = p.Partial;
        MismatchPercent = p.Mismatch;
        NoFindingsPercent = p.NoFindings;
    }

    private void AddSelfTrainingLog(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        void Apply()
        {
            SelfTrainingLogEntries.Add(line);
            while (SelfTrainingLogEntries.Count > 100)
                SelfTrainingLogEntries.RemoveAt(0);
        }
        if (System.Windows.Application.Current?.Dispatcher is { } d && !d.CheckAccess())
            d.Invoke(Apply);
        else
            Apply();
    }

    private void UpdateCodeDistribution(string code, MatchLevel level)
    {
        void Apply()
        {
            var entry = CodeDistribution.FirstOrDefault(e => e.Code == code);
            if (entry is null)
            {
                entry = new CodeDistributionEntry { Code = code };
                CodeDistribution.Add(entry);
            }
            SelfTrainingStatusCalculator.ApplyMatch(entry, level);
        }
        if (System.Windows.Application.Current?.Dispatcher is { } d && !d.CheckAccess())
            d.Invoke(Apply);
        else
            Apply();
    }

    /// <summary>Wird vom SelfTrainingOrchestrator bei jedem Schritt aufgerufen.</summary>
    public void OnSelfTrainingStep(SelfTrainingStep step)
    {
        var presentation = SelfTrainingStepPresentationBuilder.Build(step, _activeVisionModel);

        void Apply()
        {
            PipelineActiveStep = presentation.PipelineActiveStep;
            CurrentEntryCode = presentation.CurrentEntryCode;
            CurrentEntryMeter = presentation.CurrentEntryMeter;
            ProgressValue = presentation.ProgressValue;
            ProgressMax = presentation.ProgressMax;
            ActiveModelName = presentation.ActiveModelName;
            IsModelActive = presentation.IsModelActive;

            if (presentation.CurrentTechniqueGrade is not null)
            {
                CurrentTechniqueGrade = presentation.CurrentTechniqueGrade;
                CurrentTechniqueDetails = presentation.CurrentTechniqueDetails ?? "";
            }

            if (presentation.CurrentComparisonText is not null)
                CurrentComparisonText = presentation.CurrentComparisonText;

            foreach (var logLine in presentation.LogLines)
                AddSelfTrainingLog(logLine);

            if (presentation.LiveFramePath is not null)
                SetLiveFrameThrottled(presentation.LiveFramePath);

            if (presentation.Result is not null && presentation.CompletedMatchLevel is { } level)
            {
                switch (level)
                {
                    case MatchLevel.ExactMatch: _totalExact++; break;
                    case MatchLevel.PartialMatch: _totalPartial++; break;
                    case MatchLevel.Mismatch: _totalMismatch++; break;
                    case MatchLevel.NoFindings: _totalNoFindings++; break;
                }
                RefreshMatchRatePercents();

                SelfTrainingResults.Add(presentation.Result);
                UpdateCodeDistribution(presentation.Result.VsaCode, level);
            }
        }

        if (System.Windows.Application.Current?.Dispatcher is { } d && !d.CheckAccess())
            d.Invoke(Apply);
        else
            Apply();
    }

    /// <summary>Setzt alle Selbsttraining-Visualisierungen zurueck.</summary>
    /// <param name="resetMatchRate">Match-Rate auf 0 setzen (nur bei echtem Selbsttraining, nicht bei Batch-Import).</param>
    private void ResetSelfTrainingVisuals(bool resetMatchRate = false)
    {
        SelfTrainingResults.Clear();
        CodeDistribution.Clear();
        SelfTrainingLogEntries.Clear();
        PipelineActiveStep = 0;
        CurrentEntryCode = "";
        CurrentEntryMeter = 0;
        CurrentComparisonText = "";
        CurrentTechniqueGrade = "";
        CurrentTechniqueDetails = "";
        if (resetMatchRate)
        {
            _totalExact = _totalPartial = _totalMismatch = _totalNoFindings = 0;
            RefreshMatchRatePercents();
        }
    }

    private readonly List<string> _rootFolders = new();
    private CancellationTokenSource? _genCts;

    /// <summary>Fügt eine Zeile zum Log hinzu (Thread-safe via Dispatcher).</summary>
    private void Log(string message)
    {
        var ts = $"[{DateTime.Now:HH:mm:ss}]";
        var line = $"{ts} {message}\n";
        void Apply()
        {
            LogText += line;
            // Auch ins Echtzeit-Log schreiben (klappbares Panel)
            SelfTrainingLogEntries.Add($"{ts} {message}");
            while (SelfTrainingLogEntries.Count > 100)
                SelfTrainingLogEntries.RemoveAt(0);
        }
        if (System.Windows.Application.Current?.Dispatcher is { } d && !d.CheckAccess())
            d.Invoke(Apply);
        else
            Apply();
    }

    /// <summary>Aktualisiert die Live-Vorschau (Thread-safe).</summary>
    private void UpdateLivePreview(string caseInfo, string code, string meter, string? framePath)
    {
        void Apply()
        {
            var preview = TrainingLivePreviewPresenter.Build(caseInfo, code, meter, framePath);
            LiveCaseInfo = preview.LiveCaseInfo;
            LiveCodeInfo = preview.LiveCodeInfo;
            LiveMeterInfo = preview.LiveMeterInfo;
            CurrentComparisonText = preview.CurrentComparisonText;
            CurrentEntryCode = preview.CurrentEntryCode;
            if (preview.FramePath is not null)
                SetLiveFrameThrottled(preview.FramePath);
            else if (string.IsNullOrEmpty(LiveFramePath))
                LiveFramePath = ""; // Explizit leer setzen damit UI reagiert
        }

        if (System.Windows.Application.Current?.Dispatcher is { } d && !d.CheckAccess())
            d.Invoke(Apply);
        else
            Apply();
    }

    private void ClearLivePreview()
    {
        var preview = TrainingLivePreviewPresenter.Clear();
        SetLiveFrameThrottled(preview.FramePath);
        LiveCaseInfo = preview.LiveCaseInfo;
        LiveCodeInfo = preview.LiveCodeInfo;
        LiveMeterInfo = preview.LiveMeterInfo;
        CurrentComparisonText = preview.CurrentComparisonText;
        CurrentEntryCode = preview.CurrentEntryCode;
    }

    private async Task RefreshKbStatusAsync()
    {
        try
        {
            var status = await _kbDiagnostics.ReadStatusAsync(20).ConfigureAwait(false);
            var presentation = TrainingKnowledgeBaseStatusPresentationBuilder.Build(status);

            void Apply()
            {
                KbSampleCount = presentation.SampleCount;
                KbErrorCount = presentation.ErrorCount;
                KbNewCount = presentation.NewCount;
                KbEmbeddingCount = presentation.EmbeddingCount;
                KbCodesCovered = presentation.CodesCovered;
                KbLastUpdate = presentation.LastUpdateText;
                KbReadinessLabel = presentation.ReadinessLabel;
                KbReadinessBrush = presentation.ReadinessBrush;
                KbTopCodesText = presentation.TopCodesText;
            }

            if (System.Windows.Application.Current?.Dispatcher is { } d && !d.CheckAccess())
                d.Invoke(Apply);
            else
                Apply();

            // KB-Qualitaet ebenfalls aktualisieren
            await RefreshKbQualityAsync();
        }
        catch
        {
            // KB might not exist yet — silently ignore
        }
    }

    /// <summary>
    /// Laedt KB-Qualitaetsmetriken: Coverage-Luecken, Accuracy, Stale Samples, Trend.
    /// </summary>
    private async Task RefreshKbQualityAsync()
    {
        try
        {
            var quality = await _kbDiagnostics.ReadQualityAsync().ConfigureAwait(false);

            var runs = await SelfTrainingHistoryStore.LoadAsync();
            var presentation = TrainingKnowledgeBaseQualityPresentationBuilder.Build(quality, runs);

            void Apply()
            {
                KbCoverageGapsText = presentation.CoverageGapsText;
                KbCoverageGapsCount = presentation.CoverageGapsCount;
                KbAccuracyText = presentation.AccuracyText;
                KbStaleSampleCount = presentation.StaleSampleCount;
                KbTrendText = presentation.TrendText;
                KbTrendDirection = presentation.TrendDirection;

                // Stale-Sample Warnung im Log (E1)
                foreach (var logLine in presentation.LogLines)
                    Log(logLine);
            }
            if (System.Windows.Application.Current?.Dispatcher is { } d && !d.CheckAccess())
                d.Invoke(Apply);
            else
                Apply();
        }
        catch { /* KB evtl. noch nicht vorhanden */ }
    }

    public TrainingCenterViewModel(
        TrainingCenterStore store,
        TrainingCenterImportService import,
        ICodeCatalogProvider? codeCatalog,
        IKnowledgeBaseDiagnosticsRunner kbDiagnostics,
        AppSettings? settings = null)
    {
        _store = store;
        _import = import;
        _codeCatalog = codeCatalog;
        _kbDiagnostics = kbDiagnostics;
        _settings = settings;
    }

    // ── Cases ────────────────────────────────────────────────────────────────

    public async Task LoadAsync()
    {
        var state = await _store.LoadAsync();
        Cases.Clear();
        foreach (var c in state.Cases)
            Cases.Add(c);

        // Root-Ordner wiederherstellen
        if (state.RootFolders.Count > 0)
        {
            _rootFolders.Clear();
            foreach (var folder in state.RootFolders)
            {
                if (Directory.Exists(folder))
                    _rootFolders.Add(folder);
            }
            RootFolder = TrainingCenterDisplayFormatter.FormatRootFolders(_rootFolders);
        }

        StatusText = $"Geladen: {Cases.Count} Fälle";

        await LoadSamplesInternalAsync();
        await RefreshKbStatusAsync();
        await LoadLastMatchRateAsync();
    }

    /// <summary>
    /// Laedt die letzte Match-Rate aus der Selbsttraining-Historie,
    /// damit beim Oeffnen des Training Centers ein sinnvoller Wert angezeigt wird.
    /// </summary>
    private async Task LoadLastMatchRateAsync()
    {
        try
        {
            var runs = await SelfTrainingHistoryStore.LoadAsync();
            var presentation = SelfTrainingLastMatchRatePresentationBuilder.Build(runs);
            if (presentation is null) return;

            ExactPercent = presentation.ExactPercent;
            PartialPercent = presentation.PartialPercent;
            MismatchPercent = presentation.MismatchPercent;
            NoFindingsPercent = presentation.NoFindingsPercent;
        }
        catch { /* Historie nicht vorhanden */ }
    }

    [RelayCommand]
    private void BrowseRootFolder()
    {
        var dlg = new OpenFolderDialog
        {
            Title = "Trainings-Ordner wählen (Mehrfachauswahl möglich)",
            Multiselect = true
        };
        if (dlg.ShowDialog() != true)
            return;

        // Neue Auswahl zu bestehenden hinzufügen (Duplikate vermeiden)
        foreach (var folder in dlg.FolderNames)
        {
            if (!_rootFolders.Contains(folder, StringComparer.OrdinalIgnoreCase))
                _rootFolders.Add(folder);
        }

        UpdateRootFolderDisplay();
    }

    [RelayCommand]
    private void ClearRootFolders()
    {
        _rootFolders.Clear();
        UpdateRootFolderDisplay();
    }

    private void UpdateRootFolderDisplay()
    {
        RootFolder = TrainingCenterDisplayFormatter.FormatRootFolders(_rootFolders);
    }

    [RelayCommand]
    private async Task DistributeHaltungAsync()
    {
        if (IsBusy) return;

        // 1. PDF auswählen
        var pdfDlg = new OpenFileDialog
        {
            Title = "Haltungs-PDF wählen",
            Filter = "PDF (*.pdf)|*.pdf"
        };
        if (pdfDlg.ShowDialog() != true) return;
        var pdfPath = pdfDlg.FileName;

        // 2. Video-Ordner auswählen
        var videoDlg = new OpenFolderDialog
        {
            Title = "Video-Ordner wählen (Film-Ordner mit Haltungs-Videos)"
        };
        if (videoDlg.ShowDialog() != true) return;
        var videoFolder = videoDlg.FolderName;

        // 3. Output-Ordner: neben dem PDF, Unterordner "TrainingCases"
        var pdfDir = Path.GetDirectoryName(pdfPath) ?? videoFolder;
        var projectName = Path.GetFileNameWithoutExtension(pdfPath);
        var outputFolder = Path.Combine(Path.GetDirectoryName(pdfDir) ?? pdfDir, $"{projectName}_Training");

        try
        {
            IsBusy = true;
            LogText = "";
            StatusText = "PDF nach Haltungen aufteilen...";
            Log($"PDF: {pdfPath}");
            Log($"Videos: {videoFolder}");
            Log($"Output: {outputFolder}");

            var result = await _import.DistributeByHaltungAsync(pdfPath, videoFolder, outputFolder);

            foreach (var msg in result.Messages)
                Log($"  {msg}");

            Log($"--- Fertig: {result.Distributed} Haltungen verteilt, {result.VideosMatched} Videos zugeordnet ---");

            if (result.Uncertain > 0)
                Log($"  {result.Uncertain} Chunks ohne Haltungs-ID uebersprungen.");

            StatusText = $"Verteilt: {result.Distributed} Haltungen, {result.VideosMatched} Videos → {outputFolder}";

            // Output-Ordner automatisch als Root-Ordner setzen
            if (result.Distributed > 0)
            {
                if (!_rootFolders.Contains(outputFolder, StringComparer.OrdinalIgnoreCase))
                    _rootFolders.Add(outputFolder);
                UpdateRootFolderDisplay();
                Log($"Output-Ordner als Trainings-Ordner hinzugefuegt. Klicke 'Scannen' zum Laden.");
            }
        }
        catch (Exception ex)
        {
            Log($"Fehler: {ex.Message}");
            StatusText = $"Fehler bei Verteilung: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        if (IsBusy) return;
        if (_rootFolders.Count == 0)
        {
            StatusText = "Bitte zuerst einen oder mehrere Ordner wählen.";
            return;
        }

        try
        {
            IsBusy = true;
            StatusText = "Scanne Ordner...";
            Cases.Clear();

            foreach (var folder in _rootFolders)
            {
                if (!Directory.Exists(folder)) continue;
                var found = await _import.ScanAsync(folder);
                foreach (var c in found.Select(TrainingCenterRuntimeHelpers.ToTrainingCase))
                    Cases.Add(c);
            }

            var withProto = Cases.Count(c => !string.IsNullOrEmpty(c.ProtocolPath));
            var pdfOnly = Cases.Count(c => string.IsNullOrEmpty(c.VideoPath) && !string.IsNullOrEmpty(c.ProtocolPath));
            StatusText = TrainingCenterDisplayFormatter.FormatScanSummary(Cases.Count, withProto, pdfOnly);

            // Auto-Save: Faelle + Ordner persistieren
            await AutoSaveStateAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Speichert Faelle + Root-Ordner automatisch (ohne UI-Feedback).</summary>
    private async Task AutoSaveStateAsync()
    {
        try
        {
            await _store.SaveAsync(BuildState());
        }
        catch { /* stilles Speichern */ }
    }

    private TrainingCenterState BuildState()
        => new()
        {
            Cases = Cases.ToList(),
            RootFolders = new List<string>(_rootFolders),
            UpdatedUtc = DateTime.UtcNow
        };

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            await _store.SaveAsync(BuildState());
            StatusText = $"Gespeichert: {Cases.Count} Fälle, {_rootFolders.Count} Ordner";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool HasSelection() => SelectedCase is not null;

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Approve()
    {
        if (SelectedCase is null) return;
        SelectedCase.Status = TrainingCaseStatus.Approved;
        StatusText = $"Approved: {SelectedCase.CaseId}";
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Reject()
    {
        if (SelectedCase is null) return;
        SelectedCase.Status = TrainingCaseStatus.Rejected;
        StatusText = $"Rejected: {SelectedCase.CaseId}";
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void SetNew()
    {
        if (SelectedCase is null) return;
        SelectedCase.Status = TrainingCaseStatus.New;
        StatusText = $"Status New: {SelectedCase.CaseId}";
    }

    partial void OnSelectedCaseChanged(TrainingCase? value)
    {
        ApproveCommand.NotifyCanExecuteChanged();
        RejectCommand.NotifyCanExecuteChanged();
        SetNewCommand.NotifyCanExecuteChanged();
        GenerateSamplesCommand.NotifyCanExecuteChanged();
    }

    // ── Samples ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task LoadSamplesAsync()
    {
        await LoadSamplesInternalAsync();
    }

    private async Task LoadSamplesInternalAsync()
    {
        var list = await TrainingSamplesStore.LoadAsync().ConfigureAwait(false);
        OnUi(() =>
        {
            Samples.Clear();
            foreach (var s in list)
                Samples.Add(s);
        });
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task GenerateSamplesAsync()
    {
        if (SelectedCase is null || IsBusy) return;

        _genCts?.Cancel();
        _genCts?.Dispose();
        _genCts = new CancellationTokenSource();
        var ct = _genCts.Token;

        using var _aiToken = AiTrack.Begin("Training Center");
        try
        {
            IsBusy = true;
            StatusText = $"Generiere Samples für {SelectedCase.CaseId}...";

            var cfg = new AppSettingsAiSettingsProvider()
                .Load()
                .ToRuntimeSettings();
            var settings = await TrainingCenterSettingsStore.LoadAsync();
            var meterSvc = TrainingCenterRuntimeHelpers.CreateMeterTimelineService(cfg, settings.GpuConcurrency);
            var generator = new TrainingSampleGenerator(cfg, meterSvc, settings, _codeCatalog);

            var existing = await TrainingSamplesStore.LoadAsync();
            var existingSigs = existing.Select(s => s.Signature).ToHashSet(StringComparer.Ordinal);

            var generation = await generator.GenerateWithDiagnosticsAsync(
                TrainingCenterRuntimeHelpers.ToTrainingCaseInput(SelectedCase), existingSigs, framesDir: null, ct);
            var newSamples = generation.Samples;

            if (newSamples.Count == 0)
            {
                StatusText = TrainingCenterSampleGenerationStatusFormatter.FormatEmptyCaseStatus(
                    SelectedCase.CaseId,
                    SelectedCase.ProtocolPath,
                    generation);
                return;
            }

            await TrainingSamplesStore.MergeAndSaveAsync(newSamples);

            foreach (var s in newSamples)
                Samples.Add(s);

            StatusText = $"{newSamples.Count} neue Samples generiert für {SelectedCase.CaseId}.";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Sample-Generierung abgebrochen.";
        }
        catch (Exception ex)
        {
            StatusText = $"Fehler bei Sample-Generierung: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool HasSampleSelection() => SelectedSample is not null;

    [RelayCommand(CanExecute = nameof(HasSampleSelection))]
    private async Task ApproveSampleAsync()
    {
        var sample = SelectedSample;
        if (sample is null) return;

        var decision = TrainingSampleDecisionController.Approve(sample);
        StatusText = decision.StatusText;
        await PersistSamplesAsync(decision.PersistChangedSample ? sample : null);
    }

    [RelayCommand(CanExecute = nameof(HasSampleSelection))]
    private async Task RejectSampleAsync()
    {
        var sample = SelectedSample;
        if (sample is null) return;

        var decision = TrainingSampleDecisionController.Reject(sample);
        if (decision.ShouldDeindex)
            TryDeindexSample(sample.SampleId);
        StatusText = decision.StatusText;
        await PersistSamplesAsync(decision.PersistChangedSample ? sample : null);
    }

    [RelayCommand(CanExecute = nameof(HasSampleSelection))]
    private async Task RemoveSampleAsync()
    {
        var sample = SelectedSample;
        if (sample is null) return;

        var decision = TrainingSampleDecisionController.Remove(sample);
        if (decision.ShouldDeindex)
            TryDeindexSample(sample.SampleId);
        StatusText = decision.StatusText;
        await PersistSamplesAsync(decision.PersistChangedSample ? sample : null);
    }

    /// <summary>
    /// Entfernt ein Sample aus der Wissensdatenbank (Deindex), ohne Ollama zu benoetigen.
    /// Fehler werden still geschluckt — die Status-Aenderung bleibt persistiert.
    /// </summary>
    private void TryDeindexSample(string sampleId)
    {
        try
        {
            var ollamaConfig = new AppSettingsAiSettingsProvider().Load().ToOllamaConfig();
            _kbHttpClient ??= new System.Net.Http.HttpClient { Timeout = ollamaConfig.RequestTimeout };
            using var kbCtx = new KnowledgeBaseContext();
            var embedder = new EmbeddingService(_kbHttpClient, ollamaConfig);
            var kbManager = new KnowledgeBaseManager(kbCtx, embedder);
            kbManager.DeindexSample(sampleId);
        }
        catch { /* KB evtl. nicht erreichbar — Status-Aenderung bleibt persistiert */ }
    }

    [RelayCommand]
    private async Task ExportApprovedAsync()
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            var targetPath = Path.Combine(AppSettings.AppDataDir, "data", "protocol_training.json");
            var result = await TrainingApprovedProtocolExportController.RunAsync(
                Samples.ToList(),
                IsTrainingExportEligible,
                ProtocolTrainingStore.AddSample,
                () => PersistSamplesAsync(),
                () => DateTime.UtcNow,
                targetPath).ConfigureAwait(false);

            foreach (var line in result.LogLines)
                Log(line);

            StatusText = result.StatusText;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Batch-Import: Scannt alle Ordner, generiert Samples, approved automatisch,
    /// indiziert in die Knowledge Base. Alles in einem Durchlauf.
    /// </summary>
    [RelayCommand]
    private async Task BatchImportAndIndexAsync()
    {
        var runPreparation = TrainingBatchImportRunPreparationController.Prepare(
            IsBusy,
            _rootFolders.Count,
            _genCts,
            value => StatusText = value);
        if (runPreparation.ShouldStop)
            return;

        // S8: Dieser Pfad setzt erkannte Samples automatisch auf Approved und indexiert sie
        // OHNE manuelle Pruefung direkt in die Knowledge Base (umgeht die Review-Politik des
        // Selbsttrainings). Falsche Labels verschlechtern damit dauerhaft alle kuenftigen
        // KI-Vorschlaege. Darum einmalige, bewusste Bestaetigung pro Lauf verlangen.
        var confirmation = TrainingBatchImportAutoApproveConfirmationController.Confirm(
            DialogHost.Current);
        if (!confirmation.ShouldContinue)
        {
            runPreparation.CancellationTokenSource?.Dispose();
            StatusText = confirmation.StatusText ?? "";
            return;
        }

        _genCts = runPreparation.CancellationTokenSource;
        var ct = runPreparation.CancellationToken;

        using var _aiToken = AiTrack.Begin("Training Center");
        try
        {
            TrainingBatchImportRunStartController.Apply(
                value => IsBusy = value,
                value => LogText = value,
                value => ProgressValue = value,
                value => ProgressMax = value,
                ClearLivePreview,
                () => ResetSelfTrainingVisuals());

            var scanWorkflow = await TrainingBatchImportScanWorkflowController.RunAsync(
                _rootFolders.Count,
                () => TrainingBatchImportScanController.ScanAsync(
                    _rootFolders,
                    Directory.Exists,
                    async folder => (await _import.ScanAsync(folder).ConfigureAwait(false))
                        .Select(TrainingCenterRuntimeHelpers.ToTrainingCase)
                        .ToList(),
                    Log),
                Cases,
                Log,
                value => StatusText = value);
            if (scanWorkflow.ShouldStop)
                return;
            var casesWithProtocol = scanWorkflow.CasesWithProtocol;

            var runtimeSetup = await TrainingBatchImportRuntimeSetupController.PrepareAsync(
                casesWithProtocol,
                () => PlayerAiSettingsLoader.LoadRuntimeSettings(),
                TrainingCenterSettingsStore.LoadAsync,
                (cfg, settings) =>
                {
                    var meterSvc = TrainingCenterRuntimeHelpers.CreateMeterTimelineService(cfg, settings.GpuConcurrency);
                    return new TrainingSampleGenerator(cfg, meterSvc, settings, _codeCatalog);
                },
                TrainingSamplesStore.LoadAsync,
                value => ProgressMax = value,
                Log);
            var cfg = runtimeSetup.Config;
            var generator = runtimeSetup.Generator;
            var allSamples = runtimeSetup.AllSamples;
            var existingSigs = runtimeSetup.ExistingSignatures;
            var casesToProcess = runtimeSetup.CasesToProcess;
            var runSummary = runtimeSetup.RunSummary;

            await TrainingBatchImportCaseLoopController.RunAsync(
                casesToProcess,
                (caseIndex, totalCount, trainingCase) => TrainingBatchImportCaseProgressUiController.Apply(
                    caseIndex,
                    totalCount,
                    trainingCase,
                    value => ProgressValue = value,
                    value => StatusText = value,
                    Log),
                async (caseIndex, trainingCase, token) =>
                {
                    await TrainingBatchImportCaseWorkflowController.ProcessAsync(
                        trainingCase,
                        existingSigs,
                        allSamples,
                        SelfTrainingResults.Count + 1,
                        caseIndex + 1,
                        runSummary,
                        (currentCase, currentToken) => TrainingCenterRuntimeHelpers.ExtractPreviewFrameAsync(currentCase, cfg, currentToken),
                        (input, signatures, currentToken) => generator.GenerateWithDiagnosticsAsync(input, signatures, framesDir: null, currentToken),
                        preview => UpdateLivePreview(
                            preview.CaseInfo,
                            preview.CodeInfo,
                            preview.MeterInfo,
                            preview.FramePath),
                        OnUi,
                        SelfTrainingResults.Add,
                        UpdateCodeDistribution,
                        TrainingSamplesStore.MergeAndSaveAsync,
                        () => _store.SaveAsync(BuildState()),
                        value => KbSampleCount = value,
                        value => KbCodesCovered = value,
                        Log,
                        token).ConfigureAwait(false);
                },
                ex => TrainingBatchImportRunExceptionController.RecordCaseFailure(
                    ex,
                    runSummary,
                    Log),
                ct).ConfigureAwait(false);

            var completion = await TrainingBatchImportRunCompletionController.CompleteAsync(
                runSummary,
                casesToProcess.Count,
                async () => await TrainingSamplesStore.LoadAsync(),
                Samples.Clear,
                Samples.Add,
                RefreshKbStatusAsync,
                () => _store.SaveAsync(BuildState()),
                Log,
                value => StatusText = value);
            if (completion.ShouldStop)
                return;
        }
        catch (OperationCanceledException)
        {
            TrainingBatchImportRunExceptionController.ApplyCanceled(
                Log,
                value => StatusText = value);
        }
        catch (Exception ex)
        {
            TrainingBatchImportRunExceptionController.ApplyFatal(
                ex,
                Log,
                value => StatusText = value);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void CancelBatch()
    {
        StatusText = TrainingBatchImportRunControlController.RequestCancel(_genCts);
    }

    [RelayCommand]
    private async Task CheckKnowledgeBaseAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            StatusText = "Prüfe Knowledge Base...";

            var summary = await _kbDiagnostics.ReadSummaryAsync(12).ConfigureAwait(false);

            var presentation = TrainingKnowledgeBaseCheckPresentationBuilder.Build(summary);
            foreach (var line in presentation.LogLines)
                Log(line);
            StatusText = presentation.StatusText;

            await RefreshKbStatusAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"KB-Prüfung fehlgeschlagen: {ex.Message}";
            Log($"KB-Prüfung FEHLER: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSelectedSampleChanged(TrainingSample? value)
    {
        ApproveSampleCommand.NotifyCanExecuteChanged();
        RejectSampleCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Beim Wechsel des Review-Kandidaten PendingBox zuruecksetzen (B5).
    /// Die visuelle Box wird zusaetzlich vom Code-behind via OnVmPropertyChanged geloescht.
    /// </summary>
    partial void OnSelectedReviewItemChanged(InfraSelfImproving.ReviewQueueItem? value)
    {
        PendingBox = null;
        PendingSamMask = null;
    }


    /// <summary>
    /// Speichert alle Samples und indexiert optional ein gerade geaendertes Sample in die KB.
    /// </summary>
    private async Task PersistSamplesAsync(TrainingSample? changedSample = null)
    {
        await TrainingSamplePersistenceWorkflowController.PersistAsync(
            Samples.ToList(),
            changedSample,
            TrainingSamplesStore.MergeOrUpdateAsync,
            (samples, ct) => IncrementalKbUpdateWithReasonAsync(samples.ToList(), ct),
            CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// Holt alle menschlich bestaetigten Gold-Samples (Status=Approved), die noch nicht in der KB
    /// stehen (KbIndexState != Indexed), nachtraeglich in die KnowledgeBase.db. Legt vorher ein
    /// reversibles Backup an. Schreibt den KbIndexState pro Sample zurueck in training_samples.json.
    ///
    /// Hintergrund: Der Codiermodus persistiert bestaetigte Befunde zwar als Gold, indexiert sie aber
    /// nicht zuverlaessig in die KB (das Index-Ergebnis wurde frueher nicht zurueckgeschrieben). Dieser
    /// Lauf holt genau diese Nachzuegler — "mehr konkrete, richtige Beispiele" in der durchsuchbaren KB.
    /// Rein additiv: Eval-Schutz und IsIndexWorthy greifen ueber den bestehenden Index-Pfad weiter.
    /// </summary>
    [RelayCommand]
    private async Task ReconcileGoldToKbAsync()
    {
        // Nebenlaeufigkeits-Guard: nie parallel zu Batch/Self-Training oder mehrfach gleichzeitig —
        // sonst koennten Backup, JSON-Status und KB-Index gegeneinander arbeiten.
        if (IsBusy || IsSelfTrainingRunning) return;

        // An die vorhandene Abbruch-Infrastruktur anschliessen (CancelBatch cancelt _genCts,
        // der "Abbrechen"-Button ist bei IsBusy sichtbar).
        _genCts?.Cancel();
        _genCts?.Dispose();
        _genCts = new CancellationTokenSource();
        var ct = _genCts.Token;

        try
        {
            IsBusy = true;

            await TrainingGoldKbReconcileWorkflowController.RunAsync(
                TrainingSamplesStore.LoadAsync,
                TrainingSamplesStore.MergeOrUpdateAsync,
                IncrementalKbUpdateWithReasonAsync,
                async (path, progress, token) =>
                {
                    var backup = await KnowledgeBackupService.ExportAsync(path, progress, token).ConfigureAwait(false);
                    return new TrainingGoldKbReconcileBackupResult(
                        backup.Success,
                        backup.Error,
                        backup.FileCount);
                },
                () => KnowledgeBasePaths.GetRoot(),
                () => DateTime.Now,
                directory => System.IO.Directory.CreateDirectory(directory),
                Log,
                SetStatus,
                ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Abbruch ist sauber: bereits verarbeitete Bloecke sind persistiert, der Rest behaelt
            // seinen Zustand und wird beim naechsten Lauf erneut aufgegriffen.
            Log("KB-Nachholen abgebrochen.");
            SetStatus("KB-Nachholen abgebrochen");
        }
        catch (Exception ex)
        {
            Log($"KB-Nachholen Fehler: {ex.Message}");
            SetStatus("KB-Nachholen fehlgeschlagen");
        }
        finally
        {
            // IsBusy ist ein UI-Property; finally laeuft nach ConfigureAwait(false) ggf. auf
            // dem Worker-Thread -> dispatcher-sicher zuruecksetzen.
            OnUi(() => IsBusy = false);
        }
    }


    // ── Review Queue (Self-Improving Loop) ──────────────────────────────

    /// <summary>Fuehrt UI-gebundene Aenderungen (ObservableCollection/Status) auf dem Dispatcher-Thread aus.</summary>
    private static void OnUi(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess()) action();
        else dispatcher.Invoke(action);
    }

    // Thread-sicheres Setzen von StatusText: in async-Methoden mit ConfigureAwait(false)
    // laeuft die Fortsetzung auf einem Worker-Thread; ein direktes StatusText = ... waere ein
    // UI-Update vom falschen Thread (WPF-Crash-Risiko). Daher ueber den Dispatcher (OnUi).
    private void SetStatus(string text) => OnUi(() => StatusText = text);

    /// <summary>Loads pending review items into the queue.</summary>
    public void LoadReviewQueue(InfraSelfImproving.ReviewQueueService queueService)
    {
        OnUi(() =>
        {
            var loadResult = TrainingReviewQueueLoadController.Load(queueService);
            TrainingReviewQueueLoadController.Apply(loadResult, ReviewQueue);
            ReviewQueueCount = loadResult.ReviewQueueCount;
            ReviewStatusText = loadResult.StatusText;
        });
    }

    /// <summary>
    /// Loest die SampleId eines Self-Training-Review-Items auf: bevorzugt die direkte SampleId,
    /// sonst (Altbestand ohne SampleId) ueber Fuzzy-Match CaseId/Code/Meter±0.2. Null = nicht gefunden.
    /// </summary>
    private async Task<string?> ResolveSelfTrainingSampleIdAsync(InfraSelfImproving.ReviewQueueItem item)
    {
        return await SelfTrainingReviewSampleIdResolver.ResolveAsync(
            item,
            TrainingSamplesStore.LoadAsync).ConfigureAwait(false);
    }

    /// <summary>Baut den Review-Approval-Service mit Delegate auf die bestehende VM-KB-Indexierung.</summary>
    private IReviewApprovalService BuildReviewApprovalService()
    {
        var indexer = new DelegatingKnowledgeBaseIndexer(
            (s, c) => IncrementalKbUpdateWithReasonAsync(s.ToList(), c),
            TryDeindexSample);
        return new ReviewApprovalService(new TrainingSamplesStoreAdapter(), indexer);
    }

    /// <summary>
    /// Approve a review item (accept the suggested code).
    /// Optionaler <paramref name="box"/>-Parameter: vom Reviewer gezeichnete YOLO-Box (B5).
    /// </summary>
    public async Task ApproveReviewItemAsync(
        InfraSelfImproving.ReviewQueueItem item,
        InfraSelfImproving.FeedbackIngestionService feedback,
        InfraSelfImproving.ReviewQueueService queueService,
        CancellationToken ct = default,
        BoundingBox? box = null,
        TrainingSegmentationMask? mask = null)
    {
        if (item.Entry is not null)
        {
            await feedback.ProcessFeedbackAsync(
                item.Entry, item.Entry.SuggestedCode ?? "", accepted: true, ct).ConfigureAwait(false);
        }
        else if (item.IsFromSelfTraining)
        {
            var sampleId = await ResolveSelfTrainingSampleIdAsync(item).ConfigureAwait(false);
            if (string.IsNullOrEmpty(sampleId))
            {
                Log($"Self-Training Review: Sample nicht gefunden ({item.SelfTrainingCaseId}/{item.SelfTrainingVsaCode}@{item.SelfTrainingMeter:F1}m)");
            }
            else
            {
                var svc = BuildReviewApprovalService();
                // box uebergeben: wenn Reviewer eine Box gezeichnet hat, wird HasBbox=true gesetzt (B5)
                var result = await svc.ApproveSelfTrainingAsync(sampleId, box, ct, System.Environment.UserName, mask).ConfigureAwait(false);
                if (result.Found)
                {
                    var bboxInfo = box.HasValue ? " (Box gesetzt)" : "";
                    Log($"Self-Training Review: {item.SelfTrainingVsaCode}@{item.SelfTrainingMeter:F1}m → Approved{bboxInfo}, KB: {(result.Indexed ? "Indexed" : "Error")}");
                }
                await LoadSamplesInternalAsync().ConfigureAwait(false);
            }
        }
        OnUi(() =>
        {
            var completion = TrainingReviewQueueCompletionController.ApplyApproved(item, queueService, ReviewQueue);
            ReviewQueueCount = completion.ReviewQueueCount;
            ReviewStatusText = completion.StatusText;
            Log(completion.LogText);
        });
    }

    /// <summary>Reject a review item with a corrected code.</summary>
    public async Task RejectReviewItemAsync(
        InfraSelfImproving.ReviewQueueItem item,
        string correctedCode,
        InfraSelfImproving.FeedbackIngestionService feedback,
        InfraSelfImproving.ReviewQueueService queueService,
        CancellationToken ct = default,
        string? correctedDescription = null)
    {
        if (item.Entry is not null)
        {
            await feedback.ProcessFeedbackAsync(
                item.Entry, correctedCode, accepted: false, ct).ConfigureAwait(false);
        }
        else if (item.IsFromSelfTraining)
        {
            var sampleId = await ResolveSelfTrainingSampleIdAsync(item).ConfigureAwait(false);
            if (string.IsNullOrEmpty(sampleId))
            {
                Log($"Self-Training Review: Sample nicht gefunden ({item.SelfTrainingCaseId}/{item.SelfTrainingVsaCode}@{item.SelfTrainingMeter:F1}m)");
            }
            else
            {
                var svc = BuildReviewApprovalService();
                var result = await svc.RejectSelfTrainingAsync(
                    sampleId,
                    correctedCode,
                    ct,
                    System.Environment.UserName,
                    correctedDescription).ConfigureAwait(false);
                if (result.Found)
                {
                    if (!string.IsNullOrEmpty(result.CorrectedSampleId))
                        Log($"Korrigiertes Sample {result.CorrectedSampleId} erzeugt");
                    else
                        Log($"Self-Training Review: {item.SelfTrainingVsaCode}@{item.SelfTrainingMeter:F1}m → Rejected");
                }
                await LoadSamplesInternalAsync().ConfigureAwait(false);
            }
        }
        OnUi(() =>
        {
            var completion = TrainingReviewQueueCompletionController.ApplyRejected(item, correctedCode, queueService, ReviewQueue);
            ReviewQueueCount = completion.ReviewQueueCount;
            ReviewStatusText = completion.StatusText;
            Log(completion.LogText);
        });
    }

    // ── Review Queue Commands ────────────────────────────────────────────

    private bool HasSelectedReviewItem => SelectedReviewItem is not null;

    /// <summary>Erzeugt FeedbackIngestionService mit optionalem KbManager fuer KB-Re-Indexierung.</summary>
    private InfraSelfImproving.FeedbackIngestionService CreateFeedbackService(
        KnowledgeBaseContext db)
    {
        var logger  = new AuswertungPro.Next.Infrastructure.Ai.QualityGate.ValidationLogger(db.Connection);
        var weights = new AuswertungPro.Next.Infrastructure.Ai.QualityGate.WeightLearningService(db.Connection);

        // KbManager optional — wenn Ollama offline, wird nur geloggt
        KnowledgeBaseManager? kbManager = null;
        try
        {
            var cfg = new AppSettingsAiSettingsProvider()
                .Load()
                .ToOllamaConfig();
            var http = new System.Net.Http.HttpClient { Timeout = cfg.RequestTimeout };
            var embedder = new EmbeddingService(http, cfg);
            var evalSets = EvalContaminationSetProvider.Load(_settings);
            kbManager = new KnowledgeBaseManager(db, embedder, evalSets.ImageHashes, evalSets.HaltungKeys);
        }
        catch { /* Ollama nicht verfuegbar — Feedback wird geloggt, KB-Update uebersprungen */ }

        return new InfraSelfImproving.FeedbackIngestionService(logger, weights, kbManager);
    }

    [RelayCommand(CanExecute = nameof(HasSelectedReviewItem))]
    private async Task ApproveSelectedReviewAsync(CancellationToken ct)
    {
        var item = SelectedReviewItem;
        if (item is null || ReviewQueueServiceRef is null) return;

        // Box/Maske vor dem await captureren (UI-State kann sich aendern) (B5)
        var box = PendingBox;
        var mask = PendingSamMask;

        try
        {
            using var db = new KnowledgeBaseContext();
            var feedback = CreateFeedbackService(db);
            await ApproveReviewItemAsync(item, feedback, ReviewQueueServiceRef, ct, box, mask).ConfigureAwait(false);

            // Box-/Masken-Model zuruecksetzen (visuelle Box wird via PropertyChanged/Selection-Change geloescht)
            PendingBox = null;
            PendingSamMask = null;
        }
        catch (Exception ex)
        {
            Log($"Review-Freigabe Fehler: {ex.Message}");
            OnUi(() => ReviewStatusText = $"Fehler: {ex.Message}");
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedReviewItem))]
    private async Task RejectSelectedReviewAsync(CancellationToken ct)
    {
        var item = SelectedReviewItem;
        if (item is null || ReviewQueueServiceRef is null) return;
        try
        {
            using var db = new KnowledgeBaseContext();
            var feedback = CreateFeedbackService(db);
            // Reine Ablehnung ohne Korrektur (correctedCode leer) -> Status Rejected + KB-Eintrag entfernt, kein _corr-Sample.
            await RejectReviewItemAsync(item, string.Empty, feedback, ReviewQueueServiceRef, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log($"Review-Ablehnung Fehler: {ex.Message}");
            OnUi(() => ReviewStatusText = $"Fehler: {ex.Message}");
        }
    }

    /// <summary>Wendet eine Review-Korrektur an: Original ablehnen+deindexieren, korrigiertes Sample anlegen+indexieren.</summary>
    public async Task ApplyReviewCorrectionAsync(
        InfraSelfImproving.ReviewQueueItem item,
        string correctedCode,
        CancellationToken ct = default,
        string? correctedDescription = null)
    {
        if (item is null || ReviewQueueServiceRef is null || string.IsNullOrWhiteSpace(correctedCode)) return;
        try
        {
            using var db = new KnowledgeBaseContext();
            var feedback = CreateFeedbackService(db);
            await RejectReviewItemAsync(
                item,
                correctedCode,
                feedback,
                ReviewQueueServiceRef,
                ct,
                correctedDescription).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log($"Review-Korrektur Fehler: {ex.Message}");
            OnUi(() => ReviewStatusText = $"Fehler: {ex.Message}");
        }
    }

    // ── Selbsttraining (Orchestrator) ──────────────────────────────────

    [ObservableProperty] private bool _isSelfTrainingRunning;
    private CancellationTokenSource? _selfTrainingCts;
    private ISelfTrainingOrchestrator? _selfTrainingOrchestrator;
    private string _activeVisionModel = OllamaConfig.DefaultVisionModel;

    [RelayCommand]
    private async Task RunSelfTrainingAsync()
    {
        if (IsBusy || IsSelfTrainingRunning) return;

        await SelfTrainingAutoScanController.RunAsync(
            Cases.Count,
            _rootFolders.Count,
            _rootFolders,
            Directory.Exists,
            async folder => (await _import.ScanAsync(folder))
                .Select(TrainingCenterRuntimeHelpers.ToTrainingCase)
                .ToList(),
            value => StatusText = value,
            Cases.Add);

        var selection = await SelfTrainingCaseSelectionWorkflowController.RunAsync(
            SelectedCase,
            Cases,
            TrainingSamplesStore.LoadAsync,
            value => StatusText = value,
            value => SelectedCase = value);
        if (selection.ShouldStop)
            return;

        var selectedCase = selection.Case;
        if (selectedCase is null)
            return;

        var runPreparation = SelfTrainingRunPreparationController.PrepareCancellation(_selfTrainingCts);
        _selfTrainingCts = runPreparation.CancellationTokenSource;
        var ct = runPreparation.CancellationToken;

        using var _aiToken = AiTrack.Begin("Selbsttraining");
        try
        {
            SelfTrainingRunStartController.Apply(
                selectedCase,
                value => IsBusy = value,
                value => IsSelfTrainingRunning = value,
                () => ResetSelfTrainingVisuals(resetMatchRate: true),
                value => LogText = value,
                value => StatusText = value,
                Log);

            using var selfTrainingSetup = await SelfTrainingRuntimeSetupController.PrepareAsync(
                () => PlayerAiSettingsLoader.LoadRuntimeSettings(),
                TrainingCenterSettingsStore.LoadAsync,
                () => PlayerAiSettingsLoader.LoadPlatformSettings().ToOllamaConfig(),
                config => _kbHttpClient ??= new System.Net.Http.HttpClient { Timeout = config.RequestTimeout },
                _settings,
                _codeCatalog,
                Log);
            _activeVisionModel = selfTrainingSetup.Session.ActiveVisionModel;
            _selfTrainingOrchestrator = selfTrainingSetup.Session.Orchestrator;

            // Progress-Callback verbindet Orchestrator → ViewModel-Visualisierungen
            var progress = new Progress<SelfTrainingStep>(OnSelfTrainingStep);

            Log(SelfTrainingRunPresentationBuilder.BuildPipelineStartedLog());
            var result = await SelfTrainingRunExecutionController.RunAsync(
                selfTrainingSetup.Session.Orchestrator,
                TrainingCenterRuntimeHelpers.ToTrainingCaseInput(selectedCase),
                progress,
                SelfTrainingHistorySnapshotBuilder.Build,
                SelfTrainingHistoryStore.AppendRunAsync,
                () => DateTime.UtcNow,
                ct);

            SelfTrainingRunCompletionController.Apply(
                result,
                Log,
                value => StatusText = value);

            // Inkrementelles KB-Update fuer ExactMatch-Samples (B1)
            await SelfTrainingKbUpdateController.RunApprovedSamplesUpdateAsync(
                result,
                TrainingSamplesStore.LoadAsync,
                TrainingSamplesStore.MergeOrUpdateAsync,
                IncrementalKbUpdateWithReasonAsync,
                Log,
                ct);

            await SelfTrainingReviewQueueWorkflowController.RunAsync(
                ReviewQueueServiceRef,
                result,
                TrainingSamplesStore.LoadAsync,
                LoadReviewQueue,
                Log);

            await SelfTrainingPostRunRefreshController.RefreshAsync(
                LoadSamplesInternalAsync,
                RefreshKbStatusAsync);
        }
        catch (OperationCanceledException)
        {
            SelfTrainingRunExceptionController.ApplyCanceled(
                Log,
                value => StatusText = value);
        }
        catch (Exception ex)
        {
            SelfTrainingRunExceptionController.ApplyFailure(
                ex,
                Log,
                value => StatusText = value);
        }
        finally
        {
            SelfTrainingRunFinalizerController.Apply(
                value => IsBusy = value,
                value => IsSelfTrainingRunning = value,
                () => _selfTrainingOrchestrator = null);
        }
    }

    /// <summary>
    /// Indexiert Samples inkrementell in die KB (ohne vollen Rebuild).
    /// Nutzt KnowledgeBaseManager.IndexSampleAsync pro Sample.
    /// </summary>
    /// <summary>
    /// Indexiert Samples inkrementell in die KB und liefert ein <see cref="KbIndexOutcome"/>, das
    /// erfolgreich indexierte von bewusst/dauerhaft uebersprungenen (Skipped: Eval-Schutz/nicht
    /// index-wuerdig) Samples unterscheidet. Alles, was in keiner der Listen steht, ist ein echter
    /// (transienter) Fehler -> der Aufrufer setzt KbIndexState.Error. Einziger KB-Index-Pfad fuer
    /// Review-Approval, Backfill und Self-Training.
    /// </summary>
    private async Task<KbIndexOutcome> IncrementalKbUpdateWithReasonAsync(List<TrainingSample> samples, CancellationToken ct)
    {
        var ollamaConfig = new AppSettingsAiSettingsProvider().Load().ToOllamaConfig();
        _kbHttpClient ??= new System.Net.Http.HttpClient { Timeout = ollamaConfig.RequestTimeout };
        var runner = TrainingKbIndexRunner.CreateDefault(
            ollamaConfig,
            _kbHttpClient,
            _settings,
            Log);
        return await runner.RunAsync(samples, ct);
    }

    [RelayCommand]
    private void StopSelfTraining()
    {
        StatusText = SelfTrainingRunControlController.RequestCancel(_selfTrainingCts);
    }

    [RelayCommand]
    private void PauseSelfTraining()
    {
        var pauseResult = SelfTrainingRunControlController.TogglePause(_selfTrainingOrchestrator);
        if (!pauseResult.Handled) return;

        StatusText = pauseResult.StatusText ?? "";
        Log(pauseResult.LogMessage ?? "");
    }

    // ── Protokoll-Startdaten (B6) ────────────────────────────────────────

    /// <summary>
    /// Reiht gepruefte Protokoll-Samples (Status=New, katalog-gueltig) als Startdaten-Kandidaten
    /// in die Review Queue ein. Keine KI-Analyse — Freigabe nur ueber ApproveReviewItemAsync.
    /// Bereits eingereihte Samples werden per SampleId dedupliziert.
    /// </summary>
    [RelayCommand]
    private async Task SuggestProtocolStartdataAsync()
    {
        if (ReviewQueueServiceRef is null) return;

        // Katalog: bevorzugt injizierter Catalog, Fallback auf globalen VsaCodeResolver
        var catalog = _codeCatalog ?? AuswertungPro.Next.Infrastructure.Ai.VsaCodeResolver.CurrentCatalog;
        if (catalog is null) { OnUi(() => ReviewStatusText = "Kein Code-Katalog verfuegbar."); return; }

        var all = await TrainingSamplesStore.LoadAsync().ConfigureAwait(false);
        var result = TrainingProtocolStartdataQueueController.Run(all, catalog, ReviewQueueServiceRef);
        LoadReviewQueue(ReviewQueueServiceRef);
        OnUi(() => ReviewStatusText = result.StatusText);
        Log(result.LogText);
    }

    /// <summary>Anzahl der aktuell als Protokoll-Startdaten eingereihten Kandidaten.</summary>
    public int StartdataCandidateCount =>
        TrainingProtocolStartdataReviewItemSelector.Count(ReviewQueue);

    private List<InfraSelfImproving.ReviewQueueItem> GetProtocolStartdataReviewItems()
    {
        List<InfraSelfImproving.ReviewQueueItem>? items = null;
        OnUi(() =>
        {
            items = TrainingProtocolStartdataReviewItemSelector.Select(ReviewQueue);
        });
        return items ?? new List<InfraSelfImproving.ReviewQueueItem>();
    }

    /// <summary>Gibt ALLE Protokoll-Startdaten-Kandidaten frei (nach expliziter Bestaetigung im View).</summary>
    public async Task ApproveAllStartdataAsync(CancellationToken ct = default)
    {
        var queueService = ReviewQueueServiceRef;
        if (queueService is null) return;

        var items = GetProtocolStartdataReviewItems();
        var result = await TrainingProtocolStartdataApprovalController.ApproveAllAsync(
            items,
            async (item, token) =>
            {
                using var db = new KnowledgeBaseContext();
                var feedback = CreateFeedbackService(db);
                await ApproveReviewItemAsync(item, feedback, queueService, token).ConfigureAwait(false);
            },
            ct).ConfigureAwait(false);

        foreach (var errorLog in result.ErrorLogTexts)
            Log(errorLog);

        OnUi(() => ReviewStatusText = result.StatusText);
    }
}
