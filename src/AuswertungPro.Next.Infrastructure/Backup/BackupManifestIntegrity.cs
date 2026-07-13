using System.Security.Cryptography;
using System.Text.Json;

namespace AuswertungPro.Next.Infrastructure.Backup;

public sealed record BackupManifestFileEntry(string Path, long Length, string Sha256);

public sealed record BackupIntegrityIssue(string Path, string Message);

public sealed record BackupIntegrityReport(
    int CheckedFiles,
    IReadOnlyList<BackupIntegrityIssue> Issues)
{
    public bool IsValid => Issues.Count == 0;
}

/// <summary>Erzeugt und prueft die Inhaltsnachweise einer Datensicherung.</summary>
public static class BackupManifestIntegrity
{
    private static readonly string[] ManifestFiles = ["manifest.json", "manifest.json.bak"];

    public static async Task<IReadOnlyList<BackupManifestFileEntry>> CreateEntriesAsync(
        string backupRoot,
        CancellationToken ct = default)
        => await CreateEntriesAsync(backupRoot, onFileHashed: null, ct).ConfigureAwait(false);

    public static async Task<IReadOnlyList<BackupManifestFileEntry>> CreateEntriesAsync(
        string backupRoot,
        Action<string>? onFileHashed,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupRoot);
        var root = Path.GetFullPath(backupRoot);
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException($"Sicherungsordner nicht gefunden: {root}");

        var result = new List<BackupManifestFileEntry>();
        foreach (var file in EnumerateProtectedFiles(root))
        {
            ct.ThrowIfCancellationRequested();
            var infoBefore = new FileInfo(file);
            var hash = await HashFileAsync(file, ct).ConfigureAwait(false);
            var infoAfter = new FileInfo(file);
            if (infoBefore.Length != infoAfter.Length
                || infoBefore.LastWriteTimeUtc != infoAfter.LastWriteTimeUtc)
            {
                throw new IOException($"Datei wurde waehrend der Manifest-Pruefung geaendert: {file}");
            }

            result.Add(new BackupManifestFileEntry(
                NormalizeRelativePath(Path.GetRelativePath(root, file)),
                infoAfter.Length,
                Convert.ToHexString(hash)));
            onFileHashed?.Invoke(file);
        }

        return result
            .OrderBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static async Task<BackupIntegrityReport> VerifyAsync(
        string backupRoot,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupRoot);
        var root = Path.GetFullPath(backupRoot);
        var issues = new List<BackupIntegrityIssue>();
        var manifestPath = Path.Combine(root, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            issues.Add(new BackupIntegrityIssue("manifest.json", "Manifest fehlt."));
            return new BackupIntegrityReport(0, issues);
        }

        IReadOnlyList<BackupManifestFileEntry> entries;
        try
        {
            await using var stream = new FileStream(
                manifestPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                useAsync: true);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct)
                .ConfigureAwait(false);
            if (!document.RootElement.TryGetProperty("Files", out var filesElement)
                || filesElement.ValueKind != JsonValueKind.Array)
            {
                issues.Add(new BackupIntegrityIssue(
                    "manifest.json",
                    "Manifest enthaelt keine Datei-Hashes."));
                return new BackupIntegrityReport(0, issues);
            }

            entries = filesElement.EnumerateArray()
                .Select(ParseEntry)
                .ToArray();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is JsonException
                                   or IOException
                                   or UnauthorizedAccessException
                                   or InvalidOperationException
                                   or KeyNotFoundException
                                   or FormatException)
        {
            issues.Add(new BackupIntegrityIssue("manifest.json", $"Manifest nicht lesbar: {ex.Message}"));
            return new BackupIntegrityReport(0, issues);
        }

        var manifestPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var checkedFiles = 0;
        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();
            if (!manifestPaths.Add(entry.Path))
            {
                issues.Add(new BackupIntegrityIssue(entry.Path, "Pfad steht mehrfach im Manifest."));
                continue;
            }

            if (!TryResolveProtectedPath(root, entry.Path, out var fullPath))
            {
                issues.Add(new BackupIntegrityIssue(entry.Path, "Unsicherer oder ungueltiger Manifest-Pfad."));
                continue;
            }

            if (!File.Exists(fullPath))
            {
                issues.Add(new BackupIntegrityIssue(entry.Path, "Datei fehlt."));
                continue;
            }

            try
            {
                var info = new FileInfo(fullPath);
                if (info.Length != entry.Length)
                {
                    issues.Add(new BackupIntegrityIssue(
                        entry.Path,
                        $"Dateigroesse stimmt nicht ({info.Length} statt {entry.Length})."));
                    continue;
                }

                var actualHash = Convert.ToHexString(
                    await HashFileAsync(fullPath, ct).ConfigureAwait(false));
                checkedFiles++;
                if (!string.Equals(actualHash, entry.Sha256, StringComparison.OrdinalIgnoreCase))
                    issues.Add(new BackupIntegrityIssue(entry.Path, "SHA-256 stimmt nicht."));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                issues.Add(new BackupIntegrityIssue(entry.Path, $"Datei nicht pruefbar: {ex.Message}"));
            }
        }

        try
        {
            foreach (var file in EnumerateProtectedFiles(root))
            {
                var relative = NormalizeRelativePath(Path.GetRelativePath(root, file));
                if (!manifestPaths.Contains(relative))
                    issues.Add(new BackupIntegrityIssue(relative, "Datei steht nicht im Manifest."));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            issues.Add(new BackupIntegrityIssue(".", $"Sicherungsordner nicht vollstaendig lesbar: {ex.Message}"));
        }

        return new BackupIntegrityReport(checkedFiles, issues);
    }

    private static BackupManifestFileEntry ParseEntry(JsonElement element)
    {
        var path = element.GetProperty("Path").GetString() ?? string.Empty;
        var length = element.GetProperty("Length").GetInt64();
        var sha256 = element.GetProperty("Sha256").GetString() ?? string.Empty;
        return new BackupManifestFileEntry(path, length, sha256);
    }

    private static bool TryResolveProtectedPath(string root, string relativePath, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
            return false;

        try
        {
            var platformPath = relativePath
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);
            fullPath = Path.GetFullPath(Path.Combine(root, platformPath));
            var canonicalRelative = NormalizeRelativePath(Path.GetRelativePath(root, fullPath));
            return string.Equals(
                       NormalizeRelativePath(relativePath),
                       canonicalRelative,
                       StringComparison.OrdinalIgnoreCase)
                   && BackupTargetGuard.IsInsideBackupRoot(root, fullPath)
                   && !IsExcludedRelativePath(canonicalRelative);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static IEnumerable<string> EnumerateProtectedFiles(string root)
    {
        var stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            foreach (var file in Directory.EnumerateFiles(current)
                         .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                var relative = NormalizeRelativePath(Path.GetRelativePath(root, file));
                if (!IsExcludedRelativePath(relative))
                    yield return file;
            }

            foreach (var directory in Directory.EnumerateDirectories(current)
                .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase))
            {
                var relative = NormalizeRelativePath(Path.GetRelativePath(root, directory));
                if (!IsVersionsRelativePath(relative))
                    stack.Push(directory);
            }
        }
    }

    private static bool IsExcludedRelativePath(string relativePath)
    {
        if (ManifestFiles.Contains(relativePath, StringComparer.OrdinalIgnoreCase))
            return true;

        return IsVersionsRelativePath(relativePath);
    }

    private static bool IsVersionsRelativePath(string relativePath)
    {
        var versionsRoot = NormalizeRelativePath(
            AuswertungPro.Next.Application.Backup.BackupVersionRetention.VersionsFolderName);
        return string.Equals(relativePath, versionsRoot, StringComparison.OrdinalIgnoreCase)
               || relativePath.StartsWith(versionsRoot + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeRelativePath(string path)
        => path.Replace('\\', '/');

    private static async Task<byte[]> HashFileAsync(string path, CancellationToken ct)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 128 * 1024,
            useAsync: true);
        return await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
    }
}
