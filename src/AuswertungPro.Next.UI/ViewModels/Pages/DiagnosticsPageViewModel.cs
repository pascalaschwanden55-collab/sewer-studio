using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Diagnostics;
using AuswertungPro.Next.UI.Settings;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

public sealed partial class DiagnosticsPageViewModel : ObservableObject
{
    private readonly ILogTailReader _logTailReader;
    private readonly IDiagnosticsPackageService? _diagnosticsPackage;
    private readonly IDialogService? _dialogs;
    private readonly IFolderOpenService _folderOpen;

    [ObservableProperty] private string _logTail = "";
    [ObservableProperty] private string _packageStatus = "";

    public IRelayCommand RefreshCommand { get; }
    public IRelayCommand OpenLogFolderCommand { get; }
    public IAsyncRelayCommand CreatePackageCommand { get; }

    public DiagnosticsPageViewModel(ILogTailReader logTailReader)
        : this(logTailReader, diagnosticsPackage: null, dialogs: null)
    {
    }

    public DiagnosticsPageViewModel(
        ILogTailReader logTailReader,
        IDiagnosticsPackageService? diagnosticsPackage,
        IDialogService? dialogs,
        IFolderOpenService? folderOpen = null)
    {
        _logTailReader = logTailReader ?? throw new ArgumentNullException(nameof(logTailReader));
        _diagnosticsPackage = diagnosticsPackage;
        _dialogs = dialogs;
        _folderOpen = folderOpen ?? SettingsPathWorkflow.CompatibilityService;
        RefreshCommand = new RelayCommand(Refresh);
        OpenLogFolderCommand = new RelayCommand(
            OpenLogFolder,
            () => _diagnosticsPackage is not null && _dialogs is not null);
        CreatePackageCommand = new AsyncRelayCommand(
            CreatePackageAsync,
            () => _diagnosticsPackage is not null && _dialogs is not null);
        Refresh();
    }

    private void OpenLogFolder()
    {
        if (_diagnosticsPackage is null || _dialogs is null)
            return;

        SettingsPathWorkflow.OpenFolder(
            _diagnosticsPackage.LogDirectory,
            _dialogs,
            _folderOpen);
    }

    private async Task CreatePackageAsync()
    {
        if (_diagnosticsPackage is null || _dialogs is null)
            return;

        var destination = _dialogs.SaveFile(
            "Diagnosepaket speichern",
            "ZIP-Datei (*.zip)|*.zip",
            defaultExt: ".zip",
            defaultFileName: $"SewerStudio-Diagnose-{DateTime.Now:yyyyMMdd-HHmm}.zip");
        if (string.IsNullOrWhiteSpace(destination))
            return;

        PackageStatus = "Diagnosepaket wird erstellt ...";
        try
        {
            var result = await _diagnosticsPackage.CreateAsync(destination);
            PackageStatus = result.Success && !string.IsNullOrWhiteSpace(result.PackagePath)
                ? $"{result.UserMessage}  {result.PackagePath}"
                : result.UserMessage;

            if (result.Success)
                _dialogs.Info(PackageStatus, "Diagnosepaket");
            else
                _dialogs.Warn(result.UserMessage, "Diagnosepaket");
        }
        catch (OperationCanceledException)
        {
            PackageStatus = "Diagnosepaket wurde abgebrochen.";
        }
        catch (Exception ex)
        {
            BestEffort.ReportWarning($"[Diagnose] Diagnosepaket fehlgeschlagen: {ex}");
            PackageStatus = "Diagnosepaket konnte nicht erstellt werden. Details stehen im Programmlog.";
            _dialogs.Warn(PackageStatus, "Diagnosepaket");
        }
    }

    private void Refresh()
    {
        try
        {
            var result = _logTailReader.ReadToday(maximumLines: 200);
            if (!string.IsNullOrWhiteSpace(result.UserMessage))
            {
                LogTail = result.UserMessage;
                return;
            }

            if (!result.FileExists)
            {
                LogTail = "Noch keine Log-Datei vorhanden.";
                return;
            }

            LogTail = string.Join(Environment.NewLine, result.Lines);
        }
        catch (Exception ex)
        {
            BestEffort.ReportWarning($"[Diagnose] Log-Anzeige fehlgeschlagen: {ex}");
            LogTail = "Tageslog konnte nicht gelesen werden. Details stehen im Programmlog.";
        }
    }
}
