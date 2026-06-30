using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Media;

namespace AuswertungPro.Next.UI.DataPage;

public static class DataPageVideoPathWorkflowController
{
    public static VideoResolveResult ResolveWithVideoSearchTool(string folder, HaltungRecord record)
    {
        var tool = new VideoSearchTool(folder);
        return tool.ResolveForRecord(record);
    }

    public static string? Resolve(
        HaltungRecord record,
        string? rawLink,
        string? initialFolder,
        Func<string?, string?> resolveExistingPath,
        Func<string, bool> directoryExists,
        Func<string, HaltungRecord, VideoResolveResult> resolveVideoInFolder,
        Func<string, string?, string?> selectFolder,
        Action<string> persistSelectedFolder,
        Action<string, string> showInfo,
        Func<string, string, string?, string?> openFile,
        Func<string, bool, string> saveVideoLink)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(resolveExistingPath);
        ArgumentNullException.ThrowIfNull(directoryExists);
        ArgumentNullException.ThrowIfNull(resolveVideoInFolder);
        ArgumentNullException.ThrowIfNull(selectFolder);
        ArgumentNullException.ThrowIfNull(persistSelectedFolder);
        ArgumentNullException.ThrowIfNull(showInfo);
        ArgumentNullException.ThrowIfNull(openFile);
        ArgumentNullException.ThrowIfNull(saveVideoLink);

        var resolved = resolveExistingPath(rawLink);
        if (!string.IsNullOrWhiteSpace(resolved))
        {
            if (!string.Equals(resolved, rawLink?.Trim(), StringComparison.OrdinalIgnoreCase))
                saveVideoLink(resolved, false);

            return resolved;
        }

        if (!string.IsNullOrWhiteSpace(initialFolder) && directoryExists(initialFolder))
        {
            var initialResult = resolveVideoInFolder(initialFolder, record);
            if (initialResult.Success && !string.IsNullOrWhiteSpace(initialResult.VideoPath))
                return saveVideoLink(initialResult.VideoPath!, false);
        }

        var folder = selectFolder("Video-Ordner auswaehlen", initialFolder);
        if (string.IsNullOrWhiteSpace(folder))
            return null;

        persistSelectedFolder(folder);

        var selectedResult = resolveVideoInFolder(folder, record);
        if (selectedResult.Success && !string.IsNullOrWhiteSpace(selectedResult.VideoPath))
            return saveVideoLink(selectedResult.VideoPath!, false);

        showInfo(selectedResult.Message, "Video");

        var manual = openFile(
            "Video auswaehlen",
            MediaFileTypes.VideoDialogFilter,
            folder);
        if (string.IsNullOrWhiteSpace(manual))
            return null;

        return saveVideoLink(manual, true);
    }
}
