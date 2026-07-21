using System.Diagnostics;
using System.IO;

namespace AuswertungPro.Next.UI.Tests;

internal static class WpfIsolatedTestProcess
{
    private const string ChildProcessVariable = "SEWERSTUDIO_WPF_SMOKE_CHILD";
    private const string ChildReceiptPathVariable = "SEWERSTUDIO_WPF_SMOKE_RECEIPT_PATH";
    private const string ChildReceiptTokenVariable = "SEWERSTUDIO_WPF_SMOKE_RECEIPT_TOKEN";
    private static readonly TimeSpan PostKillTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan OutputReadTimeout = TimeSpan.FromSeconds(2);

    public static bool IsChildProcess
        => string.Equals(
               Environment.GetEnvironmentVariable(ChildProcessVariable),
               "1",
               StringComparison.Ordinal)
           && HasValidChildHandshake();

    public static async Task<WpfIsolatedTestProcessResult> RunAsync(
        string fullyQualifiedTestName,
        TimeSpan timeout)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullyQualifiedTestName);
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));
        if (IsChildProcess)
            throw new InvalidOperationException("Ein WPF-Kindprozess darf keinen weiteren Kindprozess starten.");

        var assemblyPath = typeof(WpfIsolatedTestProcess).Assembly.Location;
        var dotnetPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (string.IsNullOrWhiteSpace(dotnetPath))
            dotnetPath = "dotnet";

        var receiptDirectory = Path.Combine(
            Path.GetTempPath(),
            $"sewerstudio-wpf-smoke-{Guid.NewGuid():N}");
        Directory.CreateDirectory(receiptDirectory);
        var receiptPath = Path.Combine(receiptDirectory, "completed.receipt");
        var receiptToken = Guid.NewGuid().ToString("N");

        var startInfo = new ProcessStartInfo
        {
            FileName = dotnetPath,
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("vstest");
        startInfo.ArgumentList.Add(assemblyPath);
        startInfo.ArgumentList.Add($"--TestCaseFilter:FullyQualifiedName={fullyQualifiedTestName}");
        startInfo.ArgumentList.Add("--logger:console;verbosity=normal");
        startInfo.Environment[ChildProcessVariable] = "1";
        startInfo.Environment[ChildReceiptPathVariable] = receiptPath;
        startInfo.Environment[ChildReceiptTokenVariable] = receiptToken;

        try
        {
            using var process = new Process { StartInfo = startInfo };
            if (!process.Start())
                throw new InvalidOperationException("Der isolierte WPF-Testprozess konnte nicht gestartet werden.");

            using var outputCancellation = new CancellationTokenSource();
            var standardOutput = process.StandardOutput
                .ReadToEndAsync(outputCancellation.Token);
            var standardError = process.StandardError
                .ReadToEndAsync(outputCancellation.Token);
            using var timeoutCancellation = new CancellationTokenSource(timeout);

            try
            {
                await process.WaitForExitAsync(timeoutCancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (timeoutCancellation.IsCancellationRequested)
            {
                if (process.HasExited)
                {
                    var racedOutput = await CollectOutputAsync(
                            standardOutput,
                            standardError,
                            outputCancellation)
                        .ConfigureAwait(false);
                    return new WpfIsolatedTestProcessResult(
                        process.ExitCode,
                        TimedOut: false,
                        ChildScenarioCompleted: HasValidReceipt(receiptPath, receiptToken),
                        racedOutput.StandardOutput,
                        racedOutput.StandardError);
                }

                string? killError = null;
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (Exception ex) when (
                    ex is InvalidOperationException
                    or System.ComponentModel.Win32Exception
                    or NotSupportedException)
                {
                    killError = $"Prozessbaum konnte nicht beendet werden: {ex.Message}";
                }

                var exitedAfterKill = process.HasExited;
                if (!exitedAfterKill)
                {
                    using var postKillCancellation = new CancellationTokenSource(PostKillTimeout);
                    try
                    {
                        await process.WaitForExitAsync(postKillCancellation.Token).ConfigureAwait(false);
                        exitedAfterKill = true;
                    }
                    catch (OperationCanceledException) when (postKillCancellation.IsCancellationRequested)
                    {
                        outputCancellation.Cancel();
                    }
                }

                var timedOutOutput = await CollectOutputAsync(
                        standardOutput,
                        standardError,
                        outputCancellation)
                    .ConfigureAwait(false);
                return new WpfIsolatedTestProcessResult(
                    ExitCode: exitedAfterKill ? process.ExitCode : null,
                    TimedOut: true,
                    ChildScenarioCompleted: HasValidReceipt(receiptPath, receiptToken),
                    StandardOutput: timedOutOutput.StandardOutput,
                    StandardError: AppendDiagnostic(timedOutOutput.StandardError, killError));
            }

            var output = await CollectOutputAsync(
                    standardOutput,
                    standardError,
                    outputCancellation)
                .ConfigureAwait(false);
            return new WpfIsolatedTestProcessResult(
                process.ExitCode,
                TimedOut: false,
                ChildScenarioCompleted: HasValidReceipt(receiptPath, receiptToken),
                output.StandardOutput,
                output.StandardError);
        }
        finally
        {
            TryDeleteDirectory(receiptDirectory);
        }
    }

    public static void MarkChildScenarioCompleted()
    {
        if (!IsChildProcess)
            throw new InvalidOperationException("Die WPF-Bestaetigung darf nur der Kindprozess schreiben.");

        var receiptPath = Environment.GetEnvironmentVariable(ChildReceiptPathVariable);
        var receiptToken = Environment.GetEnvironmentVariable(ChildReceiptTokenVariable);
        if (string.IsNullOrWhiteSpace(receiptPath) || string.IsNullOrWhiteSpace(receiptToken))
            throw new InvalidOperationException("Die WPF-Bestaetigung des Kindprozesses ist unvollstaendig.");

        using var stream = new FileStream(
            receiptPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        using var writer = new StreamWriter(stream);
        writer.Write(receiptToken);
    }

    private static bool HasValidChildHandshake()
    {
        var receiptPath = Environment.GetEnvironmentVariable(ChildReceiptPathVariable);
        var receiptToken = Environment.GetEnvironmentVariable(ChildReceiptTokenVariable);
        if (string.IsNullOrWhiteSpace(receiptPath)
            || !Guid.TryParseExact(receiptToken, "N", out _))
        {
            return false;
        }

        try
        {
            var fullPath = Path.GetFullPath(receiptPath);
            var tempRoot = Path.GetFullPath(Path.GetTempPath())
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            var parentName = Directory.GetParent(fullPath)?.Name;
            return fullPath.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase)
                   && string.Equals(Path.GetFileName(fullPath), "completed.receipt", StringComparison.Ordinal)
                   && parentName?.StartsWith("sewerstudio-wpf-smoke-", StringComparison.Ordinal) == true;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static bool HasValidReceipt(string receiptPath, string expectedToken)
    {
        try
        {
            return File.Exists(receiptPath)
                   && string.Equals(File.ReadAllText(receiptPath), expectedToken, StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static async Task<(string StandardOutput, string StandardError)> CollectOutputAsync(
        Task<string> standardOutput,
        Task<string> standardError,
        CancellationTokenSource outputCancellation)
    {
        var combined = Task.WhenAll(standardOutput, standardError);
        var completed = await Task.WhenAny(combined, Task.Delay(OutputReadTimeout)).ConfigureAwait(false);
        if (completed != combined)
        {
            outputCancellation.Cancel();
            _ = combined.ContinueWith(
                task =>
                {
                    _ = task.Exception;
                },
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
            return (
                "Ausgabe konnte nicht vollstaendig gelesen werden.",
                "Fehlerausgabe konnte nicht vollstaendig gelesen werden.");
        }

        try
        {
            var output = await combined.ConfigureAwait(false);
            return (output[0], output[1]);
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or ObjectDisposedException)
        {
            return (
                "Ausgabe konnte nicht vollstaendig gelesen werden.",
                $"Fehlerausgabe konnte nicht vollstaendig gelesen werden: {ex.Message}");
        }
    }

    private static string AppendDiagnostic(string standardError, string? diagnostic)
        => string.IsNullOrWhiteSpace(diagnostic)
            ? standardError
            : standardError + Environment.NewLine + diagnostic;

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Test-Tempdaten duerfen den eigentlichen Prozessbefund nicht verdecken.
        }
    }
}

internal sealed record WpfIsolatedTestProcessResult(
    int? ExitCode,
    bool TimedOut,
    bool ChildScenarioCompleted,
    string StandardOutput,
    string StandardError)
{
    public string DescribeFailure()
    {
        var status = TimedOut
            ? "Zeitlimit ueberschritten"
            : $"Exit-Code {ExitCode?.ToString() ?? "unbekannt"}";
        var receipt = ChildScenarioCompleted
            ? "Szenario-Bestaetigung vorhanden"
            : "Szenario-Bestaetigung fehlt";
        return $"Isolierter WPF-Test fehlgeschlagen ({status}; {receipt}).\n\nstdout:\n{StandardOutput}\n\nstderr:\n{StandardError}";
    }
}
