using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Backup;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.Application.Maintenance;
using AuswertungPro.Next.Application.Map;
using AuswertungPro.Next.Infrastructure.Ai.Configuration;
using AuswertungPro.Next.Infrastructure.Maintenance;
using AuswertungPro.Next.UI.Settings;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class SettingsPageViewModel : ObservableObject, IDisposable
{
    private const double DefaultYoloConfidence = 0.25d;
    private const double DefaultDinoBoxThreshold = 0.25d;
    private const double DefaultDinoTextThreshold = 0.20d;

    private readonly AppSettings _settings;
    private readonly DiagnosticsOptions _diagnostics;
    private readonly IDialogService _dialogs;
    private readonly IFullBackupService _fullBackup;
    private readonly ToastService _toasts;
    private readonly FullBackupOperationState _fullBackupOperation;
    private readonly ProgramCleanupService _programCleanup;
    private readonly ICodexArtifactCleanupService _codexArtifactCleanup;
    private readonly IKnowledgeBackupService _knowledgeBackup;
    private readonly IKatasterXtfPathResolver _katasterXtfPaths;
    private readonly IFolderOpenService _folderOpen;

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
    [ObservableProperty] private bool _includeProjectVideosInFullBackup;
    [ObservableProperty] private string _aiStartupStatusText = string.Empty;
    [ObservableProperty] private string _programCleanupStatusText = "Noch nicht geprueft.";
    [ObservableProperty] private string _codexArtifactCleanupStatusText = "Noch nicht geprueft.";
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
    public AsyncRelayCommand CleanCodexArtifactsCommand { get; }
    public FullBackupOperationState FullBackupOperation => _fullBackupOperation;

    public SettingsPageViewModel(ServiceProvider sp)
        : this(
            settings: sp.Settings,
            diagnostics: sp.Diagnostics,
            dialogs: sp.Dialogs,
            fullBackup: sp.FullBackup,
            toasts: sp.Toasts,
            fullBackupOperation: sp.FullBackupOperation,
            programCleanup: sp.ProgramCleanup,
            codexArtifactCleanup: sp.CodexArtifactCleanup,
            knowledgeBackup: sp.KnowledgeBackup,
            katasterXtfPaths: sp.KatasterXtfPaths,
            folderOpen: sp.FolderOpen)
    {
    }

    public SettingsPageViewModel(
        AppSettings settings,
        DiagnosticsOptions diagnostics,
        IDialogService dialogs,
        IFullBackupService fullBackup,
        ToastService toasts,
        FullBackupOperationState fullBackupOperation,
        ProgramCleanupService programCleanup)
        : this(
            settings,
            diagnostics,
            dialogs,
            fullBackup,
            toasts,
            fullBackupOperation,
            programCleanup,
            new KnowledgeBackupTransferService())
    {
    }

    public SettingsPageViewModel(
        AppSettings settings,
        DiagnosticsOptions diagnostics,
        IDialogService dialogs,
        IFullBackupService fullBackup,
        ToastService toasts,
        FullBackupOperationState fullBackupOperation,
        ProgramCleanupService programCleanup,
        IKnowledgeBackupService knowledgeBackup)
        : this(
            settings,
            diagnostics,
            dialogs,
            fullBackup,
            toasts,
            fullBackupOperation,
            programCleanup,
            new CodexArtifactCleanupService(),
            knowledgeBackup)
    {
    }

    public SettingsPageViewModel(
        AppSettings settings,
        DiagnosticsOptions diagnostics,
        IDialogService dialogs,
        IFullBackupService fullBackup,
        ToastService toasts,
        FullBackupOperationState fullBackupOperation,
        ProgramCleanupService programCleanup,
        ICodexArtifactCleanupService codexArtifactCleanup,
        IKnowledgeBackupService knowledgeBackup,
        IKatasterXtfPathResolver? katasterXtfPaths = null,
        IFolderOpenService? folderOpen = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _fullBackup = fullBackup ?? throw new ArgumentNullException(nameof(fullBackup));
        _toasts = toasts ?? throw new ArgumentNullException(nameof(toasts));
        _fullBackupOperation = fullBackupOperation ?? throw new ArgumentNullException(nameof(fullBackupOperation));
        _programCleanup = programCleanup ?? throw new ArgumentNullException(nameof(programCleanup));
        _codexArtifactCleanup = codexArtifactCleanup ?? throw new ArgumentNullException(nameof(codexArtifactCleanup));
        _knowledgeBackup = knowledgeBackup ?? throw new ArgumentNullException(nameof(knowledgeBackup));
        _katasterXtfPaths = katasterXtfPaths ?? Mapping.KatasterXtfPathResolver.CompatibilityService;
        _folderOpen = folderOpen ?? SettingsPathWorkflow.CompatibilityService;

        EnableDiagnostics = _settings.EnableDiagnostics;
        PdfToTextPath = _settings.PdfToTextPath;
        ProjectPath = _settings.LastProjectPath;
        ProjectsRootDirectory = _settings.ProjectsRootDirectory;
        AbwasserkatasterXtfPath = _settings.AbwasserkatasterXtfPath;
        VideoFolder = _settings.LastVideoSourceFolder ?? _settings.LastVideoFolder;
        KantonUriXtfDirectory = _settings.KantonUriXtfDirectory;
        DataAutoSaveMode = _settings.DataAutoSaveMode.Normalize();
        EnableRestorePoints = _settings.EnableRestorePoints;
        VideoHwDecoding = _settings.VideoHwDecoding;
        VideoDropLateFrames = _settings.VideoDropLateFrames;
        VideoSkipFrames = _settings.VideoSkipFrames;
        VideoFileCachingMs = SettingsSaveWorkflow.ClampCaching(_settings.VideoFileCachingMs);
        VideoNetworkCachingMs = SettingsSaveWorkflow.ClampCaching(_settings.VideoNetworkCachingMs);
        VideoCodecThreads = SettingsSaveWorkflow.ClampCodecThreads(_settings.VideoCodecThreads);
        VideoOutput = SettingsSaveWorkflow.NormalizeVideoOutput(_settings.VideoOutput);
        UiTheme = ThemeManager.NormalizeTheme(_settings.UiTheme);
        IsDarkTheme = string.Equals(UiTheme, ThemeManager.Dark, StringComparison.Ordinal);
        StartAiOnProgramStart = _settings.AiStartOnProgramStart;
        var pipelineConfig = AiSettingsFactory
            .Load(AppSettingsAiSettingsProvider.ToSource(_settings))
            .ToPipelineConfig();
        PipelineYoloConfidence = SettingsSaveWorkflow.ClampThreshold(pipelineConfig.YoloConfidence);
        PipelineDinoBoxThreshold = SettingsSaveWorkflow.ClampThreshold(pipelineConfig.DinoBoxThreshold);
        PipelineDinoTextThreshold = SettingsSaveWorkflow.ClampThreshold(pipelineConfig.DinoTextThreshold);

        DataFolderPath = AppSettings.AppDataDir;
        LogsFolderPath = Path.Combine(AppSettings.AppDataDir, "logs");
        RestorePointsFolderPath = RestorePointService.SettingsRestoreRoot;
        IncludeProjectVideosInFullBackup = _settings.FullBackupIncludeProjectVideos;

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
        CreateFullBackupCommand = new AsyncRelayCommand(
            CreateFullBackupAsync,
            () => !FullBackupOperation.IsRunning);
        CancelFullBackupCommand = new RelayCommand(
            FullBackupOperation.Cancel,
            () => FullBackupOperation.IsRunning);
        FullBackupOperation.PropertyChanged += OnFullBackupOperationPropertyChanged;
        CleanProgramDataCommand = new AsyncRelayCommand(
            CleanProgramDataAsync,
            () => !IsProgramCleanupRunning);
        CleanCodexArtifactsCommand = new AsyncRelayCommand(
            CleanCodexArtifactsAsync,
            () => !IsProgramCleanupRunning);
    }

    private void OnFullBackupOperationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(FullBackupOperationState.IsRunning))
            return;

        CreateFullBackupCommand.NotifyCanExecuteChanged();
        CancelFullBackupCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
        => FullBackupOperation.PropertyChanged -= OnFullBackupOperationPropertyChanged;

    partial void OnIncludeProjectVideosInFullBackupChanged(bool value)
    {
        _settings.FullBackupIncludeProjectVideos = value;
        _settings.SaveImmediate();
    }

    partial void OnIsProgramCleanupRunningChanged(bool value)
    {
        CleanProgramDataCommand?.NotifyCanExecuteChanged();
        CleanCodexArtifactsCommand?.NotifyCanExecuteChanged();
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

    private void OpenFolder(string path)
        => SettingsPathWorkflow.OpenFolder(path, _dialogs, _folderOpen);

    private void BrowsePdfToText()
    {
        var p = SettingsPathWorkflow.SelectPdfToText(_dialogs);
        if (p is null) return;
        PdfToTextPath = p;
    }

    private void BrowseProjectPath()
    {
        var p = SettingsPathWorkflow.SelectProjectPath(_dialogs, ProjectPath);
        if (p is null)
            return;

        ProjectPath = p;
    }

    private void BrowseVideoFolder()
    {
        var p = SettingsPathWorkflow.SelectVideoFolder(_dialogs, VideoFolder);
        if (p is null) return;
        VideoFolder = p;
    }

    private void BrowseProjectsRoot()
    {
        var p = SettingsPathWorkflow.SelectProjectsRoot(_dialogs, ProjectsRootDirectory);
        if (p is null) return;
        ProjectsRootDirectory = p;
    }

    private void BrowseAbwasserkatasterXtfPath()
    {
        var p = SettingsPathWorkflow.SelectAbwasserkatasterXtfPath(_dialogs, AbwasserkatasterXtfPath);
        if (p is null) return;
        AbwasserkatasterXtfPath = p;
    }

    private void BrowseKantonUriXtfDirectory()
    {
        var p = SettingsPathWorkflow.SelectKantonUriXtfDirectory(_dialogs, KantonUriXtfDirectory);
        if (p is null) return;
        KantonUriXtfDirectory = p;
    }

    private void Save()
    {
        SettingsSaveWorkflow.Save(new SettingsSaveWorkflowRequest(
            _settings,
            _diagnostics,
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
            _settings.Save,
            _katasterXtfPaths));
    }

    private async Task StartAiAsync()
    {
        await SettingsAiStartupWorkflow.RunAsync(
            _settings,
            _dialogs,
            new SettingsAiStartupWorkflowUi(
                () => IsAiStarting,
                value => IsAiStarting = value,
                value => AiStartupStatusText = value),
            _settings.SaveImmediate).ConfigureAwait(true);
    }

    private void ApplyTheme()
    {
        SettingsThemeWorkflow.ApplyTheme(_settings, UiTheme, _settings.SaveImmediate);
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
            _knowledgeBackup,
            _dialogs,
            value => BackupStatusText = value,
            () => DateTime.Now).ConfigureAwait(true);
    }

    private async Task ImportBackupAsync()
    {
        await SettingsKnowledgeBackupWorkflow.ImportAsync(
            _knowledgeBackup,
            _dialogs,
            value => BackupStatusText = value,
            () => DateTime.Now).ConfigureAwait(true);
    }

    private async Task CreateFullBackupAsync(CancellationToken ct)
    {
        await SettingsFullBackupWorkflow.RunAsync(
            new SettingsFullBackupWorkflowRequest(
                _settings,
                _fullBackup,
                _dialogs,
                _toasts,
                FullBackupOperation,
                AppSettings.FlushPendingSave,
                _settings.SaveImmediate,
                () => DateTime.UtcNow),
            ct).ConfigureAwait(true);
    }

    private async Task CleanProgramDataAsync()
    {
        await SettingsProgramCleanupWorkflow.RunAsync(
            new SettingsProgramCleanupWorkflowRequest(
                SettingsProgramCleanupRequestFactory.Create(_settings, DateTime.UtcNow),
                _programCleanup,
                _dialogs,
                _toasts,
                new SettingsProgramCleanupWorkflowUi(
                    value => IsProgramCleanupRunning = value,
                    value => ProgramCleanupStatusText = value))).ConfigureAwait(true);
    }

    private async Task CleanCodexArtifactsAsync()
    {
        await SettingsCodexArtifactCleanupWorkflow.RunAsync(
            new SettingsCodexArtifactCleanupWorkflowRequest(
                SettingsCodexArtifactCleanupRequestFactory.Create(DateTime.UtcNow),
                _codexArtifactCleanup,
                _dialogs,
                _toasts,
                new SettingsProgramCleanupWorkflowUi(
                    value => IsProgramCleanupRunning = value,
                    value => CodexArtifactCleanupStatusText = value))).ConfigureAwait(true);
    }

    public sealed record AutoSaveModeOption(AutoSaveMode Value, string Label);
    public sealed record IntOption(int Value, string Label);
    public sealed record StringOption(string Value, string Label);
}
