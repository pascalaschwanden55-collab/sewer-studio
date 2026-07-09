using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Domain.Models;
using System.IO;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text.Json;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System;
using AuswertungPro.Next.Application.Dashboard;
using AuswertungPro.Next.Infrastructure.Costs;
using System.Windows.Threading;
using AuswertungPro.Next.UI.DataPage;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.UI.ViewModels.Pages
{
    public sealed partial class OverviewPageViewModel : ObservableObject, IDisposable
    {
        [ObservableProperty]
        private ProjectOverviewEntry? _selectedProjectEntry;
        [ObservableProperty]
        private ProjectPreview? _selectedPreview;
        // Unterdrueckt den Vorschau-Neuaufbau, waehrend ApplyFilter die Liste neu befuellt
        // (sonst Flackern + Neuladen bei jedem Tastendruck im Suchfeld).
        private bool _suppressPreviewRebuild;
        // Pfad des zuletzt geladenen Vorschau-Projekts: gleiche Auswahl -> nicht erneut laden.
        private string? _previewedPath;
        private readonly ShellViewModel _shell;
        private readonly ServiceProvider _sp;
        private readonly DispatcherTimer _dashboardRefreshTimer;
        private readonly DispatcherTimer _previewRefreshTimer;
        private readonly ProjectCostStoreRepository _haltungCostRepo = new();
        private readonly ProjectCostStoreRepository _schachtCostRepo = new("schacht_costs.json");
        private Project? _subscribedProject;
        private CancellationTokenSource? _previewCts;
        private ProjectOverviewEntry? _pendingPreviewEntry;
        private string? _previewLoadingPath;

        public Project Project => _shell.Project;
        public bool IsProjectReady => _shell.IsProjectReady;
        public bool ShowFullDashboard => _shell.IsProjectReady;
        public DashboardStatistics? ActiveDashboard => ShowFullDashboard ? Dashboard : SelectedPreview?.Statistics;
        public bool HasActiveDashboard => ActiveDashboard?.HasData == true;
        public bool ShowProjectChoice => !ShowFullDashboard && SelectedPreview is null && !IsPreviewLoading;
        public bool ShowDataEmptyState => !ShowProjectChoice && !IsPreviewLoading && !HasActiveDashboard;
        public string DashboardTitle => ShowFullDashboard ? "Projekt-Cockpit" : "Projektvorschau";
        public string DashboardProjectName => ShowFullDashboard ? Project.Name ?? string.Empty : SelectedPreview?.Name ?? string.Empty;

        [ObservableProperty] private string? _lastProjectPath;
        [ObservableProperty] private string _projectStatus = string.Empty;
        [ObservableProperty] private string _filterText = string.Empty;
        [ObservableProperty] private DashboardStatistics? _dashboard;
        [ObservableProperty] private bool _isProjectListCollapsed;
        [ObservableProperty] private string _dashboardCostText = "-";
        [ObservableProperty] private bool _isPreviewLoading;

        public ObservableCollection<ProjectOverviewEntry> ProjectEntries { get; } = new();
        private List<ProjectOverviewEntry> _allEntries = new();
        private bool _disposed;

        public IRelayCommand NewCommand { get; }
        public IRelayCommand OpenCommand { get; }
        public IRelayCommand OpenSelectedCommand { get; }
        public IRelayCommand ContinueCommand { get; }
        public IRelayCommand RefreshCommand { get; }
        public IRelayCommand DeleteSelectedCommand { get; }
        public IRelayCommand<object?> NavigateConditionCommand { get; }
        public IRelayCommand<object?> NavigateDamageCommand { get; }
        public IRelayCommand<object?> NavigateDnCommand { get; }
        public IRelayCommand ToggleProjectListCommand { get; }
        public bool HasLastProject => !string.IsNullOrWhiteSpace(LastProjectPath) && File.Exists(LastProjectPath);

        public OverviewPageViewModel(ShellViewModel shell, ServiceProvider sp)
        {
            _shell = shell;
            _sp = sp;
            _dashboardRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _dashboardRefreshTimer.Tick += DashboardRefreshTimerTick;
            _previewRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _previewRefreshTimer.Tick += PreviewRefreshTimerTick;

            LastProjectPath = _sp.Settings.LastProjectPath;
            ProjectStatus = BuildProjectStatus();
            IsProjectListCollapsed = _sp.Settings.OverviewProjectListCollapsed;

            NewCommand = new RelayCommand(NewProject);
            OpenCommand = new RelayCommand(OpenProject);
            OpenSelectedCommand = new RelayCommand(OpenSelectedProject, () => SelectedProjectEntry is not null);
            ContinueCommand = new RelayCommand(OpenLastProject, () => HasLastProject);
            RefreshCommand = new RelayCommand(LoadAllProjects);
            DeleteSelectedCommand = new RelayCommand(DeleteSelectedProject, () => SelectedProjectEntry is not null);
            NavigateConditionCommand = new RelayCommand<object?>(NavigateCondition);
            NavigateDamageCommand = new RelayCommand<object?>(NavigateDamage);
            NavigateDnCommand = new RelayCommand<object?>(NavigateDn);
            ToggleProjectListCommand = new RelayCommand(ToggleProjectList);

            LoadAllProjects();

            _shell.PropertyChanged += ShellPropertyChanged;
            _sp.DashboardRefresh.CostsChanged += DashboardCostsChanged;
            SubscribeProject(_shell.Project);
            RefreshDashboard();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _shell.PropertyChanged -= ShellPropertyChanged;
            _sp.DashboardRefresh.CostsChanged -= DashboardCostsChanged;
            UnsubscribeProject();
            _dashboardRefreshTimer.Stop();
            _dashboardRefreshTimer.Tick -= DashboardRefreshTimerTick;
            CancelPreviewLoad();
            _previewRefreshTimer.Tick -= PreviewRefreshTimerTick;
        }

        private void ShellPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ShellViewModel.Project) ||
                e.PropertyName == nameof(ShellViewModel.IsProjectReady) ||
                e.PropertyName == nameof(ShellViewModel.HasPersistedProject) ||
                e.PropertyName == nameof(ShellViewModel.IsDirty))
            {
                OnPropertyChanged(nameof(Project));
                OnPropertyChanged(nameof(IsProjectReady));
                OnPropertyChanged(nameof(ShowFullDashboard));
                OnPropertyChanged(nameof(DashboardTitle));
                OnPropertyChanged(nameof(DashboardProjectName));
                if (e.PropertyName == nameof(ShellViewModel.Project))
                    SubscribeProject(_shell.Project);
                ScheduleDashboardRefresh();
                UpdateDashboardPresentation();
                ProjectStatus = BuildProjectStatus();
                LastProjectPath = _sp.Settings.LastProjectPath;
                if (e.PropertyName == nameof(ShellViewModel.IsProjectReady))
                    LoadAllProjects();
            }
        }

        partial void OnSelectedPreviewChanged(ProjectPreview? value)
            => UpdateDashboardPresentation();

        partial void OnDashboardChanged(DashboardStatistics? value)
            => UpdateDashboardPresentation();

        partial void OnIsPreviewLoadingChanged(bool value)
            => UpdateDashboardPresentation();

        partial void OnIsProjectListCollapsedChanged(bool value)
        {
            _sp.Settings.OverviewProjectListCollapsed = value;
            _sp.Settings.Save();
        }

        private void SubscribeProject(Project? project)
        {
            if (ReferenceEquals(_subscribedProject, project))
                return;

            UnsubscribeProject();
            _subscribedProject = project;
            if (_subscribedProject is null)
                return;

            _subscribedProject.Data.CollectionChanged += ProjectCollectionChanged;
            _subscribedProject.SchaechteData.CollectionChanged += ProjectCollectionChanged;
        }

        private void UnsubscribeProject()
        {
            if (_subscribedProject is null)
                return;

            _subscribedProject.Data.CollectionChanged -= ProjectCollectionChanged;
            _subscribedProject.SchaechteData.CollectionChanged -= ProjectCollectionChanged;
            _subscribedProject = null;
        }

        private void ProjectCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
            => ScheduleDashboardRefresh();

        private void DashboardCostsChanged(object? sender, EventArgs e)
            => ScheduleDashboardRefresh();

        private void DashboardRefreshTimerTick(object? sender, EventArgs e)
        {
            _dashboardRefreshTimer.Stop();
            RefreshDashboard();
        }

        private void ScheduleDashboardRefresh()
        {
            if (_disposed)
                return;

            var dispatcher = _dashboardRefreshTimer.Dispatcher;
            if (!dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke((Action)ScheduleDashboardRefresh);
                return;
            }

            _dashboardRefreshTimer.Stop();
            _dashboardRefreshTimer.Start();
        }

        private void RefreshDashboard()
        {
            if (_disposed)
                return;

            Dashboard = _shell.IsProjectReady
                ? BuildStatsFor(_shell.Project, _sp.Settings.LastProjectPath, out _)
                : null;
        }

        private DashboardStatistics BuildStatsFor(Project project, string? projectPath, out bool costAvailable)
        {
            var hCosts = LoadCostStore(_haltungCostRepo, projectPath, out var hOk);
            var sCosts = LoadCostStore(_schachtCostRepo, projectPath, out var sOk);
            costAvailable = hOk || sOk;
            return DashboardStatisticsBuilder.Build(project, hCosts, sCosts);
        }

        private static ProjectCostStore LoadCostStore(ProjectCostStoreRepository repo, string? projectPath, out bool ok)
        {
            ok = false;
            if (string.IsNullOrWhiteSpace(projectPath))
                return new ProjectCostStore();

            var store = repo.Load(projectPath, out var error);
            ok = error is null;
            return error is null ? store : new ProjectCostStore();
        }

        private void UpdateDashboardPresentation()
        {
            OnPropertyChanged(nameof(ActiveDashboard));
            OnPropertyChanged(nameof(HasActiveDashboard));
            OnPropertyChanged(nameof(ShowProjectChoice));
            OnPropertyChanged(nameof(ShowDataEmptyState));
            OnPropertyChanged(nameof(DashboardTitle));
            OnPropertyChanged(nameof(DashboardProjectName));
            DashboardCostText = FormatDashboardCostText(ActiveDashboard);
        }

        private static string FormatDashboardCostText(DashboardStatistics? stats)
            => stats is null ? "-" : $"{stats.TotalCost:N2} CHF";

        private void NavigateCondition(object? key)
        {
            var text = key?.ToString();
            if (string.IsNullOrWhiteSpace(text) || string.Equals(text, "ohne", StringComparison.OrdinalIgnoreCase))
                return;

            _shell.NavigateToDataPage(DataPageStartFilter.FromDashboardZustand(text));
        }

        private void NavigateDamage(object? key)
        {
            var text = key?.ToString();
            if (string.IsNullOrWhiteSpace(text))
                return;

            _shell.NavigateToDataPage(DataPageStartFilter.FromDashboardSchaden(text));
        }

        private void NavigateDn(object? key)
        {
            var text = key?.ToString();
            if (string.IsNullOrWhiteSpace(text))
                return;

            _shell.NavigateToDataPage(DataPageStartFilter.FromDashboardDn(text));
        }

        private void ToggleProjectList()
            => IsProjectListCollapsed = !IsProjectListCollapsed;

    partial void OnFilterTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        // Auswahl merken und Vorschau-Neuaufbau waehrend des Neubefuellens unterdruecken:
        // Clear() setzt die ListBox-Auswahl transient auf null -> ohne Guard blinkt die Vorschau
        // und laedt bei jedem Tastendruck das Projekt neu.
        var previous = SelectedProjectEntry;
        _suppressPreviewRebuild = true;
        try
        {
            ProjectEntries.Clear();
            var filter = FilterText?.Trim() ?? "";
            var filtered = string.IsNullOrEmpty(filter)
                ? _allEntries
                : _allEntries.Where(e =>
                    e.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    e.Description.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    e.Path.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (var entry in filtered)
                ProjectEntries.Add(entry);

            // Bisherige Auswahl erhalten, wenn sie noch sichtbar ist; sonst erstes Element.
            SelectedProjectEntry = previous is not null && ProjectEntries.Contains(previous)
                ? previous
                : ProjectEntries.FirstOrDefault();
        }
        finally
        {
            _suppressPreviewRebuild = false;
        }

        // Genau einmal bauen; der Pfad-Guard in BuildPreview verhindert Neuladen bei gleicher Auswahl.
        BuildPreview(SelectedProjectEntry);
    }

    private void LoadAllProjects()
    {
        _allEntries.Clear();
        ProjectEntries.Clear();

        var entries = new List<ProjectOverviewEntry>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddEntry(string file, bool isLast)
        {
            if (string.IsNullOrWhiteSpace(file) || !File.Exists(file)) return;
            // Aus der Uebersicht ausgeblendete Projekte nicht anzeigen (Dateien bleiben erhalten).
            if (_sp.Settings.HiddenProjectPaths.Any(p => string.Equals(p, file, StringComparison.OrdinalIgnoreCase))) return;
            if (!seen.Add(file)) return;

            try
            {
                using var stream = File.OpenRead(file);
                using var doc = JsonDocument.Parse(stream);
                var root = doc.RootElement;

                // Namens-Fallback: "projekt.json" heisst wie der Projektordner, nicht "projekt".
                var fallbackName = Path.GetFileNameWithoutExtension(file);
                if (string.Equals(fallbackName, "projekt", StringComparison.OrdinalIgnoreCase))
                {
                    var projectRoot = AuswertungPro.Next.Application.Common.ProjectFileLocator.ProjectRootFromFile(file);
                    if (!string.IsNullOrWhiteSpace(projectRoot))
                        fallbackName = Path.GetFileName(projectRoot);
                }

                var name = root.TryGetProperty("Name", out var n) && !string.IsNullOrWhiteSpace(n.GetString())
                    ? n.GetString()
                    : fallbackName;
                var desc = root.TryGetProperty("Description", out var d) ? d.GetString() : "";
                var modified = TryReadModifiedAt(root) ?? File.GetLastWriteTimeUtc(file);

                // Record-Anzahl aus JSON lesen.
                int recordCount = 0;
                if (root.TryGetProperty("Data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Array)
                    recordCount = dataEl.GetArrayLength();
                int schachtCount = 0;
                if (root.TryGetProperty("SchaechteData", out var schaechteEl) && schaechteEl.ValueKind == JsonValueKind.Array)
                    schachtCount = schaechteEl.GetArrayLength();

                entries.Add(new ProjectOverviewEntry
                {
                    Name = name ?? fallbackName,
                    Description = desc ?? string.Empty,
                    Path = file,
                    ModifiedAtUtc = modified,
                    IsLastProject = isLast,
                    RecordCount = recordCount,
                    SchachtCount = schachtCount
                });
            }
            catch
            {
                entries.Add(ProjectOverviewEntry.Corrupt(file, isLast));
            }
        }

        // 1. Letztes Projekt
        if (HasLastProject && LastProjectPath is not null)
            AddEntry(LastProjectPath, true);

        // 2. Alle RecentProjectPaths
        foreach (var recentPath in _sp.Settings.RecentProjectPaths)
            AddEntry(recentPath, string.Equals(recentPath, LastProjectPath, StringComparison.OrdinalIgnoreCase));

        // 3. Dateisystem-Scan als Wahrheitsquelle: gelernte Wurzeln (letztes Projekt,
        //    Merkliste), konfiguriertes Verzeichnis und Standard-Fallbacks. Findet
        //    Alt-Projekte UND die neue Struktur <Projekt>\Projektdateien\projekt.json —
        //    auch wenn die Settings-Merkliste verloren ging.
        var baseDirs = ProjectScanRoots.ResolveAll(
            Directory.GetCurrentDirectory(),
            _sp.Settings.ProjectsRootDirectory,
            _sp.Settings.LastProjectPath,
            _sp.Settings.RecentProjectPaths);

        foreach (var file in ProjectFileDiscovery.FindProjectFiles(baseDirs))
            AddEntry(file, false);

        _allEntries = entries
            .OrderByDescending(e => e.IsLastProject)
            .ThenByDescending(e => e.ModifiedAtUtc ?? DateTime.MinValue)
            .ThenBy(e => e.Name)
            .ToList();

        ApplyFilter();
    }

    private string BuildProjectStatus()
        => OverviewProjectStatusPolicy.Build(_shell.Project.Dirty, _shell.HasPersistedProject);

    private void NewProject()
    {
        // Startet den Draft-Modus in der Shell (neues Projekt + Infoblatt).
        _shell.StartNewProjectDraft();
    }

    private void OpenProject()
    {
        if (!_shell.TryOpenProjectWithDialog())
            return;
        LastProjectPath = _sp.Settings.LastProjectPath;
        ProjectStatus = BuildProjectStatus();
        LoadAllProjects();
        _shell.EnterWorkspaceOn("Uebersicht");
    }

    private void OpenSelectedProject()
    {
        var path = SelectedProjectEntry?.Path;
        if (string.IsNullOrWhiteSpace(path))
            return;
        OpenProjectFile(path);
    }

    public bool OpenProjectFromPath(string path)
    {
        var projectFile = ProjectDropPathResolver.ResolveProjectFile(path);
        if (string.IsNullOrWhiteSpace(projectFile))
            return false;

        return OpenProjectFile(projectFile);
    }

    private bool OpenProjectFile(string path)
    {
        if (!_shell.TryOpenProject(path))
            return false;
        // Merkliste pflegt TryOpenProject selbst.
        LastProjectPath = _sp.Settings.LastProjectPath;
        ProjectStatus = BuildProjectStatus();
        LoadAllProjects();
        _shell.EnterWorkspaceOn("Uebersicht");
        return true;
    }

    private void DeleteSelectedProject()
    {
        var entry = SelectedProjectEntry;
        if (entry is null || string.IsNullOrWhiteSpace(entry.Path))
            return;

        var confirmed = _sp.Dialogs.Confirm(
            $"Projekt aus der Übersicht entfernen?\n\n{entry.Name}\n{entry.Path}\n\n" +
            "Die Daten im Projektordner bleiben erhalten — beim erneuten Öffnen erscheint das Projekt wieder.",
            "Aus Übersicht entfernen");
        if (!confirmed)
            return;

        try
        {
            // Nur ausblenden, NICHT loeschen: die Projektdatei und alle Daten bleiben auf der Platte.
            var wasActive = string.Equals(_sp.Settings.LastProjectPath, entry.Path, StringComparison.OrdinalIgnoreCase);
            _sp.Settings.HideProject(entry.Path);
            _sp.Settings.Save();

            if (wasActive)
            {
                // Das aktive Projekt wurde ausgeblendet: Dirty-Flag zuruecksetzen und zum
                // Start-Bildschirm navigieren. EnterLauncher() baut die OverviewPage neu auf.
                _shell.Project.Dirty = false;
                _shell.EnterLauncher();
                return;
            }

            LoadAllProjects();
        }
        catch (Exception ex)
        {
            _sp.Dialogs.Error($"Entfernen fehlgeschlagen: {ex.Message}", "Fehler");
        }
    }

    private void OpenLastProject()
    {
        if (!HasLastProject || LastProjectPath is null)
            return;
        if (!_shell.TryOpenProject(LastProjectPath))
            return;
        LastProjectPath = _sp.Settings.LastProjectPath;
        ProjectStatus = BuildProjectStatus();
        LoadAllProjects();
        _shell.EnterWorkspaceOn("Uebersicht");
    }

    partial void OnSelectedProjectEntryChanged(ProjectOverviewEntry? value)
    {
        (OpenSelectedCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (DeleteSelectedCommand as RelayCommand)?.NotifyCanExecuteChanged();
        if (!_suppressPreviewRebuild)
            BuildPreview(value);
    }

    /// <summary>
    /// Plant die rechte Vorschau aus dem gewaehlten Listeneintrag. Der Datei-Load
    /// laeuft nach kurzem Debounce im Hintergrund, damit Listenwechsel die UI nicht blockieren.
    /// </summary>
    private void BuildPreview(ProjectOverviewEntry? entry)
    {
        if (_disposed)
            return;

        if (ShowFullDashboard)
        {
            _previewedPath = null;
            _pendingPreviewEntry = null;
            CancelPreviewLoad();
            SelectedPreview = null;
            return;
        }

        if (entry is null)
        {
            _previewedPath = null;
            _pendingPreviewEntry = null;
            CancelPreviewLoad();
            SelectedPreview = null;
            return;
        }

        if (string.Equals(entry.Path, _previewedPath, StringComparison.OrdinalIgnoreCase) && SelectedPreview is not null)
            return;
        if (_previewRefreshTimer.IsEnabled &&
            string.Equals(entry.Path, _pendingPreviewEntry?.Path, StringComparison.OrdinalIgnoreCase))
            return;
        if (IsPreviewLoading && string.Equals(entry.Path, _previewLoadingPath, StringComparison.OrdinalIgnoreCase))
            return;

        _pendingPreviewEntry = entry;
        _previewRefreshTimer.Stop();
        _previewRefreshTimer.Start();
    }

    private void PreviewRefreshTimerTick(object? sender, EventArgs e)
    {
        _previewRefreshTimer.Stop();
        StartPreviewLoad(_pendingPreviewEntry);
    }

    private void StartPreviewLoad(ProjectOverviewEntry? entry)
    {
        if (_disposed || ShowFullDashboard || entry is null)
        {
            CancelPreviewLoad();
            SelectedPreview = null;
            return;
        }

        _previewCts?.Cancel();
        var cts = new CancellationTokenSource();
        _previewCts = cts;
        _previewLoadingPath = entry.Path;
        IsPreviewLoading = true;
        SelectedPreview = null;

        var dispatcher = _previewRefreshTimer.Dispatcher;
        _ = Task.Run(() => BuildPreviewCore(entry, cts.Token), cts.Token)
            .ContinueWith(task =>
            {
                if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
                {
                    cts.Dispose();
                    return;
                }

                dispatcher.BeginInvoke((Action)(() => CompletePreviewLoad(entry, cts, task)));
            }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
    }

    private ProjectPreview BuildPreviewCore(ProjectOverviewEntry entry, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            var res = _sp.Projects.Load(entry.Path);
            ct.ThrowIfCancellationRequested();
            if (res.Ok && res.Value is not null)
            {
                var hCosts = LoadCostStore(_haltungCostRepo, entry.Path, out _);
                var sCosts = LoadCostStore(_schachtCostRepo, entry.Path, out _);
                ct.ThrowIfCancellationRequested();
                return ProjectPreviewFactory.FromProject(res.Value, entry.Path, hCosts, sCosts);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Faellt unten auf die Metadaten-Vorschau zurueck.
        }

        return BuildFallbackPreview(entry);
    }

    private void CompletePreviewLoad(
        ProjectOverviewEntry entry,
        CancellationTokenSource cts,
        Task<ProjectPreview> task)
    {
        try
        {
            if (!ReferenceEquals(_previewCts, cts))
                return;
            if (cts.IsCancellationRequested || ShowFullDashboard)
                return;
            if (!string.Equals(_pendingPreviewEntry?.Path, entry.Path, StringComparison.OrdinalIgnoreCase))
                return;
            if (!task.IsCompletedSuccessfully)
                return;

            _previewedPath = entry.Path;
            SelectedPreview = task.Result;
        }
        finally
        {
            if (ReferenceEquals(_previewCts, cts))
            {
                _previewCts = null;
                _previewLoadingPath = null;
                IsPreviewLoading = false;
            }

            cts.Dispose();
        }
    }

    private void CancelPreviewLoad()
    {
        _previewRefreshTimer.Stop();
        var cts = _previewCts;
        _previewCts = null;
        _previewLoadingPath = null;
        cts?.Cancel();
        IsPreviewLoading = false;
    }

    private static ProjectPreview BuildFallbackPreview(ProjectOverviewEntry entry)
    {
        var emptyStatistics = DashboardStatisticsBuilder.Build(new Project(), null, null);
        return new ProjectPreview(
            Name: entry.Name,
            Description: entry.Description,
            Path: entry.Path,
            ModifiedAtUtc: entry.ModifiedAtUtc,
            HoldingCount: entry.RecordCount,
            SchachtCount: entry.SchachtCount,
            TotalLengthMeters: 0,
            TotalCost: 0m,
            Auftraggeber: string.Empty,
            Gemeinde: string.Empty,
            Zone: string.Empty,
            Strasse: string.Empty,
            Bearbeiter: string.Empty,
            Inspektionsdatum: string.Empty,
            AuftragNr: string.Empty,
            Firma: string.Empty,
            Statistics: emptyStatistics);
    }

    partial void OnLastProjectPathChanged(string? value)
    {
        (ContinueCommand as RelayCommand)?.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(HasLastProject));
    }

    private static DateTime? TryReadModifiedAt(JsonElement root)
    {
        if (!root.TryGetProperty("ModifiedAtUtc", out var m))
            return null;
        if (m.ValueKind != JsonValueKind.String)
            return null;
        var raw = m.GetString();
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
            return dt;
        if (DateTime.TryParse(raw, out dt))
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        return null;
    }
}

    public class ProjectOverviewEntry
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public DateTime? ModifiedAtUtc { get; set; }
        public bool IsLastProject { get; set; }
        public int RecordCount { get; set; }
        public int SchachtCount { get; set; }
        public bool IsCorrupt { get; set; }
        public string ModifiedAtDisplay => ModifiedAtUtc?.ToLocalTime().ToString("dd.MM.yyyy HH:mm", CultureInfo.CurrentCulture) ?? "-";
        public string FolderName => string.IsNullOrEmpty(Path) ? "" : System.IO.Path.GetDirectoryName(Path) ?? "";
        public string StatsText => FormatStatsText(RecordCount, SchachtCount, IsCorrupt);

        public static ProjectOverviewEntry Corrupt(string file, bool isLast)
        {
            var name = System.IO.Path.GetFileNameWithoutExtension(file);
            if (string.Equals(name, "projekt", StringComparison.OrdinalIgnoreCase))
            {
                var projectRoot = AuswertungPro.Next.Application.Common.ProjectFileLocator.ProjectRootFromFile(file);
                if (!string.IsNullOrWhiteSpace(projectRoot))
                    name = System.IO.Path.GetFileName(projectRoot);
            }

            return new ProjectOverviewEntry
            {
                Name = string.IsNullOrWhiteSpace(name) ? System.IO.Path.GetFileName(file) : name,
                Description = "Projektdatei konnte nicht gelesen werden.",
                Path = file,
                ModifiedAtUtc = File.Exists(file) ? File.GetLastWriteTimeUtc(file) : null,
                IsLastProject = isLast,
                IsCorrupt = true
            };
        }

        internal static string FormatStatsText(int haltungCount, int schachtCount, bool isCorrupt = false)
        {
            if (isCorrupt)
                return "Datei fehlerhaft";
            if (haltungCount > 0 && schachtCount > 0)
                return $"{haltungCount} Haltungen · {schachtCount} Schaechte";
            if (haltungCount > 0)
                return $"{haltungCount} Haltungen";
            if (schachtCount > 0)
                return $"{schachtCount} Schaechte";
            return "Leer";
        }
    }
}
