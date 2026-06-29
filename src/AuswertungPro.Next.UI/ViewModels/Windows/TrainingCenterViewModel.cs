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
        void Apply()
        {
            PipelineActiveStep = (int)step.Stage;
            CurrentEntryCode = step.VsaCode;
            CurrentEntryMeter = step.MeterPosition;
            ProgressValue = step.EntryIndex + 1;
            ProgressMax = step.TotalEntries;

            // Aktives Modell je Stage anzeigen
            (ActiveModelName, IsModelActive) = SelfTrainingStatusCalculator.ResolveActiveModel(step.Stage, _activeVisionModel);

            // Stage-spezifisches Logging
            switch (step.Stage)
            {
                case SelfTrainingStage.BuildingTimeline:
                    if (step.ErrorMessage is not null)
                        AddSelfTrainingLog(step.ErrorMessage);
                    break;
                case SelfTrainingStage.ExtractingFrame:
                    AddSelfTrainingLog($"Frame extrahieren: {step.VsaCode} @ {step.MeterPosition:F1}m");
                    if (step.FramePath is not null) SetLiveFrameThrottled(step.FramePath);
                    break;
                case SelfTrainingStage.Analyzing:
                    AddSelfTrainingLog($"KI-Analyse [{_activeVisionModel}]: {step.VsaCode}");
                    break;
                case SelfTrainingStage.Comparing:
                    AddSelfTrainingLog($"Vergleich: {step.VsaCode}");
                    break;
                case SelfTrainingStage.AssessingTechnique:
                    if (step.Technique is { } tech)
                    {
                        CurrentTechniqueGrade = tech.OverallGrade;
                        CurrentTechniqueDetails = $"Licht: {tech.LightingQuality} | Schaerfe: {tech.SharpnessQuality}";
                        AddSelfTrainingLog($"Technik: {tech.OverallGrade} (Licht={tech.LightingQuality}, Schaerfe={tech.SharpnessQuality})");
                    }
                    break;
                case SelfTrainingStage.Completed:
                    if (step.Comparison is { } cmp)
                    {
                        CurrentComparisonText = $"{cmp.Level} ({cmp.ConfidenceScore:P0})";
                        var levelStr = SelfTrainingStatusCalculator.FormatLevel(cmp.Level);
                        AddSelfTrainingLog($"Ergebnis: {step.VsaCode} → {levelStr} ({cmp.ConfidenceScore:P0}) {cmp.Explanation}");

                        // Zaehler aktualisieren
                        switch (cmp.Level)
                        {
                            case MatchLevel.ExactMatch: _totalExact++; break;
                            case MatchLevel.PartialMatch: _totalPartial++; break;
                            case MatchLevel.Mismatch: _totalMismatch++; break;
                            case MatchLevel.NoFindings: _totalNoFindings++; break;
                        }
                        RefreshMatchRatePercents();

                        // Ergebnis-Eintrag hinzufuegen
                        SelfTrainingResults.Add(new SelfTrainingEntryResult
                        {
                            Index = step.EntryIndex + 1,
                            VsaCode = step.VsaCode,
                            Meter = step.MeterPosition,
                            Level = cmp.Level,
                            Summary = cmp.Explanation
                        });

                        UpdateCodeDistribution(step.VsaCode, cmp.Level);
                    }
                    break;
            }

            if (step.ErrorMessage is not null)
                AddSelfTrainingLog($"FEHLER: {step.ErrorMessage}");
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

            void Apply()
            {
                KbSampleCount = status.SampleCount;
                KbErrorCount = status.ErrorCount;
                KbNewCount = status.NewCount;
                KbEmbeddingCount = status.EmbeddingCount;
                KbCodesCovered = status.CodesCovered;
                KbLastUpdate = status.LatestVersionAtUtc?.ToLocalTime().ToString("dd.MM.yyyy HH:mm") ?? "\u2014";

                static System.Windows.Media.SolidColorBrush Rgb(byte r, byte g, byte b)
                    => new(System.Windows.Media.Color.FromRgb(r, g, b));

                (KbReadinessLabel, KbReadinessBrush) = status.SampleCount switch
                {
                    >= 100 => ("KI-Modell einsatzbereit", Rgb(0x4A, 0xDE, 0x80)),
                    >= 25  => ("Lernbasis grundlegend",   Rgb(0xFA, 0xCC, 0x15)),
                    > 0    => ("Lernbasis unzureichend",  Rgb(0xF8, 0x71, 0x71)),
                    _      => ("Keine Trainingsdaten",    Rgb(0x94, 0xA3, 0xB8))
                };

                KbTopCodesText = string.Join("\n", status.TopCodes
                    .Select(c => $"{c.VsaCode}: {c.Count} Samples"));
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

            // Trend (aus JSON, kein DB-Zugriff)
            var runs = await SelfTrainingHistoryStore.LoadAsync();
            var last5 = runs.TakeLast(5).ToList();
            var trendText = last5.Count > 0
                ? string.Join("\n", last5.Select(r =>
                    $"{r.TimestampUtc.ToLocalTime():dd.MM. HH:mm} — " +
                    $"Exact: {r.ExactPercent:P0} | Partial: {r.PartialPercent:P0} | " +
                    $"Miss: {r.MismatchPercent:P0} | Leer: {r.NoFindingsPercent:P0}"))
                : "Noch keine Selbsttraining-Laeufe";

            var direction = "";
            if (last5.Count >= 2)
            {
                var delta = last5[^1].ExactPercent - last5[^2].ExactPercent;
                direction = delta > 0.02 ? "\u2191" : delta < -0.02 ? "\u2193" : "\u2192";
            }

            void Apply()
            {
                KbCoverageGapsText = quality.CoverageGapsText;
                KbCoverageGapsCount = quality.CoverageGapsCount;
                KbAccuracyText = quality.AccuracyText;
                KbStaleSampleCount = quality.StaleSampleCount;
                KbTrendText = trendText;
                KbTrendDirection = direction;

                // Stale-Sample Warnung im Log (E1)
                if (quality.StaleSampleCount > 0)
                    Log($"KB-Qualitaet: {quality.StaleSampleCount} veraltete Samples erkannt (manuell pruefen im Tab 'Samples')");
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
            if (runs.Count == 0) return;
            var last = runs[^1];
            ExactPercent = last.ExactPercent;
            PartialPercent = last.PartialPercent;
            MismatchPercent = last.MismatchPercent;
            NoFindingsPercent = last.NoFindingsPercent;
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
        if (SelectedSample is null) return;
        SelectedSample.Status = TrainingSampleStatus.Approved;
        StatusText = $"Approved: {SelectedSample.SampleId}";
        await PersistSamplesAsync(SelectedSample);
    }

    [RelayCommand(CanExecute = nameof(HasSampleSelection))]
    private async Task RejectSampleAsync()
    {
        if (SelectedSample is null) return;
        SelectedSample.Status = TrainingSampleStatus.Rejected;
        SelectedSample.KbIndexState = KbIndexState.None;
        TryDeindexSample(SelectedSample.SampleId);
        StatusText = $"Rejected: {SelectedSample.SampleId}";
        await PersistSamplesAsync();
    }

    [RelayCommand(CanExecute = nameof(HasSampleSelection))]
    private async Task RemoveSampleAsync()
    {
        if (SelectedSample is null) return;
        SelectedSample.Status = TrainingSampleStatus.Removed;
        SelectedSample.KbIndexState = KbIndexState.None;
        TryDeindexSample(SelectedSample.SampleId);
        StatusText = $"Entfernt: {SelectedSample.SampleId}";
        await PersistSamplesAsync();
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
            var candidates = Samples
                .Where(s => s.Status == TrainingSampleStatus.Approved && s.ExportedUtc is null)
                .ToList();
            var approved = candidates
                .Where(IsTrainingExportEligible)
                .ToList();

            if (candidates.Count != approved.Count)
                await PersistSamplesAsync();

            if (approved.Count == 0)
            {
                StatusText = "Keine nicht-exportierten Approved-Samples vorhanden.";
                return;
            }

            foreach (var s in approved)
            {
                var entry = new AuswertungPro.Next.Domain.Protocol.ProtocolEntry
                {
                    Code = s.Code,
                    Beschreibung = s.Beschreibung,
                    MeterStart = s.MeterStart,
                    MeterEnd = s.MeterEnd,
                    IsStreckenschaden = s.IsStreckenschaden
                };
                ProtocolTrainingStore.AddSample(entry, s.CaseId);
                s.ExportedUtc = DateTime.UtcNow;
            }

            await PersistSamplesAsync();

            var codes = approved.Select(s => s.Code).Distinct().OrderBy(c => c).ToList();
            Log($"Protokoll-Training: {approved.Count} Samples als Few-Shot-Beispiele gespeichert.");
            Log($"  Codes: {string.Join(", ", codes)}");
            Log($"  Ziel: {Path.Combine(AppSettings.AppDataDir, "data", "protocol_training.json")}");
            Log("  Wirkung: Qwen nutzt diese Beispiele bei zukünftigen Protokoll-Generierungen.");
            StatusText = $"Protokoll-Training: {approved.Count} Samples als Few-Shot-Beispiele gespeichert ({codes.Count} Codes).";
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
        var bestaetigung = DialogHost.Current.ConfirmWarn(
            "Achtung: Der Batch-Import indexiert erkannte Samples OHNE manuelle Prüfung direkt in die Knowledge Base (Auto-Approve).\n\n" +
            "Falsche Code-/Meter-Zuordnungen verschlechtern dauerhaft alle künftigen KI-Vorschläge. " +
            "Für geprüftes Lernen stattdessen 'Selbsttraining' mit der Review-Queue nutzen.\n\n" +
            "Trotzdem ungeprüft in die Knowledge Base lernen?",
            "Batch-Import + KB (ungeprüft)");
        if (!bestaetigung)
        {
            runPreparation.CancellationTokenSource?.Dispose();
            StatusText = "Batch-Import abgebrochen.";
            return;
        }

        _genCts = runPreparation.CancellationTokenSource;
        var ct = runPreparation.CancellationToken;

        using var _aiToken = AiTrack.Begin("Training Center");
        try
        {
            IsBusy = true;
            LogText = "";
            ProgressValue = 0;
            ProgressMax = 1;
            ClearLivePreview();
            ResetSelfTrainingVisuals(); // Ergebnis-Verlauf + Code-Verteilung + Match-Rate zuruecksetzen

            // 1. Scan aller Root-Ordner
            Log($"Scanne {_rootFolders.Count} Ordner...");
            StatusText = "Scanne Ordner...";
            var scan = await TrainingBatchImportScanController.ScanAsync(
                _rootFolders,
                Directory.Exists,
                async folder => (await _import.ScanAsync(folder).ConfigureAwait(false))
                    .Select(TrainingCenterRuntimeHelpers.ToTrainingCase)
                    .ToList(),
                Log);
            var found = scan.Found;
            var casesWithProtocol = scan.CasesWithProtocol;

            StatusText = TrainingBatchImportScanPresentationBuilder.BuildSummary(found.Count, casesWithProtocol.Count);

            Cases.Clear();
            foreach (var c in found)
                Cases.Add(c);

            if (casesWithProtocol.Count == 0)
            {
                Log("STOP: Keine Ordner mit Protokoll-Dateien gefunden.");
                StatusText = "Keine Ordner mit Protokoll-Dateien gefunden.";
                return;
            }

            // 2. Generate samples for all cases
            var cfg = new AppSettingsAiSettingsProvider()
                .Load()
                .ToRuntimeSettings();
            Log($"AI Config: Enabled={cfg.Enabled}, ffmpeg={cfg.FfmpegPath}");

            var settings = await TrainingCenterSettingsStore.LoadAsync();
            var meterSvc = TrainingCenterRuntimeHelpers.CreateMeterTimelineService(cfg, settings.GpuConcurrency);
            var generator = new TrainingSampleGenerator(cfg, meterSvc, settings, _codeCatalog);

            var allSamples = await TrainingSamplesStore.LoadAsync();
            var existingSigs = allSamples.Select(s => s.Signature)
                .Where(s => !string.IsNullOrEmpty(s))
                .ToHashSet(StringComparer.Ordinal);
            Log($"Bestehende Samples: {allSamples.Count} ({existingSigs.Count} Signaturen)");

            // Dedup passiert per Signature auf Entry-Level.
            var casesToProcess = casesWithProtocol;

            ProgressMax = casesToProcess.Count;
            var runSummary = new TrainingBatchImportRunSummary();

            for (var i = 0; i < casesToProcess.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var tc = casesToProcess[i];
                ProgressValue = i + 1;
                var progressPresentation = TrainingBatchImportCaseProgressPresentationBuilder.Build(
                    i,
                    casesToProcess.Count,
                    tc);
                StatusText = progressPresentation.StatusText;
                foreach (var line in progressPresentation.LogLines)
                    Log(line);

                try
                {
                    // Preview-Frame extrahieren
                    var caseGeneration = await TrainingBatchImportCaseGenerationController.GenerateAsync(
                        tc,
                        existingSigs,
                        (trainingCase, token) => TrainingCenterRuntimeHelpers.ExtractPreviewFrameAsync(trainingCase, cfg, token),
                        (input, signatures, token) => generator.GenerateWithDiagnosticsAsync(input, signatures, framesDir: null, token),
                        ct);
                    var previewFrame = caseGeneration.PreviewFrame;
                    var processingPreview = caseGeneration.ProcessingPreview;
                    UpdateLivePreview(
                        processingPreview.CaseInfo,
                        processingPreview.CodeInfo,
                        processingPreview.MeterInfo,
                        processingPreview.FramePath);

                    var generation = caseGeneration.Generation;
                    var newSamples = generation.Samples;
                    var generatedCasePlan = TrainingBatchImportGeneratedCaseController.CreatePlan(
                        tc.CaseId,
                        generation,
                        previewFrame,
                        SelfTrainingResults.Count + 1,
                        existingSigs);

                    if (generatedCasePlan.Kind == TrainingBatchImportGeneratedCaseKind.Skipped)
                    {
                        var skip = generatedCasePlan.Skip!;
                        runSummary.RecordSkip(skip.Kind);

                        var skipUiPlan = generatedCasePlan.SkippedCase!;
                        Log(skip.LogMessage);
                        UpdateLivePreview(
                            skipUiPlan.Preview.CaseInfo,
                            skipUiPlan.Preview.CodeInfo,
                            skipUiPlan.Preview.MeterInfo,
                            skipUiPlan.Preview.FramePath);

                        // Uebersprungene Haltungen trotzdem im Ergebnis-Verlauf zeigen
                        void AddSkipped()
                        {
                            SelfTrainingResults.Add(skipUiPlan.Result);
                        }
                        OnUi(AddSkipped);

                        continue; // Naechster Case
                    }

                    // Signaturen registrieren + Live-Visualisierung
                    // nie Auto-Approve; Freigabe nur ueber Review (Modul I)
                    foreach (var plan in generatedCasePlan.SampleUiPlans)
                    {
                        // Live-Frame pro Sample (nicht nur pro Case)
                        UpdateLivePreview(
                            plan.Preview.CaseInfo,
                            plan.Preview.CodeInfo,
                            plan.Preview.MeterInfo,
                            plan.Preview.FramePath);

                        // Ergebnis-Verlauf: Sample als Eintrag hinzufuegen
                        // Batch-Import hat keinen echten KI-vs-Protokoll Vergleich,
                        // daher Match-Rate NICHT aktualisieren (nur im Selbsttraining sinnvoll).
                        void AddResult()
                        {
                            SelfTrainingResults.Add(plan.Result);
                            // Code-Verteilung aktualisieren
                            UpdateCodeDistribution(plan.Result.VsaCode, plan.Result.Level);
                        }
                        OnUi(AddResult);
                    }

                    runSummary.AddNewSamples(generatedCasePlan.NewSampleCount);

                    foreach (var line in generatedCasePlan.SampleLogLines)
                        Log(line);

                    // ══════════════════════════════════════════════════════════════════
                    // SOFORT SPEICHERN — Crash-sicher pro Haltung
                    // ══════════════════════════════════════════════════════════════════
                    var persistence = await TrainingBatchImportSamplePersistenceController.SaveCandidatesAsync(
                        newSamples,
                        allSamples,
                        TrainingSamplesStore.MergeAndSaveAsync);

                    // Kein KB-Index — Samples bleiben Kandidaten (Status: Neu)
                    Log(persistence.CandidateLogMessage);

                    // UI-Zaehler aktualisieren (Samples + Codes)
                    void UpdateCounters()
                    {
                        KbSampleCount = persistence.SampleCount;
                        KbCodesCovered = persistence.CodesCovered;
                    }
                    OnUi(UpdateCounters);

                    Log(persistence.StoredLogMessage);

                    // Case-State periodisch sichern (alle 10 Haltungen),
                    // damit die UI nach einem Crash den Fortschritt korrekt anzeigt.
                    await TrainingBatchImportCaseStateSaveController.SaveIfDueAsync(
                        i + 1,
                        5,
                        () => _store.SaveAsync(BuildState()));
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    runSummary.RecordError(ex.Message);
                    Log($"  FEHLER: {ex.Message}");
                }
            }

            // Abschlussmeldung
            Samples.Clear();
            allSamples = await TrainingSamplesStore.LoadAsync();
            foreach (var s in allSamples)
                Samples.Add(s);

            if (runSummary.BuildNoNewStatus(casesToProcess.Count) is { } noNewStatus)
            {
                Log(noNewStatus);
                StatusText = noNewStatus;
                return;
            }

            var finalStatus = runSummary.BuildCompletionStatus();
            Log(finalStatus);
            StatusText = finalStatus;

            await RefreshKbStatusAsync();

            // 5. Save cases
            await _store.SaveAsync(BuildState());
            Log("Fälle gespeichert. Batch-Import abgeschlossen.");
        }
        catch (OperationCanceledException)
        {
            Log("Batch-Import abgebrochen durch Benutzer.");
            StatusText = "Batch-Import abgebrochen.";
        }
        catch (Exception ex)
        {
            Log($"FATALER FEHLER: {ex.Message}");
            StatusText = $"Fehler beim Batch-Import: {ex.Message}";
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

            Log($"KB-Stand: Samples={summary.SampleCount}, Embeddings={summary.EmbeddingCount}, Versionen={summary.VersionCount}");
            if (summary.LatestVersionAtUtc is not null)
            {
                var latest = summary.LatestVersionAtUtc.Value.ToLocalTime();
                var notes = string.IsNullOrWhiteSpace(summary.LatestVersionNotes)
                    ? "-"
                    : summary.LatestVersionNotes;
                Log($"Letzte Version: {latest:yyyy-MM-dd HH:mm} ({summary.LatestVersionSampleCount} Samples) | Notiz: {notes}");
            }

            if (summary.TopCodes.Count > 0)
            {
                Log("Top-Codes:");
                foreach (var c in summary.TopCodes)
                    Log($"  {c.VsaCode}: {c.Count}");
            }
            else
            {
                Log("Top-Codes: keine Einträge vorhanden.");
            }

            StatusText = $"KB geprüft: {summary.SampleCount} Samples, {summary.EmbeddingCount} Embeddings, {summary.VersionCount} Versionen.";

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
        // Immer Merge/Update statt Voll-Save — verhindert Ueberschreiben
        // von parallel geschriebenen Samples (Batch-Import, Self-Training).
        if (changedSample != null)
            await TrainingSamplesStore.MergeOrUpdateAsync(new List<TrainingSample> { changedSample });
        else
            await TrainingSamplesStore.MergeOrUpdateAsync(Samples.ToList());

        // Approved Sample sofort in KB indexieren ("sofort in die Datenbank")
        if (changedSample?.Status == TrainingSampleStatus.Approved)
        {
            changedSample.KbIndexState = KbIndexState.Pending;
            await TrainingSamplesStore.MergeOrUpdateAsync(new List<TrainingSample> { changedSample });
            var outcome = await IncrementalKbUpdateWithReasonAsync(
                new List<TrainingSample> { changedSample },
                CancellationToken.None);
            // Skipped (bewusst/dauerhaft verworfen) vs. Error (echter Fehler) sauber unterscheiden.
            changedSample.KbIndexState = outcome.IndexedIds.Contains(changedSample.SampleId)
                ? KbIndexState.Indexed
                : outcome.SkippedIds.Contains(changedSample.SampleId)
                    ? KbIndexState.Skipped
                    : KbIndexState.Error;
            await TrainingSamplesStore.MergeOrUpdateAsync(new List<TrainingSample> { changedSample });
        }
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

            var all = await TrainingSamplesStore.LoadAsync().ConfigureAwait(false);
            var pending = KbReconcilePlanner.SelectPending(all);
            var (total, eligible) = KbReconcilePlanner.CountPending(all);
            if (total == 0)
            {
                Log("KB-Nachholen: keine offenen Gold-Samples (alles bereits indexiert).");
                SetStatus("KB-Nachholen: nichts zu tun");
                return;
            }

            // Ehrliche Laufzeit-Zahl: wie viele bestaetigte Gold-Samples warten wirklich,
            // und wie viele davon sind trainingsfaehig. Keine fiktive Zahl.
            Log($"KB-Nachholen: {total} bestaetigte Gold-Samples warten (davon {eligible} trainingsfaehig markiert).");

            // Reversibles Backup VOR der ersten Aenderung — vollwertiger KI-Hirn-Export mit
            // SQLite-WAL-Checkpoint (vorhandener KnowledgeBackupService). "Daten nie verlieren".
            var backupZip = System.IO.Path.Combine(
                KnowledgeBasePaths.GetRoot(), "kb_backups",
                $"vor_kb_nachholen_{DateTime.Now:yyyy-MM-dd_HHmmss}.zip");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(backupZip)!);
            SetStatus("KB-Nachholen: Backup wird erstellt…");
            // Progress<T> aus dem UI-Thread erstellt -> Callbacks laufen auf dem UI-Kontext;
            // SetStatus ist zusaetzlich dispatcher-sicher.
            var backup = await KnowledgeBackupService.ExportAsync(
                backupZip, new Progress<string>(m => SetStatus($"Backup: {m}")), ct).ConfigureAwait(false);
            if (!backup.Success)
            {
                Log($"KB-Nachholen ABGEBROCHEN: Backup fehlgeschlagen ({backup.Error}). Keine Aenderung vorgenommen.");
                SetStatus("KB-Nachholen: Backup fehlgeschlagen");
                return;
            }
            Log($"KB-Nachholen: Backup angelegt ({backup.FileCount} Dateien) unter {backupZip}");
            SetStatus($"KB-Nachholen: 0/{total}");

            var indexed = 0;
            var skipped = 0;
            var processed = 0;

            // In Bloecken indexieren, damit Status laufend zurueckgeschrieben wird (kein "alles oder nichts").
            const int batchSize = 50;
            for (var i = 0; i < pending.Count; i += batchSize)
            {
                ct.ThrowIfCancellationRequested();
                var batch = pending.Skip(i).Take(batchSize).ToList();

                foreach (var s in batch)
                    s.KbIndexState = KbIndexState.Pending;
                await TrainingSamplesStore.MergeOrUpdateAsync(batch).ConfigureAwait(false);

                var indexResult = await IncrementalKbUpdateWithReasonAsync(batch, ct).ConfigureAwait(false);

                foreach (var s in batch)
                {
                    if (indexResult.IndexedIds.Contains(s.SampleId))
                    {
                        s.KbIndexState = KbIndexState.Indexed;
                        indexed++;
                    }
                    else if (indexResult.SkippedIds.Contains(s.SampleId))
                    {
                        // Bewusst/dauerhaft verworfen (Eval-Schutz/nicht index-wuerdig) -> Skipped,
                        // damit der naechste Nachhol-Lauf es NICHT wieder aufgreift.
                        s.KbIndexState = KbIndexState.Skipped;
                        skipped++;
                    }
                    else
                    {
                        // Echter (transienter) Misserfolg, z.B. Ollama offline -> Error (spaeter erneut).
                        s.KbIndexState = KbIndexState.Error;
                        skipped++;
                    }
                    processed++;
                }
                await TrainingSamplesStore.MergeOrUpdateAsync(batch).ConfigureAwait(false);

                SetStatus($"KB-Nachholen: {processed}/{total}");
            }

            Log($"KB-Nachholen fertig: {indexed} indexiert, {skipped} uebersprungen/fehlgeschlagen (von {total}).");
            SetStatus($"KB-Nachholen: {indexed} indexiert, {skipped} uebersprungen");
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
            ReviewQueue.Clear();
            foreach (var item in queueService.GetAll())
                ReviewQueue.Add(item);
            ReviewQueueCount = ReviewQueue.Count;
            ReviewStatusText = $"{ReviewQueueCount} Einträge zur Prüfung";
        });
    }

    /// <summary>
    /// Loest die SampleId eines Self-Training-Review-Items auf: bevorzugt die direkte SampleId,
    /// sonst (Altbestand ohne SampleId) ueber Fuzzy-Match CaseId/Code/Meter±0.2. Null = nicht gefunden.
    /// </summary>
    private async Task<string?> ResolveSelfTrainingSampleIdAsync(InfraSelfImproving.ReviewQueueItem item)
    {
        if (!string.IsNullOrEmpty(item.SelfTrainingSampleId))
            return item.SelfTrainingSampleId;
        var allSamples = await TrainingSamplesStore.LoadAsync().ConfigureAwait(false);
        return allSamples.FirstOrDefault(s =>
            s.CaseId == item.SelfTrainingCaseId
            && s.Code == item.SelfTrainingVsaCode
            && Math.Abs(s.MeterStart - (item.SelfTrainingMeter ?? 0)) < 0.2)?.SampleId;
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
            queueService.Remove(item.Id);
            ReviewQueue.Remove(item);
            ReviewQueueCount = ReviewQueue.Count;
            ReviewStatusText = $"Approved: {item.SuggestedCode} | {ReviewQueueCount} verbleibend";
            Log($"Review Approved: {item.Label} → {item.SuggestedCode}");
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
            queueService.Remove(item.Id);
            ReviewQueue.Remove(item);
            ReviewQueueCount = ReviewQueue.Count;
            ReviewStatusText = $"Rejected: {item.SuggestedCode} → {correctedCode} | {ReviewQueueCount} verbleibend";
            Log($"Review Rejected: {item.Label} → {item.SuggestedCode} korrigiert zu {correctedCode}");
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

        // Auto-Scan: Wenn keine Faelle geladen, Ordner automatisch scannen
        if (SelfTrainingAutoScanController.ShouldScan(Cases.Count, _rootFolders.Count))
        {
            StatusText = SelfTrainingAutoScanController.StatusText;
            var autoScannedCases = await SelfTrainingAutoScanController.ScanAsync(
                _rootFolders,
                Directory.Exists,
                async folder => (await _import.ScanAsync(folder))
                    .Select(TrainingCenterRuntimeHelpers.ToTrainingCase)
                    .ToList());
            foreach (var c in autoScannedCases)
                Cases.Add(c);
        }

        var existingSamplesForSelection = SelectedCase is null
            ? await TrainingSamplesStore.LoadAsync()
            : Enumerable.Empty<TrainingSample>();
        var selection = SelfTrainingCaseSelectionController.Select(
            SelectedCase,
            Cases,
            existingSamplesForSelection);
        if (selection.ShouldStop)
        {
            StatusText = selection.StatusText ?? "";
            return;
        }
        if (selection.Case is null)
        {
            StatusText = "Keine Faelle mit Protokoll vorhanden. Bitte zuerst Ordner waehlen und scannen.";
            return;
        }
        var selectedCase = selection.Case;
        SelectedCase = selectedCase;

        var runPreparation = SelfTrainingRunPreparationController.PrepareCancellation(_selfTrainingCts);
        _selfTrainingCts = runPreparation.CancellationTokenSource;
        var ct = runPreparation.CancellationToken;

        using var _aiToken = AiTrack.Begin("Selbsttraining");
        try
        {
            IsBusy = true;
            IsSelfTrainingRunning = true;
            ResetSelfTrainingVisuals(resetMatchRate: true);
            LogText = "";
            var startPresentation = SelfTrainingRunPresentationBuilder.BuildStart(selectedCase);
            StatusText = startPresentation.StatusText;
            foreach (var line in startPresentation.LogLines)
                Log(line);

            // Services instanziieren (gleicher Pattern wie BatchImport)
            var cfg = new AppSettingsAiSettingsProvider()
                .Load()
                .ToRuntimeSettings();
            Log(SelfTrainingRunPresentationBuilder.BuildOllamaConfigLog(cfg.OllamaBaseUri, cfg.VisionModel));

            var stSettings = await TrainingCenterSettingsStore.LoadAsync();

            // Weg 1: read-only KB-Abgleich-Signal fuer den Orchestrator (KB-Widerspruch -> Review).
            var stOllamaConfig = new AppSettingsAiSettingsProvider().Load().ToOllamaConfig();
            _kbHttpClient ??= new System.Net.Http.HttpClient { Timeout = stOllamaConfig.RequestTimeout };
            using var selfTrainingSession = SelfTrainingSessionController.Create(
                cfg,
                stOllamaConfig,
                _kbHttpClient,
                stSettings,
                _settings,
                _codeCatalog);
            _activeVisionModel = selfTrainingSession.ActiveVisionModel;
            _selfTrainingOrchestrator = selfTrainingSession.Orchestrator;

            // Progress-Callback verbindet Orchestrator → ViewModel-Visualisierungen
            var progress = new Progress<SelfTrainingStep>(OnSelfTrainingStep);

            Log(SelfTrainingRunPresentationBuilder.BuildPipelineStartedLog());
            var result = await SelfTrainingRunExecutionController.RunAsync(
                selfTrainingSession.Orchestrator,
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

            // Samples-Liste aktualisieren
            await LoadSamplesInternalAsync();
            await RefreshKbStatusAsync();
        }
        catch (OperationCanceledException)
        {
            Log("Selbsttraining abgebrochen.");
            StatusText = "Selbsttraining abgebrochen.";
        }
        catch (Exception ex)
        {
            Log($"FEHLER: {ex.GetType().Name}: {ex.Message}");
            StatusText = $"Fehler: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            IsSelfTrainingRunning = false;
            _selfTrainingOrchestrator = null;
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
        var candidates = ProtocolReviewCandidateFilter.SelectCandidates(all, catalog).ToList();

        // Schon in der Queue stehende Samples nicht erneut einreihen (Dedup per SampleId).
        var queued = ReviewQueueServiceRef.GetAll()
            .Select(q => q.SelfTrainingSampleId).Where(s => !string.IsNullOrEmpty(s)).ToHashSet();
        int added = 0;
        foreach (var s in candidates)
        {
            if (queued.Contains(s.SampleId)) continue;
            ReviewQueueServiceRef.EnqueueFromSelfTraining(
                s.CaseId, s.Code, s.Code, s.MeterStart, s.FramePath,
                matchLevel: "ProtocolStartdata", reason: "Protokoll-Startdaten", sampleId: s.SampleId);
            added++;
        }
        LoadReviewQueue(ReviewQueueServiceRef);
        OnUi(() => ReviewStatusText = $"{added} Protokoll-Startdaten als Kandidaten eingereiht (Freigabe ueber Review).");
        Log($"Protokoll-Startdaten: {added} Kandidaten eingereiht (von {candidates.Count} gefiltert).");
    }

    /// <summary>Anzahl der aktuell als Protokoll-Startdaten eingereihten Kandidaten.</summary>
    public int StartdataCandidateCount =>
        ReviewQueue.Count(i => string.Equals(i.SelfTrainingMatchLevel, "ProtocolStartdata", StringComparison.OrdinalIgnoreCase));

    private List<InfraSelfImproving.ReviewQueueItem> GetProtocolStartdataReviewItems()
    {
        List<InfraSelfImproving.ReviewQueueItem>? items = null;
        OnUi(() =>
        {
            items = ReviewQueue
                .Where(i => string.Equals(i.SelfTrainingMatchLevel, "ProtocolStartdata", StringComparison.OrdinalIgnoreCase))
                .ToList();
        });
        return items ?? new List<InfraSelfImproving.ReviewQueueItem>();
    }

    /// <summary>Gibt ALLE Protokoll-Startdaten-Kandidaten frei (nach expliziter Bestaetigung im View).</summary>
    public async Task ApproveAllStartdataAsync(CancellationToken ct = default)
    {
        if (ReviewQueueServiceRef is null) return;
        var items = GetProtocolStartdataReviewItems();
        int ok = 0;
        foreach (var item in items)
        {
            try
            {
                using var db = new KnowledgeBaseContext();
                var feedback = CreateFeedbackService(db);
                await ApproveReviewItemAsync(item, feedback, ReviewQueueServiceRef, ct).ConfigureAwait(false);
                ok++;
            }
            catch (Exception ex) { Log($"Startdaten-Freigabe Fehler ({item.SelfTrainingVsaCode}): {ex.Message}"); }
        }
        OnUi(() => ReviewStatusText = $"{ok}/{items.Count} Protokoll-Startdaten freigegeben.");
    }
}
