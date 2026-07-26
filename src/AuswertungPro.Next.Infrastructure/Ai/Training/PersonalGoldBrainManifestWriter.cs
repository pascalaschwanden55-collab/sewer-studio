using System.Security.Cryptography;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

internal static class PersonalGoldBrainManifestWriter
{
    public const string RelativePath =
        "training/gold_standard/gold_brain_files_v1.json";

    public static async Task WriteAsync(
        string knowledgeRoot,
        CancellationToken cancellationToken)
    {
        var manifestPath = Path.Combine(
            knowledgeRoot,
            "training",
            "gold_standard",
            "gold_brain_files_v1.json");
        var files = new List<object>();
        foreach (var path in Directory
                     .EnumerateFiles(knowledgeRoot, "*", SearchOption.AllDirectories)
                     .Where(path => !string.Equals(
                         path,
                         manifestPath,
                         StringComparison.OrdinalIgnoreCase))
                     .Where(path => !path.EndsWith("-wal", StringComparison.OrdinalIgnoreCase)
                                    && !path.EndsWith("-shm", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            files.Add(new
            {
                path = Path.GetRelativePath(knowledgeRoot, path).Replace('\\', '/'),
                bytes = new FileInfo(path).Length,
                sha256 = await HashStableSharedAsync(path, cancellationToken)
                    .ConfigureAwait(false)
            });
        }

        await PersonalGoldBrainFileService
            .WriteJsonAsync(
                manifestPath,
                new
                {
                    schema_version = 1,
                    files
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<string> HashStableSharedAsync(
        string path,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var before = new FileInfo(path);
            var beforeLength = before.Length;
            var beforeWriteUtc = before.LastWriteTimeUtc;
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                1024 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var hash = await SHA256
                .HashDataAsync(stream, cancellationToken)
                .ConfigureAwait(false);
            var after = new FileInfo(path);
            if (beforeLength == after.Length
                && beforeWriteUtc == after.LastWriteTimeUtc)
            {
                return Convert.ToHexStringLower(hash);
            }

            if (attempt < 3)
                await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken)
                    .ConfigureAwait(false);
        }

        throw new IOException($"Datei war waehrend der Manifest-Pruefung nicht stabil: {path}");
    }
}
