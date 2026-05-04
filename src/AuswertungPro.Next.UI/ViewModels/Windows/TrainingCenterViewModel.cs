using System;
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

        // QualityGate mit VSA-Code-Katalog initialisieren
        try
        {
            var sp = App.Services as ServiceProvider;
            var codes = sp?.CodeCatalog?.AllowedCodes();
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

            var cfg = AiRuntimeConfig.Load();
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

        var confirm = System.Windows.MessageBox.Show(
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

        var confirm = System.Windows.MessageBox.Show(
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

    [RelayCommand]
    private async Task ExportApprovedAsync()
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            StatusText = "Protokoll-Training: zaehle Samples...";

            var approved = Samples
                .Where(s => s.Status == TrainingSampleStatus.Approved && s.ExportedUtc is null)
                .ToList();

            if (approved.Count == 0)
            {
                StatusText = "Keine nicht-exportierten Approved-Samples vorhanden.";
                return;
            }

            StatusText = $"Protokoll-Training: exportiere {approved.Count} Samples (kann 30-60 Sek dauern)...";

            // Schwere I/O-Arbeit off-UI ausfuehren damit das Fenster nicht einfriert.
            var items = approved
                .Select(s => (
                    Entry: new AuswertungPro.Next.Domain.Protocol.ProtocolEntry
                    {
                        Code = s.Code,
                        Beschreibung = s.Beschreibung,
                        MeterStart = s.MeterStart,
                        MeterEnd = s.MeterEnd,
                        IsStreckenschaden = s.IsStreckenschaden
                    },
                    HaltungId: (string?)s.CaseId))
                .ToList();

            var added = await Task.Run(() => ProtocolTrainingStore.AddSamples(items));

            var now = DateTime.UtcNow;
            foreach (var s in approved)
                s.ExportedUtc = now;

            await PersistSamplesAsync();

            var codes = approved.Select(s => s.Code).Distinct().OrderBy(c => c).ToList();
            Log($"Protokoll-Training: {added} neu gespeichert (von {approved.Count} geprueft, Rest war Duplikat).");
            Log($"  Codes: {codes.Count} verschiedene");
            Log($"  Ziel: {Path.Combine(AppSettings.AppDataDir, "data", "protocol_training.json")}");
            StatusText = $"Protokoll-Training fertig: {added} neu, {approved.Count - added} Duplikate, {codes.Count} Codes.";

            System.Windows.MessageBox.Show(
                $"Protokoll-Training fertig.\n\n" +
                $"Neu hinzugefuegt: {added}\n" +
                $"Duplikate uebersprungen: {approved.Count - added}\n" +
                $"Verschiedene Codes: {codes.Count}",
                "Protokoll-Training",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Log($"Protokoll-Training FEHLER: {ex.GetType().Name}: {ex.Message}");
            StatusText = $"Protokoll-Training fehlgeschlagen: {ex.Message}";
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

        var ct = RotateGenCts();

        try
        {
            IsBusy = true;
            Log($"YOLO-Export: {approved.Count} Samples → {outputDir}");
            StatusText = $"YOLO-Export: {approved.Count} Samples werden vorbereitet...";

            // Sidecar-Pfad (Base64-Upload) ist deaktiviert, weil er bei vielen
            // Samples den RAM sprengt (alle Bilder als Base64-Strings in einer
            // Liste + JSON-Serialisierung in einen HTTP-Request → 10+ GB
            // Peak-Memory, OOM bei 31 GB App-Baseline).
            // Der lokale Pfad (File.Copy + kleine Label-Writes) ist
            // RAM-schonend und liefert dasselbe Dataset-Layout.
            // Siehe docs/AUDIT_SEWERSTUDIO_2026-04-23.md — STAB-H7.
            Log("YOLO-Export: lokaler Pfad (RAM-schonend via File.Copy)...");
            await ExportYoloLocalAsync(approved, outputDir, ct).ConfigureAwait(false);
            return;

#pragma warning disable CS0162 // Unreachable code — Sidecar-Upload-Pfad absichtlich
            // Sidecar-Verbindung prüfen
            var pipelineCfg = PipelineConfig.Load();
            var client = new VisionPipelineClient(pipelineCfg.SidecarUrl);

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
            var response = await client.ExportTrainingAsync(request, ct).ConfigureAwait(false);

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
#pragma warning restore CS0162
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
        var annotations = await Ai.Teacher.TeacherAnnotationStore.LoadAsync();
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
                var clsIdx = Ai.Teacher.VsaYoloClassMap.GetClassId(a.VsaCode);
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

                var clsIdx = Ai.Teacher.VsaYoloClassMap.GetClassId(s.Code);
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
        var fullMap = Ai.Teacher.VsaYoloClassMap.GetFullMap();
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
        await Ai.Teacher.VsaYoloClassMap.ExportClassesTxtAsync(
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

        var ct = RotateGenCts();

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
                found.AddRange(result);
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

            // Status bestehender Faelle erhalten (Merge statt Clear)
            var existingStatus = new Dictionary<string, TrainingCaseStatus>();
            foreach (var c in Cases)
                existingStatus.TryAdd(c.CaseId, c.Status);
            Cases.Clear();
            foreach (var c in found)
            {
                if (existingStatus.TryGetValue(c.CaseId, out var prevStatus))
                    c.Status = prevStatus;
                Cases.Add(c);
            }

            if (casesWithProtocol.Count == 0)
            {
                Log("STOP: Keine Ordner mit Protokoll-Dateien gefunden.");
                StatusText = "Keine Ordner mit Protokoll-Dateien gefunden.";
                return;
            }

            // 2. Generate samples for all cases
            var cfg = AiRuntimeConfig.Load();
            Log($"AI Config: Enabled={cfg.Enabled}, ffmpeg={cfg.FfmpegPath}");

            var settings = await TrainingCenterSettingsStore.LoadAsync();
            var meterSvc = CreateMeterTimelineService(cfg, settings.GpuConcurrency);
            var generator = new TrainingSampleGenerator(cfg, meterSvc, settings);

            var allSamples = await TrainingSamplesStore.LoadAsync();
            var existingSigs = allSamples.Select(s => s.Signature)
                .Where(s => !string.IsNullOrEmpty(s))
                .ToHashSet(StringComparer.Ordinal);

            // ── Case-Level Skip: Haltungen die bereits Samples haben komplett ueberspringen ──
            var existingCaseIds = allSamples
                .Where(s => !string.IsNullOrEmpty(s.CaseId))
                .Select(s => s.CaseId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var casesToProcess = new List<TrainingCase>();
            var caseSkipped = 0;
            foreach (var c in casesWithProtocol)
            {
                if (existingCaseIds.Contains(c.CaseId))
                {
                    caseSkipped++;
                    c.Status = TrainingCaseStatus.BatchImported; // UI-Status aktualisieren
                }
                else
                {
                    casesToProcess.Add(c);
                }
            }

            Log($"Bestehende Samples: {allSamples.Count} ({existingSigs.Count} Signaturen, {existingCaseIds.Count} CaseIds)");
            if (caseSkipped > 0)
                Log($"Case-Level Skip: {caseSkipped} Haltungen bereits verarbeitet, {casesToProcess.Count} neu");
            else
                Log($"Keine bereits verarbeiteten Haltungen gefunden — alle {casesToProcess.Count} werden verarbeitet");

            if (casesToProcess.Count == 0)
            {
                StatusText = $"Alle {casesWithProtocol.Count} Haltungen bereits verarbeitet. KB ist aktuell.";
                Log($"FERTIG: Alle {casesWithProtocol.Count} Haltungen haben bereits Samples in der KB.");
                // KB-Nachholpfad trotzdem pruefen (unten)
            }

            // Ollama-Verbindung einmalig pruefen + KB-Objekte vorbereiten
            var ollamaConfig = OllamaConfig.Load();
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

                    var generation = await generator.GenerateWithDiagnosticsAsync(tc, existingSigs, framesDir: null, ct, skipVideoTimeline: true);
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

                    // ── QualityGate: Samples pruefen bevor sie gespeichert werden ──
                    var qgBatch = _sampleQualityGate.EvaluateBatch(newSamples);
                    if (qgBatch.Red > 0)
                    {
                        Log($"  QualityGate: {qgBatch.Red} Samples abgelehnt (Red)");
                        foreach (var (rs, rr) in qgBatch.Results.Where(r => !r.Result.IsAcceptable))
                            Log($"     REJECT {rs.Code} @ {rs.MeterStart:F2}m: {string.Join(", ", rr.Issues)}");
                    }
                    // Nur akzeptierte Samples weiterverarbeiten
                    newSamples = qgBatch.Accepted.ToList();

                    // Smart-Approve basierend auf QualityGate-Ergebnis
                    foreach (var s in newSamples)
                    {
                        var qr = qgBatch.Results.First(r => r.Sample == s).Result;
                        s.Status = qr.IsGreen
                            ? TrainingSampleStatus.Approved
                            : TrainingSampleStatus.New; // Yellow → Review Queue
                        existingSigs.Add(s.Signature);

                        // Live-Frame pro Sample (nicht nur pro Case)
                        var sampleFrame = !string.IsNullOrEmpty(s.FramePath) ? s.FramePath : previewFrame;
                        UpdateLivePreview(tc.CaseId, s.Code, $"{s.MeterStart:F2} – {s.MeterEnd:F2} m", sampleFrame);

                        // Ergebnis-Verlauf (Yellow = brauchbar mit Maengeln, nicht "nichts gefunden")
                        var level = qr.IsGreen ? MatchLevel.ExactMatch : MatchLevel.PartialMatch;
                        void AddResult()
                        {
                            SelfTrainingResults.Add(new SelfTrainingEntryResult
                            {
                                Index = SelfTrainingResults.Count + 1,
                                VsaCode = s.Code,
                                Meter = s.MeterStart,
                                Level = level,
                                Summary = qr.IsGreen ? s.Beschreibung : $"[Yellow] {string.Join(", ", qr.Issues)}"
                            });
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

                    Log($"  -> {newSamples.Count} Samples (QG: {qgBatch.Green}G/{qgBatch.Yellow}Y/{qgBatch.Red}R, {autoApproved} approved):");
                    foreach (var s in newSamples)
                        Log($"     {s.Code} @ {s.MeterStart:F2}m [{s.Status}] - {s.Beschreibung}");

                    // Approved Samples als Pending markieren (vor dem Speichern)
                    foreach (var s in newSamples.Where(s => s.Status == TrainingSampleStatus.Approved))
                        s.KbIndexState = KbIndexState.Pending;

                    // ══════════════════════════════════════════════════════════════════
                    // SOFORT SPEICHERN — nur QualityGate-akzeptierte Samples
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

                    // Fall als BatchImported markieren
                    tc.Status = TrainingCaseStatus.BatchImported;

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

            if (totalNew == 0 && casesToProcess.Count == 0 && caseSkipped > 0)
            {
                // Alle Haltungen komplett uebersprungen (Case-Level Skip)
                var diag = $"Alle {caseSkipped} Haltungen bereits verarbeitet — KB ist aktuell.";
                Log(diag);
                StatusText = diag;
            }
            else if (totalNew == 0 && casesToProcess.Count > 0)
            {
                var diag = $"0 neue Samples aus {casesToProcess.Count} Faellen.";
                if (caseSkipped > 0) diag += $" {caseSkipped} bereits verarbeitet.";
                if (errors > 0) diag += $" {errors} Fehler (letzter: {lastError}).";
                if (emptyProtocols > 0) diag += $" {emptyProtocols} ohne Eintraege.";
                if (duplicateOnlyCases > 0) diag += $" {duplicateOnlyCases} nur Duplikate.";
                if (missingProtocols > 0) diag += $" {missingProtocols} fehlende Protokolle.";
                if (unreadableProtocols > 0) diag += $" {unreadableProtocols} nicht lesbar.";
                Log(diag);
                StatusText = diag;
            }
            else
            {
                var finalStatus = $"Fertig! {totalNew} Samples gespeichert, {totalIndexed} in KB indexiert";
                if (caseSkipped > 0) finalStatus += $", {caseSkipped} uebersprungen";
                if (errors > 0) finalStatus += $", {errors} Fehler";
                if (!ollamaReachable) finalStatus += " (KB-Indexierung uebersprungen: Ollama offline)";
                Log(finalStatus);
                StatusText = finalStatus;
            }

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

    /// <summary>
    /// V4.3: Indexiert alle Approved-Samples nachtraeglich in die KB,
    /// die noch nicht indexiert sind (KbIndexState != Indexed).
    /// Faengt verlorene Approve-Aktionen auf wenn Ollama temporaer down war.
    /// </summary>
    [RelayCommand]
    private async Task ReindexPendingSamplesAsync()
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            StatusText = "Lade ausstehende Samples...";

            var allSamples = await TrainingSamplesStore.LoadAsync();
            var pending = allSamples
                .Where(s => s.Status == TrainingSampleStatus.Approved
                            && s.KbIndexState != KbIndexState.Indexed
                            && s.KbIndexState != KbIndexState.Deduplicated)
                .ToList();

            if (pending.Count == 0)
            {
                StatusText = "Keine ausstehenden Samples — alles bereits indexiert.";
                Log("Re-Index: Nichts zu tun, alle Approved-Samples sind bereits in der KB.");
                return;
            }

            Log($"Re-Index: {pending.Count} Approved-Samples werden indexiert...");
            StatusText = $"Re-Index laeuft: {pending.Count} Samples...";

            var indexed = await IncrementalKbUpdateAsync(pending, CancellationToken.None);

            foreach (var s in pending)
            {
                s.KbIndexState = indexed.Contains(s.SampleId)
                    ? KbIndexState.Indexed
                    : KbIndexState.Error;
            }
            await TrainingSamplesStore.MergeOrUpdateAsync(pending);

            Log($"Re-Index fertig: {indexed.Count} von {pending.Count} Samples indexiert.");
            StatusText = $"Re-Index fertig: {indexed.Count}/{pending.Count} Samples in KB.";

            await RefreshKbStatusAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"Re-Index Fehler: {ex.Message}";
            Log($"Re-Index FEHLER: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CheckKnowledgeBaseAsync()
    {
        if (IsBusy) return;

        try
        {
            IsBusy = true;
            StatusText = "Prüfe Knowledge Base...";

            var summary = await Task.Run(() =>
            {
                using var db = new KnowledgeBaseContext();
                var diag = new KnowledgeBaseDiagnosticsService(db);
                return diag.ReadSummary(12);
            });

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
        OnPropertyChanged(nameof(SelectedSampleCodeLabel));
    }

    /// <summary>
    /// VSA-Code mit Klartext fuer die Sample-Details-Anzeige.
    /// Beispiel: "BBCC — Ablagerung verfestigt".
    /// </summary>
    public string SelectedSampleCodeLabel
    {
        get
        {
            var code = SelectedSample?.Code;
            if (string.IsNullOrWhiteSpace(code)) return "";
            var label = Ai.VsaCodeResolver.LookupLabel(code);
            return string.IsNullOrWhiteSpace(label) ? code : $"{code} — {label}";
        }
    }

    /// <summary>
    /// Extrahiert einen einzelnen Preview-Frame aus dem Video (bei Sekunde 2).
    /// Wird für die Live-Vorschau genutzt, auch wenn keine neuen Samples generiert werden.
    /// </summary>
    private static async Task<string?> ExtractPreviewFrameAsync(TrainingCase tc, AiRuntimeConfig cfg, CancellationToken ct)
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

    private static MeterTimelineService CreateMeterTimelineService(AiRuntimeConfig cfg, int concurrency = 1)
    {
        if (!cfg.Enabled)
            return new MeterTimelineService(cfg);

        var ollamaClient = cfg.CreateOllamaClient();
        var vision = new OllamaVisionFindingsService(ollamaClient, cfg.VisionModel);
        var osd = new OsdMeterDetectionService(vision);
        return new MeterTimelineService(cfg, osd, concurrency);
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
                foreach (var c in found)
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

        // Rotation analog zu RotateGenCts (Audit D2.2 / April-Race-Fix):
        // Atomarer Tausch + delayed Dispose. Verhindert die Race zwischen
        // Dispose und Token-Registrierungen in noch laufenden Self-Training-Tasks.
        var ct = RotateSelfTrainingCts();

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
            var settings = await TrainingCenterSettingsStore.LoadAsync();
            var gpuConcurrency = Math.Clamp(settings.GpuConcurrency, 1, 12);
            var preExtractCpuParallelism = Math.Clamp(settings.CpuPreExtractParallelism, 1, 48);
            var caseParallelism = Math.Clamp(settings.CaseParallelism, 1, 8);
            var maxInFlightRequests = gpuConcurrency * caseParallelism;
            settings.GpuConcurrency = gpuConcurrency;
            settings.CpuPreExtractParallelism = preExtractCpuParallelism;
            settings.CaseParallelism = caseParallelism;

            // Services instanziieren (gleicher Pattern wie BatchImport)
            var cfg = AiRuntimeConfig.Load();
            Log($"Ollama: {cfg.OllamaBaseUri}, Modell: {cfg.VisionModel}");
            Log($"Parallelitaet: GPU={gpuConcurrency}, Faelle={caseParallelism}, PDF-CPU={preExtractCpuParallelism} (max. Requests ~{maxInFlightRequests})");
            if (int.TryParse(Environment.GetEnvironmentVariable("OLLAMA_NUM_PARALLEL"), out var ollamaSlots)
                && ollamaSlots > 0
                && ollamaSlots < gpuConcurrency)
            {
                Log($"Hinweis: OLLAMA_NUM_PARALLEL={ollamaSlots} < GPU-Parallelitaet={gpuConcurrency}. Dadurch bleibt Kapazitaet ungenutzt.");
            }

            var visionModel = cfg.VisionModel ?? "Qwen2.5-VL";
            _activeVisionModel = visionModel;
            var ollamaClient = cfg.CreateOllamaClient();
            var vision = new EnhancedVisionAnalysisService(ollamaClient, visionModel);
            try
            {
                await vision.EnableFewShotAsync(new Ai.Training.FewShotExampleStore(), ct);
                Log("Few-Shot aktiviert: gespeicherte Beispiele werden fuer Self-Training genutzt.");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log($"Few-Shot konnte nicht aktiviert werden: {ex.Message}");
            }
            var comparison = new SelfTrainingComparisonService();
            var technique = new TechniqueAssessmentService(ollamaClient, visionModel);
            var pdfExtractor = new PdfProtocolExtractor();

            // Multi-Modell-Pipeline (YOLO/DINO/SAM) wenn Sidecar verfuegbar
            Ai.Pipeline.SingleFrameMultiModelService? multiModel = null;
            try
            {
                var pipeCfg = Ai.PipelineConfig.Load();
                if (pipeCfg.MultiModelEnabled)
                {
                    var sidecarHttp = new System.Net.Http.HttpClient
                    {
                        BaseAddress = pipeCfg.SidecarUrl,
                        Timeout = TimeSpan.FromSeconds(pipeCfg.SidecarTimeoutSec)
                    };
                    var pipelineClient = new Ai.Pipeline.VisionPipelineClient(pipeCfg.SidecarUrl, sidecarHttp);
                    multiModel = new Ai.Pipeline.SingleFrameMultiModelService(
                        pipelineClient, pipeCfg.YoloConfidence, pipeCfg.DinoBoxThreshold, pipeCfg.DinoTextThreshold);
                }
            }
            catch { /* Sidecar nicht konfiguriert — nur Qwen */ }

            _selfTrainingOrchestrator = new SelfTrainingOrchestrator(
                vision, comparison, technique, pdfExtractor, settings, multiModel, _sampleQualityGate,
                reviewQueue: ReviewQueueServiceRef);

            // Progress-Callback verbindet Orchestrator → ViewModel-Visualisierungen
            var progress = new Progress<SelfTrainingStep>(OnSelfTrainingStep);

            // Alle Faelle mit Protokoll durchlaufen (Batch-Selbsttraining)
            var existingSamples = await TrainingSamplesStore.LoadAsync();
            var processedIds = existingSamples.Select(s => s.CaseId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var casesToTrain = Cases
                .Where(c => !string.IsNullOrEmpty(c.ProtocolPath))
                .Where(c => ForceRerunAll || !processedIds.Contains(c.CaseId))
                .ToList();

            if (casesToTrain.Count == 0)
            {
                // Fallback: Alle bereits verarbeitet → nur den ausgewaehlten Fall erneut
                casesToTrain = new List<TrainingCase> { SelectedCase };
            }

            Log($"Selbsttraining: {casesToTrain.Count} Faelle zu verarbeiten");
            ProgressMax = casesToTrain.Count;

            // PDF-Fotos fuer ALLE Faelle vorab extrahieren (CPU-parallel, blockiert GPU nicht)
            Log($"PDF-Fotos vorab extrahieren (CPU-Parallelitaet: {preExtractCpuParallelism})...");
            await Parallel.ForEachAsync(casesToTrain,
                new ParallelOptions { MaxDegreeOfParallelism = preExtractCpuParallelism, CancellationToken = ct },
                async (c, token) =>
                {
                    if (string.IsNullOrEmpty(c.ProtocolPath)) return;
                    var framesDir = Path.Combine(c.FolderPath, "self_training_frames");
                    if (Directory.Exists(framesDir) && Directory.GetFiles(framesDir, "*.png").Length > 0) return;
                    // PdfProtocolExtractor wird in RunAsync nochmal aufgerufen —
                    // aber die Frames sind dann schon auf Disk und muessen nicht nochmal extrahiert werden
                    try
                    {
                        var extractor = new PdfProtocolExtractor();
                        await extractor.ExtractAsync(c.ProtocolPath, framesDir, token);
                    }
                    catch { /* Fehler beim Vorextrahieren ignorieren — RunAsync versucht es nochmal */ }
                });
            Log("PDF-Fotos vorab extrahiert");

            int totalExact = 0, totalPartial = 0, totalMismatch = 0, totalNoFindings = 0, totalSamples = 0;
            int caseErrors = 0;
            int completedCases = 0;

            // Mehrere Faelle parallel: waehrend Fall A KB-Update macht (CPU+Disk),
            // analysieren andere Faelle bereits Frames (GPU).
            await Parallel.ForEachAsync(
                casesToTrain.Select((c, i) => (Case: c, Index: i)),
                new ParallelOptions { MaxDegreeOfParallelism = caseParallelism, CancellationToken = ct },
                async (item, token) =>
            {
                var (currentCase, ci) = item;
                var caseNum = Interlocked.Increment(ref completedCases);

                // UI-Updates via Dispatcher (Thread-Safety)
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    SelectedCase = currentCase;
                    ProgressValue = caseNum;
                    StatusText = $"[{caseNum}/{casesToTrain.Count}] {currentCase.CaseId}...";
                });
                Log($"--- [{ci + 1}/{casesToTrain.Count}] Selbsttraining: {currentCase.CaseId} ---");
                Log($"  Protokoll: {currentCase.ProtocolPath}");

                SelfTrainingResult result;
                try
                {
                    result = await _selfTrainingOrchestrator.RunAsync(currentCase, progress, token);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Log($"  FEHLER: {ex.Message}");
                    Interlocked.Increment(ref caseErrors);
                    return;
                }

                // Ergebnis loggen
                Log($"  Eintraege: {result.TotalEntries} | CodeHit: {result.CodeHits} | "
                  + $"ExactMatch: {result.ExactMatches} | Partial: {result.PartialMatches} | "
                  + $"Mismatch: {result.Mismatches} | NoFindings: {result.NoFindings} | "
                  + $"Samples: {result.SamplesGenerated} | Dauer: {result.Duration:mm\\:ss}");
                if (result.OverallTechnique is { } tech)
                    Log($"  Technik: {tech.OverallGrade} (Licht={tech.LightingQuality}, Schaerfe={tech.SharpnessQuality})");

                Interlocked.Add(ref totalExact, result.ExactMatches);
                Interlocked.Add(ref totalPartial, result.PartialMatches);
                Interlocked.Add(ref totalMismatch, result.Mismatches);
                Interlocked.Add(ref totalNoFindings, result.NoFindings);
                Interlocked.Add(ref totalSamples, result.SamplesGenerated);

                // Fall als verarbeitet markieren
                currentCase.Status = TrainingCaseStatus.SelfTrained;

                // Match-Rate-Verlauf persistieren (HistoryStore hat eigenen Lock)
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
                        (double)result.NoFindings / matchTotal,
                        (double)result.CodeHits / matchTotal));
                }

                // Inkrementelles KB-Update — mit Lock serialisiert (SQLite)
                if (result.SamplesGenerated > 0)
                {
                    await _kbUpdateLock.WaitAsync(token);
                    try
                    {
                        var allSamples = await TrainingSamplesStore.LoadAsync();
                        var newApproved = allSamples
                            .Where(s => s.CaseId == result.CaseId
                                && s.Status == TrainingSampleStatus.Approved)
                            .ToList();

                        if (newApproved.Count > 0)
                        {
                            foreach (var s in newApproved.Where(s => s.KbIndexState is KbIndexState.None or KbIndexState.Error))
                                s.KbIndexState = KbIndexState.Pending;
                            await TrainingSamplesStore.MergeOrUpdateAsync(newApproved);

                            Log($"  {newApproved.Count} Samples → KB-Update...");
                            try
                            {
                                var indexed = await IncrementalKbUpdateAsync(newApproved, token);
                                var indexedSet = indexed.ToHashSet();
                                foreach (var s in newApproved)
                                    s.KbIndexState = indexedSet.Contains(s.SampleId)
                                        ? KbIndexState.Indexed
                                        : (s.KbIndexState == KbIndexState.Pending ? KbIndexState.Error : s.KbIndexState);
                                await TrainingSamplesStore.MergeOrUpdateAsync(newApproved);
                            }
                            catch (Exception ex) when (ex is not OperationCanceledException)
                            {
                                Log($"  KB-Update Fehler: {ex.Message}");
                            }
                        }
                    }
                    finally
                    {
                        _kbUpdateLock.Release();
                    }
                }
            });

            ForceRerunAll = false; // Nach Durchlauf zuruecksetzen
            var totalCodeHits = totalExact + totalPartial;
            Log($"=== Selbsttraining abgeschlossen ===");
            Log($"  Faelle: {casesToTrain.Count} ({caseErrors} Fehler)");
            Log($"  CodeHit: {totalCodeHits} (Code korrekt) | ExactMatch: {totalExact} (Code+Meter+Clock) | Partial: {totalPartial} | Mismatch: {totalMismatch} | NoFindings: {totalNoFindings}");
            Log($"  Samples erzeugt: {totalSamples}");

            StatusText = $"Fertig! {casesToTrain.Count} Faelle, {totalCodeHits} CodeHit, {totalExact} ExactMatch, {totalSamples} Samples";

            // Hinweis fuer Few-Shot-Export (B2)
            if (totalExact > 0)
            {
                Log($"{totalExact} ExactMatch-Samples erzeugt. Fuer Few-Shot-Export: Tab 'Samples' → 'Export Approved'");
            }

            // Review Queue befuellen mit PartialMatch/Mismatch (C1)
            if (ReviewQueueServiceRef is not null && (totalPartial > 0 || totalMismatch > 0))
            {
                var allSamplesForReview = await TrainingSamplesStore.LoadAsync();
                var reviewCandidates = allSamplesForReview
                    .Where(s => casesToTrain.Any(c => c.CaseId == s.CaseId)
                        && s.Status == TrainingSampleStatus.New
                        && (s.MatchLevel is MatchLevelNames.PartialMatch or MatchLevelNames.Mismatch))
                    .ToList();

                foreach (var s in reviewCandidates)
                {
                    ReviewQueueServiceRef.EnqueueFromSelfTraining(
                        s.CaseId, s.Code, s.KiCode ?? s.Code,
                        s.MeterStart, s.FramePath, s.MatchLevel!,
                        s.SampleId);
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
            var ollamaConfig = OllamaConfig.Load();
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
