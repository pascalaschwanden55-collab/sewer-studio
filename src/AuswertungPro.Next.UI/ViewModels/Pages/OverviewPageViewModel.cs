using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Domain.Models;
using System.IO;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AuswertungPro.Next.UI.Helpers;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.ViewModels.Pages
{
    public sealed partial class OverviewPageViewModel : ObservableObject
    {
        [ObservableProperty]
        private ProjectOverviewEntry? _selectedProjectEntry;
        private readonly ShellViewModel _shell;
        private readonly AppSettings _settings;
        private readonly IDialogService _dialogs;

        public Project Project => _shell.Project;
        public bool IsProjectReady => _shell.IsProjectReady;

        [ObservableProperty] private string? _lastProjectPath;
        [ObservableProperty] private string _projectStatus = string.Empty;
        [ObservableProperty] private string _filterText = string.Empty;

        public ObservableCollection<ProjectOverviewEntry> ProjectEntries { get; } = new();
        private List<ProjectOverviewEntry> _allEntries = new();
        private int _loadRevision;

        public IRelayCommand NewCommand { get; }
        public IRelayCommand OpenCommand { get; }
        public IRelayCommand OpenSelectedCommand { get; }
        public IRelayCommand ContinueCommand { get; }
        public IRelayCommand RefreshCommand { get; }
        public IRelayCommand DeleteSelectedCommand { get; }
        public bool HasLastProject => !string.IsNullOrWhiteSpace(LastProjectPath) && File.Exists(LastProjectPath);

        // Phase 5.1.B Etappe 4 Sub-A: ServiceProvider-Bundle entfernt, AppSettings injiziert.
        public OverviewPageViewModel(ShellViewModel shell)
        {
            _shell = shell;
            _settings = App.Resolve<AppSettings>();
            _dialogs = App.Resolve<IDialogService>();

            LastProjectPath = _settings.LastProjectPath;
            ProjectStatus = BuildProjectStatus();

            NewCommand = new RelayCommand(NewProject);
            OpenCommand = new RelayCommand(OpenProject);
            OpenSelectedCommand = new RelayCommand(OpenSelectedProject, () => SelectedProjectEntry is not null);
            ContinueCommand = new RelayCommand(OpenLastProject, () => HasLastProject);
            RefreshCommand = new RelayCommand(() => LoadAllProjectsAsync().SafeFireAndForget("OverviewRefresh"));
            DeleteSelectedCommand = new RelayCommand(DeleteSelectedProject, () => SelectedProjectEntry is not null);

            LoadAllProjectsAsync().SafeFireAndForget("OverviewLoadProjects");

            _shell.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ShellViewModel.Project) ||
                    e.PropertyName == nameof(ShellViewModel.IsProjectReady))
                {
                    OnPropertyChanged(nameof(Project));
                    OnPropertyChanged(nameof(IsProjectReady));
                    ProjectStatus = BuildProjectStatus();
                    LastProjectPath = _settings.LastProjectPath;
                    if (e.PropertyName == nameof(ShellViewModel.IsProjectReady))
                        LoadAllProjectsAsync().SafeFireAndForget("OverviewLoadProjects");
                }
            };
        }

    partial void OnFilterTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
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
    }

    private async Task LoadAllProjectsAsync()
    {
        _allEntries.Clear();
        ProjectEntries.Clear();

        var lastProjectPath = LastProjectPath;
        var hasLastProject = HasLastProject && !string.IsNullOrWhiteSpace(lastProjectPath);
        var recentPaths = _settings.RecentProjectPaths.ToList();
        var loadRevision = Interlocked.Increment(ref _loadRevision);

        var entries = await Task.Run(() => CollectProjectEntries(lastProjectPath, hasLastProject, recentPaths));
        if (loadRevision != Volatile.Read(ref _loadRevision))
            return;

        _allEntries = entries
            .OrderByDescending(e => e.IsLastProject)
            .ThenByDescending(e => e.ModifiedAtUtc ?? DateTime.MinValue)
            .ThenBy(e => e.Name)
            .ToList();

        ApplyFilter();
    }

    private static List<ProjectOverviewEntry> CollectProjectEntries(string? lastProjectPath, bool hasLastProject, IReadOnlyCollection<string> recentPaths)
    {
        var entries = new List<ProjectOverviewEntry>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddEntry(string file, bool isLast)
        {
            if (string.IsNullOrWhiteSpace(file) || !File.Exists(file)) return;
            if (!seen.Add(file)) return;

            try
            {
                using var stream = File.OpenRead(file);
                using var doc = JsonDocument.Parse(stream);
                var root = doc.RootElement;
                var name = root.TryGetProperty("Name", out var n) ? n.GetString() : Path.GetFileNameWithoutExtension(file);
                var desc = root.TryGetProperty("Description", out var d) ? d.GetString() : "";
                var modified = TryReadModifiedAt(root) ?? File.GetLastWriteTimeUtc(file);

                // Record-Anzahl aus JSON lesen
                int recordCount = 0;
                if (root.TryGetProperty("Data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Array)
                    recordCount = dataEl.GetArrayLength();

                entries.Add(new ProjectOverviewEntry
                {
                    Name = name ?? Path.GetFileNameWithoutExtension(file),
                    Description = desc ?? string.Empty,
                    Path = file,
                    ModifiedAtUtc = modified,
                    IsLastProject = isLast,
                    RecordCount = recordCount
                });
            }
            catch { /* ignore invalid files */ }
        }

        // 1. Letztes Projekt
        if (hasLastProject && lastProjectPath is not null)
            AddEntry(lastProjectPath, true);

        // 2. Alle RecentProjectPaths
        foreach (var recentPath in recentPaths)
            AddEntry(recentPath, string.Equals(recentPath, lastProjectPath, StringComparison.OrdinalIgnoreCase));

        // 3. Standard-Scan-Ordner
        var rootDirs = new List<string>
        {
            Path.Combine(Directory.GetCurrentDirectory(), "Rohdaten"),
            Path.Combine(Directory.GetCurrentDirectory(), "Rohdaten", "Section_PDF")
        };

        // 4. D:\Projekt\ und D:\Haltungen\ (typische Speicherorte)
        foreach (var drive in new[] { "D:\\", "C:\\" })
        {
            var projektDir = Path.Combine(drive, "Projekt");
            if (Directory.Exists(projektDir))
            {
                rootDirs.Add(projektDir);
                // Auch Unterordner scannen (z.B. D:\Projekt\Zone 1.15\)
                try
                {
                    foreach (var subDir in Directory.GetDirectories(projektDir))
                        rootDirs.Add(subDir);
                }
                catch { /* Zugriff verweigert */ }
            }
        }

        foreach (var dir in rootDirs)
        {
            if (!Directory.Exists(dir)) continue;
            try
            {
                foreach (var file in Directory.GetFiles(dir, "*.json"))
                    AddEntry(file, false);
            }
            catch { /* Zugriff verweigert */ }
        }

        return entries;
    }

    private string BuildProjectStatus()
        => IsProjectReady ? "Projekt gespeichert" : "Projekt noch nicht gespeichert";

    private void NewProject()
    {
        _shell.NewProject();
        LastProjectPath = _settings.LastProjectPath;
        ProjectStatus = BuildProjectStatus();
        LoadAllProjectsAsync().SafeFireAndForget("OverviewLoadProjects");
        _shell.NavigateTo("Projekt");
    }

    private void OpenProject()
    {
        if (!_shell.TryOpenProjectWithDialog())
            return;
        LastProjectPath = _settings.LastProjectPath;
        ProjectStatus = BuildProjectStatus();
        LoadAllProjectsAsync().SafeFireAndForget("OverviewLoadProjects");
        _shell.NavigateTo("Projekt");
    }

    private void OpenSelectedProject()
    {
        var path = SelectedProjectEntry?.Path;
        if (string.IsNullOrWhiteSpace(path))
            return;
        if (!_shell.TryOpenProject(path))
            return;
        _settings.AddRecentProject(path);
        _settings.Save();
        LastProjectPath = _settings.LastProjectPath;
        ProjectStatus = BuildProjectStatus();
        LoadAllProjectsAsync().SafeFireAndForget("OverviewLoadProjects");
        _shell.NavigateTo("Projekt");
    }

    private void DeleteSelectedProject()
    {
        var entry = SelectedProjectEntry;
        if (entry is null || string.IsNullOrWhiteSpace(entry.Path))
            return;
        if (!File.Exists(entry.Path))
            return;

        var fileName = Path.GetFileName(entry.Path);
        var result = _dialogs.ShowMessage(
            $"Projekt wirklich lÃ¶schen?\n\n{fileName}\n{entry.Path}",
            "Projekt lÃ¶schen",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            File.Delete(entry.Path);

            if (string.Equals(_settings.LastProjectPath, entry.Path, StringComparison.OrdinalIgnoreCase))
            {
                _settings.LastProjectPath = null;
                _settings.Save();
                _shell.NewProject();
            }

            LoadAllProjectsAsync().SafeFireAndForget("OverviewLoadProjects");
        }
        catch (Exception ex)
        {
            _dialogs.ShowMessage($"LÃ¶schen fehlgeschlagen: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenLastProject()
    {
        if (!HasLastProject || LastProjectPath is null)
            return;
        if (!_shell.TryOpenProject(LastProjectPath))
            return;
        LastProjectPath = _settings.LastProjectPath;
        ProjectStatus = BuildProjectStatus();
        LoadAllProjectsAsync().SafeFireAndForget("OverviewLoadProjects");
        _shell.NavigateTo("Projekt");
    }

    partial void OnSelectedProjectEntryChanged(ProjectOverviewEntry? value)
    {
        (OpenSelectedCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (DeleteSelectedCommand as RelayCommand)?.NotifyCanExecuteChanged();
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
        public string ModifiedAtDisplay => ModifiedAtUtc?.ToLocalTime().ToString("dd.MM.yyyy HH:mm", CultureInfo.CurrentCulture) ?? "-";
        public string FolderName => string.IsNullOrEmpty(Path) ? "" : System.IO.Path.GetDirectoryName(Path) ?? "";
        public string StatsText => RecordCount > 0 ? $"{RecordCount} Haltungen" : "Leer";
    }
}

