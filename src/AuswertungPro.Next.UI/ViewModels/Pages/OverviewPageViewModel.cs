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
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Projects;
using AuswertungPro.Next.Infrastructure.Costs;
using AuswertungPro.Next.UI.Services;
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
        private readonly AppSettings _settings;
        private readonly DashboardRefreshNotifier _dashboardRefresh;
        private readonly IDialogService _dialogs;
        private readonly IProjectRepository _projects;
        private readonly IProjectFileDiscovery _projectFileDiscovery;
        private readonly IProjectDropPathResolver _projectDropPaths;
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
        public IReadOnlyList<ProjectPreviewMetadataItem> PreviewMetadataItems
            => ProjectPreviewMetadataItems.Build(SelectedPreview);
        public bool ShowPreviewMetadata => !ShowFullDashboard && !IsPreviewLoading && PreviewMetadataItems.Count > 0;
        public string DashboardTitle => ShowFullDashboard ? "Projekt-Cockpit" : "Projektvorschau";
        public string DashboardProjectName => ShowFullDashboard ? Project.Name ?? string.Empty : SelectedPreview?.Name ?? string.Empty;

        [ObservableProperty] private string? _lastProjectPath;
        [ObservableProperty] private string _projectStatus = string.Empty;
        [ObservableProperty] private string _filterText = string.Empty;
        [ObservableProperty] private DashboardStatistics? _dashboard;
        [ObservableProperty] private bool _isProjectListCollapsed;
        [ObservableProperty] private string _dashboardCostText = "-";
        [ObservableProperty] private bool _isPreviewLoading;
        [ObservableProperty] private bool _isPreviewPdfExportInProgress;

        public ObservableCollection<ProjectOverviewEntry> ProjectEntries { get; } = new();
        private List<ProjectOverviewEntry> _allEntries = new();
        private bool _disposed;

        public IRelayCommand NewCommand { get; }
        public IRelayCommand OpenCommand { get; }
        public IRelayCommand OpenSelectedCommand { get; }
        public IRelayCommand ContinueCommand { get; }
        public IRelayCommand RefreshCommand { get; }
        public IAsyncRelayCommand PrintPreviewPdfCommand { get; }
        public IRelayCommand ClearFilterCommand { get; }
        public IRelayCommand DeleteSelectedCommand { get; }
        public IRelayCommand<object?> NavigateConditionCommand { get; }
        public IRelayCommand<object?> NavigateSchachtConditionCommand { get; }
        public IRelayCommand<object?> NavigateDamageCommand { get; }
        public IRelayCommand<object?> NavigateDnCommand { get; }
        public IRelayCommand ToggleProjectListCommand { get; }
        public bool HasLastProject => !string.IsNullOrWhiteSpace(LastProjectPath) && File.Exists(LastProjectPath);

        public OverviewPageViewModel(ShellViewModel shell, ServiceProvider sp)
            : this(
                shell,
                sp.Settings,
                sp.DashboardRefresh,
                sp.Dialogs,
                sp.Projects,
                sp.ProjectFileDiscovery,
                sp.ProjectDropPaths)
        {
        }

        public OverviewPageViewModel(
            ShellViewModel shell,
            AppSettings settings,
            DashboardRefreshNotifier dashboardRefresh,
            IDialogService dialogs,
            IProjectRepository projects)
            : this(
                shell,
                settings,
                dashboardRefresh,
                dialogs,
                projects,
                ProjectFileDiscovery.CompatibilityService,
                ProjectDropPathResolver.CompatibilityService)
        {
        }

        public OverviewPageViewModel(
            ShellViewModel shell,
            AppSettings settings,
            DashboardRefreshNotifier dashboardRefresh,
            IDialogService dialogs,
            IProjectRepository projects,
            IProjectFileDiscovery projectFileDiscovery,
            IProjectDropPathResolver? projectDropPaths = null)
        {
            _shell = shell ?? throw new ArgumentNullException(nameof(shell));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _dashboardRefresh = dashboardRefresh ?? throw new ArgumentNullException(nameof(dashboardRefresh));
            _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
            _projects = projects ?? throw new ArgumentNullException(nameof(projects));
            _projectFileDiscovery = projectFileDiscovery
                ?? throw new ArgumentNullException(nameof(projectFileDiscovery));
            _projectDropPaths = projectDropPaths ?? ProjectDropPathResolver.CompatibilityService;
            _dashboardRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _dashboardRefreshTimer.Tick += DashboardRefreshTimerTick;
            _previewRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _previewRefreshTimer.Tick += PreviewRefreshTimerTick;

            NewCommand = new RelayCommand(NewProject);
            OpenCommand = new AsyncRelayCommand(OpenProjectAsync);
            OpenSelectedCommand = new AsyncRelayCommand(OpenSelectedProjectAsync, () => SelectedProjectEntry is not null);
            ContinueCommand = new AsyncRelayCommand(OpenLastProjectAsync, () => HasLastProject);
            RefreshCommand = new RelayCommand(LoadAllProjects);
            PrintPreviewPdfCommand = new AsyncRelayCommand(PrintPreviewPdfAsync, CanPrintPreviewPdf);
            ClearFilterCommand = new RelayCommand(ClearFilter);
            DeleteSelectedCommand = new RelayCommand(DeleteSelectedProject, () => SelectedProjectEntry is not null);
            NavigateConditionCommand = new RelayCommand<object?>(NavigateCondition);
            NavigateSchachtConditionCommand = new RelayCommand<object?>(NavigateSchachtCondition);
            NavigateDamageCommand = new RelayCommand<object?>(NavigateDamage);
            NavigateDnCommand = new RelayCommand<object?>(NavigateDn);
            ToggleProjectListCommand = new RelayCommand(ToggleProjectList);

            // ObservableProperty-Hooks benachrichtigen die Commands. Darum erst setzen,
            // nachdem alle Commands vollstaendig erzeugt wurden.
            LastProjectPath = _settings.LastProjectPath;
            ProjectStatus = BuildProjectStatus();
            IsProjectListCollapsed = _settings.OverviewProjectListCollapsed;

            LoadAllProjects();

            _shell.PropertyChanged += ShellPropertyChanged;
            _dashboardRefresh.CostsChanged += DashboardCostsChanged;
            SubscribeProject(_shell.Project);
            RefreshDashboard();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _shell.PropertyChanged -= ShellPropertyChanged;
            _dashboardRefresh.CostsChanged -= DashboardCostsChanged;
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
                LastProjectPath = _settings.LastProjectPath;
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

        partial void OnIsPreviewPdfExportInProgressChanged(bool value)
            => PrintPreviewPdfCommand.NotifyCanExecuteChanged();

        partial void OnIsProjectListCollapsedChanged(bool value)
        {
            _settings.OverviewProjectListCollapsed = value;
            _settings.Save();
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
                ? BuildStatsFor(_shell.Project, _settings.LastProjectPath, out _)
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
            OnPropertyChanged(nameof(PreviewMetadataItems));
            OnPropertyChanged(nameof(ShowPreviewMetadata));
            OnPropertyChanged(nameof(DashboardTitle));
            OnPropertyChanged(nameof(DashboardProjectName));
            DashboardCostText = FormatDashboardCostText(ActiveDashboard);
            PrintPreviewPdfCommand.NotifyCanExecuteChanged();
        }

        private static string FormatDashboardCostText(DashboardStatistics? stats)
            => stats is null ? "-" : stats.TotalCost.ToString("N0", CultureInfo.CurrentCulture);

        private bool CanPrintPreviewPdf()
            => HasActiveDashboard && !IsPreviewLoading && !IsPreviewPdfExportInProgress;

        private async Task PrintPreviewPdfAsync()
        {
            if (!CanPrintPreviewPdf())
                return;

            var preview = BuildPrintablePreview();
            if (preview is null)
            {
                _dialogs.Info("Keine Projektvorschau zum Drucken vorhanden.", "Projektvorschau");
                return;
            }

            var output = _dialogs.SaveFile(
                "Projektvorschau PDF speichern",
                "PDF (*.pdf)|*.pdf",
                defaultExt: "pdf",
                defaultFileName: BuildPreviewPdfFileName(preview.Name));
            if (string.IsNullOrWhiteSpace(output))
                return;

            IsPreviewPdfExportInProgress = true;
            try
            {
                var target = Path.GetFullPath(output);
                var directory = Path.GetDirectoryName(target);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                var pdf = await Task.Run(() => ProjectPreviewPdfBuilder.Build(preview));
                await File.WriteAllBytesAsync(target, pdf);
                _dialogs.Info($"PDF erstellt:\n{target}", "Projektvorschau");
            }
            catch (Exception ex)
            {
                _dialogs.Error(
                    $"PDF konnte nicht erstellt werden:\n{UserError.DescribeAndReport(ex, "Projektvorschau PDF erstellen")}",
                    "Projektvorschau");
            }
            finally
            {
                IsPreviewPdfExportInProgress = false;
            }
        }

        private ProjectPreview? BuildPrintablePreview()
        {
            if (!ShowFullDashboard)
                return SelectedPreview;

            var projectPath = _settings.LastProjectPath ?? string.Empty;
            var hCosts = LoadCostStore(_haltungCostRepo, projectPath, out _);
            var sCosts = LoadCostStore(_schachtCostRepo, projectPath, out _);
            return ProjectPreviewFactory.FromProject(Project, projectPath, hCosts, sCosts);
        }

        internal static string BuildPreviewPdfFileName(string? projectName)
        {
            var safeName = SanitizeFilePart(string.IsNullOrWhiteSpace(projectName) ? "Projekt" : projectName);
            return $"Projektvorschau_{safeName}_{DateTime.Now:yyyyMMdd}.pdf";
        }

        private static string SanitizeFilePart(string? value)
        {
            var text = (value ?? string.Empty).Trim();
            if (text.Length == 0)
                return "Projekt";

            foreach (var invalid in Path.GetInvalidFileNameChars())
                text = text.Replace(invalid, '_');

            return string.IsNullOrWhiteSpace(text) ? "Projekt" : text;
        }

        private void NavigateCondition(object? key)
        {
            var text = key?.ToString();
            if (string.IsNullOrWhiteSpace(text))
                return;

            _shell.NavigateToDataPage(DataPageStartFilter.FromDashboardZustand(text));
        }

        private void NavigateSchachtCondition(object? key)
        {
            var text = key?.ToString();
            if (string.IsNullOrWhiteSpace(text))
                return;

            _shell.EnterWorkspaceOn("Schaechte");
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

    private void ClearFilter()
        => FilterText = string.Empty;

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
            if (_settings.HiddenProjectPaths.Any(p => string.Equals(p, file, StringComparison.OrdinalIgnoreCase))) return;
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
        foreach (var recentPath in _settings.RecentProjectPaths)
            AddEntry(recentPath, string.Equals(recentPath, LastProjectPath, StringComparison.OrdinalIgnoreCase));

        // 3. Dateisystem-Scan als Wahrheitsquelle: gelernte Wurzeln (letztes Projekt,
        //    Merkliste), konfiguriertes Verzeichnis und Standard-Fallbacks. Findet
        //    Alt-Projekte UND die neue Struktur <Projekt>\Projektdateien\projekt.json —
        //    auch wenn die Settings-Merkliste verloren ging.
        var baseDirs = ProjectScanRoots.ResolveAll(
            Directory.GetCurrentDirectory(),
            _settings.ProjectsRootDirectory,
            _settings.LastProjectPath,
            _settings.RecentProjectPaths);

        foreach (var file in _projectFileDiscovery.FindProjectFiles(baseDirs))
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

    private async Task OpenProjectAsync()
    {
        if (!await _shell.TryOpenProjectWithDialogAsync())
            return;
        AfterProjectOpened();
    }

    private async Task OpenSelectedProjectAsync()
    {
        var path = SelectedProjectEntry?.Path;
        if (string.IsNullOrWhiteSpace(path))
            return;
        await OpenProjectFileAsync(path);
    }

    public bool OpenProjectFromPath(string path)
    {
        // Drag&Drop bleibt synchron (seltener Pfad, bool-Rueckgabe fuer den Drop-Handler).
        var projectFile = _projectDropPaths.ResolveProjectFile(path);
        if (string.IsNullOrWhiteSpace(projectFile))
            return false;

        if (!_shell.TryOpenProject(projectFile))
            return false;
        AfterProjectOpened();
        return true;
    }

    private async Task<bool> OpenProjectFileAsync(string path)
    {
        if (!await _shell.TryOpenProjectAsync(path))
            return false;
        AfterProjectOpened();
        return true;
    }

    /// <summary>Gemeinsamer Abschluss nach erfolgreichem Oeffnen (Merkliste pflegt die Shell selbst).</summary>
    private void AfterProjectOpened()
    {
        LastProjectPath = _settings.LastProjectPath;
        ProjectStatus = BuildProjectStatus();
        LoadAllProjects();
        _shell.EnterWorkspaceOn("Uebersicht");
    }

    private void DeleteSelectedProject()
    {
        var entry = SelectedProjectEntry;
        if (entry is null || string.IsNullOrWhiteSpace(entry.Path))
            return;

        var confirmed = _dialogs.Confirm(
            $"Projekt aus der Übersicht entfernen?\n\n{entry.Name}\n{entry.Path}\n\n" +
            "Die Daten im Projektordner bleiben erhalten — beim erneuten Öffnen erscheint das Projekt wieder.",
            "Aus Übersicht entfernen");
        if (!confirmed)
            return;

        try
        {
            // Nur ausblenden, NICHT loeschen: die Projektdatei und alle Daten bleiben auf der Platte.
            var wasActive = string.Equals(_settings.LastProjectPath, entry.Path, StringComparison.OrdinalIgnoreCase);
            if (wasActive && !_shell.ConfirmDiscardUnsavedChanges())
                return;

            _settings.HideProject(entry.Path);
            _settings.Save();

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
            _dialogs.Error(
                $"Entfernen fehlgeschlagen: {UserError.DescribeAndReport(ex, "Letztes Projekt entfernen")}",
                "Fehler");
        }
    }

    private async Task OpenLastProjectAsync()
    {
        if (!HasLastProject || LastProjectPath is null)
            return;
        if (!await _shell.TryOpenProjectAsync(LastProjectPath))
            return;
        AfterProjectOpened();
    }

    partial void OnSelectedProjectEntryChanged(ProjectOverviewEntry? value)
    {
        // Direkt ueber IRelayCommand — 'as RelayCommand' waere fuer AsyncRelayCommand null (stiller No-Op).
        OpenSelectedCommand.NotifyCanExecuteChanged();
        DeleteSelectedCommand.NotifyCanExecuteChanged();
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
            var res = _projects.Load(entry.Path);
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
        ContinueCommand.NotifyCanExecuteChanged();
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

    public sealed record ProjectPreviewMetadataItem(string Label, string Value);

    internal static class ProjectPreviewMetadataItems
    {
        public static IReadOnlyList<ProjectPreviewMetadataItem> Build(ProjectPreview? preview)
        {
            if (preview is null)
                return Array.Empty<ProjectPreviewMetadataItem>();

            var items = new List<ProjectPreviewMetadataItem>(capacity: 8);
            Add(items, "Auftraggeber", preview.Auftraggeber);
            Add(items, "Gemeinde", preview.Gemeinde);
            Add(items, "Zone", preview.Zone);
            Add(items, "Strasse", preview.Strasse);
            Add(items, "Bearbeiter", preview.Bearbeiter);
            Add(items, "Inspektionsdatum", preview.Inspektionsdatum);
            Add(items, "Auftrag-Nr", preview.AuftragNr);
            Add(items, "Firma", preview.Firma);
            return items;
        }

        private static void Add(List<ProjectPreviewMetadataItem> items, string label, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            items.Add(new ProjectPreviewMetadataItem(label, value.Trim()));
        }
    }
}
