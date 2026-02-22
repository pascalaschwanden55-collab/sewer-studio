using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Domain.Models;
using System.IO;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace AuswertungPro.Next.UI.ViewModels.Pages
{
    public sealed partial class OverviewPageViewModel : ObservableObject
    {
        [ObservableProperty]
        private ProjectOverviewEntry? _selectedProjectEntry;
        private readonly ShellViewModel _shell;
        private readonly ServiceProvider _sp;

        public Project Project => _shell.Project;
        public bool IsProjectReady => _shell.IsProjectReady;

        [ObservableProperty] private string? _lastProjectPath;
        [ObservableProperty] private string _projectStatus = string.Empty;

        public ObservableCollection<ProjectOverviewEntry> ProjectEntries { get; } = new();

        public IRelayCommand NewCommand { get; }
        public IRelayCommand OpenCommand { get; }
        public IRelayCommand OpenSelectedCommand { get; }
        public IRelayCommand ContinueCommand { get; }
        public IRelayCommand RefreshCommand { get; }
        public IRelayCommand DeleteSelectedCommand { get; }
        public bool HasLastProject => !string.IsNullOrWhiteSpace(LastProjectPath) && File.Exists(LastProjectPath);

        public OverviewPageViewModel(ShellViewModel shell, ServiceProvider sp)
        {
            _shell = shell;
            _sp = sp;

            LastProjectPath = _sp.Settings.LastProjectPath;
            ProjectStatus = BuildProjectStatus();

            NewCommand = new RelayCommand(NewProject);
            OpenCommand = new RelayCommand(OpenProject);
            OpenSelectedCommand = new RelayCommand(OpenSelectedProject, () => SelectedProjectEntry is not null);
            ContinueCommand = new RelayCommand(OpenLastProject, () => HasLastProject);
            RefreshCommand = new RelayCommand(LoadAllProjects);
            DeleteSelectedCommand = new RelayCommand(DeleteSelectedProject, () => SelectedProjectEntry is not null);

            LoadAllProjects();

            _shell.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ShellViewModel.Project) ||
                    e.PropertyName == nameof(ShellViewModel.IsProjectReady))
                {
                    OnPropertyChanged(nameof(Project));
                    OnPropertyChanged(nameof(IsProjectReady));
                    ProjectStatus = BuildProjectStatus();
                    LastProjectPath = _sp.Settings.LastProjectPath;
                    if (e.PropertyName == nameof(ShellViewModel.IsProjectReady))
                        LoadAllProjects();
                }
            };
        }

    private void LoadAllProjects()
    {
        ProjectEntries.Clear();

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
                entries.Add(new ProjectOverviewEntry
                {
                    Name = name ?? Path.GetFileNameWithoutExtension(file),
                    Description = desc ?? string.Empty,
                    Path = file,
                    ModifiedAtUtc = modified,
                    IsLastProject = isLast
                });
            }
            catch { /* ignore invalid files */ }
        }

        if (HasLastProject && LastProjectPath is not null)
            AddEntry(LastProjectPath, true);

        var rootDirs = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "Rohdaten"),
            Path.Combine(Directory.GetCurrentDirectory(), "Rohdaten", "Section_PDF")
        };
        foreach (var dir in rootDirs)
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.GetFiles(dir, "*.json"))
                AddEntry(file, false);
        }

        foreach (var entry in entries
                     .OrderByDescending(e => e.IsLastProject)
                     .ThenByDescending(e => e.ModifiedAtUtc ?? DateTime.MinValue)
                     .ThenBy(e => e.Name))
        {
            ProjectEntries.Add(entry);
        }
    }

    private string BuildProjectStatus()
        => IsProjectReady ? "Projekt gespeichert" : "Projekt noch nicht gespeichert";

    private void NewProject()
    {
        _shell.NewProject();
        LastProjectPath = _sp.Settings.LastProjectPath;
        ProjectStatus = BuildProjectStatus();
        LoadAllProjects();
        _shell.NavigateTo("Projekt");
    }

    private void OpenProject()
    {
        if (!_shell.TryOpenProjectWithDialog())
            return;
        LastProjectPath = _sp.Settings.LastProjectPath;
        ProjectStatus = BuildProjectStatus();
        LoadAllProjects();
        _shell.NavigateTo("Projekt");
    }

    private void OpenSelectedProject()
    {
        var path = SelectedProjectEntry?.Path;
        if (string.IsNullOrWhiteSpace(path))
            return;
        if (!_shell.TryOpenProject(path))
            return;
        LastProjectPath = _sp.Settings.LastProjectPath;
        ProjectStatus = BuildProjectStatus();
        LoadAllProjects();
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
        var result = MessageBox.Show(
            $"Projekt wirklich löschen?\n\n{fileName}\n{entry.Path}",
            "Projekt löschen",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            File.Delete(entry.Path);

            if (string.Equals(_sp.Settings.LastProjectPath, entry.Path, StringComparison.OrdinalIgnoreCase))
            {
                _sp.Settings.LastProjectPath = null;
                _sp.Settings.Save();
                _shell.NewProject();
            }

            LoadAllProjects();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Löschen fehlgeschlagen: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
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
        public string ModifiedAtDisplay => ModifiedAtUtc?.ToLocalTime().ToString("dd.MM.yyyy HH:mm", CultureInfo.CurrentCulture) ?? "-";
    }
}
