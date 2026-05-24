using System.Globalization;
using System.IO;

namespace AuswertungPro.Next.UI;

public static class LogFileRetentionService
{
    public static LogRetentionResult Apply(string logDirectory, TimeSpan maxAge, DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(logDirectory) || maxAge <= TimeSpan.Zero)
            return new LogRetentionResult(0, 0, 0);

        if (!Directory.Exists(logDirectory))
            return new LogRetentionResult(0, 0, 0);

        string[] files;
        try
        {
            files = Directory
                .EnumerateFiles(logDirectory, "app-*.log", SearchOption.TopDirectoryOnly)
                .Where(IsDailyAppLog)
                .ToArray();
        }
        catch
        {
            return new LogRetentionResult(0, 0, 1);
        }

        var cutoffUtc = nowUtc.UtcDateTime - maxAge;
        var scanned = 0;
        var deleted = 0;
        var failed = 0;

        foreach (var file in files)
        {
            scanned++;

            try
            {
                var info = new FileInfo(file);
                if (info.LastWriteTimeUtc >= cutoffUtc)
                    continue;

                info.Delete();
                deleted++;
            }
            catch
            {
                failed++;
            }
        }

        return new LogRetentionResult(scanned, deleted, failed);
    }

    private static bool IsDailyAppLog(string path)
    {
        var name = Path.GetFileName(path);
        if (name.Length != "app-yyyymmdd.log".Length)
            return false;

        if (!name.StartsWith("app-", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!name.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
            return false;

        return DateTime.TryParseExact(
            name.Substring(4, 8),
            "yyyyMMdd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out _);
    }
}

public sealed record LogRetentionResult(int Scanned, int Deleted, int Failed);
