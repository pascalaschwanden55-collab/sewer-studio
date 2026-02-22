using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class SettingsPageViewModel : ObservableObject
{
    private readonly ServiceProvider _sp;

    [ObservableProperty] private bool _enableDiagnostics;
    [ObservableProperty] private string? _pdfToTextPath;
    [ObservableProperty] private string? _projectPath;
    [ObservableProperty] private string? _videoFolder;
    [ObservableProperty] private AutoSaveMode _dataAutoSaveMode;
    [ObservableProperty] private bool _enableRestorePoints;
    [ObservableProperty] private bool _videoHwDecoding;
    [ObservableProperty] private bool _videoDropLateFrames;
    [ObservableProperty] private bool _videoSkipFrames;
    [ObservableProperty] private int _videoFileCachingMs;
    [ObservableProperty] private int _videoNetworkCachingMs;
    [ObservableProperty] private int _videoCodecThreads;
    [ObservableProperty] private string _videoOutput = "direct3d11";

    [ObservableProperty] private string _dataFolderPath = string.Empty;
    [ObservableProperty] private string _logsFolderPath = string.Empty;
    [ObservableProperty] private string _restorePointsFolderPath = string.Empty;

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
    public IRelayCommand SaveCommand { get; }

    public SettingsPageViewModel(ServiceProvider sp)
    {
        _sp = sp;
        EnableDiagnostics = _sp.Settings.EnableDiagnostics;
        PdfToTextPath = _sp.Settings.PdfToTextPath;
        ProjectPath = _sp.Settings.LastProjectPath;
        VideoFolder = _sp.Settings.LastVideoSourceFolder ?? _sp.Settings.LastVideoFolder;
        DataAutoSaveMode = _sp.Settings.DataAutoSaveMode.Normalize();
        EnableRestorePoints = _sp.Settings.EnableRestorePoints;
        VideoHwDecoding = _sp.Settings.VideoHwDecoding;
        VideoDropLateFrames = _sp.Settings.VideoDropLateFrames;
        VideoSkipFrames = _sp.Settings.VideoSkipFrames;
        VideoFileCachingMs = ClampCaching(_sp.Settings.VideoFileCachingMs);
        VideoNetworkCachingMs = ClampCaching(_sp.Settings.VideoNetworkCachingMs);
        VideoCodecThreads = ClampCodecThreads(_sp.Settings.VideoCodecThreads);
        VideoOutput = NormalizeVideoOutput(_sp.Settings.VideoOutput);

        DataFolderPath = AppSettings.AppDataDir;
        LogsFolderPath = Path.Combine(AppSettings.AppDataDir, "logs");
        RestorePointsFolderPath = RestorePointService.SettingsRestoreRoot;

        BrowsePdfToTextCommand = new RelayCommand(BrowsePdfToText);
        BrowseProjectPathCommand = new RelayCommand(BrowseProjectPath);
        BrowseVideoFolderCommand = new RelayCommand(BrowseVideoFolder);
        OpenDataFolderCommand = new RelayCommand(OpenDataFolder);
        OpenLogsFolderCommand = new RelayCommand(OpenLogsFolder);
        OpenRestorePointsFolderCommand = new RelayCommand(OpenRestorePointsFolder);
        SaveCommand = new RelayCommand(Save);
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
        var p = _sp.Dialogs.OpenFile("pdftotext.exe waehlen", "pdftotext.exe|pdftotext.exe|Alle Dateien|*.*");
        if (p is null) return;
        PdfToTextPath = p;
    }

    private void BrowseProjectPath()
    {
        var currentName = string.IsNullOrWhiteSpace(ProjectPath)
            ? "Projekt"
            : Path.GetFileNameWithoutExtension(ProjectPath);

        var p = _sp.Dialogs.SaveFile("Projektpfad waehlen", "Projekt (*.json)|*.json", ".json", currentName);
        if (p is null)
            return;

        ProjectPath = p;
    }

    private void BrowseVideoFolder()
    {
        var p = _sp.Dialogs.SelectFolder("Video-Ordner (Haltungen) waehlen", VideoFolder);
        if (p is null) return;
        VideoFolder = p;
    }

    private void Save()
    {
        _sp.Settings.EnableDiagnostics = EnableDiagnostics;
        _sp.Settings.PdfToTextPath = PdfToTextPath;
        _sp.Settings.LastProjectPath = NormalizeProjectPath(ProjectPath);
        _sp.Settings.LastVideoSourceFolder = VideoFolder;
        _sp.Settings.LastVideoFolder = VideoFolder; // legacy compatibility
        _sp.Settings.DataAutoSaveMode = DataAutoSaveMode.Normalize();
        _sp.Settings.EnableRestorePoints = EnableRestorePoints;
        _sp.Settings.VideoHwDecoding = VideoHwDecoding;
        _sp.Settings.VideoDropLateFrames = VideoDropLateFrames;
        _sp.Settings.VideoSkipFrames = VideoSkipFrames;
        _sp.Settings.VideoFileCachingMs = ClampCaching(VideoFileCachingMs);
        _sp.Settings.VideoNetworkCachingMs = ClampCaching(VideoNetworkCachingMs);
        _sp.Settings.VideoCodecThreads = ClampCodecThreads(VideoCodecThreads);
        _sp.Settings.VideoOutput = NormalizeVideoOutput(VideoOutput);
        _sp.Settings.Save();

        _sp.Diagnostics.EnableDiagnostics = EnableDiagnostics;
        _sp.Diagnostics.ExplicitPdfToTextPath = PdfToTextPath;
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

    public sealed record AutoSaveModeOption(AutoSaveMode Value, string Label);
    public sealed record IntOption(int Value, string Label);
    public sealed record StringOption(string Value, string Label);
}
