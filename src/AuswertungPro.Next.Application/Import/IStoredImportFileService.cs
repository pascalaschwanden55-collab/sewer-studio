namespace AuswertungPro.Next.Application.Import;

public sealed record StoredImportFilesResult(
    bool MissingProjectPath,
    IReadOnlyList<string> StoredRelativePaths);

public interface IStoredImportFileService
{
    StoredImportFilesResult Store(
        string? projectPath,
        IDictionary<string, string> metadata,
        string importKind,
        IReadOnlyCollection<string> paths,
        Func<DateTime>? now = null);
}
