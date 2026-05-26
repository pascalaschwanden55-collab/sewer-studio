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
    [ObservableProperty] private InfraSelfImproving.ReviewQueueItem? _selectedReviewItem;
    [ObservableProperty] private int _reviewQueueCount;
    [ObservableProperty] private string _reviewStatusText = "";

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
        var total = _totalExact + _totalPartial + _totalMismatch + _totalNoFindings;
        if (total == 0) { ExactPercent = PartialPercent = MismatchPercent = NoFindingsPercent = 0; return; }
        ExactPercent = (double)_totalExact / total;
        PartialPercent = (double)_totalPartial / total;
        MismatchPercent = (double)_totalMismatch / total;
        NoFindingsPercent = (double)_totalNoFindings / total;
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
            entry.Total++;
            switch (level)
            {
                case MatchLevel.ExactMatch: entry.Exact++; break;
                case MatchLevel.PartialMatch: entry.Partial++; break;
                case MatchLevel.Mismatch: entry.Mismatch++; break;
                case MatchLevel.NoFindings: entry.NoFindings++; break;
            }
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
            (ActiveModelName, IsModelActive) = step.Stage switch
            {
                SelfTrainingStage.BuildingTimeline => ("PdfPig (CPU)", true),
                SelfTrainingStage.ExtractingFrame  => ("ffmpeg (CPU)", true),
                SelfTrainingStage.Analyzing        => ($"{_activeVisionModel} (GPU)", true),
                SelfTrainingStage.Comparing        => ("Deterministisch (CPU)", true),
                SelfTrainingStage.AssessingTechnique => ($"{_activeVisionModel} (GPU)", true),
                SelfTrainingStage.Completed        => ("", false),
                _ => ("", false)
            };

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
                        var levelStr = cmp.Level switch
                        {
                            MatchLevel.ExactMatch => "EXACT",
                            MatchLevel.PartialMatch => "PARTIAL",
                            MatchLevel.Mismatch => "MISMATCH",
                            _ => "NO_FINDINGS"
                        };
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
            LiveCaseInfo = caseInfo;
            LiveCodeInfo = code;
            LiveMeterInfo = meter;
            CurrentComparisonText = $"{code} @ {meter}";
            CurrentEntryCode = code;
            if (framePath is not null)
                SetLiveFrameThrottled(framePath);
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
        SetLiveFrameThrottled(null);
        LiveCaseInfo = "";
        LiveCodeInfo = "";
        LiveMeterInfo = "";
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
        IKnowledgeBaseDiagnosticsRunner kbDiagnostics)
    {
        _store = store;
        _import = import;
        _codeCatalog = codeCatalog;
        _kbDiagnostics = kbDiagnostics;
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
            RootFolder = string.Join("; ", _rootFolders.Select(f => Path.GetFileName(f)));
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
        RootFolder = _rootFolders.Count switch
        {
            0 => "",
            1 => _rootFolders[0],
            _ => $"{_rootFolders.Count} Ordner: {string.Join("; ", _rootFolders.Select(Path.GetFileName))}"
        };
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
                foreach (var c in found.Select(ToTrainingCase))
                    Cases.Add(c);
            }

            var withProto    = Cases.Count(c => !string.IsNullOrEmpty(c.ProtocolPath));
            var withoutProto = Cases.Count - withProto;
            var pdfOnly = Cases.Count(c => string.IsNullOrEmpty(c.VideoPath) && !string.IsNullOrEmpty(c.ProtocolPath));
            var parts = new List<string> { $"Gefunden: {Cases.Count} Fälle" };
            if (pdfOnly > 0) parts.Add($"{pdfOnly} nur PDF");
            if (withoutProto > 0) parts.Add($"{withoutProto} ohne Protokoll");
            StatusText = string.Join(", ", parts);

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
            var state = new TrainingCenterState
            {
                Cases = Cases.ToList(),
                RootFolders = new List<string>(_rootFolders),
                UpdatedUtc = DateTime.UtcNow
            };
            await _store.SaveAsync(state);
        }
        catch { /* stilles Speichern */ }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            var state = new TrainingCenterState
            {
                Cases = Cases.ToList(),
                RootFolders = new List<string>(_rootFolders),
                UpdatedUtc = DateTime.UtcNow
            };
            await _store.SaveAsync(state);
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
        var list = await TrainingSamplesStore.LoadAsync();
        Samples.Clear();
        foreach (var s in list)
            Samples.Add(s);
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
            var meterSvc = CreateMeterTimelineService(cfg, settings.GpuConcurrency);
            var generator = new TrainingSampleGenerator(cfg, meterSvc, settings, _codeCatalog);

            var existing = await TrainingSamplesStore.LoadAsync();
            var existingSigs = existing.Select(s => s.Signature).ToHashSet(StringComparer.Ordinal);

            var generation = await generator.GenerateWithDiagnosticsAsync(
                ToTrainingCaseInput(SelectedCase), existingSigs, framesDir: null, ct);
            var newSamples = generation.Samples;

            if (newSamples.Count == 0)
            {
                StatusText = generation.Outcome switch
                {
                    TrainingSampleGenerationOutcome.OnlyDuplicates
                        => $"Keine neuen Samples für {SelectedCase.CaseId} (alle {generation.ParsedEntries} Einträge bereits vorhanden).",
                    TrainingSampleGenerationOutcome.NoProtocolEntries
                        => $"Keine Protokolleinträge erkannt für {SelectedCase.CaseId}.",
                    TrainingSampleGenerationOutcome.ProtocolUnreadable
                        => $"Protokoll konnte nicht gelesen werden: {SelectedCase.ProtocolPath}",
                    TrainingSampleGenerationOutcome.ProtocolFileMissing
                        => $"Protokolldatei fehlt: {SelectedCase.ProtocolPath}",
                    _ => "Keine neuen Samples generiert."
                };
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
        StatusText = $"Rejected: {SelectedSample.SampleId}";
        await PersistSamplesAsync();
    }

    [RelayCommand]
    private async Task ExportApprovedAsync()
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            var approved = Samples
                .Where(s => s.Status == TrainingSampleStatus.Approved && s.ExportedUtc is null)
                .ToList();

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
    /// Exportiert Approved-Samples im YOLO-Format über den Sidecar.
    /// Erzeugt images/, labels/ und data.yaml für YOLO-Training.
    /// </summary>
    [RelayCommand]
    private async Task ExportYoloAsync()
    {
        if (IsBusy) return;

        var approved = Samples
            .Where(s => s.Status == TrainingSampleStatus.Approved
                        && !string.IsNullOrWhiteSpace(s.FramePath)
                        && File.Exists(s.FramePath))
            .ToList();

        if (approved.Count == 0)
        {
            StatusText = "Keine Approved-Samples mit gültigen Frames vorhanden.";
            Log("YOLO-Export: Keine exportierbaren Samples gefunden.");
            return;
        }

        // Zielordner wählen
        var dlg = new OpenFolderDialog { Title = "YOLO-Export Zielordner wählen" };
        if (dlg.ShowDialog() != true)
            return;

        var outputDir = dlg.FolderName;

        _genCts?.Cancel();
        _genCts?.Dispose();
        _genCts = new CancellationTokenSource();
        var ct = _genCts.Token;

        try
        {
            IsBusy = true;
            Log($"YOLO-Export: {approved.Count} Samples → {outputDir}");
            StatusText = $"YOLO-Export: {approved.Count} Samples werden vorbereitet...";

            // Sidecar-Verbindung prüfen
            var pipelineCfg = new AppSettingsAiSettingsProvider()
                .Load()
                .ToPipelineConfig();
            var client = new VisionPipelineClient(pipelineCfg.SidecarUrl, sidecarToken: pipelineCfg.SidecarToken);

            var health = await client.HealthCheckAsync(ct).ConfigureAwait(false);
            if (health is null)
            {
                // Fallback: lokaler Export ohne Sidecar
                Log($"Sidecar nicht erreichbar ({pipelineCfg.SidecarUrl}). Versuche lokalen Export...");
                await ExportYoloLocalAsync(approved, outputDir, ct).ConfigureAwait(false);
                return;
            }

            Log($"Sidecar erreichbar: v{health.Version}, GPU: {health.Gpu?.CurrentModel ?? "?"}");

            // Samples zu DTOs konvertieren
            ProgressMax = approved.Count;
            ProgressValue = 0;

            var exportSamples = new List<TrainingExportSample>();
            for (var i = 0; i < approved.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var s = approved[i];
                ProgressValue = i + 1;
                StatusText = $"YOLO-Export: Lade Frame {i + 1}/{approved.Count}...";

                var bytes = await File.ReadAllBytesAsync(s.FramePath, ct).ConfigureAwait(false);
                var base64 = Convert.ToBase64String(bytes);

                var labels = new List<TrainingExportSampleLabel>();
                if (!string.IsNullOrWhiteSpace(s.Code))
                {
                    // Echte BBox aus TeacherAnnotation suchen (falls vorhanden)
                    // Fallback: Dummy-BBox fuer Samples ohne Annotation
                    labels.Add(new TrainingExportSampleLabel(
                        ClassName: s.Code,
                        XCenter: 0.5, YCenter: 0.5,
                        Width: 0.8, Height: 0.8));
                }

                exportSamples.Add(new TrainingExportSample(base64, labels));
            }

            StatusText = $"YOLO-Export: Sende {exportSamples.Count} Samples an Sidecar...";
            var request = new TrainingExportRequestDto(exportSamples, outputDir, 0.8);
            TrainingExportResponseDto response;
            try
            {
                response = await client.ExportTrainingAsync(request, ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log($"Sidecar-Export nicht moeglich ({ex.Message}). Lokaler Export wird verwendet...");
                await ExportYoloLocalAsync(approved, outputDir, ct).ConfigureAwait(false);
                return;
            }

            // Samples als exportiert markieren
            foreach (var s in approved)
                s.ExportedUtc = DateTime.UtcNow;
            await PersistSamplesAsync();

            var msg = $"YOLO-Export fertig: {response.TotalSamples} Samples " +
                      $"({response.TrainCount} Train, {response.ValCount} Val), " +
                      $"{response.ClassesUsed.Count} Klassen → {outputDir}";
            Log(msg);
            Log($"  data.yaml: {response.DataYamlPath}");
            Log($"  Klassen: {string.Join(", ", response.ClassesUsed)}");
            StatusText = msg;
        }
        catch (OperationCanceledException)
        {
            Log("YOLO-Export abgebrochen.");
            StatusText = "YOLO-Export abgebrochen.";
        }
        catch (Exception ex)
        {
            Log($"YOLO-Export FEHLER: {ex.Message}");
            StatusText = $"YOLO-Export fehlgeschlagen: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Lokaler YOLO-Export — bevorzugt TeacherAnnotations (echte BBoxen),
    /// Fallback auf TrainingSamples (Dummy-BBoxen nur wenn keine Annotationen vorhanden).
    /// </summary>
    private async Task ExportYoloLocalAsync(
        List<TrainingSample> approved, string outputDir, CancellationToken ct)
    {
        // TeacherAnnotations laden (echte BBoxen)
        var annotations = await TeacherAnnotationStore.LoadAsync();
        var annotationsWithImages = annotations
            .Where(a => !string.IsNullOrWhiteSpace(a.FullFramePath) && File.Exists(a.FullFramePath))
            .ToList();

        Log($"YOLO-Export: {annotationsWithImages.Count} TeacherAnnotations mit Bildern, {approved.Count} TrainingSamples");

        var imgTrain = Path.Combine(outputDir, "images", "train");
        var imgVal = Path.Combine(outputDir, "images", "val");
        var lblTrain = Path.Combine(outputDir, "labels", "train");
        var lblVal = Path.Combine(outputDir, "labels", "val");
        foreach (var d in new[] { imgTrain, imgVal, lblTrain, lblVal })
            Directory.CreateDirectory(d);

        int totalExported = 0;

        // ── Phase 1: TeacherAnnotations exportieren (echte BBoxen) ──
        if (annotationsWithImages.Count > 0)
        {
            var splitIdx = (int)(annotationsWithImages.Count * 0.8);
            ProgressMax = annotationsWithImages.Count;

            for (var i = 0; i < annotationsWithImages.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var a = annotationsWithImages[i];
                ProgressValue = i + 1;
                StatusText = $"YOLO-Export (Teacher): {i + 1}/{annotationsWithImages.Count}...";

                var isTrain = i < splitIdx;
                var imgDir = isTrain ? imgTrain : imgVal;
                var lblDir = isTrain ? lblTrain : lblVal;

                // Bild kopieren
                var ext = Path.GetExtension(a.FullFramePath);
                var imgDst = Path.Combine(imgDir, $"teacher_{a.AnnotationId}{ext}");
                File.Copy(a.FullFramePath!, imgDst, overwrite: true);

                // Label mit echten BBoxen schreiben
                var clsIdx = VsaYoloClassMap.GetClassId(a.VsaCode);
                var bbox = a.BoundingBox;
                var lblPath = Path.Combine(lblDir, $"teacher_{a.AnnotationId}.txt");
                if (bbox is not null && bbox.Width > 0 && bbox.Height > 0)
                {
                    // Echte BBox aus TeacherAnnotation
                    await File.WriteAllTextAsync(lblPath,
                        $"{clsIdx} {bbox.XCenter:F6} {bbox.YCenter:F6} {bbox.Width:F6} {bbox.Height:F6}", ct);
                }
                else
                {
                    // Annotation ohne BBox → Vollbild als Fallback
                    await File.WriteAllTextAsync(lblPath,
                        $"{clsIdx} 0.500000 0.500000 1.000000 1.000000", ct);
                }

                totalExported++;
            }
        }

        // ── Phase 2: TrainingSamples IMMER exportieren (mit echten BBoxen wenn vorhanden) ──
        if (approved.Count > 0)
        {
            int withBbox = approved.Count(s => s.HasBbox);
            Log($"  Exportiere {approved.Count} TrainingSamples ({withBbox} mit echten BBoxen)");
            var sampleSplitIdx = (int)(approved.Count * 0.8);

            for (var i = 0; i < approved.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var s = approved[i];
                StatusText = $"YOLO-Export (Samples): {i + 1}/{approved.Count}...";

                var isTrain = i < sampleSplitIdx;
                var imgDir = isTrain ? imgTrain : imgVal;
                var lblDir = isTrain ? lblTrain : lblVal;

                // Sicherheitscheck: Frame-Datei koennte zwischen Filter und Export geloescht worden sein
                if (!File.Exists(s.FramePath)) continue;

                var ext = Path.GetExtension(s.FramePath);
                var imgDst = Path.Combine(imgDir, $"sample_{i:D6}{ext}");
                try { File.Copy(s.FramePath, imgDst, overwrite: true); }
                catch (IOException) { continue; } // Datei gesperrt oder nicht mehr vorhanden

                var clsIdx = VsaYoloClassMap.GetClassId(s.Code);
                var lblPath = Path.Combine(lblDir, $"sample_{i:D6}.txt");

                // Echte BBox aus Eingabemarker nutzen, sonst Fallback
                if (s.HasBbox)
                {
                    await File.WriteAllTextAsync(lblPath,
                        $"{clsIdx} {s.BboxXCenter!.Value:F6} {s.BboxYCenter!.Value:F6} " +
                        $"{s.BboxWidth!.Value:F6} {s.BboxHeight!.Value:F6}", ct);
                }
                else
                {
                    // Kein BBox → zentrierte Fallback-Box
                    await File.WriteAllTextAsync(lblPath,
                        $"{clsIdx} 0.500000 0.500000 0.800000 0.800000", ct);
                }

                s.ExportedUtc = DateTime.UtcNow;
                totalExported++;
            }
            await PersistSamplesAsync();
        }

        // ── data.yaml mit exaktem Klassenmapping ──
        var fullMap = VsaYoloClassMap.GetFullMap();
        var sortedClasses = fullMap.OrderBy(kv => kv.Value).Select(kv => kv.Key).ToList();

        var yamlPath = Path.Combine(outputDir, "data.yaml");
        var yamlLines = new[]
        {
            $"path: {Path.GetFullPath(outputDir)}",
            "train: images/train",
            "val: images/val",
            $"nc: {sortedClasses.Count}",
            $"names: [{string.Join(", ", sortedClasses.Select(c => $"'{c}'"))}]"
        };
        await File.WriteAllLinesAsync(yamlPath, yamlLines, ct);

        // classes.txt exportieren
        await VsaYoloClassMap.ExportClassesTxtAsync(
            Path.Combine(outputDir, "classes.txt"));

        var msg = $"YOLO-Export fertig: {totalExported} Samples " +
                  $"({annotationsWithImages.Count} Teacher + {totalExported - annotationsWithImages.Count} Samples), " +
                  $"{sortedClasses.Count} Klassen → {outputDir}";
        Log(msg);
        Log($"  data.yaml: {yamlPath}");
        Log($"  Klassen: {string.Join(", ", sortedClasses)}");
        StatusText = msg;
    }

    /// <summary>
    /// Batch-Import: Scannt alle Ordner, generiert Samples, approved automatisch,
    /// indiziert in die Knowledge Base. Alles in einem Durchlauf.
    /// </summary>
    [RelayCommand]
    private async Task BatchImportAndIndexAsync()
    {
        if (IsBusy) return;
        if (_rootFolders.Count == 0)
        {
            StatusText = "Bitte zuerst einen oder mehrere Ordner wählen.";
            return;
        }

        _genCts?.Cancel();
        _genCts?.Dispose();
        _genCts = new CancellationTokenSource();
        var ct = _genCts.Token;

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
            var found = new List<TrainingCase>();
            foreach (var folder in _rootFolders)
            {
                if (!Directory.Exists(folder))
                {
                    Log($"  WARNUNG: Ordner existiert nicht: {folder}");
                    continue;
                }
                Log($"  Scanne: {folder}");
                var result = await _import.ScanAsync(folder);
                found.AddRange(result.Select(ToTrainingCase));
            }
            var casesWithProtocol = found.Where(c => !string.IsNullOrEmpty(c.ProtocolPath)).ToList();

            Log($"Gefunden: {found.Count} Ordner, {casesWithProtocol.Count} mit Protokoll");
            foreach (var c in found)
            {
                var hasVideo = !string.IsNullOrEmpty(c.VideoPath) ? "Video" : "kein Video";
                var hasProto = !string.IsNullOrEmpty(c.ProtocolPath) ? Path.GetFileName(c.ProtocolPath) : "kein Protokoll";
                Log($"  {c.CaseId}: {hasVideo}, {hasProto}");
            }

            StatusText = $"Gefunden: {found.Count} Ordner, {casesWithProtocol.Count} mit Protokoll";

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
            var meterSvc = CreateMeterTimelineService(cfg, settings.GpuConcurrency);
            var generator = new TrainingSampleGenerator(cfg, meterSvc, settings, _codeCatalog);

            var allSamples = await TrainingSamplesStore.LoadAsync();
            var existingSigs = allSamples.Select(s => s.Signature)
                .Where(s => !string.IsNullOrEmpty(s))
                .ToHashSet(StringComparer.Ordinal);
            Log($"Bestehende Samples: {allSamples.Count} ({existingSigs.Count} Signaturen)");

            // Dedup passiert per Signature auf Entry-Level.
            var casesToProcess = casesWithProtocol;

            // Ollama-Verbindung einmalig pruefen + KB-Objekte vorbereiten
            var ollamaConfig = new AppSettingsAiSettingsProvider()
                .Load()
                .ToOllamaConfig();
            var ollamaReachable = await CheckOllamaReachableAsync(ollamaConfig, ct);
            KnowledgeBaseContext? kbCtx = null;
            KnowledgeBaseManager? kbManager = null;
            if (ollamaReachable)
            {
                _kbHttpClient ??= new System.Net.Http.HttpClient { Timeout = ollamaConfig.RequestTimeout };
                kbCtx = new KnowledgeBaseContext();
                kbManager = new KnowledgeBaseManager(kbCtx, new EmbeddingService(_kbHttpClient, ollamaConfig));
                Log($"Ollama bereit: {ollamaConfig.BaseUri}, Embed-Modell: {ollamaConfig.EmbedModel}");
            }
            else
            {
                Log($"Ollama NICHT erreichbar auf {ollamaConfig.BaseUri} — Samples werden gespeichert, KB-Indexierung uebersprungen.");
            }

            // ── KB-Nachholpfad: Approved Samples die noch nicht in der KB sind nachindizieren ──
            // Deckt den Fall ab: Crash nach MergeAndSave aber vor IndexSampleAsync,
            // oder vorheriger Lauf ohne Ollama.
            if (kbManager is not null && allSamples.Count > 0)
            {
                // Pending + Error Samples immer nachindizieren
                var unindexed = allSamples
                    .Where(s => s.Status == TrainingSampleStatus.Approved)
                    .Where(s => s.KbIndexState is KbIndexState.Pending or KbIndexState.Error)
                    .ToList();

                // Migration-Fallback: Alte Samples mit KbIndexState.None (noch nie durch die neue Pipeline)
                var noneState = allSamples
                    .Where(s => s.Status == TrainingSampleStatus.Approved && s.KbIndexState == KbIndexState.None)
                    .ToList();
                if (noneState.Count > 0)
                {
                    var notInKb = noneState.Where(s => !kbManager.IsIndexed(s.SampleId)).ToList();
                    foreach (var s in notInKb)
                        s.KbIndexState = KbIndexState.Pending;
                    // Bereits indexierte als Indexed markieren
                    foreach (var s in noneState.Except(notInKb))
                        s.KbIndexState = KbIndexState.Indexed;
                    if (noneState.Count > 0)
                        await TrainingSamplesStore.MergeOrUpdateAsync(noneState);
                    unindexed.AddRange(notInKb);
                }

                if (unindexed.Count > 0)
                {
                    Log($"KB-Nachholpfad: {unindexed.Count} Samples noch nicht in KB — indexiere nach...");
                    StatusText = $"KB-Nachholpfad: {unindexed.Count} Samples nachindizieren...";
                    ProgressMax = unindexed.Count;
                    try
                    {
                        var indexedIds = await kbManager.IndexSamplesAsync(unindexed, ct);
                        var indexedSet = indexedIds.ToHashSet();
                        foreach (var s in unindexed)
                            s.KbIndexState = indexedSet.Contains(s.SampleId)
                                ? KbIndexState.Indexed
                                : KbIndexState.Error;
                        await TrainingSamplesStore.MergeOrUpdateAsync(unindexed);
                        Log($"KB-Nachholpfad fertig: {indexedIds.Count}/{unindexed.Count} nachindiziert");
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        Log($"  KB-Nachhol Fehler: {ex.Message}");
                        // KbIndexState bleibt Pending/Error → naechster Lauf versucht es erneut
                    }
                }
            }

            ProgressMax = casesToProcess.Count;
            var totalNew = 0;
            var totalIndexed = 0;
            var errors = 0;
            var lastError = "";
            var emptyProtocols = 0;
            var duplicateOnlyCases = 0;
            var missingProtocols = 0;
            var unreadableProtocols = 0;

            try // try-finally fuer kbCtx/kbHttp Dispose
            {
            for (var i = 0; i < casesToProcess.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var tc = casesToProcess[i];
                ProgressValue = i + 1;
                StatusText = $"[{i + 1}/{casesToProcess.Count}] {tc.CaseId}...";
                Log($"--- [{i + 1}/{casesToProcess.Count}] {tc.CaseId} ---");
                Log($"  Protokoll: {tc.ProtocolPath}");
                Log($"  Video: {(string.IsNullOrEmpty(tc.VideoPath) ? "keins" : tc.VideoPath)}");

                try
                {
                    // Preview-Frame extrahieren
                    var previewFrame = await ExtractPreviewFrameAsync(tc, cfg, ct);
                    if (!string.IsNullOrEmpty(previewFrame))
                        UpdateLivePreview(tc.CaseId, "Verarbeite...", "—", previewFrame);
                    else
                        UpdateLivePreview(tc.CaseId, "Verarbeite...", "—", null);

                    var generation = await generator.GenerateWithDiagnosticsAsync(
                        ToTrainingCaseInput(tc), existingSigs, framesDir: null, ct);
                    var newSamples = generation.Samples;

                    if (newSamples.Count == 0)
                    {
                        string skipReason;
                        switch (generation.Outcome)
                        {
                            case TrainingSampleGenerationOutcome.OnlyDuplicates:
                                duplicateOnlyCases++;
                                skipReason = $"{generation.ParsedEntries} Duplikate";
                                Log($"  -> 0 Samples (alle {generation.ParsedEntries} Eintraege bereits vorhanden)");
                                UpdateLivePreview(tc.CaseId, skipReason, "bereits vorhanden", previewFrame);
                                break;
                            case TrainingSampleGenerationOutcome.ProtocolFileMissing:
                                missingProtocols++;
                                skipReason = "Protokoll fehlt";
                                Log("  -> 0 Samples (Protokolldatei fehlt)");
                                UpdateLivePreview(tc.CaseId, "—", skipReason, previewFrame);
                                break;
                            case TrainingSampleGenerationOutcome.ProtocolUnreadable:
                                unreadableProtocols++;
                                skipReason = "nicht lesbar";
                                Log("  -> 0 Samples (Protokoll nicht lesbar)");
                                UpdateLivePreview(tc.CaseId, "—", skipReason, previewFrame);
                                break;
                            default:
                                emptyProtocols++;
                                skipReason = "keine Eintraege";
                                Log("  -> 0 Samples (keine Protokolleintraege erkannt)");
                                UpdateLivePreview(tc.CaseId, "—", skipReason, previewFrame);
                                break;
                        }

                        // Uebersprungene Haltungen trotzdem im Ergebnis-Verlauf zeigen
                        void AddSkipped()
                        {
                            SelfTrainingResults.Add(new SelfTrainingEntryResult
                            {
                                Index = SelfTrainingResults.Count + 1,
                                VsaCode = tc.CaseId,
                                Meter = 0,
                                Level = MatchLevel.NoFindings,
                                Summary = skipReason
                            });
                        }
                        if (System.Windows.Application.Current?.Dispatcher is { } dSkip && !dSkip.CheckAccess())
                            dSkip.Invoke(AddSkipped);
                        else
                            AddSkipped();

                        continue; // Naechster Case
                    }

                    // Smart-Approve + Signaturen registrieren + Live-Visualisierung
                    foreach (var s in newSamples)
                    {
                        s.Status = !string.IsNullOrEmpty(s.FramePath)
                            ? TrainingSampleStatus.Approved
                            : TrainingSampleStatus.New;
                        existingSigs.Add(s.Signature);

                        // Live-Frame pro Sample (nicht nur pro Case)
                        var sampleFrame = !string.IsNullOrEmpty(s.FramePath) ? s.FramePath : previewFrame;
                        UpdateLivePreview(tc.CaseId, s.Code, $"{s.MeterStart:F2} – {s.MeterEnd:F2} m", sampleFrame);

                        // Ergebnis-Verlauf: Sample als Eintrag hinzufuegen
                        // Batch-Import hat keinen echten KI-vs-Protokoll Vergleich,
                        // daher Match-Rate NICHT aktualisieren (nur im Selbsttraining sinnvoll).
                        var level = s.Status == TrainingSampleStatus.Approved
                            ? MatchLevel.ExactMatch : MatchLevel.NoFindings;
                        void AddResult()
                        {
                            SelfTrainingResults.Add(new SelfTrainingEntryResult
                            {
                                Index = SelfTrainingResults.Count + 1,
                                VsaCode = s.Code,
                                Meter = s.MeterStart,
                                Level = level,
                                Summary = s.Beschreibung
                            });
                            // Code-Verteilung aktualisieren
                            UpdateCodeDistribution(s.Code, level);
                        }
                        if (System.Windows.Application.Current?.Dispatcher is { } dp && !dp.CheckAccess())
                            dp.Invoke(AddResult);
                        else
                            AddResult();
                    }

                    var autoApproved = newSamples.Count(s => s.Status == TrainingSampleStatus.Approved);
                    var needsReview = newSamples.Count - autoApproved;
                    totalNew += newSamples.Count;

                    Log($"  -> {newSamples.Count} Samples ({autoApproved} approved, {needsReview} zur Pruefung):");
                    foreach (var s in newSamples)
                        Log($"     {s.Code} @ {s.MeterStart:F2}m [{s.Status}] - {s.Beschreibung}");

                    // Approved Samples als Pending markieren (vor dem Speichern)
                    foreach (var s in newSamples.Where(s => s.Status == TrainingSampleStatus.Approved))
                        s.KbIndexState = KbIndexState.Pending;

                    // ══════════════════════════════════════════════════════════════════
                    // SOFORT SPEICHERN — Crash-sicher pro Haltung
                    // ══════════════════════════════════════════════════════════════════
                    await TrainingSamplesStore.MergeAndSaveAsync(newSamples);

                    // SOFORT in KB indexieren (inkrementell, kein Rebuild/Delete)
                    if (kbManager is not null)
                    {
                        var approvedForKb = newSamples
                            .Where(s => s.Status == TrainingSampleStatus.Approved)
                            .ToList();

                        if (approvedForKb.Count > 0)
                        {
                            try
                            {
                                var indexedIds = await kbManager.IndexSamplesAsync(approvedForKb, ct);
                                totalIndexed += indexedIds.Count;
                                // Pro Sample: Indexed oder Error je nach Ergebnis
                                var indexedSet = indexedIds.ToHashSet();
                                foreach (var s in approvedForKb)
                                    s.KbIndexState = indexedSet.Contains(s.SampleId)
                                        ? KbIndexState.Indexed
                                        : KbIndexState.Error;
                                await TrainingSamplesStore.MergeOrUpdateAsync(approvedForKb);
                            }
                            catch (Exception kbEx) when (kbEx is not OperationCanceledException)
                            {
                                Log($"     KB-Index Fehler: {kbEx.Message}");
                                // KbIndexState bleibt Pending → Nachholpfad beim naechsten Lauf
                            }
                        }
                    }

                    // UI-Zaehler aktualisieren (Samples + Codes)
                    allSamples.AddRange(newSamples);
                    var distinctCodes = allSamples.Select(s => s.Code).Distinct().Count();
                    void UpdateCounters()
                    {
                        KbSampleCount = allSamples.Count;
                        KbCodesCovered = distinctCodes;
                    }
                    if (System.Windows.Application.Current?.Dispatcher is { } disp && !disp.CheckAccess())
                        disp.Invoke(UpdateCounters);
                    else
                        UpdateCounters();

                    Log($"  Gespeichert + KB: {autoApproved} indexiert | Gesamt: {allSamples.Count} Samples, {distinctCodes} Codes");

                    // Case-State periodisch sichern (alle 10 Haltungen),
                    // damit die UI nach einem Crash den Fortschritt korrekt anzeigt.
                    if ((i + 1) % 5 == 0)
                    {
                        try
                        {
                            await _store.SaveAsync(new TrainingCenterState
                            {
                                Cases = Cases.ToList(),
                                UpdatedUtc = DateTime.UtcNow
                            });
                        }
                        catch { /* best-effort, Samples sind bereits gesichert */ }
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    errors++;
                    lastError = ex.Message;
                    Log($"  FEHLER: {ex.Message}");
                }
            }
            } // end try
            finally
            {
                kbCtx?.Dispose();
                // _kbHttpClient wird wiederverwendet, nicht disposen
            }

            // KB-Version erstellen (nach allen Cases)
            if (totalIndexed > 0)
            {
                try
                {
                    _kbHttpClient ??= new System.Net.Http.HttpClient { Timeout = ollamaConfig.RequestTimeout };
                    using var finalKbCtx = new KnowledgeBaseContext();
                    var finalManager = new KnowledgeBaseManager(finalKbCtx, new EmbeddingService(_kbHttpClient, ollamaConfig));
                    finalManager.CreateVersion($"Batch-Import {DateTime.Now:yyyy-MM-dd HH:mm}");
                }
                catch { /* Version-Erstellung ist optional */ }
            }

            // Abschlussmeldung
            Samples.Clear();
            allSamples = await TrainingSamplesStore.LoadAsync();
            foreach (var s in allSamples)
                Samples.Add(s);

            if (totalNew == 0 && casesToProcess.Count > 0)
            {
                var diag = $"0 neue Samples aus {casesToProcess.Count} Faellen.";
                if (errors > 0) diag += $" {errors} Fehler (letzter: {lastError}).";
                if (emptyProtocols > 0) diag += $" {emptyProtocols} ohne Eintraege.";
                if (duplicateOnlyCases > 0) diag += $" {duplicateOnlyCases} nur Duplikate.";
                if (missingProtocols > 0) diag += $" {missingProtocols} fehlende Protokolle.";
                if (unreadableProtocols > 0) diag += $" {unreadableProtocols} nicht lesbar.";
                Log(diag);
                StatusText = diag;
                return;
            }

            var finalStatus = $"Fertig! {totalNew} Samples gespeichert, {totalIndexed} in KB indexiert";
            if (errors > 0) finalStatus += $", {errors} Fehler";
            if (!ollamaReachable) finalStatus += " (KB-Indexierung uebersprungen: Ollama offline)";
            Log(finalStatus);
            StatusText = finalStatus;

            await RefreshKbStatusAsync();

            // 5. Save cases
            await _store.SaveAsync(new TrainingCenterState
            {
                Cases = Cases.ToList(),
                UpdatedUtc = DateTime.UtcNow
            });
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
        _genCts?.Cancel();
        StatusText = "Abbruch angefordert...";
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
    /// Extrahiert einen einzelnen Preview-Frame aus dem Video (bei Sekunde 2).
    /// Wird für die Live-Vorschau genutzt, auch wenn keine neuen Samples generiert werden.
    /// </summary>
    private static async Task<string?> ExtractPreviewFrameAsync(TrainingCase tc, AiRuntimeSettings cfg, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(tc.VideoPath) || !File.Exists(tc.VideoPath))
            return null;

        var ffmpeg = cfg.FfmpegPath ?? "ffmpeg";
        var sampleId = $"preview_{Regex.Replace(tc.CaseId, @"[^\w\-]", "_")}";
        try
        {
            return await FrameStore.ExtractAndStoreAsync(ffmpeg, tc.VideoPath, 2.0, sampleId, null, ct);
        }
        catch
        {
            return null;
        }
    }

    private static MeterTimelineService CreateMeterTimelineService(AiRuntimeSettings cfg, int concurrency = 1)
    {
        if (!cfg.Enabled)
            return new MeterTimelineService(cfg);

        var ollamaClient = new OllamaClient(
            cfg.OllamaBaseUri,
            ownedTimeout: cfg.OllamaRequestTimeout,
            keepAlive: cfg.OllamaKeepAlive,
            numCtx: cfg.OllamaNumCtx);
        var vision = new OllamaVisionFindingsService(ollamaClient, cfg.VisionModel);
        var osd = new OsdMeterDetectionService(vision);
        return new MeterTimelineService(cfg, osd, concurrency);
    }

    private static TrainingCaseInput ToTrainingCaseInput(TrainingCase tc)
        => new(tc.CaseId, tc.FolderPath, tc.VideoPath, tc.ProtocolPath);

    private static TrainingCase ToTrainingCase(TrainingCaseInput input)
        => new()
        {
            CaseId = input.CaseId,
            FolderPath = input.FolderPath,
            VideoPath = input.VideoPath,
            ProtocolPath = input.ProtocolPath,
            Status = TrainingCaseStatus.New,
            CreatedUtc = DateTime.UtcNow
        };

    private static string ResolveFfmpegPath(string? ffmpegPath)
    {
        if (string.IsNullOrWhiteSpace(ffmpegPath))
            return "ffmpeg";

        return File.Exists(ffmpegPath) || string.Equals(ffmpegPath, "ffmpeg", StringComparison.OrdinalIgnoreCase)
            ? ffmpegPath
            : "ffmpeg";
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
            var indexedIds = await IncrementalKbUpdateAsync(
                new List<TrainingSample> { changedSample },
                CancellationToken.None);
            changedSample.KbIndexState = indexedIds.Contains(changedSample.SampleId)
                ? KbIndexState.Indexed
                : KbIndexState.Error;
            await TrainingSamplesStore.MergeOrUpdateAsync(new List<TrainingSample> { changedSample });
        }
    }

    /// <summary>
    /// Prüft ob Ollama erreichbar ist (GET /api/tags).
    /// </summary>
    private static async Task<bool> CheckOllamaReachableAsync(OllamaConfig config, CancellationToken ct)
    {
        try
        {
            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var resp = await http.GetAsync(new Uri(config.BaseUri, "/api/tags"), ct).ConfigureAwait(false);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // ── Review Queue (Self-Improving Loop) ──────────────────────────────

    /// <summary>Loads pending review items into the queue.</summary>
    public void LoadReviewQueue(InfraSelfImproving.ReviewQueueService queueService)
    {
        ReviewQueue.Clear();
        foreach (var item in queueService.GetAll())
            ReviewQueue.Add(item);
        ReviewQueueCount = ReviewQueue.Count;
        ReviewStatusText = $"{ReviewQueueCount} Einträge zur Prüfung";
    }

    /// <summary>Approve a review item (accept the suggested code).</summary>
    public async Task ApproveReviewItemAsync(
        InfraSelfImproving.ReviewQueueItem item,
        InfraSelfImproving.FeedbackIngestionService feedback,
        InfraSelfImproving.ReviewQueueService queueService,
        CancellationToken ct = default)
    {
        if (item.Entry is not null)
        {
            await feedback.ProcessFeedbackAsync(
                item.Entry, item.Entry.SuggestedCode ?? "", accepted: true, ct).ConfigureAwait(false);
        }
        else if (item.IsFromSelfTraining)
        {
            // Self-Training Review: Sample-Status auf Approved setzen + in KB indexieren
            await ApplySelfTrainingReviewAsync(
                item.SelfTrainingCaseId!, item.SelfTrainingVsaCode!,
                item.SelfTrainingMeter ?? 0, approved: true, correctedCode: null, ct);
        }
        queueService.Remove(item.Id);
        ReviewQueue.Remove(item);
        ReviewQueueCount = ReviewQueue.Count;
        ReviewStatusText = $"Approved: {item.SuggestedCode} | {ReviewQueueCount} verbleibend";
        Log($"Review Approved: {item.Label} → {item.SuggestedCode}");
    }

    /// <summary>Reject a review item with a corrected code.</summary>
    public async Task RejectReviewItemAsync(
        InfraSelfImproving.ReviewQueueItem item,
        string correctedCode,
        InfraSelfImproving.FeedbackIngestionService feedback,
        InfraSelfImproving.ReviewQueueService queueService,
        CancellationToken ct = default)
    {
        if (item.Entry is not null)
        {
            await feedback.ProcessFeedbackAsync(
                item.Entry, correctedCode, accepted: false, ct).ConfigureAwait(false);
        }
        else if (item.IsFromSelfTraining)
        {
            // Self-Training Review: Sample-Status auf Rejected setzen, Code korrigieren
            await ApplySelfTrainingReviewAsync(
                item.SelfTrainingCaseId!, item.SelfTrainingVsaCode!,
                item.SelfTrainingMeter ?? 0, approved: false, correctedCode: correctedCode, ct);
        }
        queueService.Remove(item.Id);
        ReviewQueue.Remove(item);
        ReviewQueueCount = ReviewQueue.Count;
        ReviewStatusText = $"Rejected: {item.SuggestedCode} → {correctedCode} | {ReviewQueueCount} verbleibend";
        Log($"Review Rejected: {item.Label} → {item.SuggestedCode} korrigiert zu {correctedCode}");
    }

    /// <summary>
    /// Wendet eine Self-Training-Review-Entscheidung auf das TrainingSample an.
    /// Bei Approve: Status → Approved, inkrementelles KB-Update.
    /// Bei Reject: Status → Rejected (mit korrigiertem Code falls angegeben).
    /// </summary>
    private async Task ApplySelfTrainingReviewAsync(
        string caseId, string vsaCode, double meter,
        bool approved, string? correctedCode, CancellationToken ct)
    {
        try
        {
            var allSamples = await TrainingSamplesStore.LoadAsync();
            var match = allSamples.FirstOrDefault(s =>
                s.CaseId == caseId
                && s.Code == vsaCode
                && Math.Abs(s.MeterStart - meter) < 0.2);

            if (match is null)
            {
                Log($"Self-Training Review: Sample nicht gefunden ({caseId}/{vsaCode}@{meter:F1}m)");
                return;
            }

            if (approved)
            {
                match.Status = TrainingSampleStatus.Approved;
                match.KbIndexState = KbIndexState.Pending;
                match.MatchLevel = MatchLevelNames.ReviewApproved;
                Log($"Self-Training Review: {vsaCode}@{meter:F1}m → Approved");

                // Inkrementell in KB indexieren
                var indexedIds = await IncrementalKbUpdateAsync(new List<TrainingSample> { match }, ct);
                match.KbIndexState = indexedIds.Contains(match.SampleId)
                    ? KbIndexState.Indexed
                    : KbIndexState.Error;
            }
            else
            {
                match.Status = TrainingSampleStatus.Rejected;
                if (!string.IsNullOrEmpty(correctedCode))
                {
                    Log($"Self-Training Review: {vsaCode}@{meter:F1}m → Rejected, Code korrigiert zu {correctedCode}");
                    match.Notes = $"Korrigiert: {vsaCode} → {correctedCode}";

                    // Korrigiertes Sample als neues Trainingsbeispiel erzeugen
                    var corrected = new TrainingSample
                    {
                        SampleId = $"{match.SampleId}_corr",
                        CaseId = match.CaseId,
                        Code = correctedCode,
                        Beschreibung = match.Beschreibung,
                        MeterStart = match.MeterStart,
                        MeterEnd = match.MeterEnd,
                        IsStreckenschaden = match.IsStreckenschaden,
                        TimeSeconds = match.TimeSeconds,
                        DetectedMeter = match.DetectedMeter,
                        MeterSource = match.MeterSource,
                        FramePath = match.FramePath,
                        Status = TrainingSampleStatus.Approved,
                        KbIndexState = KbIndexState.Pending,
                        TruthMeterCenter = match.TruthMeterCenter,
                        OdsDeltaMeters = match.OdsDeltaMeters,
                        HasOsdMismatch = match.HasOsdMismatch,
                        Signature = TrainingSample.BuildCanonicalSignature(match.CaseId, correctedCode, match.MeterStart, match.MeterEnd),
                        MatchLevel = MatchLevelNames.ReviewCorrected,
                        SourceType = match.SourceType,
                        TechniqueGrade = match.TechniqueGrade,
                        KiCode = match.KiCode,
                        Notes = $"Korrektur aus Review: {vsaCode} → {correctedCode}"
                    };
                    // Korrigiertes Sample per Merge speichern (Race-Condition-sicher)
                    await TrainingSamplesStore.MergeAndSaveAsync(new List<TrainingSample> { corrected });

                    // Inkrementell in KB indexieren
                    var corrIndexedIds = await IncrementalKbUpdateAsync(new List<TrainingSample> { corrected }, ct);
                    corrected.KbIndexState = corrIndexedIds.Contains(corrected.SampleId)
                        ? KbIndexState.Indexed
                        : KbIndexState.Error;
                    await TrainingSamplesStore.MergeOrUpdateAsync(new List<TrainingSample> { corrected });

                    Log($"Korrigiertes Sample {corrected.SampleId} erzeugt, KB-Status: {corrected.KbIndexState}");
                }
                else
                {
                    Log($"Self-Training Review: {vsaCode}@{meter:F1}m → Rejected");
                }
            }

            // Status-Aenderung von match (Approved/Rejected) atomar speichern.
            // MergeOrUpdateAsync statt SaveAsync: verhindert Ueberschreiben von parallel
            // geschriebenen Samples (z.B. corrected Sample aus L1569).
            await TrainingSamplesStore.MergeOrUpdateAsync(new List<TrainingSample> { match });
            await LoadSamplesInternalAsync();
        }
        catch (Exception ex)
        {
            Log($"Self-Training Review Fehler: {ex.Message}");
        }
    }

    // ── Selbsttraining (Orchestrator) ──────────────────────────────────

    [ObservableProperty] private bool _isSelfTrainingRunning;
    private CancellationTokenSource? _selfTrainingCts;
    private ISelfTrainingOrchestrator? _selfTrainingOrchestrator;
    private string _activeVisionModel = "Qwen2.5-VL";

    [RelayCommand]
    private async Task RunSelfTrainingAsync()
    {
        if (IsBusy || IsSelfTrainingRunning) return;

        // Auto-Scan: Wenn keine Faelle geladen, Ordner automatisch scannen
        if (Cases.Count == 0 && _rootFolders.Count > 0)
        {
            StatusText = "Scanne Ordner automatisch...";
            foreach (var folder in _rootFolders)
            {
                if (!Directory.Exists(folder)) continue;
                var found = await _import.ScanAsync(folder);
                foreach (var c in found.Select(ToTrainingCase))
                    Cases.Add(c);
            }
        }

        // Auto-Auswahl: Bereits verarbeitete Haltungen ueberspringen
        if (SelectedCase is null)
        {
            var existingSamples = await TrainingSamplesStore.LoadAsync();
            var processedIds = existingSamples.Select(s => s.CaseId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var firstUnprocessed = Cases.FirstOrDefault(c =>
                !string.IsNullOrEmpty(c.ProtocolPath) && !processedIds.Contains(c.CaseId));

            if (firstUnprocessed is null)
            {
                // Fallback: Alle bereits verarbeitet oder keine mit Protokoll
                var withProtocol = Cases.Count(c => !string.IsNullOrEmpty(c.ProtocolPath));
                StatusText = withProtocol > 0
                    ? $"Alle {withProtocol} Faelle bereits verarbeitet. Waehle manuell fuer erneutes Training."
                    : "Keine Faelle mit Protokoll vorhanden. Bitte zuerst Ordner waehlen und scannen.";
                return;
            }
            SelectedCase = firstUnprocessed;
        }
        if (string.IsNullOrEmpty(SelectedCase.ProtocolPath))
        {
            StatusText = "Der ausgewaehlte Fall hat kein Protokoll (PDF).";
            return;
        }

        _selfTrainingCts?.Cancel();
        _selfTrainingCts?.Dispose();
        _selfTrainingCts = new CancellationTokenSource();
        var ct = _selfTrainingCts.Token;

        using var _aiToken = AiTrack.Begin("Selbsttraining");
        try
        {
            IsBusy = true;
            IsSelfTrainingRunning = true;
            ResetSelfTrainingVisuals(resetMatchRate: true);
            LogText = "";
            StatusText = $"Selbsttraining: {SelectedCase.CaseId}...";
            Log($"--- Selbsttraining starten: {SelectedCase.CaseId} ---");
            Log($"  Protokoll: {SelectedCase.ProtocolPath}");

            // Services instanziieren (gleicher Pattern wie BatchImport)
            var cfg = new AppSettingsAiSettingsProvider()
                .Load()
                .ToRuntimeSettings();
            Log($"Ollama: {cfg.OllamaBaseUri}, Modell: {cfg.VisionModel}");

            var visionModel = cfg.VisionModel ?? "Qwen2.5-VL";
            _activeVisionModel = visionModel;
            var ollamaClient = new OllamaClient(
                cfg.OllamaBaseUri,
                ownedTimeout: cfg.OllamaRequestTimeout,
                keepAlive: cfg.OllamaKeepAlive,
                numCtx: cfg.OllamaNumCtx);
            var vision = new EnhancedVisionAnalysisService(ollamaClient, visionModel);
            var comparison = new SelfTrainingComparisonService();
            var technique = new TechniqueAssessmentService(ollamaClient, visionModel);
            var pdfExtractor = new PdfProtocolExtractor();

            var stSettings = await TrainingCenterSettingsStore.LoadAsync();
            _selfTrainingOrchestrator = new SelfTrainingOrchestrator(
                vision, comparison, technique, pdfExtractor, stSettings, ResolveFfmpegPath(cfg.FfmpegPath));

            // Progress-Callback verbindet Orchestrator → ViewModel-Visualisierungen
            var progress = new Progress<SelfTrainingStep>(OnSelfTrainingStep);

            Log("Pipeline gestartet: OSD-Scan → Frame → KI-Analyse → Vergleich → Technik");
            var result = await _selfTrainingOrchestrator.RunAsync(ToTrainingCaseInput(SelectedCase), progress, ct);

            // Ergebnis loggen
            Log($"--- Selbsttraining abgeschlossen ---");
            Log($"  Dauer: {result.Duration:mm\\:ss}");
            Log($"  Eintraege: {result.TotalEntries} gesamt");
            Log($"  ExactMatch: {result.ExactMatches} | PartialMatch: {result.PartialMatches}");
            Log($"  Mismatch: {result.Mismatches} | NoFindings: {result.NoFindings}");
            Log($"  Samples erzeugt: {result.SamplesGenerated}");
            if (result.OverallTechnique is { } t)
                Log($"  Technik: {t.OverallGrade} (Licht={t.LightingQuality}, Schaerfe={t.SharpnessQuality})");

            StatusText = $"Fertig! {result.ExactMatches}/{result.TotalEntries} ExactMatch, "
                       + $"{result.SamplesGenerated} Samples in {result.Duration:mm\\:ss}";

            // Match-Rate-Verlauf persistieren (Counts → Prozente)
            var matchTotal = result.ExactMatches + result.PartialMatches + result.Mismatches + result.NoFindings;
            if (matchTotal > 0)
            {
                await SelfTrainingHistoryStore.AppendRunAsync(new SelfTrainingRunSnapshot(
                    DateTime.UtcNow,
                    result.CaseId,
                    result.TotalEntries,
                    (double)result.ExactMatches / matchTotal,
                    (double)result.PartialMatches / matchTotal,
                    (double)result.Mismatches / matchTotal,
                    (double)result.NoFindings / matchTotal));
            }

            // Inkrementelles KB-Update fuer ExactMatch-Samples (B1)
            if (result.ExactMatches > 0 && result.SamplesGenerated > 0)
            {
                var allSamples = await TrainingSamplesStore.LoadAsync();
                var newApproved = allSamples
                    .Where(s => s.CaseId == result.CaseId
                        && s.Status == TrainingSampleStatus.Approved)
                    .ToList();

                if (newApproved.Count > 0)
                {
                    // Samples als Pending markieren VOR dem Index-Versuch
                    foreach (var s in newApproved.Where(s => s.KbIndexState is KbIndexState.None or KbIndexState.Error))
                        s.KbIndexState = KbIndexState.Pending;
                    await TrainingSamplesStore.MergeOrUpdateAsync(newApproved);

                    Log($"{newApproved.Count} ExactMatch-Samples — starte KB-Update...");
                    var stIndexedIds = await IncrementalKbUpdateAsync(newApproved, ct);
                    var stIndexedSet = stIndexedIds.ToHashSet();
                    foreach (var s in newApproved)
                        s.KbIndexState = stIndexedSet.Contains(s.SampleId)
                            ? KbIndexState.Indexed
                            : (s.KbIndexState == KbIndexState.Pending ? KbIndexState.Error : s.KbIndexState);
                    await TrainingSamplesStore.MergeOrUpdateAsync(newApproved);
                }
            }

            // Hinweis fuer Few-Shot-Export (B2)
            if (result.ExactMatches > 0)
            {
                Log($"{result.ExactMatches} ExactMatch-Samples erzeugt. Fuer Few-Shot-Export: Tab 'Samples' → 'Export Approved'");
            }

            // Review Queue befuellen mit PartialMatch/Mismatch (C1)
            if (ReviewQueueServiceRef is not null && (result.PartialMatches > 0 || result.Mismatches > 0))
            {
                var allSamplesForReview = await TrainingSamplesStore.LoadAsync();
                var reviewCandidates = allSamplesForReview
                    .Where(s => s.CaseId == result.CaseId
                        && s.MatchLevel is MatchLevelNames.PartialMatch or MatchLevelNames.Mismatch)
                    .ToList();

                foreach (var s in reviewCandidates)
                {
                    ReviewQueueServiceRef.EnqueueFromSelfTraining(
                        s.CaseId, s.Code, s.KiCode ?? s.Code,
                        s.MeterStart, s.FramePath, s.MatchLevel!);
                }

                if (reviewCandidates.Count > 0)
                {
                    LoadReviewQueue(ReviewQueueServiceRef);
                    Log($"{reviewCandidates.Count} Samples in Review Queue eingereiht (PartialMatch/Mismatch)");
                }
            }

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
    /// Indexiert Samples inkrementell in die KB. Gibt die SampleIds zurueck,
    /// die tatsaechlich erfolgreich indexiert wurden (leere Liste bei Fehler/Skip).
    /// </summary>
    private async Task<List<string>> IncrementalKbUpdateAsync(List<TrainingSample> samples, CancellationToken ct)
    {
        var indexedIds = new List<string>();
        try
        {
            var ollamaConfig = new AppSettingsAiSettingsProvider()
                .Load()
                .ToOllamaConfig();
            var ollamaReachable = await CheckOllamaReachableAsync(ollamaConfig, ct);
            if (!ollamaReachable)
            {
                Log($"KB-Update uebersprungen: Ollama nicht erreichbar auf {ollamaConfig.BaseUri}");
                return indexedIds;
            }

            _kbHttpClient ??= new System.Net.Http.HttpClient { Timeout = ollamaConfig.RequestTimeout };
            using var kbCtx = new KnowledgeBaseContext();
            var embedder = new EmbeddingService(_kbHttpClient, ollamaConfig);
            var kbManager = new KnowledgeBaseManager(kbCtx, embedder);

            foreach (var sample in samples)
            {
                ct.ThrowIfCancellationRequested();
                if (kbManager.IsIndexed(sample.SampleId))
                    continue;
                if (await kbManager.IndexSampleAsync(sample, ct))
                    indexedIds.Add(sample.SampleId);
            }

            if (indexedIds.Count > 0)
            {
                kbManager.CreateVersion($"Self-Training inkrementell {DateTime.Now:yyyy-MM-dd HH:mm}");
                Log($"KB-Update: {indexedIds.Count} Samples inkrementell indexiert");
            }
            else
            {
                Log("KB-Update: Alle Samples bereits indexiert");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log($"KB-Update Fehler: {ex.Message}");
        }
        return indexedIds;
    }

    [RelayCommand]
    private void StopSelfTraining()
    {
        _selfTrainingCts?.Cancel();
        StatusText = "Selbsttraining wird abgebrochen...";
    }

    [RelayCommand]
    private void PauseSelfTraining()
    {
        if (_selfTrainingOrchestrator is null) return;
        if (_selfTrainingOrchestrator.IsPaused)
        {
            _selfTrainingOrchestrator.Resume();
            StatusText = "Selbsttraining fortgesetzt.";
            Log("Pipeline fortgesetzt.");
        }
        else
        {
            _selfTrainingOrchestrator.Pause();
            StatusText = "Selbsttraining pausiert.";
            Log("Pipeline pausiert.");
        }
    }
}
