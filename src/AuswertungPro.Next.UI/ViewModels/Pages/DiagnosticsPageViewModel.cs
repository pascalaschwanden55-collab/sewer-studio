using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using AuswertungPro.Next.Application.Ai.SelfImproving;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class DiagnosticsPageViewModel : ObservableObject
{
    private readonly IDialogService _dialogs = App.Resolve<IDialogService>();

    [ObservableProperty] private string _logTail = "";

    // ── Wartungs-Tools (Audit-Tab, 2026-05-07) ──

    [ObservableProperty] private string _mirrorHealthStatus = "Noch nicht geprueft";
    [ObservableProperty] private string _mirrorHealthMessage = "";
    [ObservableProperty] private Brush _mirrorHealthBrush =
        new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)); // Slate-400 (Neutral)

    [ObservableProperty] private string _frameCleanupResult = "";
    [ObservableProperty] private bool _isFrameCleanupRunning;

    [ObservableProperty] private string _versionsPruneResult = "";
    [ObservableProperty] private bool _isVersionsPruneRunning;

    // ── KB-Dashboard (Roadmap P1.4, 2026-05-08) ──

    [ObservableProperty] private string _dashboardStatus = "Noch nicht geladen";
    [ObservableProperty] private string _totalSamplesText = "—";
    [ObservableProperty] private string _totalValidationsText = "—";
    [ObservableProperty] private string _overallAccuracyText = "—";
    [ObservableProperty] private Brush _overallAccuracyBrush =
        new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
    [ObservableProperty] private string _reviewQueueLengthText = "—";

    [ObservableProperty] private GridLength _greenStarWidth = new(1, GridUnitType.Star);
    [ObservableProperty] private GridLength _yellowStarWidth = new(1, GridUnitType.Star);
    [ObservableProperty] private GridLength _redStarWidth = new(1, GridUnitType.Star);
    [ObservableProperty] private GridLength _unknownStarWidth = new(1, GridUnitType.Star);
    [ObservableProperty] private string _qualityDistributionText = "Quality-Verteilung: noch nicht geladen.";

    public ObservableCollection<CodeStatViewModel> TopProblemCodes { get; } = [];
    public ObservableCollection<CodeStatViewModel> UnderRepresentedCodes { get; } = [];

    [ObservableProperty] private int _reviewExportCount = 100;
    [ObservableProperty] private string _reviewExportResult = "";
    [ObservableProperty] private bool _isReviewExportRunning;

    public IRelayCommand RefreshCommand { get; }
    public IRelayCommand CheckMirrorHealthCommand { get; }
    public IAsyncRelayCommand FrameCleanupDryRunCommand { get; }
    public IAsyncRelayCommand FrameCleanupExecuteCommand { get; }
    public IAsyncRelayCommand VersionsPruneCommand { get; }
    public IAsyncRelayCommand RefreshDashboardCommand { get; }
    public IAsyncRelayCommand ExportReviewListCommand { get; }

    public DiagnosticsPageViewModel()
    {
        RefreshCommand = new RelayCommand(Refresh);
        CheckMirrorHealthCommand = new RelayCommand(CheckMirrorHealth);
        FrameCleanupDryRunCommand = new AsyncRelayCommand(() => RunFrameCleanupAsync(dryRun: true));
        FrameCleanupExecuteCommand = new AsyncRelayCommand(() => RunFrameCleanupAsync(dryRun: false));
        VersionsPruneCommand = new AsyncRelayCommand(RunVersionsPruneAsync);
        RefreshDashboardCommand = new AsyncRelayCommand(RefreshDashboardAsync);
        ExportReviewListCommand = new AsyncRelayCommand(ExportReviewListAsync);
        Refresh();
    }

    private void Refresh()
    {
        try
        {
            var logDir = Path.Combine(AppSettings.AppDataDir, "logs");
            var logPath = Path.Combine(logDir, $"app-{DateTime.Now:yyyyMMdd}.log");
            if (!File.Exists(logPath))
            {
                LogTail = "Noch keine Log-Datei vorhanden.";
                return;
            }

            LogTail = string.Join(Environment.NewLine, File.ReadLines(logPath).TakeLast(200));
        }
        catch (Exception ex)
        {
            LogTail = ex.Message;
        }
    }

    /// <summary>
    /// Audit Punkt 3.8: Brain-Mirror Health-Check.
    /// Liefert Green/Yellow/Red mit konkreter Diagnose.
    /// </summary>
    private void CheckMirrorHealth()
    {
        var svc = KnowledgeMirrorService.Current;
        if (svc is null)
        {
            MirrorHealthStatus = "—";
            MirrorHealthMessage = "Brain-Mirror nicht aktiv (Singleton fehlt)";
            MirrorHealthBrush = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
            return;
        }

        var health = svc.GetHealth();
        MirrorHealthStatus = health.Status.ToString().ToUpperInvariant();
        var size = health.BrainDbBytes > 0 ? $", {health.BrainDbBytes / 1_000_000.0:F1} MB" : "";
        var age = health.BrainDbAgeHours.HasValue ? $", {health.BrainDbAgeHours:F1}h alt" : "";
        MirrorHealthMessage = $"{health.Message}{size}{age}";
        MirrorHealthBrush = health.Status switch
        {
            KnowledgeMirrorStatus.Green => new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)),  // Green-500
            KnowledgeMirrorStatus.Yellow => new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)), // Amber-500
            KnowledgeMirrorStatus.Red => new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)),    // Red-500
            _ => new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)),
        };
    }

    /// <summary>
    /// Audit Top-10 Punkt 7: frames/-Cleanup.
    /// Identifiziert verwaiste PNGs (zu keinem Sample mehr gehoeren),
    /// schont Frames juenger als 7 Tage.
    /// </summary>
    private async Task RunFrameCleanupAsync(bool dryRun)
    {
        if (IsFrameCleanupRunning) return;

        // Bestaetigung wenn nicht DryRun
        if (!dryRun)
        {
            var confirm = _dialogs.ShowMessage(
                "Verwaiste Frame-PNGs werden geloescht. Vorher DryRun ausfuehren empfohlen.\n\nFortfahren?",
                "Frames bereinigen",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;
        }

        IsFrameCleanupRunning = true;
        FrameCleanupResult = dryRun ? "DryRun laeuft..." : "Bereinigung laeuft...";

        try
        {
            var svc = new FrameStoreCleanupService { DryRun = dryRun, MinimumAgeDays = 7 };
            var result = await svc.RunAsync();

            var bytes = result.OrphanBytes / 1_000_000.0;
            var deletedBytes = result.DeletedBytes / 1_000_000.0;
            var summary = $"Frames-Verzeichnis: {result.FramesDir}\n" +
                          $"Total-Files: {result.TotalFiles}\n" +
                          $"Aktive Sample-IDs: {result.ActiveSampleIds}\n" +
                          $"Verwaist: {result.OrphanFiles} ({bytes:F1} MB)\n" +
                          (dryRun
                              ? "→ DryRun: nichts geloescht."
                              : $"→ Geloescht: {result.DeletedFiles} ({deletedBytes:F1} MB)");

            if (result.Errors.Count > 0)
                summary += $"\nFehler: {result.Errors.Count}\n{string.Join("\n", result.Errors.Take(5))}";

            FrameCleanupResult = summary;
        }
        catch (Exception ex)
        {
            FrameCleanupResult = $"Fehler: {ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            IsFrameCleanupRunning = false;
        }
    }

    /// <summary>
    /// Audit Top-10 Punkt 8: Versions-Pruning.
    /// Behaelt letzte 20 Versionen + alle juenger als 30 Tage.
    /// </summary>
    private async Task RunVersionsPruneAsync()
    {
        if (IsVersionsPruneRunning) return;

        var confirm = _dialogs.ShowMessage(
            "Alte KB-Versions-Snapshots werden geloescht.\n" +
            "Behalten: letzte 20 Versionen + alle juenger als 30 Tage.\n" +
            "Aktuelle Version bleibt immer erhalten.\n\nFortfahren?",
            "Versionen bereinigen",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        IsVersionsPruneRunning = true;
        VersionsPruneResult = "Bereinigung laeuft...";

        try
        {
            using var db = new AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase.KnowledgeBaseContext();
            var emb = new AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase.EmbeddingService(
                new System.Net.Http.HttpClient(),
                AuswertungPro.Next.Application.Ai.Ollama.OllamaConfigProvider.Load());
            var mgr = new AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase.KnowledgeBaseManager(db, emb);

            var deleted = await Task.Run(() => mgr.PruneOldVersions(keepLastN: 20, keepDaysMin: 30));
            VersionsPruneResult = deleted == 0
                ? "Nichts zu bereinigen — alle Versionen sind juenger als 30 Tage oder unter den letzten 20."
                : $"{deleted} alte Versionen bereinigt.";
        }
        catch (Exception ex)
        {
            VersionsPruneResult = $"Fehler: {ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            IsVersionsPruneRunning = false;
        }
    }

    /// <summary>
    /// Roadmap P1.4: Laedt KB-Dashboard-Daten (Quality-Verteilung, Top-Problemcodes,
    /// Long-Tail-Codes, Gesamt-Accuracy). Async damit die UI nicht einfriert,
    /// auch wenn der KB-DB-Zugriff bei vollen DBs ein paar Sekunden dauern kann.
    /// </summary>
    private async Task RefreshDashboardAsync()
    {
        DashboardStatus = "Lade...";
        try
        {
            var snap = await Task.Run(() =>
            {
                using var ctx = new KnowledgeBaseContext();
                var diag = new KnowledgeBaseDiagnosticsService(ctx);
                Func<int>? queueLen = TryReadReviewQueueLength;
                var dashboard = new KbDashboardService(diag, queueLen);
                return dashboard.BuildSnapshot(topProblemCodes: 12, topUnderRepresented: 12);
            });

            ApplySnapshot(snap);
            DashboardStatus = $"Stand {snap.GeneratedUtc.ToLocalTime():HH:mm:ss}";
        }
        catch (Exception ex)
        {
            DashboardStatus = $"Fehler: {ex.Message}";
        }
    }

    private void ApplySnapshot(KbDashboardSnapshot snap)
    {
        TotalSamplesText = snap.TotalSamples.ToString("N0");
        TotalValidationsText = snap.TotalValidations.ToString("N0");
        if (snap.OverallAccuracy is null)
        {
            OverallAccuracyText = "—";
            OverallAccuracyBrush = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
        }
        else
        {
            var pct = snap.OverallAccuracy.Value;
            OverallAccuracyText = pct.ToString("P1");
            OverallAccuracyBrush = pct switch
            {
                >= 0.75 => new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)), // Green
                >= 0.55 => new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)), // Amber
                _ => new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)),       // Red
            };
        }
        ReviewQueueLengthText = snap.ReviewQueueLength.ToString("N0");

        // GridLength-Sterne: Anteil als float verwenden. Bei 0 wird die Spalte
        // nicht ganz unsichtbar (sonst hat die Bar Loecher), 0.0001 reicht.
        var q = snap.Quality;
        GreenStarWidth = MakeStar(q.GreenRatio);
        YellowStarWidth = MakeStar(q.YellowRatio);
        RedStarWidth = MakeStar(q.RedRatio);
        UnknownStarWidth = MakeStar(q.UnknownRatio);
        QualityDistributionText =
            $"Green: {q.Green:N0} ({q.GreenRatio:P0})    " +
            $"Yellow: {q.Yellow:N0} ({q.YellowRatio:P0})    " +
            $"Red: {q.Red:N0} ({q.RedRatio:P0})    " +
            $"Unbekannt: {q.Unknown:N0} ({q.UnknownRatio:P0})";

        TopProblemCodes.Clear();
        foreach (var c in snap.TopProblemCodes)
            TopProblemCodes.Add(CodeStatViewModel.From(c));
        UnderRepresentedCodes.Clear();
        foreach (var c in snap.UnderRepresentedCodes)
            UnderRepresentedCodes.Add(CodeStatViewModel.From(c));
    }

    private static GridLength MakeStar(double ratio)
        => new(System.Math.Max(0.0001, ratio), GridUnitType.Star);

    /// <summary>
    /// Liest die Laenge der persistierten Review-Queue ohne harte Abhaengigkeit
    /// auf den ReviewQueueService — nur die JSON-Datei zaehlen ist robust auch
    /// wenn der Service noch nicht initialisiert wurde.
    /// </summary>
    private static int TryReadReviewQueueLength()
    {
        try
        {
            return ReviewQueueStore.LoadAsync().GetAwaiter().GetResult().Count;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Roadmap P1.1: Erzeugt eine CSV mit den N unsichersten Samples
    /// (60% Uncertainty + 40% Diversity nach ActiveLearningSelector).
    /// CSV wird Excel-kompatibel mit BOM + Semikolon-Trenner geschrieben.
    /// </summary>
    private async Task ExportReviewListAsync()
    {
        if (IsReviewExportRunning) return;
        if (ReviewExportCount <= 0)
        {
            ReviewExportResult = "Anzahl muss > 0 sein.";
            return;
        }

        IsReviewExportRunning = true;
        ReviewExportResult = "Lade Review-Queue...";
        try
        {
            var result = await Task.Run<(string Path, int Total, int Picked)>(async () =>
            {
                var queue = await ReviewQueueStore.LoadAsync().ConfigureAwait(false);
                var selector = new ActiveLearningSelector();
                var freqs = ReadCodeFrequencies();
                var picks = selector.Select(queue, ReviewExportCount, freqs);

                var dir = Path.Combine(KnowledgeRoot.GetRoot(), "review_lists");
                Directory.CreateDirectory(dir);
                var name = $"review_{DateTime.Now:yyyyMMdd_HHmmss}_{picks.Count}items.csv";
                var fullPath = Path.Combine(dir, name);
                WriteReviewCsv(fullPath, picks);
                return (fullPath, queue.Count, picks.Count);
            });

            ReviewExportResult = result.Picked == 0
                ? "Review-Queue ist leer — keine Samples zu reviewen."
                : $"{result.Picked} von {result.Total} Samples exportiert.\n{result.Path}";
        }
        catch (Exception ex)
        {
            ReviewExportResult = $"Fehler: {ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            IsReviewExportRunning = false;
        }
    }

    private static IReadOnlyDictionary<string, int> ReadCodeFrequencies()
    {
        try
        {
            using var ctx = new KnowledgeBaseContext();
            var diag = new KnowledgeBaseDiagnosticsService(ctx);
            return diag.ReadAllCodeCounts()
                .ToDictionary(c => c.VsaCode, c => c.Count, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, int>();
        }
    }

    private static void WriteReviewCsv(string path, IReadOnlyList<ReviewQueueItem> picks)
    {
        // Excel-kompatibles UTF-8 mit BOM + Semikolon (deutsche Locale).
        using var sw = new StreamWriter(path, append: false,
            new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        sw.WriteLine("Sample-Id;Vorgeschlagener Code;Meter;Match-Level;Priority;Eingereiht;Frame-Pfad;Korrektur (manuell)");
        foreach (var p in picks)
        {
            var sampleId = Csv(p.SelfTrainingSampleId ?? p.Id);
            var code = Csv(p.SelfTrainingSuggestedCode ?? p.SelfTrainingVsaCode ?? "");
            var meter = p.SelfTrainingMeter?.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) ?? "";
            var match = Csv(p.SelfTrainingMatchLevel ?? "");
            var prio = p.Priority.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
            var enq = p.EnqueuedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            var frame = Csv(p.SelfTrainingFramePath ?? "");
            sw.WriteLine($"{sampleId};{code};{meter};{match};{prio};{enq};{frame};");
        }
    }

    private static string Csv(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        if (s.IndexOfAny([';', '"', '\n', '\r']) >= 0)
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }
}

/// <summary>
/// View-Wrapper fuer <see cref="CodeStat"/> mit formatierten Strings fuer
/// die DataGrid-Anzeige. Reine Pass-Through-Klasse (keine Notify-Logik noetig).
/// </summary>
public sealed class CodeStatViewModel
{
    public string VsaCode { get; init; } = "";
    public int SampleCount { get; init; }
    public int ValidationTotal { get; init; }
    public int ValidationCorrect { get; init; }
    public string AccuracyText { get; init; } = "—";
    public string ProblemScoreText { get; init; } = "—";

    public static CodeStatViewModel From(CodeStat s) => new()
    {
        VsaCode = s.VsaCode,
        SampleCount = s.SampleCount,
        ValidationTotal = s.ValidationTotal,
        ValidationCorrect = s.ValidationCorrect,
        AccuracyText = s.Accuracy is null ? "—" : s.Accuracy.Value.ToString("P0"),
        ProblemScoreText = s.ProblemScore.ToString("F2"),
    };
}
