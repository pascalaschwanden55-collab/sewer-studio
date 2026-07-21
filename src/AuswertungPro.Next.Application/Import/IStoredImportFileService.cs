namespace AuswertungPro.Next.Application.Import;

public sealed record StoredImportFilesResult(
    bool MissingProjectPath,
    IReadOnlyList<string> StoredRelativePaths)
{
    public IReadOnlyList<StoredImportFileError> Errors { get; init; } = [];
}

public sealed record StoredImportFileError(string SourcePath, string Message);

public interface IStoredImportFileService
{
    StoredImportFilesResult Store(
        string? projectPath,
        IDictionary<string, string> metadata,
        string importKind,
        IReadOnlyCollection<string> paths,
        Func<DateTime>? now = null);

    StoredImportFilesResult StoreStaged(
        string? projectPath,
        IDictionary<string, string> metadata,
        string importKind,
        IReadOnlyCollection<string> paths,
        IImportFileStagingSession fileStaging,
        CancellationToken cancellationToken = default,
        Func<DateTime>? now = null)
        => throw new NotSupportedException(
            "Dieser Importdatei-Dienst unterstuetzt noch kein Datei-Staging.");
}
