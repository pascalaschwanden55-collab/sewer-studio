using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Diagnostics;

namespace AuswertungPro.Next.Infrastructure.Diagnostics;

public sealed class DailyLogTailReader : ILogTailReader
{
    private readonly string _logDirectory;
    private readonly Func<DateTime> _now;

    public DailyLogTailReader(string logDirectory, Func<DateTime>? now = null)
    {
        if (string.IsNullOrWhiteSpace(logDirectory))
            throw new ArgumentException("Log-Ordner fehlt.", nameof(logDirectory));

        _logDirectory = logDirectory;
        _now = now ?? (() => DateTime.Now);
    }

    public LogTailReadResult ReadToday(int maximumLines = 200)
    {
        if (maximumLines <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumLines));

        var fileExists = false;
        try
        {
            var logPath = Path.Combine(_logDirectory, $"app-{_now():yyyyMMdd}.log");
            fileExists = File.Exists(logPath);
            if (!fileExists)
                return new LogTailReadResult(false, [], UserMessage: null);

            var lines = File.ReadLines(logPath).TakeLast(maximumLines).ToArray();
            return new LogTailReadResult(true, lines, UserMessage: null);
        }
        catch (Exception ex)
        {
            BestEffort.ReportWarning($"[Diagnose] Tageslog konnte nicht gelesen werden: {ex}");
            return new LogTailReadResult(
                fileExists,
                [],
                "Tageslog konnte nicht gelesen werden. Details stehen im Programmlog.");
        }
    }
}
