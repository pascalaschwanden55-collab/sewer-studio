using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Domain.Models;
using System;
using System.IO;
using System.Linq;
using System.Windows.Data;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using AuswertungPro.Next.Infrastructure.Projects;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.UI.Player;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Settings;

namespace AuswertungPro.Next.UI.ViewModels;

/// <summary>Die drei Zustaende der Shell: Startbildschirm, Draft (neues Projekt) und Arbeitsbereich.</summary>
public enum ShellMode
{
    Launcher,
    Draft,
    Workspace
}

public static class ShellNavigationPolicy
{
    public static bool RequiresProject(string? title)
        => !CanOpenWithoutProject(title);

    public static bool CanOpenWithoutProject(string? title)
        => title is "Projekt" or "Export" or "Einstellungen";
}

public sealed partial class ShellViewModel : ObservableObject, IDisposable, IPlayerShellProjectContext
{
    private readonly ServiceProvider _sp;
    private bool _disposed;

    [ObservableProperty] private string _title = "SewerStudio";
    [ObservableProperty] private string _subtitle = "Bereit";

    /// <summary>System resource monitor (CPU, RAM, GPU) — polls every 2s.</summary>
    public SystemMonitorService Monitor { get; }

    /// <summary>AP-50: Modale Lade-/Speicheranzeige. Gebunden an das BusyOverlay im MainWindow.</summary>
    public BusyState Busy { get; } = new();

    /// <summary>Laufender PC-Ausfallschutz, sichtbar in der Haupt-Statusleiste.</summary>
    public FullBackupOperationState FullBackupOperation => _sp.FullBackupOperation;

    public Project Project => _project;
    private Project _project = new();

    /// <summary>S1: True bei ungespeicherten Aenderungen. Global sichtbar via Fenstertitel-Marker
    /// und Uebersichts-Badge. Wird ueber RefreshTitleAndDirty() benachrichtigt.</summary>
    public bool IsDirty => _project.Dirty;

    public IReadOnlyList<NavItem> NavItems { get; }
    [ObservableProperty] private NavItem? _selectedNavItem;
    [ObservableProperty] private object? _currentPage;

    [ObservableProperty] private ShellMode _currentMode = ShellMode.Launcher;

    /// <summary>Menue/Nav/Shortcuts nur im Workspace sichtbar.</summary>
    public bool IsMenuVisible => CurrentMode == ShellMode.Workspace;

    partial void OnCurrentModeChanged(ShellMode value)
    {
        OnPropertyChanged(nameof(IsMenuVisible));
        SaveCommand?.NotifyCanExecuteChanged();
    }

    public IRelayCommand SaveCommand { get; }
    public IRelayCommand NewProjectCommand { get; }
    public IRelayCommand OpenProjectCommand { get; }
    public IRelayCommand SaveAsProjectCommand { get; }
    public IRelayCommand OpenPriceCatalogCommand { get; }
    public IRelayCommand OpenTemplateEditorCommand { get; }
    public IRelayCommand ToggleFocusModeCommand { get; }
    public IRelayCommand SwitchProjectCommand { get; }
    [ObservableProperty] private bool _isProjectReady;

    /// <summary>Echter Persistenz-Zustand: Das aktuelle Projekt hat eine Datei auf der Platte.
    /// Anders als IsProjectReady wird dies beim Wechsel in den Launcher NICHT zurueckgesetzt.</summary>
    [ObservableProperty] private bool _hasPersistedProject;

    [ObservableProperty] private bool _isFocusMode;
    [ObservableProperty] private bool _isAiWorking;
    [ObservableProperty] private string _aiStatusLabel = "";
    [ObservableProperty] private string _aiLoadedModels = "";
    [ObservableProperty] private bool _isAiRuntimeVisible;
    [ObservableProperty] private string _aiRuntimeTitle = "";
    [ObservableProperty] private string _aiRuntimeStatusLabel = "";
    [ObservableProperty] private string _aiRuntimeLoadedModels = "";

    public bool IsAiIndicatorVisible => IsAiWorking || IsAiRuntimeVisible;
    public string AiIndicatorTitle => IsAiWorking ? "KI AKTIV" : AiRuntimeTitle;
    public string AiDisplayStatusLabel => IsAiWorking ? AiStatusLabel : AiRuntimeStatusLabel;
    public string AiDisplayLoadedModels => IsAiWorking ? AiLoadedModels : AiRuntimeLoadedModels;

    // Lock-Objekt fuer thread-sichere ObservableCollection-Zugriffe
    private readonly object _collectionLock = new();

    /// <summary>S10: Sync-Objekt fuer Project.Data/SchaechteData (EnableCollectionSynchronization).
    /// Hintergrund-Threads, die diese Collections mutieren (z.B. VSA-Lauf, Importe), MUESSEN
    /// diesen Lock halten, damit der UI-Thread nicht waehrend einer Mutation enumeriert.</summary>
    public object CollectionLock => _collectionLock;

    public ShellViewModel(ServiceProvider services, SystemMonitorService? monitor = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        _sp = services;
        Monitor = monitor ?? new SystemMonitorService();
        EnableCollectionSync(_project);

        NavItems = new List<NavItem>
        {
            new("\uE9D2", "Uebersicht", () => new Pages.OverviewPageViewModel(
                this,
                settings: _sp.Settings,
                dashboardRefresh: _sp.DashboardRefresh,
                dialogs: _sp.Dialogs,
                projects: _sp.Projects), canOpenWithoutProject: true),
            new("\uE8B7", "Projekt", () => new Pages.ProjectPageViewModel(this, dropdownOptions: _sp.DropdownOptions), canOpenWithoutProject: true),
            new("\uE8FD", "Haltungen", () => new Pages.DataPageViewModel(this, _sp)),
            new("\uE7F4", "Schaechte", () => new Pages.SchaechtePageViewModel(
                this,
                settings: _sp.Settings,
                dialogs: _sp.Dialogs,
                schachtProtocolImport: _sp.SchachtProtocolImport,
                schachtStammdatenErgaenzung: _sp.SchachtStammdatenErgaenzung,
                schachtMassnahmenKatalog: _sp.SchachtMassnahmenKatalog)),
            // Segoe MDL2: Import = Download, Export = Upload
            new("\uE896", "Import", () => new Pages.ImportPageViewModel(this, _sp)),
            new("\uE898", "Export", () => new Pages.ExportPageViewModel(
                this,
                settings: _sp.Settings,
                dialogs: _sp.Dialogs,
                excelExport: _sp.ExcelExport,
                toasts: _sp.Toasts,
                costFieldSync: _sp.CostFieldSync), canOpenWithoutProject: true),
            new("\uE707", "Karte", () => new AuswertungPro.Next.UI.Views.Pages.KartePage
            {
                DataContext = new Pages.KarteViewModel(
                    this,
                    settings: _sp.Settings,
                    networkFeatures: _sp.NetworkFeatures,
                    playVideo: KarteVideoLauncher.Create(_sp))
            }),
            new("\uE7BA", "Medienkonflikte", () => new Pages.MediaConflictsPageViewModel(
                getProject: () => Project,
                getProjectFolder: GetProjectFolder,
                getLastVideoSourceFolder: () => _sp.Settings.LastVideoSourceFolder,
                saveVideoSourceFolder: folder =>
                {
                    _sp.Settings.LastVideoSourceFolder = folder;
                    _sp.Settings.LastVideoFolder = folder;
                    _sp.Settings.Save();
                },
                dialogs: _sp.Dialogs,
                service: _sp.MediaConflictCenter,
                setStatus: SetStatus,
                playVideo: MediaConflictVideoLauncher.Create(_sp))),
            new("\uE749", "Druckcenter", () => new Pages.BuilderPageViewModel(
                this,
                settings: _sp.Settings,
                dialogs: _sp.Dialogs,
                protocolPdfExporter: _sp.ProtocolPdfExporter,
                costFieldSync: _sp.CostFieldSync)),
            new("\uECA5", "Sanierungs-Matrix", () => new Pages.SanierungsMatrixPageViewModel(
                this,
                settings: _sp.Settings,
                dialogs: _sp.Dialogs,
                costFieldSync: _sp.CostFieldSync,
                dashboardRefresh: _sp.DashboardRefresh,
                holding: null,
                singleHoldingMode: false)),
            new("\uE7F4", "Schacht-Matrix", () => new Pages.SchachtSanierungsMatrixPageViewModel(
                getProject: () => Project,
                getProjectPath: () => _sp.Settings.LastProjectPath,
                dialogs: _sp.Dialogs,
                dashboardRefresh: _sp.DashboardRefresh)),
            // Segoe MDL2 E8AA = "ViewAll": zwei Auswertungen nebeneinander (Mensch vs. Schatten-KI)
            new("\uE8AA", "Schattenauswertung", () => new Pages.SchattenauswertungPageViewModel(
                getProject: () => Project,
                store: _sp.SchattenStore,
                createService: _sp.CreateSchattenAuswertung,
                getProjectPath: () => _sp.Settings.LastProjectPath)),
            new("\uE128", "VSA", () => new Pages.VsaPageViewModel(
                getProject: () => Project,
                collectionLock: CollectionLock,
                getProjectPath: () => _sp.Settings.LastProjectPath,
                getExplicitPdfToTextPath: () => _sp.Diagnostics.ExplicitPdfToTextPath,
                xtfImport: _sp.XtfImport,
                pdfImport: _sp.PdfImport,
                vsaEvaluation: _sp.Vsa,
                measureRecommendation: _sp.MeasureRecommendation,
                setStatus: SetStatus,
                createImportRestorePoint: TryCreateImportRestorePoint,
                refreshTitleAndDirty: RefreshTitleAndDirty)),
            new("\uE9CE", "Diagnose", () => new Pages.DiagnosticsPageViewModel(
                _sp.LogTailReader,
                _sp.DiagnosticsPackages,
                _sp.Dialogs)),
            new("\uE713", "Einstellungen", () => new Pages.SettingsPageViewModel(
                settings: _sp.Settings,
                diagnostics: _sp.Diagnostics,
                dialogs: _sp.Dialogs,
                fullBackup: _sp.FullBackup,
                toasts: _sp.Toasts,
                fullBackupOperation: _sp.FullBackupOperation,
                programCleanup: _sp.ProgramCleanup), canOpenWithoutProject: true)
        };
        RefreshNavigationAvailability();

        SaveCommand = new RelayCommand(SaveProject, () => CurrentMode == ShellMode.Workspace);
        NewProjectCommand = new RelayCommand(StartNewProjectDraft);
        SwitchProjectCommand = new RelayCommand(SwitchProject);
        OpenProjectCommand = new AsyncRelayCommand(OpenProjectWithDialogAsync);
        SaveAsProjectCommand = new RelayCommand(SaveProjectAs);
        OpenPriceCatalogCommand = new RelayCommand(OpenPriceCatalog);
        OpenTemplateEditorCommand = new RelayCommand(OpenTemplateEditor);
        ToggleFocusModeCommand = new RelayCommand(() => IsFocusMode = !IsFocusMode);

        EnterLauncher();
        Monitor.Start();

        AiActivityTracker.ActiveChanged += OnAiActivityChanged;
        ApplyAiRuntimeStatus(AiRuntimeStatusTracker.Current);
        AiRuntimeStatusTracker.Changed += ApplyAiRuntimeStatus;

        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(SelectedNavItem) || SelectedNavItem is null)
                return;
            if (_suppressLeaveGuard)
                return;

            // Seiten mit ungespeichertem Zustand duerfen den Wechsel stoppen (Audit W2).
            if (!ShellLeaveGuard.CanLeave(CurrentPage))
            {
                _suppressLeaveGuard = true;
                SelectedNavItem = _navItemBeforeChange;
                _suppressLeaveGuard = false;
                return;
            }

            _navItemBeforeChange = SelectedNavItem;
            SetCurrentPage(SelectedNavItem.CreatePage());
        };
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        AiActivityTracker.ActiveChanged -= OnAiActivityChanged;
        AiRuntimeStatusTracker.Changed -= ApplyAiRuntimeStatus;
        Monitor.Dispose();
        SetCurrentPage(null);
        GC.SuppressFinalize(this);
    }

    partial void OnIsAiWorkingChanged(bool value) => NotifyAiIndicatorChanged();
    partial void OnAiStatusLabelChanged(string value) => NotifyAiIndicatorChanged();
    partial void OnAiLoadedModelsChanged(string value) => NotifyAiIndicatorChanged();
    partial void OnIsAiRuntimeVisibleChanged(bool value) => NotifyAiIndicatorChanged();
    partial void OnAiRuntimeTitleChanged(string value) => NotifyAiIndicatorChanged();
    partial void OnAiRuntimeStatusLabelChanged(string value) => NotifyAiIndicatorChanged();
    partial void OnAiRuntimeLoadedModelsChanged(string value) => NotifyAiIndicatorChanged();

    private void ApplyAiRuntimeStatus(AiRuntimeStatus status)
    {
        if (_disposed)
            return;

        IsAiRuntimeVisible = status.IsVisible;
        AiRuntimeTitle = status.Title;
        AiRuntimeStatusLabel = status.StatusText;
        AiRuntimeLoadedModels = status.ModelText;
    }

    private void OnAiActivityChanged(bool active, string label)
    {
        if (_disposed)
            return;

        IsAiWorking = active;
        AiStatusLabel = active ? label : "";
        if (active)
        {
            try
            {
                var cfg = new AppSettingsAiSettingsProvider()
                    .Load()
                    .ToRuntimeSettings();
                AiLoadedModels = cfg.VisionModel ?? OllamaConfig.DefaultVisionModel;
            }
            catch { AiLoadedModels = ""; }
        }
        else
        {
            AiLoadedModels = "";
        }
    }

    private void NotifyAiIndicatorChanged()
    {
        OnPropertyChanged(nameof(IsAiIndicatorVisible));
        OnPropertyChanged(nameof(AiIndicatorTitle));
        OnPropertyChanged(nameof(AiDisplayStatusLabel));
        OnPropertyChanged(nameof(AiDisplayLoadedModels));
    }

    // Letzter aktiver Nav-Eintrag (null im Einzelhaltungsmodus) — Ruecksprungziel,
    // wenn eine Seite den Wechsel per IConfirmLeave ablehnt.
    private NavItem? _navItemBeforeChange;
    private bool _suppressLeaveGuard;

    private void SetCurrentPage(object? nextPage)
    {
        var previousPage = CurrentPage;
        if (ReferenceEquals(previousPage, nextPage))
            return;

        CurrentPage = nextPage;
        ShellPageLifecycle.DisposeIfReplaced(previousPage, nextPage);
    }

    partial void OnIsProjectReadyChanged(bool value)
    {
        RefreshNavigationAvailability();
        RefreshTitleAndDirty();
    }

    /// <summary>S1: Fenstertitel mit Projektname und Ungespeichert-Marker aktualisieren
    /// und IsDirty fuer gebundene Anzeigen (Uebersichts-Badge) benachrichtigen.</summary>
    public void RefreshTitleAndDirty()
    {
        var name = IsProjectReady && !string.IsNullOrWhiteSpace(Project.Name)
            ? $"{Project.Name} – SewerStudio"
            : "SewerStudio";
        Title = Project.Dirty ? $"● {name}" : name;
        OnPropertyChanged(nameof(IsDirty));
    }

    private void RefreshNavigationAvailability()
    {
        foreach (var item in NavItems)
            item.UpdateAvailability(IsProjectReady);
    }

    public void SetStatus(string text) => Subtitle = text;

    public void ReplaceProject(Project p)
    {
        _project = p;
        EnableCollectionSync(p);
        OnPropertyChanged(nameof(Project));
        SetStatus($"Projekt: {p.Name}");
        RefreshTitleAndDirty();

        // Kartennetz im Hintergrund vorladen -> die Karte ist beim ersten Oeffnen sofort da.
        Mapping.KarteNetzVorladen.ImHintergrund(_sp, p);
    }

    /// <summary>
    /// Aktiviert thread-sichere Zugriffe auf die ObservableCollections des Projekts,
    /// damit Import-Services aus Task.Run heraus Haltungen/Schaechte hinzufuegen koennen.
    /// </summary>
    private void EnableCollectionSync(Project p)
    {
        BindingOperations.EnableCollectionSynchronization(p.Data, _collectionLock);
        BindingOperations.EnableCollectionSynchronization(p.SchaechteData, _collectionLock);
    }

    public void NavigateTo(string title)
    {
        var target = NavItems.FirstOrDefault(x => string.Equals(x.Title, title, StringComparison.OrdinalIgnoreCase));
        if (target is not null)
            SelectedNavItem = target;
    }

    public void NavigateToDataPage(DataPageStartFilter startFilter)
    {
        var target = NavItems.FirstOrDefault(x => string.Equals(x.Title, "Haltungen", StringComparison.OrdinalIgnoreCase));
        if (target is null)
            return;

        if (!ShellLeaveGuard.CanLeave(CurrentPage))
            return;

        CurrentMode = ShellMode.Workspace;
        _suppressLeaveGuard = true;
        SelectedNavItem = target;
        _suppressLeaveGuard = false;
        _navItemBeforeChange = target;
        SetCurrentPage(new Pages.DataPageViewModel(this, _sp, startFilter));
    }

    public void NavigateToSanierungsMatrix(string? holding, bool singleHoldingMode = false, HaltungRecord? targetRecord = null)
    {
        var target = NavItems.FirstOrDefault(x => string.Equals(x.Title, "Sanierungs-Matrix", StringComparison.OrdinalIgnoreCase));
        if (target is null)
            return;

        // Direkter Seitenwechsel am Nav-Handler vorbei -> Leave-Guard hier ebenfalls (Audit W2).
        if (!ShellLeaveGuard.CanLeave(CurrentPage))
            return;

        if (singleHoldingMode)
        {
            _suppressLeaveGuard = true;
            SelectedNavItem = null;
            _suppressLeaveGuard = false;
            _navItemBeforeChange = null;
            SetCurrentPage(new Pages.SanierungsMatrixPageViewModel(
                this,
                settings: _sp.Settings,
                dialogs: _sp.Dialogs,
                costFieldSync: _sp.CostFieldSync,
                dashboardRefresh: _sp.DashboardRefresh,
                holding: holding,
                singleHoldingMode: true,
                targetRecord: targetRecord));
            return;
        }

        SelectedNavItem = target;

        if (CurrentPage is Pages.SanierungsMatrixPageViewModel matrix)
            matrix.SelectHolding(holding);
    }

    /// <summary>Zurueck zum Start-Bildschirm (Projektauswahl).</summary>
    public void EnterLauncher()
    {
        _suppressLeaveGuard = true;
        SelectedNavItem = null;
        _suppressLeaveGuard = false;
        _navItemBeforeChange = null;
        CurrentMode = ShellMode.Launcher;
        ResetProjectReady();
        SetCurrentPage(new Pages.OverviewPageViewModel(
            this,
            settings: _sp.Settings,
            dashboardRefresh: _sp.DashboardRefresh,
            dialogs: _sp.Dialogs,
            projects: _sp.Projects));
    }

    /// <summary>„Neues Projekt": leeres Projekt + Infoblatt im Draft-Modus.</summary>
    public void StartNewProjectDraft()
    {
        if (!ConfirmDiscardUnsavedChanges())
            return;

        ReplaceProject(AuswertungPro.Next.Application.Projects.NewProjectDraftFactory.Create());
        ResetProjectReady();
        HasPersistedProject = false;
        _suppressLeaveGuard = true;
        SelectedNavItem = null;
        _suppressLeaveGuard = false;
        CurrentMode = ShellMode.Draft;
        SetCurrentPage(new Pages.ProjectPageViewModel(this, dropdownOptions: _sp.DropdownOptions));
    }

    /// <summary>Wechselt in den Arbeitsbereich und navigiert auf die Landeseite.</summary>
    public void EnterWorkspaceOn(string navTitle)
    {
        CurrentMode = ShellMode.Workspace;
        NavigateTo(navTitle);
    }

    private void SwitchProject()
    {
        if (!ConfirmDiscardUnsavedChanges())
            return;
        EnterLauncher();
    }

    /// <summary>Legt aus dem Draft-Infoblatt Projektordner + projekt.json an.</summary>
    public bool CreateProjectFromDraft()
    {
        var name = Project.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            SetStatus("Bitte einen Projektnamen eingeben.");
            return false;
        }

        var baseDir = _sp.Settings.ProjectsRootDirectory;
        if (string.IsNullOrWhiteSpace(baseDir))
        {
            baseDir = _sp.Dialogs.SelectFolder("Projekte-Verzeichnis waehlen", @"D:\Projekt");
            if (string.IsNullOrWhiteSpace(baseDir))
                return false;

            _sp.Settings.ProjectsRootDirectory = baseDir;
            _sp.Settings.Save();
        }

        var plan = NewProjectFolderPlanner.Plan(baseDir, name, Directory.Exists);

        try
        {
            Directory.CreateDirectory(plan.FolderPath);
            // Feste Projekt-Struktur anlegen (Importdateien/Haltungen_Verteilt/Schächte_Verteilt/Fotos/Projektdateien/...).
            AuswertungPro.Next.Infrastructure.Import.ProjectStructure.EnsureCreated(plan.FolderPath);
        }
        catch (Exception ex)
        {
            SetStatus(
                "Projektordner konnte nicht angelegt werden: "
                + UserError.DescribeAndReport(ex, "Projektordner anlegen"));
            return false;
        }

        // projekt.json kommt nach <Projekt>\Projektdateien\ (+ Root-Pointer fuer Auffindbarkeit beim Oeffnen).
        var projektJsonPath = ProjectFileLocator.TargetPath(plan.FolderPath);
        var res = _sp.Projects.Save(Project, projektJsonPath);
        if (!res.Ok)
        {
            SetStatus($"Fehler: {res.ErrorMessage}");
            return false;
        }
        ProjectFileLocator.WriteRootPointer(plan.FolderPath, projektJsonPath);

        _sp.Settings.AddRecentProject(projektJsonPath);
        _sp.Settings.Save();
        MarkProjectReady();
        HasPersistedProject = true;
        SetStatus($"Neues Projekt: {name}");
        EnterWorkspaceOn("Import");
        return true;
    }

    /// <summary>
    /// Gibt den Projekt-Root zurueck. Liegt die projekt.json in &lt;Projekt&gt;\Projektdateien\, ist der Root
    /// dessen Eltern-Ordner; bei Alt-Projekten (projekt.json im Root) das Verzeichnis selbst.
    /// So loesen relative Medienpfade weiterhin korrekt gegen den Projekt-Root auf.
    /// </summary>
    public string? GetProjectFolder()
        => ProjectFileLocator.ProjectRootFromFile(_sp.Settings.LastProjectPath);

    /// <summary>Synchrones Oeffnen (Drag&amp;Drop, Tests). UI blockiert waehrend des Ladens.</summary>
    public bool TryOpenProject(string path)
    {
        if (!TryBeginOpen(path))
            return false;

        var (res, recovery) = LoadOrRecover(path);
        return ApplyLoadOutcome(path, res, recovery);
    }

    /// <summary>
    /// AP-50: Async Oeffnen fuer die UI-Commands — der schwere Lade-/Rettungsteil laeuft im
    /// Hintergrund mit modaler Ladeanzeige, das Fenster friert nicht mehr ein. Uebernahme
    /// (Dialoge, ReplaceProject) laeuft nach dem await wieder auf dem UI-Thread.
    /// </summary>
    public async Task<bool> TryOpenProjectAsync(string path)
    {
        if (!TryBeginOpen(path))
            return false;

        (Result<Project> res, ProjectRecoveryResult? recovery) outcome;
        using (Busy.Enter("Projekt wird geladen …"))
        {
            outcome = await Task.Run(() => LoadOrRecover(path));
        }

        return ApplyLoadOutcome(path, outcome.res, outcome.recovery);
    }

    /// <summary>Vorab-Pruefungen + Dirty-Guard (schnell, UI-Thread). false = abbrechen.</summary>
    private bool TryBeginOpen(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            SetStatus("Datei nicht gefunden");
            return false;
        }

        return ConfirmDiscardUnsavedChanges();
    }

    /// <summary>Reines Laden inkl. AP-01-Rettung — hintergrundtauglich, kein UI-Zugriff.</summary>
    private (Result<Project> res, ProjectRecoveryResult? recovery) LoadOrRecover(string path)
    {
        var res = _sp.Projects.Load(path);
        if (res.Ok && res.Value is not null)
            return (res, null);

        // AP-01: Beschaedigte projekt.json aus .bak/Restore-Point retten (nur Daten, Dialog folgt spaeter).
        return (res, ProjectRecovery.TryRecover(path, _sp.Projects));
    }

    /// <summary>Uebernahme des Ladeergebnisses (UI-Thread): Dialoge, Merkliste, ReplaceProject.</summary>
    private bool ApplyLoadOutcome(string path, Result<Project> res, ProjectRecoveryResult? recovery)
    {
        Project loaded;
        if (res.Ok && res.Value is not null)
        {
            loaded = res.Value;
        }
        else if (recovery is { Recovered: true, Project: not null })
        {
            _sp.Dialogs.Warn(
                "Das Projekt war beschaedigt und wurde aus einer Sicherungskopie wiederhergestellt.\n\n" +
                $"Wiederhergestellt aus: {recovery.RecoveredFromPath}\n" +
                (recovery.QuarantinedPath is null
                    ? string.Empty
                    : $"Beschaedigte Datei gesichert als: {recovery.QuarantinedPath}\n") +
                "\nBitte pruefe das Projekt und speichere es.",
                "Projekt wiederhergestellt");

            loaded = recovery.Project;
            loaded.Dirty = true; // erzwingt Neuspeicherung der guten Version an den Originalpfad
        }
        else
        {
            _sp.Dialogs.Error(
                "Das Projekt konnte nicht geoeffnet werden, und es wurde keine gueltige Sicherungskopie gefunden.\n\n" +
                $"Datei: {path}\n" +
                $"Fehler: {res.ErrorMessage}\n\n" +
                "Die Originaldatei wurde NICHT veraendert. Bitte pruefe eine Datensicherung.",
                "Projekt beschaedigt");
            SetStatus($"Fehler: {res.ErrorMessage}");
            return false;
        }

        // Jedes erfolgreiche Oeffnen pflegt die Merkliste (setzt auch LastProjectPath) —
        // egal ob Dialog, Drag&Drop oder "Letztes Projekt fortsetzen". Sonst bleiben
        // Projekte fuer die Projektuebersicht unsichtbar.
        _sp.Settings.AddRecentProject(path);
        _sp.Settings.Save();
        MarkProjectReady();
        HasPersistedProject = true;

        ReplaceProject(loaded);
        if (Project.Dirty && !TrySaveProject())
        {
            SetStatus($"Geladen mit ungespeicherter Reparatur: {Path.GetFileName(path)}");
            return true;
        }

        SetStatus($"Geladen: {Path.GetFileName(path)}");
        return true;
    }

    public bool TryOpenProjectWithDialog()
    {
        var path = _sp.Dialogs.OpenFile("Projekt öffnen", "Projekt (*.json)|*.json");
        if (path is null)
            return false;
        return TryOpenProject(path);
    }

    /// <summary>AP-50: Async-Variante fuer die Menue-/Command-Nutzung mit Ladeanzeige.</summary>
    public async Task<bool> TryOpenProjectWithDialogAsync()
    {
        var path = _sp.Dialogs.OpenFile("Projekt öffnen", "Projekt (*.json)|*.json");
        if (path is null)
            return false;
        return await TryOpenProjectAsync(path);
    }

    /// <summary>
    /// Fragt bei ungespeicherten Aenderungen nach (Speichern/Verwerfen/Abbrechen).
    /// Gibt false zurueck, wenn der Vorgang abgebrochen werden soll.
    /// </summary>
    public bool ConfirmDiscardUnsavedChanges()
    {
        // Erst die aktive Seite fragen — die Sanierungs-Matrix haelt ihren Kosten-Stand
        // ausserhalb von Project.Dirty (costs.json), siehe Audit K1/W2.
        if (!ShellLeaveGuard.CanLeave(CurrentPage))
            return false;

        if (Project is null || !Project.Dirty)
            return true;

        var answer = _sp.Dialogs.ConfirmCancel(
            "Das aktuelle Projekt hat ungespeicherte Aenderungen.\n\n" +
            "Vor dem Fortfahren speichern?",
            "Ungespeicherte Aenderungen");

        return answer switch
        {
            DialogConfirm.Cancel => false,
            DialogConfirm.Yes => TrySaveProject() && !Project.Dirty, // nur fort, wenn Speichern klappte
            _ => true, // No = verwerfen
        };
    }

    private void SaveProject()
        => TrySaveProject();

    public bool TrySaveProject()
    {
        // Save nutzt den letzten Pfad NUR, wenn das aktuelle Projekt wirklich von dort
        // stammt (HasPersistedProject). Sonst zeigt LastProjectPath noch auf das zuvor
        // geoeffnete Projekt und "Speichern" wuerde dessen Datei still ueberschreiben.
        var path = NormalizeProjectPath(_sp.Settings.LastProjectPath);
        bool isNewPath = false;
        if (string.IsNullOrWhiteSpace(path) || !HasPersistedProject)
        {
            var defaultName = MakeSafeFileName(Project.Name);
            path = _sp.Dialogs.SaveFile("Projekt speichern", "Projekt (*.json)|*.json", ".json", defaultName);
            if (path is null)
            {
                SetStatus("Speichern abgebrochen");
                return false;
            }
            isNewPath = true;
        }

        EnsureProjectDirectory(path);
        if (_sp.Settings.EnableRestorePoints)
            TryCreateProjectRestorePoint(path);

        var res = _sp.Projects.Save(Project, path);
        if (!res.Ok)
        {
            ShowProjectSaveError(path, res.ErrorMessage);
            SetStatus($"Fehler: {res.ErrorMessage}");
            return false;
        }

        // Merkliste/LastProjectPath erst NACH erfolgreichem Schreiben setzen (Audit P0-5b):
        // bei einem neuen Pfad wuerde ein Schreibfehler sonst LastProjectPath auf eine nie
        // erzeugte Datei zeigen lassen.
        if (isNewPath)
        {
            _sp.Settings.AddRecentProject(NormalizeProjectPath(path)); // setzt auch LastProjectPath
            _sp.Settings.Save();
        }
        IsProjectReady = true;
        HasPersistedProject = true;
        RefreshTitleAndDirty(); // Save setzt Project.Dirty=false -> Marker entfernen
        SetStatus("Gespeichert");
        _sp.Toasts.Success("Projekt gespeichert");
        return true;
    }

    public bool TrySaveProjectAs()
    {
        var defaultName = MakeSafeFileName(Project.Name);
        var path = _sp.Dialogs.SaveFile("Projekt speichern unter", "Projekt (*.json)|*.json", ".json", defaultName);
        if (path is null)
        {
            SetStatus("Speichern abgebrochen");
            return false;
        }

        path = NormalizeProjectPath(path);

        // Transaktionale Reihenfolge (Audit P0-5b): ZUERST tatsaechlich speichern. Merkliste,
        // LastProjectPath und "bereit"-Status erst NACH erfolgreichem Schreiben setzen — sonst
        // zeigt LastProjectPath bei einem Schreibfehler auf eine Datei, die es nie gab.
        EnsureProjectDirectory(path);
        if (_sp.Settings.EnableRestorePoints)
            TryCreateProjectRestorePoint(path);

        var res = _sp.Projects.Save(Project, path);
        if (!res.Ok)
        {
            ShowProjectSaveError(path, res.ErrorMessage);
            SetStatus($"Fehler: {res.ErrorMessage}");
            return false;
        }

        _sp.Settings.AddRecentProject(path); // Merkliste pflegen (setzt auch LastProjectPath)
        _sp.Settings.Save();
        MarkProjectReady();
        HasPersistedProject = true;
        RefreshTitleAndDirty(); // Save setzt Project.Dirty=false -> Marker entfernen
        SetStatus($"Gespeichert: {Path.GetFileName(path)}");
        _sp.Toasts.Success($"Gespeichert: {Path.GetFileName(path)}");
        return true;
    }

    private void ShowProjectSaveError(string path, string? error)
    {
        _sp.Dialogs.Error(
            "Das Projekt konnte nicht gespeichert werden. Die vorhandene Projektdatei wurde nicht geloescht.\n\n" +
            $"Ziel: {path}\n" +
            $"Fehler: {error}\n\n" +
            "Bitte pruefe freien Speicherplatz, Schreibschutz und Zugriffsrechte. " +
            "Versuche danach 'Speichern unter' in einem anderen Ordner.",
            "Projekt nicht gespeichert");
    }

    private async Task OpenProjectWithDialogAsync()
    {
        if (await TryOpenProjectWithDialogAsync())
            EnterWorkspaceOn("Uebersicht");
    }

    private void SaveProjectAs()
        => TrySaveProjectAs();

    public void MarkProjectReady()
        => IsProjectReady = true;

    public void ResetProjectReady()
        => IsProjectReady = false;

    public void MarkProjectDirty(HaltungRecord? record = null)
    {
        if (record is not null)
            record.ModifiedAtUtc = System.DateTime.UtcNow;

        Project.ModifiedAtUtc = System.DateTime.UtcNow;
        Project.Dirty = true;
        RefreshTitleAndDirty();
    }

    public void TryCreateImportRestorePoint(string importLabel)
    {
        if (!_sp.Settings.EnableRestorePoints)
            return;

        var path = NormalizeProjectPath(_sp.Settings.LastProjectPath);
        if (string.IsNullOrWhiteSpace(path) || !HasPersistedProject)
            return;

        var result = TryCreateProjectRestorePoint(path);
        SetStatus($"{importLabel}: {result.Message}");
    }

    private static string MakeSafeFileName(string? name)
    {
        var baseName = string.IsNullOrWhiteSpace(name) ? "Projekt" : name.Trim();
        foreach (var ch in Path.GetInvalidFileNameChars())
            baseName = baseName.Replace(ch, '_');
        return string.IsNullOrWhiteSpace(baseName) ? "Projekt" : baseName;
    }

    private static string NormalizeProjectPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        var trimmed = path.Trim();
        if (!trimmed.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            trimmed += ".json";
        return trimmed;
    }

    private static void EnsureProjectDirectory(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);
    }

    private static ProjectRestorePointResult TryCreateProjectRestorePoint(string projectPath) =>
        ProjectRestorePointService.TryCreateForProjectFile(projectPath);

    private void OpenPriceCatalog()
    {
        // EIN Preis-Katalog: derselbe, den Kostenrechner und Sanierungs-Matrix nutzen
        // (cost_catalog.json über CostCatalogStore). Der alte PriceCatalogEditor wird
        // bewusst nicht mehr geöffnet, damit es nur einen anwendbaren Katalog gibt.
        var dialog = new Dialogs.CostCatalogEditorDialog(_sp.Settings.LastProjectPath);
        dialog.ShowDialog();
    }

    private void OpenTemplateEditor()
    {
        var vm = new Windows.MeasureTemplateEditorViewModel(_sp.Settings.LastProjectPath, _sp.Dialogs);
        var window = new Views.Windows.MeasureTemplateEditorWindow
        {
            DataContext = vm
        };
        window.ShowDialog();
    }

    public sealed partial class NavItem : ObservableObject
    {
        private bool _isAvailable = true;

        public NavItem(string icon, string title, Func<object> createPage, bool? canOpenWithoutProject = null)
        {
            Icon = icon;
            Title = title;
            CreatePage = createPage;
            CanOpenWithoutProject = canOpenWithoutProject ?? ShellNavigationPolicy.CanOpenWithoutProject(title);
        }

        public string Icon { get; }

        public string Title { get; }

        public string ToolTipDescription => Title switch
        {
            "Uebersicht" => "Projekt-Cockpit mit Zustands-, Kosten- und Fortschrittsauswertung.",
            "Projekt" => "Projektstammdaten, Speicherort und Bearbeitungsdaten pflegen.",
            "Haltungen" => "Haltungen pruefen, filtern, Videos und Protokolle oeffnen.",
            "Schaechte" => "Schachtdaten anzeigen, kontrollieren und zugehoerige Protokolle oeffnen.",
            "Import" => "Inspektionsdaten, PDFs, Videos und Zusatzquellen ins Projekt uebernehmen.",
            "Export" => "Excel- und PDF-Ausgaben fuer Auswertung und Weitergabe erzeugen.",
            "Karte" => "Haltungen raeumlich ansehen und von der Karte aus oeffnen.",
            "Medienkonflikte" => "Fehlende, doppelte oder mehrdeutige Medienzuordnungen klaeren.",
            "Druckcenter" => "Dossiers und Berichte fuer Haltungen oder Projektumfang erstellen.",
            "Sanierungs-Matrix" => "Massnahmen, Kosten und Varianten fuer Sanierung bearbeiten.",
            "Schacht-Matrix" => "Sanierungsmassnahmen und Kosten je Schacht (NPK Kap. 700) erfassen.",
            "VSA" => "VSA-Zustandsklassen und Bewertungsdaten kontrollieren.",
            "Diagnose" => "Logs, Diagnoseinformationen und technische Details pruefen.",
            "Einstellungen" => "Pfade, Theme, KI-Start und Programmverhalten konfigurieren.",
            _ => "Ansicht oeffnen."
        };

        public string ToolTipShortcut => string.Empty;

        public Func<object> CreatePage { get; }

        public bool CanOpenWithoutProject { get; }

        public bool RequiresProject => !CanOpenWithoutProject;

        public bool IsAvailable
        {
            get => _isAvailable;
            private set
            {
                if (SetProperty(ref _isAvailable, value))
                    OnPropertyChanged(nameof(AvailabilityOpacity));
            }
        }

        public double AvailabilityOpacity => IsAvailable ? 1.0 : 0.5;

        public void UpdateAvailability(bool isProjectReady)
            => IsAvailable = isProjectReady || CanOpenWithoutProject;
    }

}
