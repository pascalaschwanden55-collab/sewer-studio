using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Import;

namespace AuswertungPro.Next.UI.Services;

public sealed record StoredImportFileRegistryResult(
    bool MissingProjectPath,
    IReadOnlyList<string> StoredRelativePaths)
{
    public IReadOnlyList<StoredImportFileError> Errors { get; init; } = [];
}

public static class StoredImportFileRegistry
{
    private static readonly IStoredImportFileService DefaultService = new StoredImportFileService();

    internal static IStoredImportFileService CompatibilityService => DefaultService;

    public static StoredImportFileRegistryResult Store(
        string? projectPath,
        IDictionary<string, string> metadata,
        string importKind,
        IReadOnlyCollection<string> paths,
        Func<DateTime>? now = null)
    {
        var result = DefaultService.Store(projectPath, metadata, importKind, paths, now);
        return new StoredImportFileRegistryResult(
            result.MissingProjectPath,
            result.StoredRelativePaths)
        {
            Errors = result.Errors
        };
    }
}
