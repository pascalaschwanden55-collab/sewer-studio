using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.Application.Diagnostics;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class SettingsPageViewModel : ObservableObject
{
    private readonly AppSettings _settings;
    private readonly DiagnosticsOptions _diagnostics;
    private readonly IDialogService _dialogs;

    [ObservableProperty] private bool _enableDiagnostics;
    [ObservableProperty] private string? _pdfToTextPath;
    [ObservableProperty] private string? _projectPath;
    [ObservableProperty] private string? _videoFolder;
    [ObservableProperty] private AutoSaveMode _dataAutoSaveMode;
    [ObservableProperty] private bool _enableRestorePoints;
    // Phase 1.4: Steuert Sichtbarkeit Eigendevis-NavItem + Hydraulik-Toolbar.
    [ObservableProperty] private bool _showExpertenmodusFeatures;
    [ObservableProperty] private bool _videoHwDecoding;
    [ObservableProperty] private bool _videoDropLateFrames;
    [ObservableProperty] private bool _videoSkipFrames;
    [ObservableProperty] private int _videoFileCachingMs;
    [ObservableProperty] private int _videoNetworkCachingMs;
    [ObservableProperty] private int _videoCodecThreads;
    [ObservableProperty] private string _videoOutput = "direct3d11";
    [ObservableProperty] private string _uiTheme = ThemeManager.Light;
    [ObservableProperty] private bool _isDarkTheme;

    [ObservableProperty] private string _dataFolderPath = string.Empty;
    [ObservableProperty] private string _logsFolderPath = string.Empty;
    [ObservableProperty] private string _restorePointsFolderPath = string.Empty;
    [ObservableProperty] private string _backupStatusText = string.Empty;
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
    public IRelayCommand BrowseVideoFolderCommand { get; }
    public IRelayCommand OpenDataFolderCommand { get; }
    public IRelayCommand OpenLogsFolderCommand { get; }
    public IRelayCommand OpenRestorePointsFolderCommand { get; }
    public IRelayCommand ApplyThemeCommand { get; }
    public IRelayCommand SaveCommand { get; }
    public IAsyncRelayCommand ExportBackupCommand { get; }
    public IAsyncRelayCommand ImportBackupCommand { get; }

    // Phase 5.1.B Etappe 4 Sub-C: ServiceProvider-Bundle entfernt, Services injiziert.
    public SettingsPageViewModel()
    {
        _settings = App.Resolve<AppSettings>();
        _diagnostics = App.Resolve<DiagnosticsOptions>();
        _dialogs = App.Resolve<IDialogService>();
        EnableDiagnostics = _settings.EnableDiagnostics;
        PdfToTextPath = _settings.PdfToTextPath;
        ProjectPath = _settings.LastProjectPath;
        VideoFolder = _settings.LastVideoSourceFolder ?? _settings.LastVideoFolder;
        DataAutoSaveMode = _settings.DataAutoSaveMode.Normalize();
        EnableRestorePoints = _settings.EnableRestorePoints;
        ShowExpertenmodusFeatures = _settings.ShowExpertenmodusFeatures;
        VideoHwDecoding = _settings.VideoHwDecoding;
        VideoDropLateFrames = _settings.VideoDropLateFrames;
        VideoSkipFrames = _settings.VideoSkipFrames;
        VideoFileCachingMs = ClampCaching(_settings.VideoFileCachingMs);
        VideoNetworkCachingMs = ClampCaching(_settings.VideoNetworkCachingMs);
        VideoCodecThreads = ClampCodecThreads(_settings.VideoCodecThreads);
        VideoOutput = NormalizeVideoOutput(_settings.VideoOutput);
        UiTheme = ThemeManager.NormalizeTheme(_settings.UiTheme);
        IsDarkTheme = string.Equals(UiTheme, ThemeManager.Dark, StringComparison.Ordinal);

        DataFolderPath = AppSettings.AppDataDir;
        LogsFolderPath = Path.Combine(AppSettings.AppDataDir, "logs");
        RestorePointsFolderPath = RestorePointService.SettingsRestoreRoot;

        BrowsePdfToTextCommand = new RelayCommand(BrowsePdfToText);
        BrowseProjectPathCommand = new RelayCommand(BrowseProjectPath);
        BrowseVideoFolderCommand = new RelayCommand(BrowseVideoFolder);
        OpenDataFolderCommand = new RelayCommand(OpenDataFolder);
        OpenLogsFolderCommand = new RelayCommand(OpenLogsFolder);
        OpenRestorePointsFolderCommand = new RelayCommand(OpenRestorePointsFolder);
        ApplyThemeCommand = new RelayCommand(ApplyTheme);
        SaveCommand = new RelayCommand(Save);
        ExportBackupCommand = new AsyncRelayCommand(ExportBackupAsync);
        ImportBackupCommand = new AsyncRelayCommand(ImportBackupAsync);
    }

    partial void OnUiThemeChanged(string value)
    {
        if (_syncingThemeState)
            return;

        _syncingThemeState = true;
        try
        {
            var normalized = ThemeManager.NormalizeTheme(value);
            if (!string.Equals(normalized, value, StringComparison.Ordinal))
            {
                UiTheme = normalized;
                return;
            }

            var shouldBeDark = string.Equals(normalized, ThemeManager.Dark, StringComparison.Ordinal);
            if (IsDarkTheme != shouldBeDark)
                IsDarkTheme = shouldBeDark;
        }
        finally
        {
            _syncingThemeState = false;
        }
    }

    partial void OnIsDarkThemeChanged(bool value)
    {
        if (_syncingThemeState)
            return;

        _syncingThemeState = true;
        try
        {
            var targetTheme = value ? ThemeManager.Dark : ThemeManager.Light;
            if (!string.Equals(UiTheme, targetTheme, StringComparison.Ordinal))
                UiTheme = targetTheme;
        }
        finally
        {
            _syncingThemeState = false;
        }
    }

    private void OpenDataFolder() => OpenFolder(DataFolderPath);

    private void OpenLogsFolder() => OpenFolder(LogsFolderPath);
    private void OpenRestorePointsFolder() => OpenFolder(RestorePointsFolderPath);

    private static void OpenFolder(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
                return;

            Directory.CreateDirectory(path);

            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{path}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ordner konnte nicht geöffnet werden:\n{ex.Message}", "SewerStudio", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BrowsePdfToText()
    {
        var p = _dialogs.OpenFile("pdftotext.exe waehlen", "pdftotext.exe|pdftotext.exe|Alle Dateien|*.*");
        if (p is null) return;
        PdfToTextPath = p;
    }

    private void BrowseProjectPath()
    {
        var currentName = string.IsNullOrWhiteSpace(ProjectPath)
            ? "Projekt"
            : Path.GetFileNameWithoutExtension(ProjectPath);

        var p = _dialogs.SaveFile("Projektpfad waehlen", "Projekt (*.json)|*.json", ".json", currentName);
        if (p is null)
            return;

        ProjectPath = p;
    }

    private void BrowseVideoFolder()
    {
        var p = _dialogs.SelectFolder("Video-Ordner (Haltungen) waehlen", VideoFolder);
        if (p is null) return;
        VideoFolder = p;
    }

    private void Save()
    {
        _settings.EnableDiagnostics = EnableDiagnostics;
        _settings.PdfToTextPath = PdfToTextPath;
        _settings.LastProjectPath = NormalizeProjectPath(ProjectPath);
        _settings.LastVideoSourceFolder = VideoFolder;
        _settings.LastVideoFolder = VideoFolder; // legacy compatibility
        _settings.DataAutoSaveMode = DataAutoSaveMode.Normalize();
        _settings.EnableRestorePoints = EnableRestorePoints;
        _settings.ShowExpertenmodusFeatures = ShowExpertenmodusFeatures;
        _settings.VideoHwDecoding = VideoHwDecoding;
        _settings.VideoDropLateFrames = VideoDropLateFrames;
        _settings.VideoSkipFrames = VideoSkipFrames;
        _settings.VideoFileCachingMs = ClampCaching(VideoFileCachingMs);
        _settings.VideoNetworkCachingMs = ClampCaching(VideoNetworkCachingMs);
        _settings.VideoCodecThreads = ClampCodecThreads(VideoCodecThreads);
        _settings.VideoOutput = NormalizeVideoOutput(VideoOutput);
        _settings.UiTheme = ThemeManager.NormalizeTheme(UiTheme);
        _settings.Save();

        _diagnostics.EnableDiagnostics = EnableDiagnostics;
        _diagnostics.ExplicitPdfToTextPath = PdfToTextPath;
    }

    private void ApplyTheme()
    {
        _settings.UiTheme = ThemeManager.NormalizeTheme(UiTheme);
        _settings.SaveImmediate();

        var restart = MessageBox.Show(
            "Design gespeichert.\n\nJetzt neu starten, damit das Theme vollstaendig angewendet wird?",
            "SewerStudio",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (restart != MessageBoxResult.Yes)
            return;

        try
        {
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(exePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true
                });
            }
        }
        catch
        {
            // best effort restart; app is still configured even if restart launch fails
        }

        System.Windows.Application.Current.Shutdown();
    }

    private static string? NormalizeProjectPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        if (!trimmed.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            trimmed += ".json";
        return trimmed;
    }

    private static int ClampCaching(int value)
        => Math.Clamp(value, 100, 10000);

    private static int ClampCodecThreads(int value)
        => Math.Clamp(value, 1, 16);

    private static string NormalizeVideoOutput(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "direct3d11";

        var normalized = value.Trim().ToLowerInvariant();
        return normalized is "direct3d11" or "direct3d9" or "any"
            ? normalized
            : "direct3d11";
    }

    private async Task ExportBackupAsync()
    {
        var defaultName = $"SewerStudio_KI_Backup_{DateTime.Now:yyyy-MM-dd}";
        var path = _dialogs.SaveFile(
            "KI-Wissen exportieren",
            "ZIP-Archiv (*.zip)|*.zip",
            ".zip",
            defaultName);
        if (path is null) return;

        BackupStatusText = "Exportiere...";
        try
        {
            var result = await KnowledgeBackupService.ExportAsync(
                path, new Progress<string>(msg => BackupStatusText = msg));

            if (result.Success)
            {
                var sizeMb = result.SizeBytes / (1024.0 * 1024.0);
                BackupStatusText = $"Export OK: {result.FileCount} Dateien, {sizeMb:F1} MB";
                MessageBox.Show(
                    $"KI-Wissen erfolgreich exportiert.\n\n" +
                    $"Dateien: {result.FileCount}\n" +
                    $"Groesse: {sizeMb:F1} MB\n" +
                    $"Pfad: {path}",
                    "SewerStudio", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                BackupStatusText = $"Fehler: {result.Error}";
                MessageBox.Show($"Export fehlgeschlagen:\n{result.Error}",
                    "SewerStudio", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            BackupStatusText = $"Fehler: {ex.Message}";
        }
    }

    private async Task ImportBackupAsync()
    {
        var path = _dialogs.OpenFile(
            "KI-Wissen importieren",
            "ZIP-Archiv (*.zip)|*.zip");
        if (path is null) return;

        var confirm = MessageBox.Show(
            "Vorhandene KI-Daten und Einstellungen werden ueberschrieben.\n\n" +
            "Nach dem Import muss die Anwendung neu gestartet werden.\n\n" +
            "Fortfahren?",
            "SewerStudio", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        BackupStatusText = "Importiere...";
        try
        {
            var result = await KnowledgeBackupService.ImportAsync(
                path, new Progress<string>(msg => BackupStatusText = msg));

            if (result.Success)
            {
                BackupStatusText = $"Import OK: {result.FileCount} Dateien";
                MessageBox.Show(
                    $"KI-Wissen erfolgreich importiert ({result.FileCount} Dateien).\n\n" +
                    "Bitte starten Sie die Anwendung neu.",
                    "SewerStudio", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                BackupStatusText = $"Fehler: {result.Error}";
                MessageBox.Show($"Import fehlgeschlagen:\n{result.Error}",
                    "SewerStudio", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            BackupStatusText = $"Fehler: {ex.Message}";
        }
    }

    public sealed record AutoSaveModeOption(AutoSaveMode Value, string Label);
    public sealed record IntOption(int Value, string Label);
    public sealed record StringOption(string Value, string Label);
}
