using System.IO;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Player;
using AuswertungPro.Next.UI.ViewModels;

namespace AuswertungPro.Next.UI.Dossiers;

/// <summary>
/// Verbindet die Dossier-Aktionen mit denselben Video- und PDF-Diensten wie
/// die Seite "Haltungen". Die fachliche Dossier-Ansicht kennt dadurch keine Fenster.
/// </summary>
internal static class DossierHoldingActionFactory
{
    public static DossierHoldingActionController Create(
        ShellViewModel shell,
        ServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(services);

        var media = new DossierHoldingMediaController(shell, services);
        return new DossierHoldingActionController(
            () => shell.Project,
            services.Dialogs,
            media.PlayVideo,
            media.OpenProtocol,
            shell.NavigateToHolding);
    }
}

internal sealed class DossierHoldingMediaController
{
    private readonly ShellViewModel _shell;
    private readonly AppSettings _settings;
    private readonly IDialogService _dialogs;
    private readonly IInspectionProtocolFileLocator _protocolFiles;
    private readonly ISafeShellOpenService _shellOpen;
    private readonly DataPageVideoPlaybackController _videoPlayback;
    private readonly DataPageOriginalPdfController _originalPdf;

    public DossierHoldingMediaController(
        ShellViewModel shell,
        ServiceProvider services)
    {
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        ArgumentNullException.ThrowIfNull(services);

        _settings = services.Settings;
        _dialogs = services.Dialogs;
        _protocolFiles = services.InspectionProtocolFiles;
        _shellOpen = services.ShellOpen;

        _videoPlayback = new DataPageVideoPlaybackController(
            _dialogs,
            EnsureVideoPath,
            () => PlayerWindowOptions.FromSettings(_settings),
            DataPageVideoOverlayBuilder.Build,
            services.DataPageWindows.ShowPlayer,
            services.VideoStartErrorLogs.TryWrite);

        _originalPdf = new DataPageOriginalPdfController(
            _dialogs,
            EnsureProtocolPath,
            _shell.GetProjectFolder,
            _protocolFiles.ResolveOriginalPdfPaths,
            TryOpenFile);
    }

    public void PlayVideo(HaltungRecord record)
        => _videoPlayback.Play(record);

    public void OpenProtocol(HaltungRecord record)
        => _originalPdf.Open(record);

    private string? EnsureVideoPath(HaltungRecord record)
        => DataPageVideoPathWorkflowController.Resolve(
            record,
            record.GetFieldValue(FieldKeys.Link),
            GetInitialMediaFolder(),
            ResolveExistingPath,
            Directory.Exists,
            DataPageVideoPathWorkflowController.ResolveWithVideoSearchTool,
            (title, initialFolder) => _dialogs.SelectFolder(title, initialFolder),
            PersistVideoFolder,
            (message, title) => _dialogs.Info(message, title),
            (title, filter, initialFolder) => _dialogs.OpenFile(title, filter, initialFolder),
            (path, userEdited) => SaveVideoLink(record, path, userEdited));

    private string? EnsureProtocolPath(HaltungRecord record)
    {
        var resolvedLink = ResolveExistingPath(record.GetFieldValue(FieldKeys.Link));
        var storedFilesRaw = _shell.Project.Metadata.TryGetValue("PDF_StoredFiles", out var raw)
            ? raw
            : null;

        return _protocolFiles.FindProtocolPath(
            record,
            resolvedLink,
            GetInitialMediaFolder(),
            _settings.LastProjectPath,
            storedFilesRaw);
    }

    private string? GetInitialMediaFolder()
        => !string.IsNullOrWhiteSpace(_settings.LastVideoSourceFolder)
            ? _settings.LastVideoSourceFolder
            : !string.IsNullOrWhiteSpace(_settings.LastVideoFolder)
                ? _settings.LastVideoFolder
                : _shell.GetProjectFolder();

    private string? ResolveExistingPath(string? raw)
        => _protocolFiles.ResolveExistingPath(raw, _settings.LastProjectPath);

    private void PersistVideoFolder(string folder)
    {
        _settings.LastVideoSourceFolder = folder;
        _settings.LastVideoFolder = folder;
        _settings.Save();
    }

    private string SaveVideoLink(HaltungRecord record, string path, bool userEdited)
    {
        var storedPath = ProjectPathResolver.MakeRelativeIfInsideProject(
            path,
            _shell.GetProjectFolder());
        record.SetFieldValue(FieldKeys.Link, storedPath, FieldSource.Unknown, userEdited);
        _shell.MarkProjectDirty(record);
        return path;
    }

    private (bool Success, string? Error) TryOpenFile(string? path)
        => _shellOpen.TryOpen(path, out var error)
            ? (true, null)
            : (false, error);
}
