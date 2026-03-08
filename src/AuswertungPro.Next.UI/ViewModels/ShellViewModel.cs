using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Domain.Models;
using System.IO;
using System.Linq;
using System.Windows.Data;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.ViewModels;

public sealed partial class ShellViewModel : ObservableObject
{
    private readonly ServiceProvider _sp = (ServiceProvider)App.Services;
    private readonly List<GuideStep> _guideSteps;

    [ObservableProperty] private string _title = "SewerStudio";
    [ObservableProperty] private string _subtitle = "Bereit";

    /// <summary>System resource monitor (CPU, RAM, GPU) — polls every 2s.</summary>
    public SystemMonitorService Monitor { get; } = new();

    public Project Project => _project;
    private Project _project = new();

    public IReadOnlyList<NavItem> NavItems { get; }
    [ObservableProperty] private NavItem? _selectedNavItem;
    [ObservableProperty] private object? _currentPage;

    public IRelayCommand SaveCommand { get; }
    public IRelayCommand NewProjectCommand { get; }
    public IRelayCommand OpenProjectCommand { get; }
    public IRelayCommand SaveAsProjectCommand { get; }
    public IRelayCommand OpenPriceCatalogCommand { get; }
    public IRelayCommand OpenTemplateEditorCommand { get; }
    public IRelayCommand GuideNextCommand { get; }
    public IRelayCommand GuidePreviousCommand { get; }
    public IRelayCommand GuideHideCommand { get; }
    public IRelayCommand GuideShowCommand { get; }
    public IRelayCommand GuideRestartCommand { get; }
    public IRelayCommand ToggleFocusModeCommand { get; }
    [ObservableProperty] private bool _isProjectReady;
    [ObservableProperty] private bool _isGuideVisible = true;
    [ObservableProperty] private bool _isFocusMode;
    [ObservableProperty] private bool _isAiWorking;
    [ObservableProperty] private string _aiStatusLabel = "";
    [ObservableProperty] private int _guideStepIndex;
    [ObservableProperty] private string _guideStepTitle = "Ratten-Assistent";
    [ObservableProperty] private string _guideMessage = "Willkommen in SewerStudio.";

    public string GuideStepCounter => _guideSteps.Count == 0 ? "0/0" : $"{GuideStepIndex + 1}/{_guideSteps.Count}";
    public bool HasGuidePrevious => GuideStepIndex > 0;
    public bool HasGuideNext => GuideStepIndex < _guideSteps.Count - 1;

    // Lock-Objekt fuer thread-sichere ObservableCollection-Zugriffe
    private readonly object _collectionLock = new();

    public ShellViewModel()
    {
        _guideSteps = BuildGuideSteps();
        EnableCollectionSync(_project);

        NavItems = new List<NavItem>
        {
            new("\uE80F", "Uebersicht", () => new Pages.OverviewPageViewModel(this, _sp)),
            new("\uE8B7", "Projekt", () => new Pages.ProjectPageViewModel(this)),
            new("\uE8FD", "Haltungen", () => new Pages.DataPageViewModel(this)),
            new("\uE7F4", "Schaechte", () => new Pages.SchaechtePageViewModel(this)),
            // Segoe MDL2: Import = Download, Export = Upload
            new("\uE896", "Import", () => new Pages.ImportPageViewModel(this, _sp)),
            new("\uE898", "Export", () => new Pages.ExportPageViewModel(this, _sp)),
            new("\uE7BA", "Medienkonflikte", () => new Pages.MediaConflictsPageViewModel(this, _sp)),
            new("\uE749", "Druckcenter", () => new Pages.BuilderPageViewModel(this)),
            new("\uE128", "VSA", () => new Pages.VsaPageViewModel(this, _sp)),
            new("\uE8A1", "Eigendevis", () => new Pages.EigendevisPageViewModel(this, _sp)),
            new("\uE9CE", "Diagnose", () => new Pages.DiagnosticsPageViewModel(_sp)),
            new("\uE713", "Einstellungen", () => new Pages.SettingsPageViewModel(_sp))
        };

        SaveCommand = new RelayCommand(SaveProject);
        NewProjectCommand = new RelayCommand(NewProject);
        OpenProjectCommand = new RelayCommand(OpenProjectWithDialog);
        SaveAsProjectCommand = new RelayCommand(SaveProjectAs);
        OpenPriceCatalogCommand = new RelayCommand(OpenPriceCatalog);
        OpenTemplateEditorCommand = new RelayCommand(OpenTemplateEditor);
        GuideNextCommand = new RelayCommand(GuideNext);
        GuidePreviousCommand = new RelayCommand(GuidePrevious);
        GuideHideCommand = new RelayCommand(() => IsGuideVisible = false);
        GuideShowCommand = new RelayCommand(() => IsGuideVisible = true);
        GuideRestartCommand = new RelayCommand(RestartGuide);
        ToggleFocusModeCommand = new RelayCommand(() => IsFocusMode = !IsFocusMode);

        SelectedNavItem = NavItems[0];
        CurrentPage = SelectedNavItem.CreatePage();
        ApplyGuideStep();
        Monitor.Start();

        AiActivityTracker.ActiveChanged += (active, label) =>
        {
            IsAiWorking = active;
            AiStatusLabel = active ? label : "";
        };

        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SelectedNavItem) && SelectedNavItem is not null)
                CurrentPage = SelectedNavItem.CreatePage();
        };
    }

    partial void OnGuideStepIndexChanged(int value)
    {
        ApplyGuideStep();
    }

    partial void OnIsProjectReadyChanged(bool value)
    {
        ApplyGuideStep();
    }

    private void RestartGuide()
    {
        GuideStepIndex = 0;
        IsGuideVisible = true;
        ApplyGuideStep();
    }

    private void GuideNext()
    {
        if (HasGuideNext)
            GuideStepIndex++;
    }

    private void GuidePrevious()
    {
        if (HasGuidePrevious)
            GuideStepIndex--;
    }

    private void ApplyGuideStep()
    {
        if (_guideSteps.Count == 0)
            return;

        var index = Math.Max(0, Math.Min(GuideStepIndex, _guideSteps.Count - 1));
        if (index != GuideStepIndex)
        {
            GuideStepIndex = index;
            return;
        }

        var step = _guideSteps[index];
        GuideStepTitle = step.Title;

        var message = step.Message;
        if (step.RequiresProject && !IsProjectReady)
            message += "\n\nHinweis: Bitte zuerst ein Projekt speichern (Datei -> Speichern).";
        GuideMessage = message;

        if (!string.IsNullOrWhiteSpace(step.NavTitle) && (!step.RequiresProject || IsProjectReady))
            NavigateTo(step.NavTitle!);

        OnPropertyChanged(nameof(GuideStepCounter));
        OnPropertyChanged(nameof(HasGuidePrevious));
        OnPropertyChanged(nameof(HasGuideNext));
    }

    private static List<GuideStep> BuildGuideSteps()
    {
        return new List<GuideStep>
        {
            new("Willkommen", "Ich bin die Ratte und fuehre dich durch SewerStudio. Mit Weiter/Zurueck gehst du Schritt fuer Schritt.", "Uebersicht"),
            new("Projekt anlegen", "Waehle 'Neues Projekt' und bestimme einen Projektordner. Das Projekt wird sofort dort gespeichert.", "Projekt"),
            new("Daten pruefen", "Auf 'Haltungen' findest du die Haltungen und kannst Videos pro Haltung oeffnen.", "Haltungen", RequiresProject: true),
            new("Import", "Auf 'Import' importierst du PDF/XTF und verteilst Dateien in die Haltungsstruktur.", "Import", RequiresProject: true),
            new("Massnahmen", "Nutze in 'Haltungen' den Knopf 'Vorschlag aus Schadenscodes'. Das lernt aus bewerteten Haltungen."),
            new("Lernen", "Wenn du Kosten/Massnahmen uebernimmst oder speicherst, werden vorhandene Schadenscodes und Massnahmen als Lernbeispiel gesichert."),
            new("Fertig", "Wenn du willst, starte den Assistenten jederzeit neu ueber Hilfe -> Ratten-Assistent neu starten.")
        };
    }

    public void SetStatus(string text) => Subtitle = text;

    public void ReplaceProject(Project p)
    {
        _project = p;
        EnableCollectionSync(p);
        OnPropertyChanged(nameof(Project));
        SetStatus($"Projekt: {p.Name}");
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

    public void NewProject()
    {
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
            IsProjectReady = true;
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
        var projectRoot = System.AppContext.BaseDirectory;
        var costService = new Infrastructure.Costs.CostCalculationService(projectRoot);
        var vm = new Windows.PriceCatalogEditorViewModel(costService);
        var window = new Views.Windows.PriceCatalogEditorWindow
        {
            DataContext = vm
        };
        window.ShowDialog();
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

    public sealed record NavItem(string Icon, string Title, Func<object> CreatePage);
    private sealed record GuideStep(string Title, string Message, string? NavTitle = null, bool RequiresProject = false);
}
