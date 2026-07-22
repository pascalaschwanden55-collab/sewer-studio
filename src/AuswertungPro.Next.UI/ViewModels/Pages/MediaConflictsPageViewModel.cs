using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Media;
using AuswertungPro.Next.UI.Controls;
using AuswertungPro.Next.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed class MediaConflictCandidateViewModel
{
    public MediaConflictCandidateViewModel(string fullPath)
    {
        FullPath = fullPath;
        FileName = Path.GetFileName(fullPath);
        DirectoryName = Path.GetDirectoryName(fullPath) ?? string.Empty;
    }

    public string FullPath { get; }
    public string FileName { get; }
    public string DirectoryName { get; }
}

public sealed partial class MediaConflictRowViewModel : ObservableObject
{
    public MediaConflictCenterService.MediaConflictCase Conflict { get; }
    public ObservableCollection<MediaConflictCandidateViewModel> Candidates { get; }

    [ObservableProperty] private MediaConflictCandidateViewModel? _selectedCandidate;
    [ObservableProperty] private string? _suggestedSourcePath;
    [ObservableProperty] private string _resolutionState = "Offen";
    [ObservableProperty] private bool _isResolved;

    public MediaConflictRowViewModel(MediaConflictCenterService.MediaConflictCase conflict)
    {
        Conflict = conflict;
        Candidates = new ObservableCollection<MediaConflictCandidateViewModel>(
            conflict.Candidates.Select(path => new MediaConflictCandidateViewModel(path)));
        SelectedCandidate = Candidates.FirstOrDefault();
    }

    public string TypeText => Conflict.Type == MediaConflictCenterService.ConflictType.Ambiguous ? "Mehrdeutig" : "Fehlend";

    public string TypeHint => Conflict.Type == MediaConflictCenterService.ConflictType.Ambiguous
        ? "Mehrere moegliche Videos wurden gefunden. Bitte waehle den richtigen Treffer aus."
        : "Es wurde kein passendes Video gefunden. Bitte weise ein Video manuell zu.";

    public string HoldingText => string.IsNullOrWhiteSpace(Conflict.HoldingRaw) ? Conflict.HoldingFolderName : Conflict.HoldingRaw!;

    public string DateText
        => !string.IsNullOrWhiteSpace(Conflict.DateStamp)
           && DateTime.TryParseExact(
               Conflict.DateStamp,
               "yyyyMMdd",
               CultureInfo.InvariantCulture,
               DateTimeStyles.None,
               out var parsed)
            ? parsed.ToString("dd.MM.yyyy")
            : (Conflict.Date?.ToString("dd.MM.yyyy") ?? "-");

    public string ExpectedVideoText => string.IsNullOrWhiteSpace(Conflict.ExpectedVideoName) ? "-" : Conflict.ExpectedVideoName!;

    public string SourcePdfText => string.IsNullOrWhiteSpace(Conflict.SourcePdfPath) ? "-" : Path.GetFileName(Conflict.SourcePdfPath);

    public string SourcePdfPathText => string.IsNullOrWhiteSpace(Conflict.SourcePdfPath) ? "-" : Conflict.SourcePdfPath!;

    public string SuggestedSourceFileName => string.IsNullOrWhiteSpace(SuggestedSourcePath)
        ? "Keine gelernte Quelle vorhanden"
        : Path.GetFileName(SuggestedSourcePath);

    public string SuggestedSourcePathText => string.IsNullOrWhiteSpace(SuggestedSourcePath) ? "-" : SuggestedSourcePath!;

    public string? SelectedCandidatePath => SelectedCandidate?.FullPath;

    public int CandidateCount => Candidates.Count;

    public string CandidateSummaryText => CandidateCount switch
    {
        0 => "Keine Kandidaten vorhanden",
        1 => "1 Kandidat gefunden",
        _ => $"{CandidateCount} Kandidaten gefunden"
    };

    partial void OnSelectedCandidateChanged(MediaConflictCandidateViewModel? value)
    {
        OnPropertyChanged(nameof(SelectedCandidatePath));
    }

    partial void OnSuggestedSourcePathChanged(string? value)
    {
        OnPropertyChanged(nameof(SuggestedSourceFileName));
        OnPropertyChanged(nameof(SuggestedSourcePathText));
    }
}

public sealed partial class MediaConflictsPageViewModel : ObservableObject
{
    private readonly Func<Project> _getProject;
    private readonly Func<string?> _getProjectFolder;
    private readonly Func<string?> _getLastVideoSourceFolder;
    private readonly Action<string> _saveVideoSourceFolder;
    private readonly IDialogService _dialogs;
    private readonly MediaConflictCenterService _service;
    private readonly Action<string> _setStatus;
    private readonly Action<string> _playVideo;
    private readonly ISafeShellOpenService _shellOpen;
    private readonly IExplorerRevealService _explorerReveal;

    [ObservableProperty] private MediaConflictRowViewModel? _selectedConflict;
    [ObservableProperty] private string _summaryText = "";
    [ObservableProperty] private string _lastResult = "";
    [ObservableProperty] private int _learnedMappingCount;
    [ObservableProperty] private int _openConflictCount;
    [ObservableProperty] private int _missingConflictCount;
    [ObservableProperty] private int _ambiguousConflictCount;

    // Steuert den StatusHost der Seite: Inhalt (Tabelle), Leer (kein Konflikt) oder Fehler
    // (Projektordner fehlt). Wird zentral in UpdateSummary bzw. im Fehlerzweig von Refresh gesetzt.
    [ObservableProperty] private StatusHostState _conflictsState = StatusHostState.Empty;
    [ObservableProperty] private string _conflictsError = "";

    public ObservableCollection<MediaConflictRowViewModel> Conflicts { get; } = new();

    public IRelayCommand RefreshCommand { get; }
    public IRelayCommand ResolveFromCandidateCommand { get; }
    public IRelayCommand ResolveManualCommand { get; }
    public IRelayCommand ResolveSuggestedCommand { get; }
    public IRelayCommand AutoResolveLearnedCommand { get; }
    public IRelayCommand ClearLearnedMappingsCommand { get; }
    public IRelayCommand OpenInfoCommand { get; }
    public IRelayCommand OpenPdfCommand { get; }
    public IRelayCommand OpenHoldingFolderCommand { get; }
    public IRelayCommand OpenSelectedCandidateCommand { get; }
    public IRelayCommand OpenSuggestedSourceCommand { get; }
    public IRelayCommand PlaySelectedCandidateCommand { get; }
    public IRelayCommand PlaySuggestedSourceCommand { get; }

    public MediaConflictsPageViewModel(ShellViewModel shell, ServiceProvider sp)
        : this(
            getProject: () => shell.Project,
            getProjectFolder: shell.GetProjectFolder,
            getLastVideoSourceFolder: () => sp.Settings.LastVideoSourceFolder,
            saveVideoSourceFolder: folder =>
            {
                sp.Settings.LastVideoSourceFolder = folder;
                sp.Settings.LastVideoFolder = folder;
                sp.Settings.Save();
            },
            dialogs: sp.Dialogs,
            service: sp.MediaConflictCenter,
            setStatus: shell.SetStatus,
            playVideo: MediaConflictVideoLauncher.Create(sp),
            shellOpen: sp.ShellOpen,
            explorerReveal: sp.ExplorerReveal)
    {
    }

    public MediaConflictsPageViewModel(
        Func<Project> getProject,
        Func<string?> getProjectFolder,
        Func<string?> getLastVideoSourceFolder,
        Action<string> saveVideoSourceFolder,
        IDialogService dialogs,
        MediaConflictCenterService service,
        Action<string> setStatus,
        Action<string> playVideo,
        ISafeShellOpenService shellOpen,
        IExplorerRevealService explorerReveal)
    {
        _getProject = getProject ?? throw new ArgumentNullException(nameof(getProject));
        _getProjectFolder = getProjectFolder ?? throw new ArgumentNullException(nameof(getProjectFolder));
        _getLastVideoSourceFolder = getLastVideoSourceFolder ?? throw new ArgumentNullException(nameof(getLastVideoSourceFolder));
        _saveVideoSourceFolder = saveVideoSourceFolder ?? throw new ArgumentNullException(nameof(saveVideoSourceFolder));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _setStatus = setStatus ?? throw new ArgumentNullException(nameof(setStatus));
        _playVideo = playVideo ?? throw new ArgumentNullException(nameof(playVideo));
        _shellOpen = shellOpen ?? throw new ArgumentNullException(nameof(shellOpen));
        _explorerReveal = explorerReveal ?? throw new ArgumentNullException(nameof(explorerReveal));

        RefreshCommand = new RelayCommand(Refresh);
        ResolveFromCandidateCommand = new RelayCommand(ResolveFromCandidate);
        ResolveManualCommand = new RelayCommand(ResolveManual);
        ResolveSuggestedCommand = new RelayCommand(ResolveSuggested);
        AutoResolveLearnedCommand = new RelayCommand(AutoResolveLearned);
        ClearLearnedMappingsCommand = new RelayCommand(ClearLearnedMappings);
        OpenInfoCommand = new RelayCommand(OpenInfo);
        OpenPdfCommand = new RelayCommand(OpenPdf);
        OpenHoldingFolderCommand = new RelayCommand(OpenHoldingFolder);
        OpenSelectedCandidateCommand = new RelayCommand(OpenSelectedCandidate);
        OpenSuggestedSourceCommand = new RelayCommand(OpenSuggestedSource);
        PlaySelectedCandidateCommand = new RelayCommand(PlaySelectedCandidate);
        PlaySuggestedSourceCommand = new RelayCommand(PlaySuggestedSource);

        Refresh();
    }

    private void Refresh()
    {
        Conflicts.Clear();
        SelectedConflict = null;

        var project = _getProject();
        var projectFolder = _getProjectFolder();
        if (string.IsNullOrWhiteSpace(projectFolder) || !Directory.Exists(projectFolder))
        {
            OpenConflictCount = 0;
            MissingConflictCount = 0;
            AmbiguousConflictCount = 0;
            SummaryText = "Projektordner nicht verfuegbar. Bitte Projekt zuerst speichern.";
            LearnedMappingCount = _service.GetMappingCount(project);
            LastResult = "";
            ConflictsError = SummaryText;
            ConflictsState = StatusHostState.Error;
            return;
        }

        var conflicts = _service.Scan(projectFolder);
        foreach (var conflict in conflicts)
        {
            var row = new MediaConflictRowViewModel(conflict)
            {
                SuggestedSourcePath = _service.TryResolveLearnedSourcePath(
                    project,
                    conflict,
                    _getLastVideoSourceFolder())
            };

            Conflicts.Add(row);
        }

        SelectedConflict = Conflicts.FirstOrDefault();
        LearnedMappingCount = _service.GetMappingCount(project);
        UpdateSummary();
        LastResult = $"Konfliktcenter aktualisiert: {Conflicts.Count} offene Faelle";
    }

    private void ResolveFromCandidate()
    {
        if (SelectedConflict is null)
            return;

        var source = SelectedConflict.SelectedCandidatePath;
        if (string.IsNullOrWhiteSpace(source))
        {
            _dialogs.Info("Bitte zuerst einen Kandidaten auswaehlen.", "Konfliktcenter");
            return;
        }

        ResolveSelected(source, setUserEdited: true);
    }

    private void ResolveManual()
    {
        if (SelectedConflict is null)
            return;

        var lastVideoSourceFolder = _getLastVideoSourceFolder();
        var initial = !string.IsNullOrWhiteSpace(lastVideoSourceFolder)
            ? lastVideoSourceFolder
            : SelectedConflict.Conflict.HoldingFolder;

        var source = _dialogs.OpenFile(
            "Video fuer Konflikt auswaehlen",
            MediaFileTypes.VideoDialogFilter,
            initial);

        if (string.IsNullOrWhiteSpace(source))
            return;

        var selectedDir = Path.GetDirectoryName(source);
        if (!string.IsNullOrWhiteSpace(selectedDir))
        {
            _saveVideoSourceFolder(selectedDir);
        }

        ResolveSelected(source, setUserEdited: true);
    }

    private void ResolveSuggested()
    {
        if (SelectedConflict is null)
            return;

        if (string.IsNullOrWhiteSpace(SelectedConflict.SuggestedSourcePath))
        {
            _dialogs.Info("Keine gelernte Quelle fuer diese Position vorhanden.", "Konfliktcenter");
            return;
        }

        ResolveSelected(SelectedConflict.SuggestedSourcePath, setUserEdited: false);
    }

    private void ResolveSelected(string sourcePath, bool setUserEdited)
    {
        if (SelectedConflict is null)
            return;

        var project = _getProject();
        var result = _service.ResolveConflict(project, SelectedConflict.Conflict, sourcePath, setUserEdited);
        if (!result.Success)
        {
            LastResult = $"Fehler: {result.Message}";
            _dialogs.Warn(result.Message, "Konfliktcenter");
            return;
        }

        var resolvedConflict = SelectedConflict;
        resolvedConflict.IsResolved = true;
        resolvedConflict.ResolutionState = "Aufgeloest";

        var resolvedHolding = string.IsNullOrWhiteSpace(result.UpdatedHolding)
            ? resolvedConflict.HoldingText
            : result.UpdatedHolding;
        var videoName = Path.GetFileName(result.DestVideoPath ?? sourcePath);
        LastResult = $"OK: {resolvedHolding} -> {videoName}";

        Conflicts.Remove(resolvedConflict);
        SelectedConflict = Conflicts.FirstOrDefault();
        LearnedMappingCount = _service.GetMappingCount(project);
        UpdateSummary();
        _setStatus("Medienkonflikt aufgelöst");
    }

    private void AutoResolveLearned()
    {
        var projectFolder = _getProjectFolder();
        if (string.IsNullOrWhiteSpace(projectFolder) || !Directory.Exists(projectFolder))
        {
            _dialogs.Warn("Projektordner nicht verfuegbar.", "Konfliktcenter");
            return;
        }

        var result = _service.AutoResolveLearned(
            _getProject(),
            projectFolder,
            _getLastVideoSourceFolder(),
            setUserEdited: false);

        Refresh();
        LastResult = $"Auto-Resolve: {result.Resolved}/{result.TotalConflicts} aufgeloest, {result.Failed} Fehler, {result.Unresolved} offen";
    }

    private void ClearLearnedMappings()
    {
        var count = _service.ClearMappings(_getProject());
        Refresh();
        LastResult = count > 0
            ? $"Gelernte Mappings geloescht: {count}"
            : "Keine gelernten Mappings vorhanden.";
    }

    private void OpenInfo()
    {
        if (SelectedConflict is null)
            return;

        if (!TryOpenWithShell(SelectedConflict.Conflict.InfoPath))
            TryOpenSelectInExplorer(SelectedConflict.Conflict.InfoPath);
    }

    private void OpenPdf()
    {
        if (SelectedConflict is null || string.IsNullOrWhiteSpace(SelectedConflict.Conflict.SourcePdfPath))
            return;

        if (!TryOpenWithShell(SelectedConflict.Conflict.SourcePdfPath))
            TryOpenSelectInExplorer(SelectedConflict.Conflict.SourcePdfPath);
    }

    private void OpenHoldingFolder()
    {
        if (SelectedConflict is null)
            return;

        TryOpenFolder(SelectedConflict.Conflict.HoldingFolder);
    }

    private void OpenSelectedCandidate()
    {
        if (SelectedConflict is null || string.IsNullOrWhiteSpace(SelectedConflict.SelectedCandidatePath))
            return;

        TryOpenSelectInExplorer(SelectedConflict.SelectedCandidatePath);
    }

    private void OpenSuggestedSource()
    {
        if (SelectedConflict is null || string.IsNullOrWhiteSpace(SelectedConflict.SuggestedSourcePath))
            return;

        TryOpenSelectInExplorer(SelectedConflict.SuggestedSourcePath);
    }

    private void PlaySelectedCandidate()
    {
        if (SelectedConflict is null || string.IsNullOrWhiteSpace(SelectedConflict.SelectedCandidatePath))
            return;

        TryPlayVideo(SelectedConflict.SelectedCandidatePath);
    }

    private void PlaySuggestedSource()
    {
        if (SelectedConflict is null || string.IsNullOrWhiteSpace(SelectedConflict.SuggestedSourcePath))
            return;

        TryPlayVideo(SelectedConflict.SuggestedSourcePath);
    }

    private void UpdateSummary()
    {
        MissingConflictCount = Conflicts.Count(x => x.Conflict.Type == MediaConflictCenterService.ConflictType.Missing);
        AmbiguousConflictCount = Conflicts.Count(x => x.Conflict.Type == MediaConflictCenterService.ConflictType.Ambiguous);
        OpenConflictCount = Conflicts.Count;
        SummaryText = $"{OpenConflictCount} offene Konflikte | Fehlend: {MissingConflictCount} | Mehrdeutig: {AmbiguousConflictCount} | Gelernte Mappings: {LearnedMappingCount}";

        // Zentrale Ableitung des Anzeigezustands (nach Refresh-Erfolg und nach jedem Aufloesen).
        ConflictsError = "";
        ConflictsState = Conflicts.Count > 0 ? StatusHostState.Content : StatusHostState.Empty;
    }

    private void TryPlayVideo(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            _dialogs.Warn("Video nicht gefunden.", "Konfliktcenter");
            return;
        }

        try
        {
            _playVideo(path);
        }
        catch (Exception ex)
        {
            _dialogs.Error(
                $"Video konnte nicht gestartet werden:\n{UserError.DescribeAndReport(ex, "Konfliktcenter Video starten")}",
                "Konfliktcenter");
        }
    }

    private bool TryOpenWithShell(string? path)
        => _shellOpen.TryOpen(path, out _);

    private void TryOpenFolder(string? folder)
        => _shellOpen.TryOpen(folder, out _);

    private void TryOpenSelectInExplorer(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        if (_explorerReveal.TryReveal(path, out _))
            return;

        try
        {
            _explorerReveal.TryReveal(Path.GetDirectoryName(path), out _);
        }
        catch (Exception)
        {
            // Ein ungueltiger Fremdpfad darf die Seite nicht unbedienbar machen.
        }
    }
}
