using System;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Common;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Tests fuer den zentralen <see cref="ProcessRunner"/>.
/// Loest STAB-H1 (Pipe-Drain-Deadlock) + SEC-H1..H3 (Command-Injection).
///
/// Tests laufen mit cmd.exe (Windows) — der Build-/Test-Pfad ist Win-only.
/// Bei wiederverwendbaren Tests sollte ein OS-Plattform-Skip stehen.
/// </summary>
[Trait("Category", "Slow")]
public class ProcessRunnerTests
{
    [Fact]
    public async Task RunAsync_SuccessfulCommand_ReturnsSuccess()
    {
        if (!OperatingSystem.IsWindows()) return;

        // cmd /c echo hello → exit code 0, stdout "hello\r\n"
        var result = await ProcessRunner.RunAsync(
            "cmd.exe",
            ["/c", "echo", "hello"],
            timeout: TimeSpan.FromSeconds(10));

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("hello", result.Stdout);
        Assert.False(result.TimedOut);
        Assert.False(result.StartFailed);
    }

    [Fact]
    public async Task RunAsync_FailingCommand_ReturnsExitCode()
    {
        if (!OperatingSystem.IsWindows()) return;

        // cmd /c exit 7 → exit code 7
        var result = await ProcessRunner.RunAsync(
            "cmd.exe",
            ["/c", "exit", "7"],
            timeout: TimeSpan.FromSeconds(10));

        Assert.False(result.IsSuccess);
        Assert.Equal(7, result.ExitCode);
        Assert.False(result.TimedOut);
    }

    [Fact]
    public async Task RunAsync_NonExistentExe_StartFailed()
    {
        var result = await ProcessRunner.RunAsync(
            "definitely_does_not_exist_anywhere_xyzzy.exe",
            ["arg1"],
            timeout: TimeSpan.FromSeconds(2));

        Assert.True(result.StartFailed);
        Assert.False(result.IsSuccess);
        Assert.Contains("Process.Start", result.Stderr);
    }

    [Fact]
    public async Task RunAsync_Timeout_KillsAndReportsTimedOut()
    {
        if (!OperatingSystem.IsWindows()) return;

        // ping -n 60 wartet 60 Sekunden — Timeout greift nach 1 Sekunde
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await ProcessRunner.RunAsync(
            "ping.exe",
            ["-n", "60", "127.0.0.1"],
            timeout: TimeSpan.FromSeconds(1));
        sw.Stop();

        Assert.True(result.TimedOut);
        Assert.False(result.IsSuccess);
        // Tree-Kill muss innerhalb weniger Sekunden zurueckkommen — nicht 60s warten
        Assert.True(sw.Elapsed.TotalSeconds < 10,
            $"ProcessRunner sollte nach Timeout schnell zurueckkommen, hat aber {sw.Elapsed.TotalSeconds:F1}s gebraucht");
    }

    [Fact]
    public async Task RunAsync_ExternalCancellation_TerminatesProcess()
    {
        if (!OperatingSystem.IsWindows()) return;

        using var cts = new CancellationTokenSource();
        var task = ProcessRunner.RunAsync(
            "ping.exe",
            ["-n", "60", "127.0.0.1"],
            timeout: TimeSpan.FromMinutes(5), // sehr lang — Cancel kommt zuerst
            ct: cts.Token);

        // Kurz warten dann Cancel
        await Task.Delay(300);
        cts.Cancel();

        var result = await task;

        // Bei externem Cancel ist TimedOut = false (es war nicht der Timeout)
        Assert.False(result.TimedOut);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task RunAsync_LargeStdoutOutput_DoesNotDeadlock()
    {
        if (!OperatingSystem.IsWindows()) return;

        // ipconfig /all liefert typischerweise mehrere KB stdout. Wenn der
        // Pipe-Drain synchron waere, koennte (wuerde aber nicht zwingend) der
        // 64KB-Buffer voll laufen → Test wuerde dann TimedOut werden.
        // Mit asynchronem Drain: zuverlaessiger Erfolg.
        var result = await ProcessRunner.RunAsync(
            "ipconfig.exe",
            ["/all"],
            timeout: TimeSpan.FromSeconds(10));

        Assert.True(result.IsSuccess, $"ipconfig fehlgeschlagen: {result.ToDiagnosticString()}");
        Assert.True(result.Stdout.Length > 100, $"stdout zu klein: {result.Stdout.Length} chars");
        Assert.False(result.TimedOut);
    }

    [Fact]
    public void Result_IsSuccess_FalseOnTimedOut()
    {
        var r = new ProcessResult(0, "", "", TimeSpan.Zero, TimedOut: true, StartFailed: false);
        Assert.False(r.IsSuccess);
    }

    [Fact]
    public void Result_IsSuccess_FalseOnStartFailed()
    {
        var r = new ProcessResult(0, "", "", TimeSpan.Zero, TimedOut: false, StartFailed: true);
        Assert.False(r.IsSuccess);
    }

    [Fact]
    public void Result_IsSuccess_TrueOnExitCode0()
    {
        var r = new ProcessResult(0, "", "", TimeSpan.Zero, TimedOut: false, StartFailed: false);
        Assert.True(r.IsSuccess);
    }

    [Fact]
    public void Result_ToDiagnosticString_IncludesExitAndDuration()
    {
        var r = new ProcessResult(7, "out", "err line", TimeSpan.FromSeconds(1.5),
            TimedOut: false, StartFailed: false);
        var s = r.ToDiagnosticString();
        Assert.Contains("exit=7", s);
        Assert.Contains("dur=1.50s", s);
        Assert.Contains("err line", s);
    }
}
