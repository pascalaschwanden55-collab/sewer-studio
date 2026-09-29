using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Infrastructure.Maintenance;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.Settings;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Optikanalyse 28.09.2026, Aufgabe 8: "Alte Haltungsansicht", "Alte Schachtansicht" und
/// "Klassische Übersicht" sind aus ihren Menüs (Haltungen/Schächte "Weitere Aktionen -&gt;
/// Ansicht", MainWindow-Menü "Ansicht") entfernt und stehen jetzt als drei Häkchen in
/// Einstellungen -&gt; Allgemein -&gt; Frühere Ansichten. Jedes Häkchen muss denselben
/// AppSettings-Schalter setzen, den früher der jeweilige Menüpunkt setzte, und sofort speichern
/// (Muster wie <c>ReduceMotion</c>/<c>HintergrundEngine</c>). Der Konstruktor selbst löst dabei
/// bereits mehrere Sofort-Speicherungen aus (ReduceMotion, HintergrundEngine,
/// IncludeProjectVideosInFullBackup, die drei neuen Häkchen) - Tests vergleichen deshalb gegen
/// die Zählerstände direkt nach dem Aufbau, nicht gegen absolute Werte.
/// </summary>
public sealed class SettingsPageViewModelFruehereAnsichtenTests
{
    [Fact]
    public void Standardmaessig_sind_alle_drei_Haekchen_unangehakt()
    {
        var settings = new AppSettings();
        using var vm = NeueViewModel(settings, out _);

        Assert.False(vm.AlteHaltungsansicht);
        Assert.False(vm.AlteSchachtansicht);
        Assert.False(vm.KlassischeProjektuebersicht);
    }

    [Fact]
    public void Ein_bereits_ausgeschaltetes_Nova_Layout_zeigt_das_Haekchen_beim_Aufbau_angehakt()
    {
        var settings = new AppSettings
        {
            ShowHaltungenNovaLayout = false,
            ShowSchaechteNovaLayout = false,
            ShowUebersichtNovaLayout = false
        };
        using var vm = NeueViewModel(settings, out _);

        Assert.True(vm.AlteHaltungsansicht);
        Assert.True(vm.AlteSchachtansicht);
        Assert.True(vm.KlassischeProjektuebersicht);
    }

    [Fact]
    public void Haekchen_Alte_Haltungsansicht_schaltet_ShowHaltungenNovaLayout_um_und_speichert_sofort()
    {
        var settings = new AppSettings();
        using var vm = NeueViewModel(settings, out var store);
        var nachAufbau = store.Calls;

        vm.AlteHaltungsansicht = true;

        Assert.False(settings.ShowHaltungenNovaLayout);
        Assert.Equal(nachAufbau + 1, store.Calls);
        Assert.Contains("\"ShowHaltungenNovaLayout\": false", store.Json);

        vm.AlteHaltungsansicht = false;

        Assert.True(settings.ShowHaltungenNovaLayout);
        Assert.Equal(nachAufbau + 2, store.Calls);
    }

    [Fact]
    public void Haekchen_Alte_Schachtansicht_schaltet_ShowSchaechteNovaLayout_um_und_speichert_sofort()
    {
        var settings = new AppSettings();
        using var vm = NeueViewModel(settings, out var store);
        var nachAufbau = store.Calls;

        vm.AlteSchachtansicht = true;

        Assert.False(settings.ShowSchaechteNovaLayout);
        Assert.Equal(nachAufbau + 1, store.Calls);
    }

    [Fact]
    public void Haekchen_Klassische_Projektuebersicht_schaltet_ShowUebersichtNovaLayout_um_und_speichert_sofort()
    {
        var settings = new AppSettings();
        using var vm = NeueViewModel(settings, out var store);
        var nachAufbau = store.Calls;

        vm.KlassischeProjektuebersicht = true;

        Assert.False(settings.ShowUebersichtNovaLayout);
        Assert.Equal(nachAufbau + 1, store.Calls);
    }

    [Fact]
    public void Die_drei_Haekchen_sind_voneinander_unabhaengig()
    {
        var settings = new AppSettings();
        using var vm = NeueViewModel(settings, out var store);
        var nachAufbau = store.Calls;

        vm.AlteHaltungsansicht = true;

        Assert.False(settings.ShowHaltungenNovaLayout);
        Assert.True(settings.ShowSchaechteNovaLayout);
        Assert.True(settings.ShowUebersichtNovaLayout);
        Assert.False(vm.AlteSchachtansicht);
        Assert.False(vm.KlassischeProjektuebersicht);
        Assert.Equal(nachAufbau + 1, store.Calls);
    }

    private static SettingsPageViewModel NeueViewModel(AppSettings settings, out RecordingSettingsFileStore store)
    {
        store = new RecordingSettingsFileStore();
        settings.UseSettingsFileStore(store);
        return new SettingsPageViewModel(
            settings,
            new DiagnosticsOptions(),
            new DialogFake(),
            new FullBackupFake(),
            new ToastService(),
            new FullBackupOperationState(),
            new ProgramCleanupService());
    }

    private sealed class RecordingSettingsFileStore : ISettingsFileStore
    {
        public int Calls { get; private set; }
        public string Json { get; private set; } = string.Empty;

        public void Persist(
            string json,
            string settingsPath,
            string appDataDirectory,
            bool enableRestorePoints)
        {
            Calls++;
            Json = json;
        }
    }

    private sealed class FullBackupFake : IFullBackupService
    {
        public Task<FullBackupSizeReport> AnalyzeAsync(
            IProgress<string>? progress = null,
            CancellationToken ct = default)
            => Task.FromResult(new FullBackupSizeReport(Array.Empty<ComponentSize>(), 0, 0));

        public Task<FullBackupResult> RunAsync(
            string targetFolder,
            IProgress<FullBackupProgress>? progress = null,
            CancellationToken ct = default)
            => Task.FromResult(new FullBackupResult(
                true,
                null,
                targetFolder,
                0,
                0,
                0,
                0,
                Array.Empty<string>(),
                TimeSpan.Zero));
    }

    private sealed class DialogFake : IDialogService
    {
        public string? OpenFile(string title, string filter, string? initialDirectory = null) => null;
        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null) => null;
        public string[] OpenFiles(string title, string filter) => Array.Empty<string>();
        public string? SelectFolder(string title, string? initialPath = null) => null;
        public void Info(string message, string title = "Hinweis") { }
        public void Warn(string message, string title = "Warnung") { }
        public void Error(string message, string title = "Fehler") { }
        public bool Confirm(string message, string title = "Bestätigung") => false;
        public bool ConfirmWarn(string message, string title = "Bestätigung", bool defaultNo = true) => false;
        public DialogConfirm ConfirmCancel(string message, string title = "Bestätigung") => DialogConfirm.Cancel;
    }
}
