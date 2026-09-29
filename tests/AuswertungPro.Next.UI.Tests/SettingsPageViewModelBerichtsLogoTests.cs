using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Infrastructure.Maintenance;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.Settings;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Optikanalyse 28.09.2026, Aufgabe 15: die neue Gruppe "Berichte" in Einstellungen &gt;
/// Allgemein liest/schreibt <see cref="AppSettings.BerichtsLogoPfad"/> und speichert sofort
/// (Muster wie <c>SettingsPageViewModelFruehereAnsichtenTests</c>). "Auswählen…" und
/// "Zurücksetzen" sind die einzigen Schreibwege; die Vorschau liest live über die injizierte
/// <see cref="IBerichtsMarke"/>.
/// </summary>
public sealed class SettingsPageViewModelBerichtsLogoTests
{
    [Fact]
    public void Standardmaessig_ist_kein_eigenes_Logo_gesetzt_und_die_Anzeige_nennt_den_Standard()
    {
        var settings = new AppSettings();
        using var vm = NeueViewModel(settings, out _, out _);

        Assert.Null(vm.BerichtsLogoPfad);
        Assert.Equal("Standardlogo wird verwendet", vm.BerichtsLogoAnzeige);
    }

    [Fact]
    public void Ein_bereits_gespeicherter_Pfad_erscheint_beim_Aufbau_und_loest_keine_zusaetzliche_Speicherung_aus()
    {
        var settings = new AppSettings { BerichtsLogoPfad = @"C:\eigenes\logo.png" };
        using var vm = NeueViewModel(settings, out var store, out _);

        Assert.Equal(@"C:\eigenes\logo.png", vm.BerichtsLogoPfad);
        Assert.Equal(@"C:\eigenes\logo.png", vm.BerichtsLogoAnzeige);
        // "Direkt ins Feld" beim Aufbau (Muster wie ProtocolPhotosPerPage): der Aufbau selbst
        // loest fuer BerichtsLogoPfad kein zusaetzliches Save aus. Der Konstruktor speichert
        // wegen anderer Sofort-Speicherungen (ReduceMotion, Fruehere Ansichten) trotzdem, daher
        // der Vergleich gegen einen zweiten, sonst gleichen Aufbau ohne eigenen Logo-Pfad.
        using var ohneEigenenPfad = NeueViewModel(new AppSettings(), out var storeOhne, out _);
        Assert.Equal(storeOhne.Calls, store.Calls);
    }

    [Fact]
    public void Auswaehlen_setzt_den_von_der_Dialogfassade_gelieferten_Pfad_und_speichert_sofort()
    {
        var settings = new AppSettings();
        using var vm = NeueViewModel(
            settings,
            out var store,
            out _,
            dialogs: new BerichtsLogoDialogFake(@"D:\Bilder\eigenes.png"));
        var nachAufbau = store.Calls;

        vm.BrowseBerichtsLogoCommand.Execute(null);

        Assert.Equal(@"D:\Bilder\eigenes.png", vm.BerichtsLogoPfad);
        Assert.Equal(@"D:\Bilder\eigenes.png", settings.BerichtsLogoPfad);
        Assert.Equal(nachAufbau + 1, store.Calls);
        Assert.Contains("BerichtsLogoPfad", store.Json);
    }

    [Fact]
    public void Abbruch_im_Dateidialog_aendert_nichts()
    {
        var settings = new AppSettings { BerichtsLogoPfad = @"C:\alt.png" };
        using var vm = NeueViewModel(
            settings,
            out var store,
            out _,
            dialogs: new BerichtsLogoDialogFake(gewaehlterPfad: null));
        var nachAufbau = store.Calls;

        vm.BrowseBerichtsLogoCommand.Execute(null);

        Assert.Equal(@"C:\alt.png", vm.BerichtsLogoPfad);
        Assert.Equal(nachAufbau, store.Calls);
    }

    [Fact]
    public void Zuruecksetzen_leert_den_Pfad_und_speichert_sofort()
    {
        var settings = new AppSettings { BerichtsLogoPfad = @"C:\eigenes\logo.png" };
        using var vm = NeueViewModel(settings, out var store, out _);
        var nachAufbau = store.Calls;

        vm.ResetBerichtsLogoCommand.Execute(null);

        Assert.Null(vm.BerichtsLogoPfad);
        Assert.Null(settings.BerichtsLogoPfad);
        Assert.Equal("Standardlogo wird verwendet", vm.BerichtsLogoAnzeige);
        Assert.Equal(nachAufbau + 1, store.Calls);
    }

    [Fact]
    public void Die_Vorschau_liest_ueber_die_injizierte_IBerichtsMarke_und_meldet_sich_bei_Aenderung()
    {
        // Eigener Pfad gesetzt, damit "Zuruecksetzen" wirklich etwas aendert (sonst loest der
        // generierte Setter bei gleichem Wert kein OnChanged/PropertyChanged aus).
        var settings = new AppSettings { BerichtsLogoPfad = @"C:\eigenes\logo.png" };
        var marke = new StubBerichtsMarke(@"C:\wirksam\logo.png");
        using var vm = NeueViewModel(settings, out _, out _, berichtsMarke: marke);

        var propertyChanged = new List<string?>();
        vm.PropertyChanged += (_, e) => propertyChanged.Add(e.PropertyName);

        Assert.Equal(@"C:\wirksam\logo.png", vm.BerichtsLogoVorschauPfad);
        Assert.True(vm.HatBerichtsLogoVorschau);

        vm.ResetBerichtsLogoCommand.Execute(null);

        Assert.Contains(nameof(SettingsPageViewModel.BerichtsLogoVorschauPfad), propertyChanged);
        Assert.Contains(nameof(SettingsPageViewModel.BerichtsLogoAnzeige), propertyChanged);
    }

    [Fact]
    public void Ohne_verfuegbares_Logo_zeigt_die_Vorschau_nichts()
    {
        var settings = new AppSettings();
        using var vm = NeueViewModel(settings, out _, out _, berichtsMarke: new StubBerichtsMarke(null));

        Assert.Null(vm.BerichtsLogoVorschauPfad);
        Assert.False(vm.HatBerichtsLogoVorschau);
    }

    private static SettingsPageViewModel NeueViewModel(
        AppSettings settings,
        out RecordingSettingsFileStore store,
        out DialogFake dialogFake,
        IDialogService? dialogs = null,
        IBerichtsMarke? berichtsMarke = null)
    {
        store = new RecordingSettingsFileStore();
        settings.UseSettingsFileStore(store);
        dialogFake = new DialogFake();
        return new SettingsPageViewModel(
            settings,
            new DiagnosticsOptions(),
            dialogs ?? dialogFake,
            new FullBackupFake(),
            new ToastService(),
            new FullBackupOperationState(),
            new ProgramCleanupService(),
            new CodexArtifactCleanupService(),
            new KnowledgeBackupTransferService(),
            katasterXtfPaths: null,
            folderOpen: null,
            programRootLocator: null,
            berichtsMarke: berichtsMarke);
    }

    private sealed class StubBerichtsMarke : IBerichtsMarke
    {
        public StubBerichtsMarke(string? logoPfad) => LogoPfad = logoPfad;
        public string? LogoPfad { get; }
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

    private sealed class BerichtsLogoDialogFake : IDialogService
    {
        private readonly string? _gewaehlterPfad;

        public BerichtsLogoDialogFake(string? gewaehlterPfad) => _gewaehlterPfad = gewaehlterPfad;

        public string? OpenFile(string title, string filter, string? initialDirectory = null) => _gewaehlterPfad;
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
