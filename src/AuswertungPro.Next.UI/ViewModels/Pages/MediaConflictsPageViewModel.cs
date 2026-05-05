using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using AuswertungPro.Next.Infrastructure.Media;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.Views.Windows;
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
    private readonly ShellViewModel _shell;
    private readonly AppSettings _settings;
    private readonly IDialogService _dialogs;
    private readonly MediaConflictCenterService _service = new();

    [ObservableProperty] private MediaConflictRowViewModel? _selectedConflict;
    [ObservableProperty] private string _summaryText = "";
    [ObservableProperty] private string _lastResult = "";
    [ObservableProperty] private int _learnedMappingCount;
    [ObservableProperty] private int _openConflictCount;
    [ObservableProperty] private int _missingConflictCount;
    [ObservableProperty] private int _ambiguousConflictCount;

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

    // Phase 5.1.B Etappe 4 Sub-B: ServiceProvider-Bundle entfernt, Settings + Dialogs injiziert.
    public MediaConflictsPageViewModel(ShellViewModel shell)
    {
        _shell = shell;
        _settings = App.Resolve<AppSettings>();
        _dialogs = App.Resolve<IDialogService>();

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

        var projectFolder = _shell.GetProjectFolder();
        if (string.IsNullOrWhiteSpace(projectFolder) || !Directory.Exists(projectFolder))
        {
            OpenConflictCount = 0;
            MissingConflictCount = 0;
            AmbiguousConflictCount = 0;
            SummaryText = "Projektordner nicht verfuegbar. Bitte Projekt zuerst speichern.";
            LearnedMappingCount = _service.GetMappingCount(_shell.Project);
            LastResult = "";
            return;
        }

        var conflicts = _service.Scan(projectFolder);
        foreach (var conflict in conflicts)
        {
            var row = new MediaConflictRowViewModel(conflict)
            {
                SuggestedSourcePath = _service.TryResolveLearnedSourcePath(
                    _shell.Project,
                    conflict,
                    _settings.LastVideoSourceFolder)
            };

            Conflicts.Add(row);
        }

        SelectedConflict = Conflicts.FirstOrDefault();
        LearnedMappingCount = _service.GetMappingCount(_shell.Project);
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
            _dialogs.ShowMessage("Bitte zuerst einen Kandidaten auswaehlen.", "Konfliktcenter", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        ResolveSelected(source, setUserEdited: true);
    }

    private void ResolveManual()
    {
        if (SelectedConflict is null)
            return;

        var initial = !string.IsNullOrWhiteSpace(_settings.LastVideoSourceFolder)
            ? _settings.LastVideoSourceFolder
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
            _settings.LastVideoSourceFolder = selectedDir;
            _settings.LastVideoFolder = selectedDir;
            _settings.Save();
        }

        ResolveSelected(source, setUserEdited: true);
    }

    private void ResolveSuggested()
    {
        if (SelectedConflict is null)
            return;

        if (string.IsNullOrWhiteSpace(SelectedConflict.SuggestedSourcePath))
        {
            _dialogs.ShowMessage("Keine gelernte Quelle fuer diese Position vorhanden.", "Konfliktcenter", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        ResolveSelected(SelectedConflict.SuggestedSourcePath, setUserEdited: false);
    }

    private void ResolveSelected(string sourcePath, bool setUserEdited)
    {
        if (SelectedConflict is null)
            return;

        var result = _service.ResolveConflict(_shell.Project, SelectedConflict.Conflict, sourcePath, setUserEdited);
        if (!result.Success)
        {
            LastResult = $"Fehler: {result.Message}";
            _dialogs.ShowMessage(result.Message, "Konfliktcenter", MessageBoxButton.OK, MessageBoxImage.Warning);
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
        LearnedMappingCount = _service.GetMappingCount(_shell.Project);
        UpdateSummary();
        _shell.SetStatus("Medienkonflikt aufgeloest");
    }

    private void AutoResolveLearned()
    {
        var projectFolder = _shell.GetProjectFolder();
        if (string.IsNullOrWhiteSpace(projectFolder) || !Directory.Exists(projectFolder))
        {
            _dialogs.ShowMessage("Projektordner nicht verfuegbar.", "Konfliktcenter", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = _service.AutoResolveLearned(
            _shell.Project,
            projectFolder,
            _settings.LastVideoSourceFolder,
            setUserEdited: false);

        Refresh();
        LastResult = $"Auto-Resolve: {result.Resolved}/{result.TotalConflicts} aufgeloest, {result.Failed} Fehler, {result.Unresolved} offen";
    }

    private void ClearLearnedMappings()
    {
        var count = _service.ClearMappings(_shell.Project);
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
    }

    private void TryPlayVideo(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            _dialogs.ShowMessage("Video nicht gefunden.", "Konfliktcenter", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var options = new PlayerWindowOptions(
                EnableHardwareDecoding: _settings.VideoHwDecoding,
                DropLateFrames: _settings.VideoDropLateFrames,
                SkipFrames: _settings.VideoSkipFrames,
                FileCachingMs: _settings.VideoFileCachingMs,
                NetworkCachingMs: _settings.VideoNetworkCachingMs,
                CodecThreads: _settings.VideoCodecThreads,
                VideoOutput: _settings.VideoOutput);

            var window = new PlayerWindow(path, options);
            _dialogs.Show(window);
        }
        catch (Exception ex)
        {
            _dialogs.ShowMessage($"Video konnte nicht gestartet werden:\n{ex.Message}", "Konfliktcenter", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static bool TryOpenWithShell(string? path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            if (!File.Exists(path) && !Directory.Exists(path))
                return false;

            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void TryOpenFolder(string folder)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                return;

            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{folder}\"",
                UseShellExecute = true
            });
        }
        catch
        {
            // Ignore UI helper errors.
        }
    }

    private static void TryOpenSelectInExplorer(string? path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            if (File.Exists(path))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{path}\"",
                    UseShellExecute = true
                });
                return;
            }

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                TryOpenFolder(dir);
        }
        catch
        {
            // Ignore UI helper errors.
        }
    }
}
