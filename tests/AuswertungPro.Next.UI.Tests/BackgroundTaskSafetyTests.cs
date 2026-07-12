using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using AuswertungPro.Next.UI.Helpers;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class BackgroundTaskSafetyTests
{
    [Fact]
    public async Task SafeFireAndForget_ProtokolliertFehlerSofortImNormalenLogger()
    {
        var logger = new CapturingLogger();
        var observed = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var expected = new InvalidOperationException("Karte kaputt");

        Task.FromException(expected).SafeFireAndForget(
            "Kartentest",
            ex => observed.TrySetResult(ex),
            logger);

        var actual = await observed.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Same(expected, actual);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Same(expected, entry.Exception);
        Assert.Contains("Kartentest", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SafeFireAndForget_ProtokolliertNormalenAbbruchNichtAlsFehler()
    {
        var logger = new CapturingLogger();

        Task.FromCanceled(new CancellationToken(canceled: true))
            .SafeFireAndForget("Programmende", logger: logger);

        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task BegrenzterRunner_LehntWeitereAufgabeAbUndWartetAufAktiveAufgaben()
    {
        var logger = new CapturingLogger();
        var runner = new BoundedBackgroundTaskRunner(maxConcurrency: 2, logger);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Assert.True(runner.TryRun(() => release.Task, "Erste"));
        Assert.True(runner.TryRun(() => release.Task, "Zweite"));
        Assert.False(runner.TryRun(() => Task.CompletedTask, "Dritte"));
        Assert.Equal(2, runner.ActiveCount);

        release.SetResult();
        await runner.WaitForIdleAsync().WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(0, runner.ActiveCount);
        Assert.Contains(
            logger.Entries,
            entry => entry.Level == LogLevel.Warning
                     && entry.Message.Contains("Parallelitaetsgrenze", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BegrenzterRunner_ProtokolliertFehlerUndBleibtDanachNutzbar()
    {
        var logger = new CapturingLogger();
        var runner = new BoundedBackgroundTaskRunner(maxConcurrency: 1, logger);
        var expected = new IOException("Verbindung abgebrochen");

        Assert.True(runner.TryRun(() => Task.FromException(expected), "QGIS-Test"));
        await runner.WaitForIdleAsync().WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(runner.TryRun(() => Task.CompletedTask, "Naechster Request"));
        await runner.WaitForIdleAsync().WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Contains(
            logger.Entries,
            entry => entry.Level == LogLevel.Error
                     && ReferenceEquals(entry.Exception, expected)
                     && entry.Message.Contains("QGIS-Test", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BegrenzterRunner_BleibtBeiWerfendemLoggerNutzbar()
    {
        var runner = new BoundedBackgroundTaskRunner(maxConcurrency: 1, new ThrowingLogger());
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Assert.True(runner.TryRun(() => release.Task, "Aktiv"));
        Assert.False(runner.TryRun(() => Task.CompletedTask, "Ueberlast"));

        release.SetResult();
        await runner.WaitForIdleAsync().WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(runner.TryRun(
            () => Task.FromException(new IOException("Testfehler")),
            "Fehler"));
        await runner.WaitForIdleAsync().WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(0, runner.ActiveCount);
    }

    [Fact]
    public async Task UeberlasteterLokalerServer_AntwortetMit503StattVerbindungStillZuSchliessen()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var caller = new TcpClient();

        var connectTask = caller.ConnectAsync(IPAddress.Loopback, port);
        var accepted = await listener.AcceptTcpClientAsync();
        await connectTask;

        await LoopbackHttpServerSafety.RejectBusyAsync(accepted, CancellationToken.None);

        using var reader = new StreamReader(
            caller.GetStream(),
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            leaveOpen: false);
        var response = await reader.ReadToEndAsync();
        Assert.StartsWith("HTTP/1.1 503 Service Unavailable", response, StringComparison.Ordinal);
        Assert.Contains("Retry-After: 1", response, StringComparison.Ordinal);
        Assert.Contains("Server ausgelastet", response, StringComparison.Ordinal);
    }

    [Fact]
    public void LokaleServer_HabenEinBegrenztesLesezeitfenster()
        => Assert.InRange(
            LoopbackHttpServerSafety.RequestReadTimeout,
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(30));

    private sealed class CapturingLogger : ILogger
    {
        public ConcurrentQueue<LogEntry> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Enqueue(new LogEntry(logLevel, formatter(state, exception), exception));
    }

    private sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);

    private sealed class ThrowingLogger : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => throw new IOException("Log-Datentraeger nicht verfuegbar");
    }
}
