using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class DiagnosticsPageViewModel : ObservableObject
{
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

    public IRelayCommand RefreshCommand { get; }
    public IRelayCommand CheckMirrorHealthCommand { get; }
    public IAsyncRelayCommand FrameCleanupDryRunCommand { get; }
    public IAsyncRelayCommand FrameCleanupExecuteCommand { get; }
    public IAsyncRelayCommand VersionsPruneCommand { get; }

    public DiagnosticsPageViewModel()
    {
        RefreshCommand = new RelayCommand(Refresh);
        CheckMirrorHealthCommand = new RelayCommand(CheckMirrorHealth);
        FrameCleanupDryRunCommand = new AsyncRelayCommand(() => RunFrameCleanupAsync(dryRun: true));
        FrameCleanupExecuteCommand = new AsyncRelayCommand(() => RunFrameCleanupAsync(dryRun: false));
        VersionsPruneCommand = new AsyncRelayCommand(RunVersionsPruneAsync);
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
            var confirm = MessageBox.Show(
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

        var confirm = MessageBox.Show(
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
}
