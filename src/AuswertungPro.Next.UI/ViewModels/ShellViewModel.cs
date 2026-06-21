using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Domain.Models;
using System.IO;
using System.Linq;
using System.Windows.Data;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;

namespace AuswertungPro.Next.UI.ViewModels;

public static class ShellNavigationPolicy
{
    public static bool RequiresProject(string? title)
        => !CanOpenWithoutProject(title);

    public static bool CanOpenWithoutProject(string? title)
        => title is "Uebersicht" or "Projekt" or "Export" or "Einstellungen";
}

public sealed partial class ShellViewModel : ObservableObject, IDisposable
{
    private readonly ServiceProvider _sp = (ServiceProvider)App.Services;
    private bool _disposed;

    [ObservableProperty] private string _title = "SewerStudio";
    [ObservableProperty] private string _subtitle = "Bereit";

    /// <summary>System resource monitor (CPU, RAM, GPU) — polls every 2s.</summary>
    public SystemMonitorService Monitor { get; } = new();

    public Project Project => _project;
    private Project _project = new();

    /// <summary>S1: True bei ungespeicherten Aenderungen. Global sichtbar via Fenstertitel-Marker
    /// und Uebersichts-Badge. Wird ueber RefreshTitleAndDirty() benachrichtigt.</summary>
    public bool IsDirty => _project.Dirty;

    public IReadOnlyList<NavItem> NavItems { get; }
    [ObservableProperty] private NavItem? _selectedNavItem;
    [ObservableProperty] private object? _currentPage;

    public IRelayCommand SaveCommand { get; }
    public IRelayCommand NewProjectCommand { get; }
    public IRelayCommand OpenProjectCommand { get; }
    public IRelayCommand SaveAsProjectCommand { get; }
    public IRelayCommand OpenPriceCatalogCommand { get; }
    public IRelayCommand OpenTemplateEditorCommand { get; }
    public IRelayCommand ToggleFocusModeCommand { get; }
    [ObservableProperty] private bool _isProjectReady;
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

    public ShellViewModel()
    {
        EnableCollectionSync(_project);

        NavItems = new List<NavItem>
        {
            new("\uE80F", "Uebersicht", () => new Pages.OverviewPageViewModel(this, _sp), canOpenWithoutProject: true),
            new("\uE8B7", "Projekt", () => new Pages.ProjectPageViewModel(this), canOpenWithoutProject: true),
            new("\uE8FD", "Haltungen", () => new Pages.DataPageViewModel(this)),
            new("\uE7F4", "Schaechte", () => new Pages.SchaechtePageViewModel(this)),
            // Segoe MDL2: Import = Download, Export = Upload
            new("\uE896", "Import", () => new Pages.ImportPageViewModel(this, _sp)),
            new("\uE898", "Export", () => new Pages.ExportPageViewModel(this, _sp), canOpenWithoutProject: true),
            new("\uE7BA", "Medienkonflikte", () => new Pages.MediaConflictsPageViewModel(this, _sp)),
            new("\uE749", "Druckcenter", () => new Pages.BuilderPageViewModel(this)),
            new("\uECA5", "Sanierungs-Matrix", () => new Pages.SanierungsMatrixPageViewModel(this)),
            new("\uE128", "VSA", () => new Pages.VsaPageViewModel(this, _sp)),
            new("\uE9CE", "Diagnose", () => new Pages.DiagnosticsPageViewModel(_sp)),
            new("\uE713", "Einstellungen", () => new Pages.SettingsPageViewModel(_sp), canOpenWithoutProject: true)
        };
        RefreshNavigationAvailability();

        SaveCommand = new RelayCommand(SaveProject);
        NewProjectCommand = new RelayCommand(NewProject);
        OpenProjectCommand = new RelayCommand(OpenProjectWithDialog);
        SaveAsProjectCommand = new RelayCommand(SaveProjectAs);
        OpenPriceCatalogCommand = new RelayCommand(OpenPriceCatalog);
        OpenTemplateEditorCommand = new RelayCommand(OpenTemplateEditor);
        ToggleFocusModeCommand = new RelayCommand(() => IsFocusMode = !IsFocusMode);

        SelectedNavItem = NavItems[0];
        SetCurrentPage(SelectedNavItem.CreatePage());
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
            SetCurrentPage(new Pages.SanierungsMatrixPageViewModel(this, holding, singleHoldingMode: true, targetRecord));
            return;
        }

        SelectedNavItem = target;

        if (CurrentPage is Pages.SanierungsMatrixPageViewModel matrix)
            matrix.SelectHolding(holding);
    }

    public void NewProject()
    {
        if (!ConfirmDiscardUnsavedChanges())
            return;

        var folder = _sp.Dialogs.SelectFolder("Projektordner waehlen");
        if (string.IsNullOrWhiteSpace(folder))
            return;

        var p = new Project();
        var projectPath = Path.Combine(folder, "projekt.json");

        EnsureProjectDirectory(projectPath);
        var res = _sp.Projects.Save(p, projectPath);
        if (!res.Ok)
        {
            SetStatus($"Fehler: {res.ErrorMessage}");
            return;
        }

        _sp.Settings.LastProjectPath = projectPath;
        _sp.Settings.Save();

        ReplaceProject(p);
        MarkProjectReady();
        SetStatus($"Neues Projekt: {Path.GetFileName(folder)}");

        NavigateTo("Import");
    }

    /// <summary>
    /// Gibt den Projektordner zurueck (Verzeichnis der projekt.json).
    /// </summary>
    public string? GetProjectFolder()
        => string.IsNullOrWhiteSpace(_sp.Settings.LastProjectPath)
           ? null : Path.GetDirectoryName(_sp.Settings.LastProjectPath);

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

        _sp.Settings.LastProjectPath = path;
        _sp.Settings.Save();
        MarkProjectReady();

        ReplaceProject(res.Value);
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
        // Save uses last path if present, else Save As
        var path = NormalizeProjectPath(_sp.Settings.LastProjectPath);
        if (string.IsNullOrWhiteSpace(path))
        {
            var defaultName = MakeSafeFileName(Project.Name);
            path = _sp.Dialogs.SaveFile("Projekt speichern", "Projekt (*.json)|*.json", ".json", defaultName);
            if (path is null)
            {
                SetStatus("Speichern abgebrochen");
                return false;
            }
            _sp.Settings.LastProjectPath = NormalizeProjectPath(path);
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
            RefreshTitleAndDirty(); // Save setzt Project.Dirty=false -> Marker entfernen
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
        _sp.Settings.LastProjectPath = path;
        _sp.Settings.Save();
        MarkProjectReady();

        EnsureProjectDirectory(path);
        if (_sp.Settings.EnableRestorePoints)
            TryCreateProjectRestorePoint(path);

        var res = _sp.Projects.Save(Project, path);
        SetStatus(res.Ok ? $"Gespeichert: {Path.GetFileName(path)}" : $"Fehler: {res.ErrorMessage}");
        if (res.Ok)
            RefreshTitleAndDirty(); // Save setzt Project.Dirty=false -> Marker entfernen
        return res.Ok;
    }

    private void OpenProjectWithDialog()
        => TryOpenProjectWithDialog();

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
        var projectRoot = System.AppContext.BaseDirectory;
        var costService = new Infrastructure.Costs.CostCalculationService(projectRoot);
        var vm = new Windows.MeasureTemplateEditorViewModel(costService);
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
