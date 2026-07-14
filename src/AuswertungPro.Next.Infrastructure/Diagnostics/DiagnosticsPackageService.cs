using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Diagnostics;

namespace AuswertungPro.Next.Infrastructure.Diagnostics;

public sealed partial class DiagnosticsPackageService : IDiagnosticsPackageService
{
    private const int MaximumLogFiles = 7;
    private const long MaximumBytesPerLog = 5L * 1024 * 1024;

    private readonly string _appVersion;
    private readonly Func<DateTimeOffset> _utcNow;

    public DiagnosticsPackageService(
        string logDirectory,
        string appVersion,
        Func<DateTimeOffset>? utcNow = null)
    {
        if (string.IsNullOrWhiteSpace(logDirectory))
            throw new ArgumentException("Log-Ordner fehlt.", nameof(logDirectory));

        LogDirectory = Path.GetFullPath(logDirectory);
        _appVersion = string.IsNullOrWhiteSpace(appVersion) ? "unbekannt" : appVersion.Trim();
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public string LogDirectory { get; }

    public Task<DiagnosticsPackageResult> CreateAsync(
        string destinationZipPath,
        CancellationToken cancellationToken = default)
        => Task.Run(() => Create(destinationZipPath, cancellationToken), cancellationToken);

    private DiagnosticsPackageResult Create(
        string destinationZipPath,
        CancellationToken cancellationToken)
    {
        string? temporaryPath = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var destination = ValidateDestination(destinationZipPath);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            temporaryPath = destination + $".{Guid.NewGuid():N}.tmp";

            var logs = EnumerateLogs();
            var includedLogCount = 0;
            var skippedLogCount = 0;
            using (var archive = ZipFile.Open(temporaryPath, ZipArchiveMode.Create))
            {
                foreach (var logPath in logs)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string content;
                    try
                    {
                        content = Redact(ReadBoundedText(logPath));
                    }
                    catch (Exception ex)
                    {
                        skippedLogCount++;
                        BestEffort.ReportWarning(
                            $"[Diagnosepaket] Logdatei {Path.GetFileName(logPath)} uebersprungen: {ex.Message}");
                        continue;
                    }

                    WriteTextEntry(
                        archive,
                        $"logs/{Path.GetFileName(logPath)}",
                        content);
                    includedLogCount++;
                }

                WriteSystemInfo(archive, includedLogCount, skippedLogCount, cancellationToken);
            }

            File.Move(temporaryPath, destination, overwrite: true);
            temporaryPath = null;
            return new DiagnosticsPackageResult(
                true,
                destination,
                includedLogCount,
                skippedLogCount == 0
                    ? $"Diagnosepaket erstellt ({includedLogCount} Logdatei(en))."
                    : $"Diagnosepaket erstellt ({includedLogCount} Logdatei(en), {skippedLogCount} nicht lesbar).");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            BestEffort.ReportWarning($"[Diagnosepaket] Erstellen fehlgeschlagen: {ex}");
            return new DiagnosticsPackageResult(
                false,
                null,
                0,
                "Diagnosepaket konnte nicht erstellt werden. Details stehen im Programmlog.");
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(temporaryPath))
                BestEffort.Try(
                    () => File.Delete(temporaryPath),
                    "Diagnosepaket: unvollstaendige Temp-Datei loeschen");
        }
    }

    private static string ValidateDestination(string destinationZipPath)
    {
        if (string.IsNullOrWhiteSpace(destinationZipPath))
            throw new ArgumentException("Zielpfad fehlt.", nameof(destinationZipPath));

        var destination = Path.GetFullPath(destinationZipPath);
        if (!string.Equals(Path.GetExtension(destination), ".zip", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Das Diagnosepaket muss eine ZIP-Datei sein.", nameof(destinationZipPath));
        if (string.IsNullOrWhiteSpace(Path.GetDirectoryName(destination)))
            throw new ArgumentException("Zielordner fehlt.", nameof(destinationZipPath));
        return destination;
    }

    private List<string> EnumerateLogs()
    {
        if (!Directory.Exists(LogDirectory))
            return [];

        return Directory
            .EnumerateFiles(LogDirectory, "app-*.log", SearchOption.TopDirectoryOnly)
            .Where(IsRegularLogFile)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ThenByDescending(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .Take(MaximumLogFiles)
            .ToList();
    }

    private static bool IsRegularLogFile(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0;
        }
        catch (Exception ex)
        {
            BestEffort.ReportWarning(
                $"[Diagnosepaket] Logdatei {Path.GetFileName(path)} konnte nicht geprueft werden: {ex.Message}");
            return false;
        }
    }

    private void WriteSystemInfo(
        ZipArchive archive,
        int logFileCount,
        int skippedLogFileCount,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var info = new StringBuilder()
            .AppendLine("SewerStudio Diagnosepaket")
            .AppendLine($"ErstelltUtc: {_utcNow():O}")
            .AppendLine($"AppVersion: {_appVersion}")
            .AppendLine($"Betriebssystem: {RuntimeInformation.OSDescription}")
            .AppendLine($".NET: {RuntimeInformation.FrameworkDescription}")
            .AppendLine($"Prozessarchitektur: {RuntimeInformation.ProcessArchitecture}")
            .AppendLine($"Logdateien: {logFileCount}")
            .AppendLine($"Nicht lesbar: {skippedLogFileCount}")
            .AppendLine("Datenschutz: Tokens, Benutzername und absolute Pfade wurden aus den Logkopien entfernt.")
            .ToString();
        WriteTextEntry(archive, "system-info.txt", info);
    }

    private static string ReadBoundedText(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        if (stream.Length > MaximumBytesPerLog)
            stream.Seek(-MaximumBytesPerLog, SeekOrigin.End);

        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true,
            leaveOpen: false);
        var content = reader.ReadToEnd();
        if (stream.Length <= MaximumBytesPerLog)
            return content;

        var firstLineBreak = content.IndexOf('\n');
        return "[Aelterer Loganfang wegen Groessenlimit ausgelassen.]\n"
            + (firstLineBreak >= 0 ? content[(firstLineBreak + 1)..] : content);
    }

    private static string Redact(string content)
    {
        var redacted = BearerTokenRegex().Replace(content, "Bearer <ENTFERNT>");
        redacted = SecretValueRegex().Replace(redacted, "${prefix}<ENTFERNT>");
        redacted = QuotedWindowsPathRegex().Replace(redacted, "\"<PFAD>\"");
        redacted = WindowsPathRegex().Replace(redacted, "<PFAD>");

        var userName = Environment.UserName;
        if (!string.IsNullOrWhiteSpace(userName))
            redacted = redacted.Replace(userName, "<BENUTZER>", StringComparison.OrdinalIgnoreCase);
        return redacted;
    }

    private static void WriteTextEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }

    [GeneratedRegex(
        """(?im)(?<prefix>\b(?:token|api[_-]?key|authorization|password|secret|SEWER_SIDECAR_TOKEN)\b\s*[:=]\s*)(?:"[^"\r\n]*"|'[^'\r\n]*'|[^\s,;]+)""",
        RegexOptions.CultureInvariant)]
    private static partial Regex SecretValueRegex();

    [GeneratedRegex(@"(?i)\bBearer\s+[A-Za-z0-9._~+/=-]+", RegexOptions.CultureInvariant)]
    private static partial Regex BearerTokenRegex();

    [GeneratedRegex("(?i)\"(?:[A-Z]:\\\\|\\\\\\\\)[^\"\\r\\n]+\"", RegexOptions.CultureInvariant)]
    private static partial Regex QuotedWindowsPathRegex();

    [GeneratedRegex("(?i)(?:[A-Z]:\\\\|\\\\\\\\)[^\\s,;\"']+", RegexOptions.CultureInvariant)]
    private static partial Regex WindowsPathRegex();
}
