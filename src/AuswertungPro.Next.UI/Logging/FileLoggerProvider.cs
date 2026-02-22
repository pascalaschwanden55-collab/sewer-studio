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
