using System;
using AuswertungPro.Next.Application.Ai.Vision;
using AuswertungPro.Next.Domain.Ai.Training;
using AuswertungPro.Next.Application.Ai.Ollama;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.SelfImproving;
using AuswertungPro.Next.Infrastructure.Ai.SelfImproving;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.UI.ViewModels.Windows;

using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training.Services;
using AuswertungPro.Next.UI.Services;
using AiTrack = AuswertungPro.Next.UI.Services.AiActivityTracker;

public partial class TrainingCenterViewModel : ObservableObject
{
    private readonly IDialogService _dialogs = App.Resolve<IDialogService>();
    private const int MaxBatchLogLines = 500;
    // KB-Update serialisieren: SQLite vertraegt keine parallelen Schreibzugriffe
    private readonly SemaphoreSlim _kbUpdateLock = new(1, 1);
    private readonly TrainingCenterStore _store;
    private readonly TrainingCenterImportService _import;
    private readonly SampleQualityGateService _sampleQualityGate;

    /// <summary>Wiederverwendbarer HttpClient fuer KB-Operationen (Embedding-Requests).</summary>
    private System.Net.Http.HttpClient? _kbHttpClient;

    /// <summary>Optionale Referenz auf die Review Queue (gesetzt von Window).</summary>
    public AuswertungPro.Next.Application.Ai.SelfImproving.ReviewQueueService? ReviewQueueServiceRef { get; set; }

    // Phase 5.3 Sub-B: ObservableCollection<TrainingCaseViewModel> statt POCO,
    // damit DataGrid PropertyChanged auf Status (Approve/Reject) bekommt.
    public ObservableCollection<TrainingCaseViewModel> Cases { get; } = new();
    public ObservableCollection<TrainingSample> Samples { get; } = new();
    public ObservableCollection<WeakSpotItem> WeakSpots { get; } = new();

    /// <summary>Gefilterte View auf Samples (fuer Bulk-Review). Filter via SampleCodeFilter + SampleStatusFilter.</summary>
    public ICollectionView SamplesView { get; }

    [ObservableProperty] private TrainingCaseViewModel? _selectedCase;
    [ObservableProperty] private TrainingSample? _selectedSample;
    [ObservableProperty] private string _sampleCodeFilter = "";   // Leer = alle
    [ObservableProperty] private string _sampleStatusFilter = "Pending"; // Pending / Approved / Rejected / Alle
    [ObservableProperty] private int _sampleVisibleCount;
    [ObservableProperty] private WeakSpotItem? _selectedWeakSpot;
    [ObservableProperty] private string _weakSpotSummary = "Noch nicht berechnet.";
    [ObservableProperty] private int _weakSpotCount;
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
    [ObservableProperty] private bool _forceRerunAll;  // Erneut durchlaufen (bereits verarbeitete nicht ueberspringen)
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
    public ObservableCollection<AuswertungPro.Next.Application.Ai.SelfImproving.ReviewQueueItem> ReviewQueue { get; } = new();
    [ObservableProperty] private AuswertungPro.Next.Application.Ai.SelfImproving.ReviewQueueItem? _selectedReviewItem;
    [ObservableProperty] private int _reviewQueueCount;
    [ObservableProperty] private string _reviewStatusText = "";

    /// <summary>V4.2 Phase 1.5: Pfad zum Frame des aktuell ausgewaehlten Review-Items (fuer Image-Binding).</summary>
    public string? SelectedReviewFramePath
    {
        get
        {
            var path = SelectedReviewItem?.SelfTrainingFramePath;
            return !string.IsNullOrWhiteSpace(path) && System.IO.File.Exists(path) ? path : null;
        }
    }

    partial void OnSelectedReviewItemChanged(AuswertungPro.Next.Application.Ai.SelfImproving.ReviewQueueItem? value)
    {
        OnPropertyChanged(nameof(SelectedReviewFramePath));
    }

    // ── Selbsttraining-Visualisierungen ──
    public ObservableCollection<SelfTrainingEntryResult> SelfTrainingResults { get; } = new();
    public ObservableCollection<CodeDistributionEntry> CodeDistribution { get; } = new();
    public ObservableCollection<string> SelfTrainingLogEntries { get; } = new();

    /// <summary>Echtzeit-Log als mehrzeiliger String (fuer TextBox-Binding, weisse Schrift auf dunkel).</summary>
    [ObservableProperty] private string _echtzeitLogText = "";

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


    private readonly List<string> _rootFolders = new();
    private CancellationTokenSource? _genCts;

    /// <summary>
    /// Wechselt _genCts atomar gegen eine neue Source. Die alte wird gecancelt
    /// und nach kurzer Verzoegerung disposed — das gibt laufenden Tasks Zeit,
    /// die OperationCanceledException sauber zu propagieren statt eine
    /// ObjectDisposedException auf einem bereits disposeten Token zu werfen.
    /// Das alte Pattern (Cancel(); Dispose(); new();) hatte hier eine Race
    /// zwischen Dispose und ct-Registrierungen in noch laufenden Tasks.
    /// </summary>
    private CancellationToken RotateGenCts()
    {
        var old = System.Threading.Interlocked.Exchange(
            ref _genCts, new CancellationTokenSource());
        if (old is not null)
        {
            try { old.Cancel(); } catch { /* race: already disposed by anderer Pfad */ }
            // Delayed Dispose: laufende Tasks koennen ihre Cancellation noch
            // sauber durchziehen, bevor das Source-Objekt weg ist.
            _ = System.Threading.Tasks.Task.Delay(2000).ContinueWith(_ =>
            {
                try { old.Dispose(); } catch { }
            });
        }
        return _genCts!.Token;
    }

    /// <summary>
    /// Selbsttraining-CTS-Rotation (Audit D2.2): atomarer Tausch + delayed Dispose,
    /// gleiches Pattern wie RotateGenCts. Loest die Race zwischen Dispose und
    /// Token-Registrierungen in noch laufenden Self-Training-Tasks.
    /// </summary>
    private CancellationToken RotateSelfTrainingCts()
    {
        var old = System.Threading.Interlocked.Exchange(
            ref _selfTrainingCts, new CancellationTokenSource());
        if (old is not null)
        {
            try { old.Cancel(); } catch { /* race: bereits disposed */ }
            _ = System.Threading.Tasks.Task.Delay(2000).ContinueWith(_ =>
            {
                try { old.Dispose(); } catch { }
            });
        }
        return _selfTrainingCts!.Token;
    }

    /// <summary>Fügt eine Zeile zum Log hinzu (Thread-safe via Dispatcher).</summary>
    private void Log(string message)
    {
        var ts = $"[{DateTime.Now:HH:mm:ss}]";
        var line = $"{ts} {message}\n";
        void Apply()
        {
            LogText = TrimLogText(LogText + line);
            // Auch ins Echtzeit-Log schreiben (klappbares Panel)
            SelfTrainingLogEntries.Add($"{ts} {message}");
            while (SelfTrainingLogEntries.Count > 100)
                SelfTrainingLogEntries.RemoveAt(0);
            EchtzeitLogText = string.Join("\n", SelfTrainingLogEntries);
        }
        if (System.Windows.Application.Current?.Dispatcher is { } d && !d.CheckAccess())
            d.Invoke(Apply);
        else
            Apply();
    }

    public void AppendToLogText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        void Apply() => LogText = TrimLogText(LogText + text);

        if (System.Windows.Application.Current?.Dispatcher is { } d && !d.CheckAccess())
            d.Invoke(Apply);
        else
            Apply();
    }

    private static string TrimLogText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        var hadTrailingNewline = normalized.EndsWith('\n');
        var lines = normalized.Split('\n');
        var lineCount = hadTrailingNewline ? lines.Length - 1 : lines.Length;

        if (lineCount <= MaxBatchLogLines)
            return normalized;

        var trimmed = string.Join('\n', lines.Skip(lineCount - MaxBatchLogLines).Take(MaxBatchLogLines));
        return hadTrailingNewline ? trimmed + "\n" : trimmed;
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


    public TrainingCenterViewModel(TrainingCenterStore store, TrainingCenterImportService import)
    {
        _store = store;
        _import = import;

        // QualityGate mit VSA-Code-Katalog initialisieren — Phase 5.1.B Etappe 3.J: via DI.
        try
        {
            AuswertungPro.Next.Application.Protocol.ICodeCatalogProvider? catalog = null;
            try { catalog = App.Resolve<AuswertungPro.Next.Application.Protocol.ICodeCatalogProvider>(); } catch { }
            var codes = catalog?.AllowedCodes();
            _sampleQualityGate = new SampleQualityGateService(codes);
        }
        catch
        {
            // Fallback ohne Code-Katalog (nur Struktur-Checks)
            _sampleQualityGate = new SampleQualityGateService();
        }

        // Gefilterte Sample-View fuer Bulk-Review
        SamplesView = CollectionViewSource.GetDefaultView(Samples);
        SamplesView.Filter = SamplePassesFilter;
        Samples.CollectionChanged += (_, _) => RefreshSamplesView();
    }

    private bool SamplePassesFilter(object? obj)
    {
        if (obj is not TrainingSample s) return false;

        // Status-Filter
        var statusOk = SampleStatusFilter switch
        {
            "Pending" => s.Status == TrainingSampleStatus.New,
            "Approved" => s.Status == TrainingSampleStatus.Approved,
            "Rejected" => s.Status == TrainingSampleStatus.Rejected,
            _ => true
        };
        if (!statusOk) return false;

        // Code-Filter (Praefix-Match, case-insensitive)
        if (!string.IsNullOrWhiteSpace(SampleCodeFilter)
            && (s.Code is null
                || !s.Code.StartsWith(SampleCodeFilter, StringComparison.OrdinalIgnoreCase)))
            return false;

        return true;
    }

    private void RefreshSamplesView()
    {
        SamplesView?.Refresh();
        SampleVisibleCount = SamplesView?.Cast<TrainingSample>().Count() ?? 0;
    }

    partial void OnSampleCodeFilterChanged(string value) => RefreshSamplesView();
    partial void OnSampleStatusFilterChanged(string value) => RefreshSamplesView();

    [RelayCommand]
    private async Task RefreshWeakSpotsAsync()
    {
        try
        {
            var curator = new WeakSpotCurator();
            var report = await curator.BuildAsync();

            void Apply()
            {
                WeakSpots.Clear();
                foreach (var item in report.Items)
                    WeakSpots.Add(item);

                WeakSpotSummary = report.Summary;
                WeakSpotCount = WeakSpots.Count;
            }

            if (System.Windows.Application.Current?.Dispatcher is { } d && !d.CheckAccess())
                d.Invoke(Apply);
            else
                Apply();
        }
        catch (Exception ex)
        {
            WeakSpotSummary = $"Schwachstellen konnten nicht berechnet werden: {ex.Message}";
            WeakSpotCount = 0;
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedWeakSpot))]
    private void ShowWeakSpotSamples()
    {
        if (SelectedWeakSpot is null) return;

        SampleCodeFilter = SelectedWeakSpot.ExpectedCode;
        SampleStatusFilter = "Alle";
        StatusText = $"Samples gefiltert nach Schwachstelle {SelectedWeakSpot.ConfusionLabel}.";
    }

    private bool HasSelectedWeakSpot() => SelectedWeakSpot is not null;

    partial void OnSelectedWeakSpotChanged(WeakSpotItem? value)
        => ShowWeakSpotSamplesCommand.NotifyCanExecuteChanged();

    // ── Cases ────────────────────────────────────────────────────────────────

    public async Task LoadAsync()
    {
        var state = await _store.LoadAsync();
        Cases.Clear();
        foreach (var c in state.Cases)
            Cases.Add(new TrainingCaseViewModel(c));

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
        await RefreshWeakSpotsAsync();
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
    private async Task BrowseRootFolderAsync()
    {
        var dlg = new OpenFolderDialog
        {
            Title = "Trainings-Ordner waehlen (Mehrfachauswahl moeglich)",
            Multiselect = true
        };
        if (dlg.ShowDialog() != true)
            return;

        // Neue Auswahl zu bestehenden hinzufuegen (Duplikate vermeiden)
        foreach (var folder in dlg.FolderNames)
        {
            if (!_rootFolders.Contains(folder, StringComparer.OrdinalIgnoreCase))
                _rootFolders.Add(folder);
        }

        UpdateRootFolderDisplay();

        // Auto-Scan nach Ordnerauswahl
        await ScanAsync();
    }

    [RelayCommand]
    private async Task ClearRootFoldersAsync()
    {
        _rootFolders.Clear();
        Cases.Clear();
        SelectedCase = null;
        UpdateRootFolderDisplay();
        await AutoSaveStateAsync();
        StatusText = "Ordner und Faelle geleert.";
    }

    [RelayCommand]
    private async Task RemoveAllCasesAsync()
    {
        var count = Cases.Count;
        Cases.Clear();
        SelectedCase = null;
        await AutoSaveStateAsync();
        StatusText = $"Alle {count} Faelle entfernt.";
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

            // Status bestehender Faelle merken (Merge statt Clear)
            var existingStatus = new Dictionary<string, TrainingCaseStatus>();
            foreach (var c in Cases)
                existingStatus.TryAdd(c.CaseId, c.Status);
            Cases.Clear();

            foreach (var folder in _rootFolders)
            {
                if (!Directory.Exists(folder)) continue;
                var found = await _import.ScanAsync(folder);
                foreach (var c in found)
                {
                    // Status wiederherstellen wenn Fall schon bekannt
                    if (existingStatus.TryGetValue(c.CaseId, out var prevStatus))
                        c.Status = prevStatus;
                    Cases.Add(new TrainingCaseViewModel(c));
                }
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
                Cases = Cases.Select(vm => vm.Model).ToList(),
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
                Cases = Cases.Select(vm => vm.Model).ToList(),
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

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task RemoveCaseAsync()
    {
        if (SelectedCase is null) return;
        var id = SelectedCase.CaseId;
        Cases.Remove(SelectedCase);
        SelectedCase = null;
        await AutoSaveStateAsync();
        StatusText = $"Entfernt: {id} ({Cases.Count} Faelle verbleiben)";
    }

    [RelayCommand]
    private async Task RemoveSelectedCasesAsync(System.Collections.IList? selectedItems)
    {
        if (selectedItems is null || selectedItems.Count == 0) return;
        var toRemove = selectedItems.Cast<TrainingCaseViewModel>().ToList();
        foreach (var c in toRemove)
            Cases.Remove(c);
        SelectedCase = null;
        await AutoSaveStateAsync();
        StatusText = $"{toRemove.Count} Faelle entfernt ({Cases.Count} verbleiben)";
    }

    partial void OnSelectedCaseChanged(TrainingCaseViewModel? value)
    {
        ApproveCommand.NotifyCanExecuteChanged();
        RejectCommand.NotifyCanExecuteChanged();
        SetNewCommand.NotifyCanExecuteChanged();
        RemoveCaseCommand.NotifyCanExecuteChanged();
        GenerateSamplesCommand.NotifyCanExecuteChanged();
    }

    // ── Samples ──────────────────────────────────────────────────────────────


    // ── Review Queue (Self-Improving Loop) ──────────────────────────────

    /// <summary>
    /// Loads pending review items into the queue.
    /// Audit 2026-05-06 Punkt 6: Wenn Code-Frequenzen verfuegbar sind, nutzt
    /// die ReviewQueue Active Learning (60% Uncertainty + 40% Diversity, rarste
    /// Codes zuerst). Sonst fallback auf Priority-only.
    /// </summary>

    // ── Selbsttraining (Orchestrator) ──────────────────────────────────


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
            var ollamaConfig = AuswertungPro.Next.Application.Ai.Ollama.OllamaConfigProvider.Load();
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

            // Nur noch nicht indexierte Samples filtern
            var toIndex = samples.Where(s => !kbManager.IsIndexed(s.SampleId)).ToList();

            // IndexSamplesAsync macht Embeddings sequentiell + DB-Writes in einer Transaktion
            // (thread-safe, kein paralleler Zugriff auf SQLite-Connection)
            if (toIndex.Count > 0)
            {
                var batchResult = await kbManager.IndexSamplesAsync(toIndex, ct);
                indexedIds.AddRange(batchResult);
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
