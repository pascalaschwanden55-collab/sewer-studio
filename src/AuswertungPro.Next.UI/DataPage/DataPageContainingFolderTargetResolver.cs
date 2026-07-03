using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

public static class DataPageContainingFolderTargetResolver
{
    public static string? Resolve(
        HaltungRecord? record,
        Func<string?, string?> resolveExistingPath,
        Func<HaltungRecord, string?> ensureProtocolPath,
        Func<string?> getProjectFolder,
        Func<HaltungRecord, string, List<string>> resolveOriginalPdfPaths)
    {
        if (record is null)
            return null;

        ArgumentNullException.ThrowIfNull(resolveExistingPath);
        ArgumentNullException.ThrowIfNull(ensureProtocolPath);
        ArgumentNullException.ThrowIfNull(getProjectFolder);
        ArgumentNullException.ThrowIfNull(resolveOriginalPdfPaths);

        var target = resolveExistingPath(record.GetFieldValue(FieldKeys.Link))
                     ?? ensureProtocolPath(record);

        if (!string.IsNullOrWhiteSpace(target))
            return target;

        var projectFolder = getProjectFolder() ?? string.Empty;
        var paths = resolveOriginalPdfPaths(record, projectFolder);
        return paths.Count > 0 ? paths[0] : null;
    }
}
