using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.Common;

/// <summary>
/// Sichere und robuste Ausfuehrung externer Prozesse (ffmpeg, python, pdftotext, ...).
///
/// Loest die Audit-Befunde 2026-04-23/25 STAB-H1 (Pipe-Deadlocks bei synchronem
/// stdout/stderr-Read) und SEC-H1..H3 (Command-Injection via String-Argumente).
///
/// Garantiert:
/// - ArgumentList statt Argument-String → keine Shell-Interpretation
/// - asynchroner Drain von stdout und stderr → kein 64-KB-Pipe-Deadlock
/// - harter Timeout via WaitForExitAsync(linkedToken)
/// - Tree-Kill bei Timeout (entireProcessTree: true)
/// - strukturiertes Result mit ExitCode, stdout, stderr, Dauer
///
/// Verwendung:
/// <code>
///   var result = await ProcessRunner.RunAsync(
///       fileName: "ffmpeg",
///       arguments: ["-i", videoPath, "-frames:v", "1", outputPng],
///       timeout: TimeSpan.FromSeconds(30),
///       ct: ct);
///   if (result.IsSuccess) { ... }
/// </code>
/// </summary>
public static class ProcessRunner
{
    /// <summary>
    /// Startet einen externen Prozess und wartet auf das Ende.
    /// </summary>
    /// <param name="fileName">Programm (z.B. "ffmpeg" — relativ via PATH oder absolut).</param>
    /// <param name="arguments">Argumente — werden via ArgumentList sicher uebergeben.</param>
    /// <param name="timeout">Maximale Laufzeit. null = unbegrenzt (nicht empfohlen).</param>
    /// <param name="workingDirectory">Optionales Arbeitsverzeichnis.</param>
    /// <param name="environment">Optionale Env-Vars (additiv zu Prozess-Env).</param>
    /// <param name="ct">Externer Cancellation-Token (z.B. Batch-Abbruch).</param>
    /// <returns>Strukturiertes Ergebnis. <c>IsSuccess</c> ⇔ ExitCode 0 und kein Timeout.</returns>
    public static async Task<ProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan? timeout = null,
        string? workingDirectory = null,
        IReadOnlyDictionary<string, string>? environment = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(arguments);

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        if (!string.IsNullOrWhiteSpace(workingDirectory))
            psi.WorkingDirectory = workingDirectory;

        foreach (var arg in arguments)
            psi.ArgumentList.Add(arg);

        if (environment is not null)
        {
            foreach (var kv in environment)
                psi.EnvironmentVariables[kv.Key] = kv.Value;
        }

        using var proc = new Process { StartInfo = psi };

        var sw = Stopwatch.StartNew();
        try
        {
            if (!proc.Start())
            {
                return new ProcessResult(
                    ExitCode: -1,
                    Stdout: "",
                    Stderr: $"Process.Start lieferte false fuer '{fileName}'",
                    Duration: sw.Elapsed,
                    TimedOut: false,
                    StartFailed: true);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            sw.Stop();
            return new ProcessResult(
                ExitCode: -1,
                Stdout: "",
                Stderr: $"Process.Start warf {ex.GetType().Name}: {ex.Message}",
                Duration: sw.Elapsed,
                TimedOut: false,
                StartFailed: true);
        }

        // Linked Token: externer Cancel + Timeout
        using var timeoutCts = timeout.HasValue
            ? new CancellationTokenSource(timeout.Value)
            : new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        // stdout/stderr ASYNCHRON drainen — kein synchron-blockierendes ReadToEnd.
        // Ohne diesen Drain blockiert der Prozess wenn der OS-Pipe-Buffer (64 KB
        // unter Windows) voll laeuft → Timeout greift dann nicht.
        var stdoutTask = proc.StandardOutput.ReadToEndAsync(linked.Token);
        var stderrTask = proc.StandardError.ReadToEndAsync(linked.Token);

        bool timedOut = false;
        try
        {
            await Task.WhenAll(stdoutTask, stderrTask, proc.WaitForExitAsync(linked.Token))
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            timedOut = timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested;
            try { proc.Kill(entireProcessTree: true); } catch { /* best effort */ }

            // Auf die Reader-Tasks noch warten, damit kein UnobservedTaskException
            // entsteht — aber tolerant fuer nochmalige Cancel.
            try { await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false); }
            catch { /* expected nach Kill */ }
        }
        sw.Stop();

        var stdout = stdoutTask.IsCompletedSuccessfully ? stdoutTask.Result : "";
        var stderr = stderrTask.IsCompletedSuccessfully ? stderrTask.Result : "";

        return new ProcessResult(
            ExitCode: proc.HasExited ? proc.ExitCode : -1,
            Stdout: stdout,
            Stderr: stderr,
            Duration: sw.Elapsed,
            TimedOut: timedOut,
            StartFailed: false);
    }
}

/// <summary>
/// Strukturiertes Ergebnis eines Prozess-Aufrufs.
/// </summary>
public sealed record ProcessResult(
    int ExitCode,
    string Stdout,
    string Stderr,
    TimeSpan Duration,
    bool TimedOut,
    bool StartFailed)
{
    /// <summary>True wenn ExitCode == 0 und kein Timeout / Start-Fehler.</summary>
    public bool IsSuccess => !StartFailed && !TimedOut && ExitCode == 0;

    /// <summary>Kurze diagnostische Beschreibung fuer Logs.</summary>
    public string ToDiagnosticString(int stderrTail = 200)
    {
        var sb = new StringBuilder();
        sb.Append($"exit={ExitCode} dur={Duration.TotalSeconds:F2}s");
        if (TimedOut) sb.Append(" TIMEOUT");
        if (StartFailed) sb.Append(" START_FAILED");
        if (!string.IsNullOrWhiteSpace(Stderr))
        {
            var tail = Stderr.Length > stderrTail
                ? "..." + Stderr[^stderrTail..]
                : Stderr;
            sb.Append($" stderr=\"{tail.Replace("\r", "").Replace('\n', ' ')}\"");
        }
        return sb.ToString();
    }
}
