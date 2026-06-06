using Microsoft.Extensions.Logging;
using System.IO;

namespace AuswertungPro.Next.UI;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly string _path;
    private readonly object _lock = new();

    public FileLoggerProvider(string path) => _path = path;

    public ILogger CreateLogger(string categoryName) => new FileLogger(_path, _lock, categoryName);

    public void Dispose() { }

    /// <summary>
    /// Loescht alte Tageslogs (app-*.log), die aelter als <paramref name="retentionDays"/> Tage sind.
    /// Verhindert unbegrenztes Anwachsen des Log-Ordners. Best-effort: Fehler werden geschluckt,
    /// der App-Start darf daran niemals scheitern. Das aktuelle Tageslog bleibt immer erhalten,
    /// weil dessen letzte Schreibzeit innerhalb des Aufbewahrungsfensters liegt.
    /// </summary>
    public static void CleanupOldLogs(string logDir, int retentionDays)
    {
        try
        {
            if (retentionDays <= 0 || !Directory.Exists(logDir))
                return;

            var cutoff = DateTime.Now.AddDays(-retentionDays);
            foreach (var file in Directory.EnumerateFiles(logDir, "app-*.log"))
            {
                try
                {
                    if (File.GetLastWriteTime(file) < cutoff)
                        File.Delete(file);
                }
                catch
                {
                    // Einzelne Datei gesperrt/verschwunden - ueberspringen, nicht abbrechen.
                }
            }
        }
        catch
        {
            // Verzeichnis-Enumeration fehlgeschlagen - Retention ist nur best-effort.
        }
    }

    private sealed class FileLogger : ILogger
    {
        private readonly string _path;
        private readonly object _lock;
        private readonly string _category;

        public FileLogger(string path, object @lock, string category)
        {
            _path = path;
            _lock = @lock;
            _category = category;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var msg = formatter(state, exception);
            var line = $"{DateTime.Now:O} [{logLevel}] {_category}: {msg}";
            if (exception is not null)
                line += Environment.NewLine + exception;

            lock (_lock)
            {
                File.AppendAllText(_path, line + Environment.NewLine);
            }
        }
    }
}
