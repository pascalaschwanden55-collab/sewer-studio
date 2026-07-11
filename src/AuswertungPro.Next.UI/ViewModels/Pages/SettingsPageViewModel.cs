using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Infrastructure.Ai.Configuration;
using AuswertungPro.Next.Infrastructure.Maintenance;
using AuswertungPro.Next.UI.Settings;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class SettingsPageViewModel : ObservableObject
{
    private const double DefaultYoloConfidence = 0.25d;
    private const double DefaultDinoBoxThreshold = 0.25d;
    private const double DefaultDinoTextThreshold = 0.20d;

    private readonly ServiceProvider _sp;
    private readonly ProgramCleanupService _programCleanup = new();

    [ObservableProperty] private bool _enableDiagnostics;
    [ObservableProperty] private string? _pdfToTextPath;
    [ObservableProperty] private string? _projectPath;
    [ObservableProperty] private string? _projectsRootDirectory;
    [ObservableProperty] private string? _abwasserkatasterXtfPath;
    [ObservableProperty] private string? _videoFolder;
    [ObservableProperty] private string? _kantonUriXtfDirectory;
    [ObservableProperty] private AutoSaveMode _dataAutoSaveMode;
    [ObservableProperty] private bool _enableRestorePoints;
    [ObservableProperty] private bool _videoHwDecoding;
    [ObservableProperty] private bool _videoDropLateFrames;
    [ObservableProperty] private bool _videoSkipFrames;
    [ObservableProperty] private int _videoFileCachingMs;
    [ObservableProperty] private int _videoNetworkCachingMs;
    [ObservableProperty] private int _videoCodecThreads;
    [ObservableProperty] private string _videoOutput = "direct3d11";
    [ObservableProperty] private string _uiTheme = ThemeManager.Light;
    [ObservableProperty] private bool _isDarkTheme;
    [ObservableProperty] private bool _startAiOnProgramStart;
    [ObservableProperty] private double _pipelineYoloConfidence = DefaultYoloConfidence;
    [ObservableProperty] private double _pipelineDinoBoxThreshold = DefaultDinoBoxThreshold;
    [ObservableProperty] private double _pipelineDinoTextThreshold = DefaultDinoTextThreshold;

    [ObservableProperty] private string _dataFolderPath = string.Empty;
    [ObservableProperty] private string _logsFolderPath = string.Empty;
    [ObservableProperty] private string _restorePointsFolderPath = string.Empty;
    [ObservableProperty] private string _backupStatusText = string.Empty;
    [ObservableProperty] private string _fullBackupStatusText = string.Empty;
    [ObservableProperty] private double _fullBackupPercent;
    [ObservableProperty] private bool _isFullBackupRunning;
    [ObservableProperty] private string _fullBackupCurrentFile = string.Empty;
    [ObservableProperty] private string _lastFullBackupInfo = string.Empty;
    [ObservableProperty] private string _aiStartupStatusText = string.Empty;
    [ObservableProperty] private string _programCleanupStatusText = "Noch nicht geprueft.";
    [ObservableProperty] private bool _isProgramCleanupRunning;
    // true, solange "KI starten" laeuft -> Fortschrittsbalken sichtbar, Knopf gesperrt.
    [ObservableProperty] private bool _isAiStarting;
    private bool _syncingThemeState;

    public IReadOnlyList<AutoSaveModeOption> AutoSaveModeOptions { get; } =
    [
        new(AutoSaveMode.OnEachChange, "Bei jeder Aenderung"),
        new(AutoSaveMode.Every5Minutes, "Alle 5 Minuten"),
        new(AutoSaveMode.Every10Minutes, "Alle 10 Minuten"),
        new(AutoSaveMode.Disabled, "Aus")
    ];

    public IReadOnlyList<IntOption> VideoCacheOptions { get; } =
    [
        new(500, "500 ms"),
        new(1000, "1000 ms"),
        new(1500, "1500 ms"),
        new(3000, "3000 ms"),
        new(5000, "5000 ms")
    ];

    public IReadOnlyList<IntOption> VideoCodecThreadOptions { get; } =
    [
        new(1, "1"),
        new(2, "2"),
        new(4, "4"),
        new(6, "6"),
        new(8, "8")
    ];

    public IReadOnlyList<StringOption> VideoOutputOptions { get; } =
    [
        new("direct3d11", "Direct3D11 (empfohlen)"),
        new("direct3d9", "Direct3D9"),
        new("any", "Automatisch")
    ];

    public IRelayCommand BrowsePdfToTextCommand { get; }
    public IRelayCommand BrowseProjectPathCommand { get; }
    public IRelayCommand BrowseProjectsRootCommand { get; }
    public IRelayCommand BrowseAbwasserkatasterXtfPathCommand { get; }
    public IRelayCommand BrowseVideoFolderCommand { get; }
    public IRelayCommand BrowseKantonUriXtfDirectoryCommand { get; }
    public IRelayCommand OpenDataFolderCommand { get; }
    public IRelayCommand OpenLogsFolderCommand { get; }
    public IRelayCommand OpenRestorePointsFolderCommand { get; }
    public IRelayCommand ApplyThemeCommand { get; }
    public IRelayCommand SaveCommand { get; }
    public IRelayCommand ResetYoloConfidenceCommand { get; }
    public IRelayCommand ResetDinoBoxThresholdCommand { get; }
    public IRelayCommand ResetDinoTextThresholdCommand { get; }
    public IAsyncRelayCommand StartAiCommand { get; }
    public IAsyncRelayCommand ExportBackupCommand { get; }
    public IAsyncRelayCommand ImportBackupCommand { get; }
    public AsyncRelayCommand CreateFullBackupCommand { get; }
    public IRelayCommand CancelFullBackupCommand { get; }
    public AsyncRelayCommand CleanProgramDataCommand { get; }

    public SettingsPageViewModel(ServiceProvider sp)
    {
        _sp = sp;
        EnableDiagnostics = _sp.Settings.EnableDiagnostics;
        PdfToTextPath = _sp.Settings.PdfToTextPath;
        ProjectPath = _sp.Settings.LastProjectPath;
        ProjectsRootDirectory = _sp.Settings.ProjectsRootDirectory;
        AbwasserkatasterXtfPath = _sp.Settings.AbwasserkatasterXtfPath;
        VideoFolder = _sp.Settings.LastVideoSourceFolder ?? _sp.Settings.LastVideoFolder;
        KantonUriXtfDirectory = _sp.Settings.KantonUriXtfDirectory;
        DataAutoSaveMode = _sp.Settings.DataAutoSaveMode.Normalize();
        EnableRestorePoints = _sp.Settings.EnableRestorePoints;
        VideoHwDecoding = _sp.Settings.VideoHwDecoding;
        VideoDropLateFrames = _sp.Settings.VideoDropLateFrames;
        VideoSkipFrames = _sp.Settings.VideoSkipFrames;
        VideoFileCachingMs = SettingsSaveWorkflow.ClampCaching(_sp.Settings.VideoFileCachingMs);
        VideoNetworkCachingMs = SettingsSaveWorkflow.ClampCaching(_sp.Settings.VideoNetworkCachingMs);
        VideoCodecThreads = SettingsSaveWorkflow.ClampCodecThreads(_sp.Settings.VideoCodecThreads);
        VideoOutput = SettingsSaveWorkflow.NormalizeVideoOutput(_sp.Settings.VideoOutput);
        UiTheme = ThemeManager.NormalizeTheme(_sp.Settings.UiTheme);
        IsDarkTheme = string.Equals(UiTheme, ThemeManager.Dark, StringComparison.Ordinal);
        StartAiOnProgramStart = _sp.Settings.AiStartOnProgramStart;
        var pipelineConfig = AiSettingsFactory
            .Load(AppSettingsAiSettingsProvider.ToSource(_sp.Settings))
            .ToPipelineConfig();
        PipelineYoloConfidence = SettingsSaveWorkflow.ClampThreshold(pipelineConfig.YoloConfidence);
        PipelineDinoBoxThreshold = SettingsSaveWorkflow.ClampThreshold(pipelineConfig.DinoBoxThreshold);
        PipelineDinoTextThreshold = SettingsSaveWorkflow.ClampThreshold(pipelineConfig.DinoTextThreshold);

        DataFolderPath = AppSettings.AppDataDir;
        LogsFolderPath = Path.Combine(AppSettings.AppDataDir, "logs");
        RestorePointsFolderPath = RestorePointService.SettingsRestoreRoot;
        LastFullBackupInfo = SettingsFullBackupPresentationBuilder.BuildLastBackupInfo(
            _sp.Settings.LastFullBackupUtc,
            _sp.Settings.LastFullBackupPath,
            _sp.Settings.LastFullBackupSizeBytes);

        BrowsePdfToTextCommand = new RelayCommand(BrowsePdfToText);
        BrowseProjectPathCommand = new RelayCommand(BrowseProjectPath);
        BrowseProjectsRootCommand = new RelayCommand(BrowseProjectsRoot);
        BrowseAbwasserkatasterXtfPathCommand = new RelayCommand(BrowseAbwasserkatasterXtfPath);
        BrowseVideoFolderCommand = new RelayCommand(BrowseVideoFolder);
        BrowseKantonUriXtfDirectoryCommand = new RelayCommand(BrowseKantonUriXtfDirectory);
        OpenDataFolderCommand = new RelayCommand(OpenDataFolder);
        OpenLogsFolderCommand = new RelayCommand(OpenLogsFolder);
        OpenRestorePointsFolderCommand = new RelayCommand(OpenRestorePointsFolder);
        ApplyThemeCommand = new RelayCommand(ApplyTheme);
        SaveCommand = new RelayCommand(Save);
        ResetYoloConfidenceCommand = new RelayCommand(() => PipelineYoloConfidence = DefaultYoloConfidence);
        ResetDinoBoxThresholdCommand = new RelayCommand(() => PipelineDinoBoxThreshold = DefaultDinoBoxThreshold);
        ResetDinoTextThresholdCommand = new RelayCommand(() => PipelineDinoTextThreshold = DefaultDinoTextThreshold);
        StartAiCommand = new AsyncRelayCommand(StartAiAsync);
        ExportBackupCommand = new AsyncRelayCommand(ExportBackupAsync);
        ImportBackupCommand = new AsyncRelayCommand(ImportBackupAsync);
        CreateFullBackupCommand = new AsyncRelayCommand(CreateFullBackupAsync, () => !IsFullBackupRunning);
        CancelFullBackupCommand = new RelayCommand(
            () => CreateFullBackupCommand.Cancel(),
            () => IsFullBackupRunning);
        CleanProgramDataCommand = new AsyncRelayCommand(
            CleanProgramDataAsync,
            () => !IsProgramCleanupRunning);
    }

    partial void OnIsFullBackupRunningChanged(bool value)
    {
        CreateFullBackupCommand?.NotifyCanExecuteChanged();
        CancelFullBackupCommand?.NotifyCanExecuteChanged();
    }

    partial void OnIsProgramCleanupRunningChanged(bool value)
    {
        CleanProgramDataCommand?.NotifyCanExecuteChanged();
    }

    partial void OnUiThemeChanged(string value)
    {
        SettingsThemeWorkflow.SyncUiThemeChanged(value, ThemeUi());
    }

    partial void OnIsDarkThemeChanged(bool value)
    {
        SettingsThemeWorkflow.SyncIsDarkThemeChanged(value, ThemeUi());
    }

    private void OpenDataFolder() => OpenFolder(DataFolderPath);

    private void OpenLogsFolder() => OpenFolder(LogsFolderPath);
    private void OpenRestorePointsFolder() => OpenFolder(RestorePointsFolderPath);

    private void OpenFolder(string path) => SettingsPathWorkflow.OpenFolder(path, _sp.Dialogs);

    private void BrowsePdfToText()
    {
        var p = SettingsPathWorkflow.SelectPdfToText(_sp.Dialogs);
        if (p is null) return;
        PdfToTextPath = p;
    }

    private void BrowseProjectPath()
    {
        var p = SettingsPathWorkflow.SelectProjectPath(_sp.Dialogs, ProjectPath);
        if (p is null)
            return;

        ProjectPath = p;
    }

    private void BrowseVideoFolder()
    {
        var p = SettingsPathWorkflow.SelectVideoFolder(_sp.Dialogs, VideoFolder);
        if (p is null) return;
        VideoFolder = p;
    }

    private void BrowseProjectsRoot()
    {
        var p = SettingsPathWorkflow.SelectProjectsRoot(_sp.Dialogs, ProjectsRootDirectory);
        if (p is null) return;
        ProjectsRootDirectory = p;
    }

    private void BrowseAbwasserkatasterXtfPath()
    {
        var p = SettingsPathWorkflow.SelectAbwasserkatasterXtfPath(_sp.Dialogs, AbwasserkatasterXtfPath);
        if (p is null) return;
        AbwasserkatasterXtfPath = p;
    }

    private void BrowseKantonUriXtfDirectory()
    {
        var p = SettingsPathWorkflow.SelectKantonUriXtfDirectory(_sp.Dialogs, KantonUriXtfDirectory);
        if (p is null) return;
        KantonUriXtfDirectory = p;
    }

    private void Save()
    {
        SettingsSaveWorkflow.Save(new SettingsSaveWorkflowRequest(
            _sp.Settings,
            _sp.Diagnostics,
            new SettingsSaveValues(
                EnableDiagnostics,
                PdfToTextPath,
                ProjectPath,
                ProjectsRootDirectory,
                AbwasserkatasterXtfPath,
                VideoFolder,
                KantonUriXtfDirectory,
                DataAutoSaveMode,
                EnableRestorePoints,
                VideoHwDecoding,
                VideoDropLateFrames,
                VideoSkipFrames,
                VideoFileCachingMs,
                VideoNetworkCachingMs,
                VideoCodecThreads,
                VideoOutput,
                UiTheme,
                StartAiOnProgramStart,
                PipelineYoloConfidence,
                PipelineDinoBoxThreshold,
                PipelineDinoTextThreshold),
            _sp.Settings.Save));
    }

    private async Task StartAiAsync()
    {
        await SettingsAiStartupWorkflow.RunAsync(
            _sp.Settings,
            _sp.Dialogs,
            new SettingsAiStartupWorkflowUi(
                () => IsAiStarting,
                value => IsAiStarting = value,
                value => AiStartupStatusText = value),
            _sp.Settings.SaveImmediate).ConfigureAwait(true);
    }

    private void ApplyTheme()
    {
        SettingsThemeWorkflow.ApplyTheme(_sp.Settings, UiTheme, _sp.Settings.SaveImmediate);
    }

    private SettingsThemeWorkflowUi ThemeUi()
        => new(
            () => _syncingThemeState,
            value => _syncingThemeState = value,
            () => UiTheme,
            value => UiTheme = value,
            () => IsDarkTheme,
            value => IsDarkTheme = value);

    private async Task ExportBackupAsync()
    {
        await SettingsKnowledgeBackupWorkflow.ExportAsync(
            _sp.Dialogs,
            value => BackupStatusText = value,
            () => DateTime.Now).ConfigureAwait(true);
    }

    private async Task ImportBackupAsync()
    {
        await SettingsKnowledgeBackupWorkflow.ImportAsync(
            _sp.Dialogs,
            value => BackupStatusText = value,
            () => DateTime.Now).ConfigureAwait(true);
    }

    private async Task CreateFullBackupAsync(CancellationToken ct)
    {
        await SettingsFullBackupWorkflow.RunAsync(
            new SettingsFullBackupWorkflowRequest(
                _sp.Settings,
                _sp.FullBackup,
                _sp.Dialogs,
                _sp.Toasts,
                new SettingsFullBackupWorkflowUi(
                    value => IsFullBackupRunning = value,
                    value => FullBackupPercent = value,
                    value => FullBackupCurrentFile = value,
                    value => FullBackupStatusText = value,
                    value => LastFullBackupInfo = value),
                AppSettings.FlushPendingSave,
                _sp.Settings.SaveImmediate,
                () => DateTime.UtcNow),
            ct).ConfigureAwait(true);
    }

    private async Task CleanProgramDataAsync()
    {
        await SettingsProgramCleanupWorkflow.RunAsync(
            new SettingsProgramCleanupWorkflowRequest(
                SettingsProgramCleanupRequestFactory.Create(_sp.Settings, DateTime.UtcNow),
                _programCleanup,
                _sp.Dialogs,
                _sp.Toasts,
                new SettingsProgramCleanupWorkflowUi(
                    value => IsProgramCleanupRunning = value,
                    value => ProgramCleanupStatusText = value))).ConfigureAwait(true);
    }

    public sealed record AutoSaveModeOption(AutoSaveMode Value, string Label);
    public sealed record IntOption(int Value, string Label);
    public sealed record StringOption(string Value, string Label);
}
