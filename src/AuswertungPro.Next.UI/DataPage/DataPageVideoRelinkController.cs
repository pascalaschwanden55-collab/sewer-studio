using System.IO;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Media;

namespace AuswertungPro.Next.UI.DataPage;

public sealed class DataPageVideoRelinkController
{
    private readonly IDialogService _dialogs;
    private readonly Func<string?> _getLastVideoSourceFolder;
    private readonly Func<string?> _getLastVideoFolder;
    private readonly Func<string?> _getLastProjectPath;
    private readonly Action<string> _persistSelectedFolder;
    private readonly Action<HaltungRecord, string, bool> _saveVideoLink;

    public DataPageVideoRelinkController(
        IDialogService dialogs,
        Func<string?> getLastVideoSourceFolder,
        Func<string?> getLastVideoFolder,
        Func<string?> getLastProjectPath,
        Action<string> persistSelectedFolder,
        Action<HaltungRecord, string, bool> saveVideoLink)
    {
        _dialogs = dialogs ?? throw new ArgumentNullException(nameof(dialogs));
        _getLastVideoSourceFolder = getLastVideoSourceFolder ?? throw new ArgumentNullException(nameof(getLastVideoSourceFolder));
        _getLastVideoFolder = getLastVideoFolder ?? throw new ArgumentNullException(nameof(getLastVideoFolder));
        _getLastProjectPath = getLastProjectPath ?? throw new ArgumentNullException(nameof(getLastProjectPath));
        _persistSelectedFolder = persistSelectedFolder ?? throw new ArgumentNullException(nameof(persistSelectedFolder));
        _saveVideoLink = saveVideoLink ?? throw new ArgumentNullException(nameof(saveVideoLink));
    }

    public void Relink(HaltungRecord? record)
    {
        if (record is null)
            return;

        var path = _dialogs.OpenFile(
            "Video auswaehlen",
            MediaFileTypes.VideoDialogFilter,
            BuildInitialFolder());
        if (string.IsNullOrWhiteSpace(path))
            return;

        var selectedDir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(selectedDir))
            _persistSelectedFolder(selectedDir);

        _saveVideoLink(record, path, true);
    }

    private string? BuildInitialFolder()
    {
        var sourceFolder = _getLastVideoSourceFolder();
        if (!string.IsNullOrWhiteSpace(sourceFolder))
            return sourceFolder;

        var legacyFolder = _getLastVideoFolder();
        if (!string.IsNullOrWhiteSpace(legacyFolder))
            return legacyFolder;

        var projectPath = _getLastProjectPath();
        return string.IsNullOrWhiteSpace(projectPath)
            ? null
            : Path.GetDirectoryName(projectPath);
    }
}
