using System.Globalization;
using System.Text;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Settings;

/// <summary>
/// Instanzbasierter Datei-Dienst fuer Rettungspunkte der Programmeinstellungen.
/// Gleichzeitige Aufrufe derselben Instanz werden serialisiert.
/// </summary>
public sealed class SettingsRestorePointStore : ISettingsRestorePointStore
{
    public const int MaxRestorePointsPerScope = 20;

    private readonly object _sync = new();

    public void TryCreate(string sourceFilePath, string restoreRoot, string scopeName)
    {
        lock (_sync)
        {
            TryCreateCore(sourceFilePath, restoreRoot, scopeName);
        }
    }

    private static void TryCreateCore(
        string sourceFilePath,
        string restoreRoot,
        string scopeName)
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
            var scopeDirectory = Path.Combine(restoreRoot, safeScope);

            Directory.CreateDirectory(scopeDirectory);

            var stamp = DateTime.UtcNow.ToString(
                "yyyyMMdd-HHmmssfff",
                CultureInfo.InvariantCulture);
            var destinationFile = EnsureUniqueSnapshotPath(
                scopeDirectory,
                stamp,
                safeSourceName);
            File.Copy(sourceFilePath, destinationFile, overwrite: false);

            PruneOldSnapshots(scopeDirectory);
        }
        catch
        {
            // Rettungspunkte duerfen den eigentlichen Speichervorgang nie abbrechen.
        }
    }

    private static string EnsureUniqueSnapshotPath(
        string scopeDirectory,
        string stamp,
        string safeSourceName)
    {
        var desiredPath = Path.Combine(
            scopeDirectory,
            $"{stamp}_{safeSourceName}");
        if (!File.Exists(desiredPath))
            return desiredPath;

        for (var suffix = 2; ; suffix++)
        {
            var candidate = Path.Combine(
                scopeDirectory,
                $"{stamp}_{suffix}_{safeSourceName}");
            if (!File.Exists(candidate))
                return candidate;
        }
    }

    private static void PruneOldSnapshots(string scopeDirectory)
    {
        var files = Directory
            .EnumerateFiles(scopeDirectory, "*", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .OrderByDescending(info => info.CreationTimeUtc)
            .ThenByDescending(info => info.Name, StringComparer.Ordinal)
            .ToList();

        foreach (var file in files.Skip(MaxRestorePointsPerScope))
        {
            try
            {
                file.Delete();
            }
            catch
            {
                // Aufraeumfehler duerfen einen Rettungspunkt nicht entwerten.
            }
        }
    }

    private static string SanitizeSegment(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);

        foreach (var character in value.Trim())
            builder.Append(invalid.Contains(character) ? '_' : character);

        var sanitized = builder.ToString().Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? fallback : sanitized;
    }
}
