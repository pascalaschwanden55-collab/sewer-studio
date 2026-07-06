using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Domain.Models;
using System;
using System.IO;
using System.Linq;
using System.Windows.Data;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using AuswertungPro.Next.UI.Player;
using AuswertungPro.Next.Application.Common;

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
            new("\uE8B7", "Projekt", () => new Pages.ProjectPageViewModel(this), canOpenWithoutProject: true),
            new("\uE8FD", "Haltungen", () => new Pages.DataPageViewModel(this, _sp)),
            new("\uE7F4", "Schaechte", () => new Pages.SchaechtePageViewModel(this, _sp)),
            // Segoe MDL2: Import = Download, Export = Upload
            new("\uE896", "Import", () => new Pages.ImportPageViewModel(this, _sp)),
            new("\uE898", "Export", () => new Pages.ExportPageViewModel(this, _sp), canOpenWithoutProject: true),
            new("\uE707", "Karte", () => new Pages.KarteViewModel(this, _sp)),
            new("\uE7BA", "Medienkonflikte", () => new Pages.MediaConflictsPageViewModel(this, _sp)),
            new("\uE749", "Druckcenter", () => new Pages.BuilderPageViewModel(this, _sp)),
            new("\uECA5", "Sanierungs-Matrix", () => new Pages.SanierungsMatrixPageViewModel(this, _sp)),
            new("\uE128", "VSA", () => new Pages.VsaPageViewModel(this, _sp)),
            new("\uE9CE", "Diagnose", () => new Pages.DiagnosticsPageViewModel(_sp)),
            new("\uE713", "Einstellungen", () => new Pages.SettingsPageViewModel(_sp), canOpenWithoutProject: true)
        };
        RefreshNavigationAvailability();

        SaveCommand = new RelayCommand(SaveProject, () => CurrentMode == ShellMode.Workspace);
        NewProjectCommand = new RelayCommand(StartNewProjectDraft);
        SwitchProjectCommand = new RelayCommand(SwitchProject);
        OpenProjectCommand = new RelayCommand(OpenProjectWithDialog);
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
            SetCurrentPage(new Pages.SanierungsMatrixPageViewModel(this, _sp, holding, singleHoldingMode: true, targetRecord));
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
        SetCurrentPage(new Pages.OverviewPageViewModel(this, _sp));
    }

    /// <summary>„Neues Projekt": leeres Projekt + Infoblatt im Draft-Modus.</summary>
    public void StartNewProjectDraft()
    {
        if (!ConfirmDiscardUnsavedChanges())
            return;

        ReplaceProject(new Project { Name = string.Empty });
        ResetProjectReady();
        HasPersistedProject = false;
        _suppressLeaveGuard = true;
        SelectedNavItem = null;
        _suppressLeaveGuard = false;
        CurrentMode = ShellMode.Draft;
        SetCurrentPage(new Pages.ProjectPageViewModel(this));
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
            SetStatus($"Projektordner konnte nicht angelegt werden: {ex.Message}");
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

    public bool TryOpenProject(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            SetStatus("Datei nicht gefunden");
            return false;
        }

        if (!ConfirmDiscardUnsavedChanges())
            return false;

        var res = _sp.Projects.Load(path);
        if (!res.Ok || res.Value is null)
        {
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

        ReplaceProject(res.Value);
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

    /// <summary>
    /// Fragt bei ungespeicherten Aenderungen nach (Speichern/Verwerfen/Abbrechen).
    /// Gibt false zurueck, wenn der Vorgang abgebrochen werden soll.
    /// </summary>
    private bool ConfirmDiscardUnsavedChanges()
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
        if (string.IsNullOrWhiteSpace(path) || !HasPersistedProject)
        {
            var defaultName = MakeSafeFileName(Project.Name);
            path = _sp.Dialogs.SaveFile("Projekt speichern", "Projekt (*.json)|*.json", ".json", defaultName);
            if (path is null)
            {
                SetStatus("Speichern abgebrochen");
                return false;
            }
            _sp.Settings.AddRecentProject(NormalizeProjectPath(path)); // setzt auch LastProjectPath
            _sp.Settings.Save();
        }

        EnsureProjectDirectory(path);
        if (_sp.Settings.EnableRestorePoints)
            TryCreateProjectRestorePoint(path);

        var res = _sp.Projects.Save(Project, path);
        SetStatus(res.Ok ? "Gespeichert" : $"Fehler: {res.ErrorMessage}");
        if (res.Ok)
        {
            IsProjectReady = true;
            HasPersistedProject = true;
            RefreshTitleAndDirty(); // Save setzt Project.Dirty=false -> Marker entfernen
            _sp.Toasts.Success("Projekt gespeichert");
        }
        return res.Ok;
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
        _sp.Settings.AddRecentProject(path); // Merkliste pflegen (setzt auch LastProjectPath)
        _sp.Settings.Save();
        MarkProjectReady();

        EnsureProjectDirectory(path);
        if (_sp.Settings.EnableRestorePoints)
            TryCreateProjectRestorePoint(path);

        var res = _sp.Projects.Save(Project, path);
        SetStatus(res.Ok ? $"Gespeichert: {Path.GetFileName(path)}" : $"Fehler: {res.ErrorMessage}");
        if (res.Ok)
        {
            HasPersistedProject = true;
            RefreshTitleAndDirty(); // Save setzt Project.Dirty=false -> Marker entfernen
            _sp.Toasts.Success($"Gespeichert: {Path.GetFileName(path)}");
        }
        return res.Ok;
    }

    private void OpenProjectWithDialog()
    {
        if (TryOpenProjectWithDialog())
            EnterWorkspaceOn("Haltungen");
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

    private static void TryCreateProjectRestorePoint(string projectPath)
    {
        var projectDir = Path.GetDirectoryName(projectPath);
        if (string.IsNullOrWhiteSpace(projectDir))
            return;

        var restoreRoot = Path.Combine(projectDir, "__RESTORE_POINTS");
        var scopeName = Path.GetFileNameWithoutExtension(projectPath);
        RestorePointService.TryCreate(projectPath, restoreRoot, scopeName);
    }

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
            "Projekt" => "Projektstammdaten, Speicherort und Bearbeitungsdaten pflegen.",
            "Haltungen" => "Haltungen pruefen, filtern, Videos und Protokolle oeffnen.",
            "Schaechte" => "Schachtdaten anzeigen, kontrollieren und zugehoerige Protokolle oeffnen.",
            "Import" => "Inspektionsdaten, PDFs, Videos und Zusatzquellen ins Projekt uebernehmen.",
            "Export" => "Excel- und PDF-Ausgaben fuer Auswertung und Weitergabe erzeugen.",
            "Karte" => "Haltungen raeumlich ansehen und von der Karte aus oeffnen.",
            "Medienkonflikte" => "Fehlende, doppelte oder mehrdeutige Medienzuordnungen klaeren.",
            "Druckcenter" => "Dossiers und Berichte fuer Haltungen oder Projektumfang erstellen.",
            "Sanierungs-Matrix" => "Massnahmen, Kosten und Varianten fuer Sanierung bearbeiten.",
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
