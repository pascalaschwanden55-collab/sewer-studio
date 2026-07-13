using System.IO;

namespace AuswertungPro.Next.UI.Services;

/// <summary>Ermittelt und öffnet Importberichte ausschließlich über den sicheren Shell-Öffner.</summary>
internal sealed class ImportReportNavigationController
{
    private readonly IDialogService _dialogs;
    private readonly Func<string?> _getProjectPath;
    private readonly Func<string, bool> _tryOpen;
    private string? _lastReportPath;

    public ImportReportNavigationController(
        IDialogService dialogs,
        Func<string?> getProjectPath,
        Func<string, bool> tryOpen)
    {
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _getProjectPath = getProjectPath ?? throw new ArgumentNullException(nameof(getProjectPath));
        _tryOpen = tryOpen ?? throw new ArgumentNullException(nameof(tryOpen));
    }

    public string? GetReportDirectory()
    {
        var projectPath = _getProjectPath();
        var projectDirectory = string.IsNullOrWhiteSpace(projectPath)
            ? null
            : Path.GetDirectoryName(projectPath);
        return string.IsNullOrWhiteSpace(projectDirectory)
            ? null
            : Path.Combine(projectDirectory, "__IMPORT_REPORTS");
    }

    public void SetLastReportPath(string? path)
        => _lastReportPath = path;

    public void OpenLastReport()
    {
        if (!string.IsNullOrWhiteSpace(_lastReportPath) && File.Exists(_lastReportPath))
        {
            _ = _tryOpen(_lastReportPath!);
            return;
        }

        OpenReportFolder();
    }

    public void OpenReportFolder()
    {
        var directory = GetReportDirectory();
        if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
        {
            _ = _tryOpen(directory!);
            return;
        }

        _dialogs.Info(
            "Bericht-Ordner nicht vorhanden.\nBitte zuerst einen Import durchfuehren.",
            "Import-Berichte");
    }
}
