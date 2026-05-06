using System;
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

namespace AuswertungPro.Next.UI.ViewModels.Windows;

using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.KnowledgeBase;
using AuswertungPro.Next.UI.Ai.Ollama;
using AuswertungPro.Next.UI.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training.Services;
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
    public Ai.SelfImproving.ReviewQueueService? ReviewQueueServiceRef { get; set; }

    public ObservableCollection<TrainingCase> Cases { get; } = new();
    public ObservableCollection<TrainingSample> Samples { get; } = new();
    public ObservableCollection<WeakSpotItem> WeakSpots { get; } = new();

    /// <summary>Gefilterte View auf Samples (fuer Bulk-Review). Filter via SampleCodeFilter + SampleStatusFilter.</summary>
    public ICollectionView SamplesView { get; }

    [ObservableProperty] private TrainingCase? _selectedCase;
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
    public ObservableCollection<Ai.SelfImproving.ReviewQueueItem> ReviewQueue { get; } = new();
    [ObservableProperty] private Ai.SelfImproving.ReviewQueueItem? _selectedReviewItem;
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

    partial void OnSelectedReviewItemChanged(Ai.SelfImproving.ReviewQueueItem? value)
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
            // TextBox-Binding aktualisieren (weisse Schrift auf dunkel)
            EchtzeitLogText = string.Join("\n", SelfTrainingLogEntries);
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
        // CodeDistribution NICHT leeren — Gesamtstand wird beibehalten
        // und im Lauf inkrementell erweitert
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

    private async Task RefreshKbStatusAsync()
    {
        try
        {
            var (summary, totalDistinctCodes, errorCount, newCount, codeCounts) = await Task.Run(() =>
            {
                using var db = new KnowledgeBaseContext();
                var diag = new KnowledgeBaseDiagnosticsService(db);
                var s = diag.ReadSummary(20);
                var allCodes = diag.ReadAllCodeCounts().Count;

                // Sample-Statistik aus JSON fuer Diagnose-Anzeige
                int errors = 0, news = 0;
                Dictionary<string, int> codeCounts = new();
                try
                {
                    var samples = TrainingSamplesStore.LoadAsync().GetAwaiter().GetResult();
                    foreach (var sample in samples)
                    {
                        if (sample.KbIndexState == KbIndexState.Error) errors++;
                        else if (sample.Status == TrainingSampleStatus.New) news++;

                        // Code-Verteilung aus allen Samples (Gesamtstand)
                        if (!string.IsNullOrEmpty(sample.Code))
                        {
                            if (!codeCounts.TryGetValue(sample.Code, out var cnt))
                                codeCounts[sample.Code] = 1;
                            else
                                codeCounts[sample.Code] = cnt + 1;
                        }
                    }
                }
                catch { /* optional */ }

                return (s, allCodes, errors, news, codeCounts);
            });

            void Apply()
            {
                KbSampleCount = summary.SampleCount;
                KbErrorCount = errorCount;
                KbNewCount = newCount;
                KbEmbeddingCount = summary.EmbeddingCount;
                KbCodesCovered = totalDistinctCodes;
                KbLastUpdate = summary.LatestVersionAtUtc?.ToLocalTime().ToString("dd.MM.yyyy HH:mm") ?? "\u2014";

                static System.Windows.Media.SolidColorBrush Rgb(byte r, byte g, byte b)
                    => new(System.Windows.Media.Color.FromRgb(r, g, b));

                (KbReadinessLabel, KbReadinessBrush) = summary.SampleCount switch
                {
                    >= 100 => ("KI-Modell einsatzbereit", Rgb(0x4A, 0xDE, 0x80)),
                    >= 25  => ("Lernbasis grundlegend",   Rgb(0xFA, 0xCC, 0x15)),
                    > 0    => ("Lernbasis unzureichend",  Rgb(0xF8, 0x71, 0x71)),
                    _      => ("Keine Trainingsdaten",    Rgb(0x94, 0xA3, 0xB8))
                };

                KbTopCodesText = string.Join("\n", summary.TopCodes
                    .Select(c => $"{c.VsaCode}: {c.Count} Samples"));

                // Code-Verteilung aus Gesamtstand befuellen (wenn leer)
                if (CodeDistribution.Count == 0 && codeCounts.Count > 0)
                {
                    foreach (var (code, count) in codeCounts.OrderByDescending(kv => kv.Value))
                    {
                        CodeDistribution.Add(new CodeDistributionEntry
                        {
                            Code = code,
                            Total = count
                        });
                    }
                }
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
    /// Eigener KnowledgeBaseContext (unabhaengig von RefreshKbStatusAsync).
    /// </summary>
    private async Task RefreshKbQualityAsync()
    {
        try
        {
            var (gaps, gapCount, accuracy, stale) = await Task.Run(() =>
            {
                // Leere KB abfangen: DB existiert evtl. noch nicht
                var dbPath = KnowledgeBaseContext.DefaultDbPath;
                if (!System.IO.File.Exists(dbPath))
                    return ("KB noch nicht erstellt", 0, "Noch keine Validierungsdaten", 0);

                using var db = new KnowledgeBaseContext();
                var diag = new KnowledgeBaseDiagnosticsService(db);

                // Coverage: ALLE Codes abfragen, nicht nur Top-N
                var allCodes = diag.ReadAllCodeCounts();
                var underRep = allCodes.Where(c => c.Count < 3).ToList();
                var gapsText = allCodes.Count == 0
                    ? "KB leer — noch keine Samples indexiert"
                    : underRep.Count > 0
                        ? string.Join("\n", underRep.Select(c => $"{c.VsaCode}: {c.Count} Samples"))
                        : "Keine Luecken (alle Codes >= 3 Samples)";

                // Accuracy (aus ValidationLog)
                string accText;
                try
                {
                    var accSvc = new Ai.Monitoring.AccuracyDashboardService(db.Connection);
                    var metrics = accSvc.ComputeMetrics();
                    accText = metrics.Count > 0
                        ? string.Join("\n", metrics
                            .OrderByDescending(m => m.TruePositives + m.FalsePositives + m.FalseNegatives)
                            .Take(8)
                            .Select(m =>
                                $"{m.VsaCode}: F1={m.F1Score:F2}  P={m.Precision:F2}  R={m.Recall:F2}  (n={m.TruePositives + m.FalsePositives + m.FalseNegatives})"))
                        : "Noch keine Validierungsdaten";
                }
                catch { accText = "Validierungsdaten nicht verfuegbar"; }

                // Stale Samples
                int staleCount = 0;
                try
                {
                    var kbq = new Ai.SelfImproving.KbQualityService(db.Connection);
                    staleCount = kbq.FindStaleCandidates().Count;
                }
                catch { }

                return (gapsText, underRep.Count, accText, staleCount);
            });

            // Trend (aus JSON, kein DB-Zugriff)
            var runs = await Ai.Training.SelfTrainingHistoryStore.LoadAsync();
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
                KbCoverageGapsText = gaps;
                KbCoverageGapsCount = gapCount;
                KbAccuracyText = accuracy;
                KbStaleSampleCount = stale;
                KbTrendText = trendText;
                KbTrendDirection = direction;

                // Stale-Sample Warnung im Log (E1)
                if (stale > 0)
                    Log($"KB-Qualitaet: {stale} veraltete Samples erkannt (manuell pruefen im Tab 'Samples')");
            }
            if (System.Windows.Application.Current?.Dispatcher is { } d && !d.CheckAccess())
                d.Invoke(Apply);
            else
                Apply();
        }
        catch { /* KB evtl. noch nicht vorhanden */ }
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
                    Cases.Add(c);
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
        var toRemove = selectedItems.Cast<TrainingCase>().ToList();
        foreach (var c in toRemove)
            Cases.Remove(c);
        SelectedCase = null;
        await AutoSaveStateAsync();
        StatusText = $"{toRemove.Count} Faelle entfernt ({Cases.Count} verbleiben)";
    }

    partial void OnSelectedCaseChanged(TrainingCase? value)
    {
        ApproveCommand.NotifyCanExecuteChanged();
        RejectCommand.NotifyCanExecuteChanged();
        SetNewCommand.NotifyCanExecuteChanged();
        RemoveCaseCommand.NotifyCanExecuteChanged();
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
        // Rejected-Samples ausblenden — nur New + Approved anzeigen
        foreach (var s in list.Where(s => s.Status != TrainingSampleStatus.Rejected))
            Samples.Add(s);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task GenerateSamplesAsync()
    {
        if (SelectedCase is null || IsBusy) return;

        var ct = RotateGenCts();

        using var _aiToken = AiTrack.Begin("Training Center");
        try
        {
            IsBusy = true;
            StatusText = $"Generiere Samples für {SelectedCase.CaseId}...";

            var cfg = AiRuntimeConfigExtensions.Load();
            var settings = await TrainingCenterSettingsStore.LoadAsync();
            var meterSvc = CreateMeterTimelineService(cfg, settings.GpuConcurrency);
            var generator = new TrainingSampleGenerator(cfg, meterSvc, settings);

            var existing = await TrainingSamplesStore.LoadAsync();
            var existingSigs = existing.Select(s => s.Signature).ToHashSet(StringComparer.Ordinal);

            var generation = await generator.GenerateWithDiagnosticsAsync(
                SelectedCase, existingSigs, framesDir: null, ct);
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
        var current = SelectedSample;
        var idx = Samples.IndexOf(current);
        current.Status = TrainingSampleStatus.Approved;
        StatusText = $"Approved: {current.Code} @ {current.MeterStart:F1}m";
        await PersistSamplesAsync(current);

        // Zum naechsten Sample springen
        Samples.Remove(current);
        if (Samples.Count > 0)
            SelectedSample = Samples[Math.Min(idx, Samples.Count - 1)];
    }

    [RelayCommand(CanExecute = nameof(HasSampleSelection))]
    private async Task RejectSampleAsync()
    {
        if (SelectedSample is null) return;
        var current = SelectedSample;
        var idx = Samples.IndexOf(current);
        current.Status = TrainingSampleStatus.Rejected;
        StatusText = $"Rejected: {current.Code} @ {current.MeterStart:F1}m";
        await PersistSamplesAsync();

        // Eintrag entfernen und zum naechsten springen
        Samples.Remove(current);
        if (Samples.Count > 0)
            SelectedSample = Samples[Math.Min(idx, Samples.Count - 1)];
    }

    [RelayCommand]
    private async Task RejectAllVisibleAsync()
        => await BulkChangeVisibleStatusAsync(TrainingSampleStatus.Rejected, "Reject", "#DC2626");

    /// <summary>
    /// Setzt alle in der DataGrid markierten Samples auf Approved.
    /// SelectedItems wird via CommandParameter aus dem View uebergeben.
    /// </summary>
    [RelayCommand]
    private async Task ApproveSelectedAsync(System.Collections.IList? selected)
    {
        if (IsBusy || selected is null) return;
        var list = selected.Cast<TrainingSample>().ToList();
        if (list.Count == 0) { StatusText = "Keine Zeilen markiert."; return; }
        try
        {
            IsBusy = true;
            foreach (var s in list)
            {
                if (s.Status == TrainingSampleStatus.Approved) continue;
                s.Status = TrainingSampleStatus.Approved;
                if (s.KbIndexState != KbIndexState.Indexed
                    && s.KbIndexState != KbIndexState.Deduplicated)
                {
                    s.KbIndexState = KbIndexState.Pending;
                }
            }
            await PersistSamplesAsync();
            RefreshSamplesView();
            StatusText = $"{list.Count} markierte Samples approved.";
            Log($"Selection-Approve: {list.Count} Samples");
        }
        finally { IsBusy = false; }
    }

    /// <summary>
    /// Loescht alle in der DataGrid markierten Samples HART aus dem JSON.
    /// Konfirmation Pflicht — destruktiv, kein Undo.
    /// </summary>
    [RelayCommand]
    private async Task DeleteSelectedAsync(System.Collections.IList? selected)
    {
        if (IsBusy || selected is null) return;
        var list = selected.Cast<TrainingSample>().ToList();
        if (list.Count == 0) { StatusText = "Keine Zeilen markiert."; return; }

        var confirm = _dialogs.ShowMessage(
            $"{list.Count} Samples werden ENDGUELTIG aus dem Training-Store geloescht.\n\n" +
            $"Frame-Dateien bleiben auf Disk. KB-Eintraege werden NICHT geloescht.\n\nFortfahren?",
            "Markierte Samples loeschen",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning,
            System.Windows.MessageBoxResult.No);
        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            IsBusy = true;
            var idsToRemove = new HashSet<string>(list.Select(s => s.SampleId));
            foreach (var s in list)
                Samples.Remove(s);

            await TrainingSamplesStore.RemoveByIdsAsync(idsToRemove);
            RefreshSamplesView();
            StatusText = $"{list.Count} markierte Samples geloescht.";
            Log($"Selection-Delete: {list.Count} Samples hart aus JSON entfernt");
        }
        finally { IsBusy = false; }
    }

    /// <summary>
    /// Bulk-Approve: alle Samples die aktuell durch den Filter (Code + Status) sichtbar sind
    /// werden auf Approved gesetzt. Mit Konfirmations-Dialog wegen Massenwirkung.
    /// </summary>
    [RelayCommand]
    private async Task ApproveAllVisibleAsync()
        => await BulkChangeVisibleStatusAsync(TrainingSampleStatus.Approved, "Approve", "#16A34A");

    /// <summary>
    /// Setzt alle aktuell durch den Filter sichtbaren Pending-Samples auf den gewaehlten Status.
    /// Konfirmations-Dialog mit Top-3-Code-Summary.
    /// </summary>
    private async Task BulkChangeVisibleStatusAsync(
        TrainingSampleStatus newStatus, string actionLabel, string colorHint)
    {
        if (IsBusy) return;
        var visible = SamplesView?.Cast<TrainingSample>().ToList() ?? new();
        var pendingOnly = visible.Where(s => s.Status == TrainingSampleStatus.New).ToList();
        if (pendingOnly.Count == 0)
        {
            StatusText = "Keine Pending-Samples im aktuellen Filter sichtbar.";
            return;
        }

        var topCodes = pendingOnly
            .GroupBy(s => s.Code ?? "")
            .OrderByDescending(g => g.Count())
            .Take(3)
            .Select(g => $"{g.Key}: {g.Count()}");
        var codeSummary = string.Join(", ", topCodes);

        var confirm = _dialogs.ShowMessage(
            $"{pendingOnly.Count} Pending-Samples werden auf {newStatus} gesetzt.\n\n" +
            $"Top-Codes: {codeSummary}\n\n" +
            $"Stichprobe vorher pruefen!\n\nFortfahren?",
            $"Bulk-{actionLabel}",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question,
            System.Windows.MessageBoxResult.No);

        if (confirm != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            IsBusy = true;
            StatusText = $"Bulk-{actionLabel} laeuft: {pendingOnly.Count} Samples...";

            foreach (var s in pendingOnly)
            {
                s.Status = newStatus;
                // KbIndexState nur setzen wenn noch nicht indexiert — sonst ueberschreiben wir
                // einen bereits erfolgreichen KB-Eintrag und machen die JSON-Statistik unwahr.
                if (newStatus == TrainingSampleStatus.Approved
                    && s.KbIndexState != KbIndexState.Indexed
                    && s.KbIndexState != KbIndexState.Deduplicated)
                {
                    s.KbIndexState = KbIndexState.Pending;
                }
            }
            await PersistSamplesAsync();

            RefreshSamplesView();
            var hint = newStatus == TrainingSampleStatus.Approved
                ? " Klicke 'KB nachindexieren' um sie in die KB zu schreiben."
                : "";
            StatusText = $"Bulk-{actionLabel} fertig: {pendingOnly.Count} Samples auf {newStatus} gesetzt.{hint}";
            Log($"Bulk-{actionLabel}: {pendingOnly.Count} Samples (Codes: {codeSummary})");
        }
        finally
        {
            IsBusy = false;
        }
    }



    /// <summary>
    /// Speichert alle Samples und indexiert optional ein gerade geaendertes Sample in die KB.
    /// </summary>
    private async Task PersistSamplesAsync(TrainingSample? changedSample = null)
    {
        // Immer Merge/Update statt Voll-Save — verhindert Ueberschreiben
        // von parallel geschriebenen Samples (Batch-Import, Self-Training).
        // V4.3: komplette Persistenz-Kette in try/catch — ein Lock-Fehler in
        // TrainingSamplesStore darf die App nicht abstuerzen lassen.
        try
        {
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
        catch (Exception ex)
        {
            StatusText = $"Speichern fehlgeschlagen: {ex.Message}";
            Log($"[Persist] FEHLER: {ex.GetType().Name}: {ex.Message}");
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
    public void LoadReviewQueue(Ai.SelfImproving.ReviewQueueService queueService)
    {
        ReviewQueue.Clear();
        foreach (var item in queueService.GetAll())
            ReviewQueue.Add(item);
        ReviewQueueCount = ReviewQueue.Count;
        ReviewStatusText = $"{ReviewQueueCount} Einträge zur Prüfung";
    }

    /// <summary>Approve a review item (accept the suggested code).</summary>
    public async Task ApproveReviewItemAsync(
        Ai.SelfImproving.ReviewQueueItem item,
        Ai.SelfImproving.FeedbackIngestionService feedback,
        Ai.SelfImproving.ReviewQueueService queueService,
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
                item.SelfTrainingMeter ?? 0, approved: true, correctedCode: null,
                sampleId: item.SelfTrainingSampleId, ct: ct);
        }
        // V4.2 Phase 1.5: TeacherAnnotation anhaengen fuer zukuenftiges Training.
        await TryAppendTeacherAnnotationAsync(item, item.SuggestedCode ?? item.SelfTrainingVsaCode ?? "", "approved");

        queueService.Remove(item.Id);
        ReviewQueue.Remove(item);
        ReviewQueueCount = ReviewQueue.Count;
        ReviewStatusText = $"Approved: {item.SuggestedCode} | {ReviewQueueCount} verbleibend";
        Log($"Review Approved: {item.Label} → {item.SuggestedCode}");
    }

    /// <summary>Reject a review item with a corrected code.</summary>
    public async Task RejectReviewItemAsync(
        Ai.SelfImproving.ReviewQueueItem item,
        string correctedCode,
        Ai.SelfImproving.FeedbackIngestionService feedback,
        Ai.SelfImproving.ReviewQueueService queueService,
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
                item.SelfTrainingMeter ?? 0, approved: false, correctedCode: correctedCode,
                sampleId: item.SelfTrainingSampleId, ct: ct);
        }
        // V4.2 Phase 1.5: TeacherAnnotation mit korrigiertem Code — besonders wertvoll!
        await TryAppendTeacherAnnotationAsync(item, correctedCode, "corrected");

        queueService.Remove(item.Id);
        ReviewQueue.Remove(item);
        ReviewQueueCount = ReviewQueue.Count;
        ReviewStatusText = $"Rejected: {item.SuggestedCode} → {correctedCode} | {ReviewQueueCount} verbleibend";
        Log($"Review Rejected: {item.Label} → {item.SuggestedCode} korrigiert zu {correctedCode}");
    }

    /// <summary>
    /// V4.2 Phase 1.5: Haengt eine TeacherAnnotation an den append-only Store, wenn Frame vorhanden.
    /// Review-Entscheidungen werden so zum Gold-Standard fuer zukuenftiges Training (inkl. DINOv2-Heads in Phase 3.2).
    /// </summary>
    private static async Task TryAppendTeacherAnnotationAsync(
        Ai.SelfImproving.ReviewQueueItem item,
        string vsaCode,
        string reviewKind)
    {
        if (string.IsNullOrWhiteSpace(vsaCode)) return;

        // V4.3 Fix: Frame-Path darf null/leer sein — NoFindings-Items haben oft kein Frame,
        // und die Review-Entscheidung waere sonst verloren. TeacherAnnotation speichert dann
        // nur Code+Meter+Haltung als textueller Gold-Standard fuer FN-Reduktion.
        var framePath = item.SelfTrainingFramePath;
        var frameExists = !string.IsNullOrWhiteSpace(framePath) && System.IO.File.Exists(framePath);

        try
        {
            var annotation = new Ai.Teacher.TeacherAnnotation
            {
                VsaCode = vsaCode.Trim().ToUpperInvariant(),
                Beschreibung = $"Review-{reviewKind}: {item.Label}",
                MeterPosition = item.SelfTrainingMeter ?? 0.0,
                HaltungName = item.SelfTrainingCaseId,
                FullFramePath = frameExists ? framePath : null
            };
            await Ai.Teacher.TeacherAnnotationStore.AppendAsync(annotation);
        }
        catch
        {
            // TeacherAnnotation ist optionale Anreicherung — Review-Flow darf nicht an IO-Fehlern scheitern.
        }
    }

    /// <summary>
    /// Wendet eine Self-Training-Review-Entscheidung auf das TrainingSample an.
    /// Bei Approve: Status → Approved, inkrementelles KB-Update.
    /// Bei Reject: Status → Rejected (mit korrigiertem Code falls angegeben).
    /// </summary>
    private async Task ApplySelfTrainingReviewAsync(
        string caseId, string vsaCode, double meter,
        bool approved, string? correctedCode,
        string? sampleId = null, CancellationToken ct = default)
    {
        try
        {
            var allSamples = await TrainingSamplesStore.LoadAsync();
            // Primaer ueber SampleId suchen (eindeutig), Fallback fuer alte Queue-Eintraege
            var match = !string.IsNullOrEmpty(sampleId)
                ? allSamples.FirstOrDefault(s => s.SampleId == sampleId)
                : allSamples.FirstOrDefault(s =>
                    s.CaseId == caseId
                    && s.Code == vsaCode
                    && Math.Abs(s.MeterStart - meter) < 0.2);

            if (match is null)
            {
                // V4.3: Fuer NoFindings-Items existiert kein TrainingSample (KI hat nichts erkannt).
                // Das ist KEIN Fehler — die Review-Entscheidung wird trotzdem als TeacherAnnotation
                // (siehe TryAppendTeacherAnnotationAsync) persistiert.
                Log($"Self-Training Review: Kein KI-Sample zu aktualisieren ({caseId}/{vsaCode}@{meter:F1}m) — als TeacherAnnotation gespeichert");
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
            var ollamaConfig = OllamaConfigExtensions.Load();
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