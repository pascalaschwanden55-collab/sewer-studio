using System.Globalization;
using System.IO;
using System.Text;

namespace AuswertungPro.Next.UI.Services;

public static class RestorePointService
{
    public const int MaxRestorePointsPerScope = 20;

    public static string SettingsRestoreRoot =>
        Path.Combine(AppSettings.AppDataDir, "restore-points");

    public static void TryCreate(string sourceFilePath, string restoreRoot, string scopeName)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath))
            return;
        if (!File.Exists(sourceFilePath))
            return;
        if (string.IsNullOrWhiteSpace(restoreRoot))
            return;

        try
        {
            var safeScope = SanitizeSegment(scopeName, "default");
            var sourceName = Path.GetFileName(sourceFilePath);
            var safeSourceName = SanitizeSegment(sourceName, "snapshot.json");
            var scopeDir = Path.Combine(restoreRoot, safeScope);

            Directory.CreateDirectory(scopeDir);

            var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture);
            var destinationFile = Path.Combine(scopeDir, $"{stamp}_{safeSourceName}");
            File.Copy(sourceFilePath, destinationFile, overwrite: false);

            PruneOldSnapshots(scopeDir);
        }
        catch
        {
            // Restore points must never break save flows.
        }
    }

    private static void PruneOldSnapshots(string scopeDir)
    {
        var files = Directory
            .EnumerateFiles(scopeDir, "*", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .OrderByDescending(info => info.CreationTimeUtc)
            .ToList();

        if (files.Count <= MaxRestorePointsPerScope)
            return;

        foreach (var file in files.Skip(MaxRestorePointsPerScope))
        {
            try
            {
                file.Delete();
            }
            catch
            {
                // Ignore cleanup errors.
            }
        }
    }

    private static string SanitizeSegment(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);

        foreach (var c in value.Trim())
        {
            builder.Append(invalid.Contains(c) ? '_' : c);
        }

        var sanitized = builder.ToString().Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? fallback : sanitized;
    }
}
