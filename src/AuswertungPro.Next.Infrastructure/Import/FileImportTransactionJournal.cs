using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>
/// Datei-basierter Transaktions-Marker <c>.import-transaction.json</c> im Projekt-Root.
/// Schreiben, eigentumsgebundenes Loeschen und Lesen laufen je Projekt unter derselben
/// prozessuebergreifenden Sperre.
/// </summary>
public sealed class FileImportTransactionJournal : IImportTransactionJournal
{
    public const string MarkerFileName = ".import-transaction.json";

    /// <summary>
    /// Groesste zulaessige Markerdatei. Der Marker liegt im Projekt und ist damit
    /// manipulierbar: ohne diese Grenze liest das Projektoeffnen eine beliebig grosse
    /// Datei vollstaendig in den Speicher, bevor ueberhaupt etwas geprueft wird.
    /// 8 MiB sind fuer zehntausend Ziele reichlich bemessen.
    /// </summary>
    public const int MaxMarkerBytes = 8 * 1024 * 1024;

    /// <summary>
    /// Groesste zulaessige Anzahl veroeffentlichter Ziele je Transaktion. Begrenzt
    /// zugleich den Aufwand der Ruecknahme, die jedes Ziel einzeln hasht.
    /// </summary>
    public const int MaxPublishedTargets = 10_000;

    private static readonly JsonSerializerOptions Opt = new() { WriteIndented = true };
    private static readonly TimeSpan SynchronizationTimeout = TimeSpan.FromSeconds(5);
    private readonly Action? _afterOwnershipCheck;

    public FileImportTransactionJournal()
    {
    }

    internal FileImportTransactionJournal(Action afterOwnershipCheck)
    {
        ArgumentNullException.ThrowIfNull(afterOwnershipCheck);
        _afterOwnershipCheck = afterOwnershipCheck;
    }

    /// <summary>
    /// Kompatibilitaetsweg. Auch alte Aufrufer erhalten die neue Eigentumspruefung.
    /// </summary>
    public void Begin(string projectRoot, ImportTransactionMarker marker)
        => BeginIfMissingOrOwned(projectRoot, marker);

    public void BeginIfMissingOrOwned(string projectRoot, ImportTransactionMarker marker)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ArgumentNullException.ThrowIfNull(marker);
        if (ExceedsTargetLimit(marker))
        {
            throw new ArgumentException(
                $"Der Import umfasst {marker.PublishedTargets!.Count} Dateien; hoechstens "
                + $"{MaxPublishedTargets} koennen im Wiederherstellungsmarker gefuehrt werden. "
                + "Bitte den Import in kleinere Teile aufteilen.",
                nameof(marker));
        }

        if (!IsValid(marker))
            throw new ArgumentException("Der Import-Wiederherstellungsmarker ist unvollstaendig.", nameof(marker));

        ExecuteSynchronized(projectRoot, () =>
        {
            var current = ReadCore(projectRoot);
            if (current.Outcome == ImportTransactionJournalReadOutcome.Failed)
            {
                throw new InvalidOperationException(
                    "Im Projekt liegt ein Import-Wiederherstellungsmarker, der nicht "
                    + "sicher gelesen werden kann. Bitte das Projekt neu laden, damit "
                    + "die Wiederherstellung ihn pruefen kann.");
            }

            if (current is
                {
                    Outcome: ImportTransactionJournalReadOutcome.Loaded,
                    Marker: not null
                }
                && !string.Equals(current.Marker.TxId, marker.TxId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Im Projekt liegt noch eine unabgeschlossene Import-Transaktion vom "
                    + $"{current.Marker.StartedUtc.ToLocalTime():g}. Bitte das Projekt zuerst "
                    + "neu laden - die Wiederherstellung prueft den offenen Import und meldet, "
                    + "was zu tun ist. Erst danach ist ein neuer Import moeglich.");
            }

            _afterOwnershipCheck?.Invoke();
            WriteCore(projectRoot, marker);

            var written = ReadCore(projectRoot);
            if (written is not
                {
                    Outcome: ImportTransactionJournalReadOutcome.Loaded,
                    Marker: not null
                }
                || !string.Equals(written.Marker.TxId, marker.TxId, StringComparison.Ordinal))
            {
                throw new IOException(
                    "Der Import-Wiederherstellungsmarker konnte nach dem Schreiben nicht bestaetigt werden.");
            }

            return true;
        });
    }

    public ImportTransactionJournalReadResult Read(string projectRoot)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
            return ImportTransactionJournalReadResult.Missing();

        try
        {
            return ExecuteSynchronized(projectRoot, () => ReadCore(projectRoot));
        }
        catch (Exception ex) when (IsExpectedAccessFailure(ex))
        {
            return ReadFailed();
        }
    }

    public ImportTransactionMarker? TryRead(string projectRoot)
        => Read(projectRoot) is
        {
            Outcome: ImportTransactionJournalReadOutcome.Loaded,
            Marker: not null
        } result
            ? result.Marker
            : null;

    /// <summary>
    /// Kompatibilitaetsweg fuer alte Aufrufer. Die zuerst gelesene TxId wird an die
    /// atomare Loeschoperation gebunden. Ein inzwischen ersetzter Marker bleibt liegen.
    /// </summary>
    public void Clear(string projectRoot)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
            return;

        var current = Read(projectRoot);
        if (current is
            {
                Outcome: ImportTransactionJournalReadOutcome.Loaded,
                Marker: not null
            })
        {
            _ = ClearIfOwned(projectRoot, current.Marker.TxId);
        }
    }

    public bool ClearIfOwned(string projectRoot, string expectedTxId)
    {
        if (string.IsNullOrWhiteSpace(projectRoot) || string.IsNullOrWhiteSpace(expectedTxId))
            return false;

        try
        {
            return ExecuteSynchronized(projectRoot, () =>
            {
                var current = ReadCore(projectRoot);
                if (current.Outcome == ImportTransactionJournalReadOutcome.Missing)
                    return true;

                if (current is not
                    {
                        Outcome: ImportTransactionJournalReadOutcome.Loaded,
                        Marker: not null
                    }
                    || !string.Equals(current.Marker.TxId, expectedTxId, StringComparison.Ordinal))
                {
                    return false;
                }

                _afterOwnershipCheck?.Invoke();
                var markerPath = SafeMarkerPath(projectRoot);
                File.Delete(markerPath);
                return ReadCore(projectRoot).Outcome == ImportTransactionJournalReadOutcome.Missing;
            });
        }
        catch (Exception ex) when (IsExpectedAccessFailure(ex))
        {
            // Fail-closed: Bei jedem unklaren Zustand bleibt der Marker erhalten.
            return false;
        }
    }

    private static ImportTransactionJournalReadResult ReadCore(string projectRoot)
    {
        string path;
        try
        {
            path = SafeMarkerPath(projectRoot);
            var attributes = File.GetAttributes(path);
            if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
                return ReadFailed();
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return ImportTransactionJournalReadResult.Missing();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or ArgumentException or NotSupportedException)
        {
            return ReadFailed();
        }

        try
        {
            // Groesse am GEOEFFNETEN Stream pruefen, nicht vorher per FileInfo: nur so
            // gilt die Grenze fuer genau die Datei, die anschliessend gelesen wird.
            using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length > MaxMarkerBytes)
                return ReadFailed();

            using var reader = new StreamReader(stream, Encoding.UTF8);
            var json = reader.ReadToEnd();
            var marker = JsonSerializer.Deserialize<ImportTransactionMarker>(json, Opt);
            return IsValid(marker) && !ExceedsTargetLimit(marker)
                ? ImportTransactionJournalReadResult.Loaded(marker!)
                : ReadFailed();
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return ImportTransactionJournalReadResult.Missing();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return ReadFailed();
        }
    }

    private static void WriteCore(string projectRoot, ImportTransactionMarker marker)
    {
        // Dieselben Grenzen wie beim Lesen - sonst schriebe der Import einen Marker,
        // den die eigene Wiederherstellung anschliessend nicht mehr annimmt.
        if (ExceedsTargetLimit(marker))
        {
            throw new ArgumentException(
                $"Der Import umfasst {marker.PublishedTargets!.Count} Dateien; hoechstens "
                + $"{MaxPublishedTargets} koennen im Wiederherstellungsmarker gefuehrt werden.",
                nameof(marker));
        }

        var json = JsonSerializer.Serialize(marker, Opt);
        var groesse = Encoding.UTF8.GetByteCount(json);
        if (groesse > MaxMarkerBytes)
        {
            // Die Datei wird gar nicht erst angefasst: ein vorhandener Marker bleibt liegen.
            throw new ArgumentException(
                $"Der Wiederherstellungsmarker waere {groesse} Bytes gross; erlaubt sind "
                + $"hoechstens {MaxMarkerBytes}. Bitte den Import in kleinere Teile aufteilen.",
                nameof(marker));
        }

        // Die Wiederherstellung baut auf einem dauerhaft lesbaren Marker auf.
        // Zwei kleine, erzwungene Schreibvorgaenge je Import fallen nicht ins Gewicht.
        AtomicTextFileWriter.WriteAllText(SafeMarkerPath(projectRoot), json, durable: true);
    }

    private static T ExecuteSynchronized<T>(string projectRoot, Func<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        using var mutex = new Mutex(initiallyOwned: false, BuildSynchronizationName(projectRoot));
        var acquired = false;
        try
        {
            try
            {
                acquired = mutex.WaitOne(SynchronizationTimeout);
            }
            catch (AbandonedMutexException)
            {
                // Der vorige Prozess ist mitten in der Operation gestorben. Der Mutex
                // gehoert jetzt diesem Aufrufer; der Marker entscheidet fail-closed.
                acquired = true;
            }

            if (!acquired)
            {
                throw new IOException(
                    "Der Import-Wiederherstellungsmarker ist durch einen anderen Vorgang gesperrt.");
            }

            return action();
        }
        finally
        {
            if (acquired)
                mutex.ReleaseMutex();
        }
    }

    private static string BuildSynchronizationName(string projectRoot)
    {
        var markerPath = Path.GetFullPath(MarkerPath(projectRoot));
        if (OperatingSystem.IsWindows())
            markerPath = markerPath.ToUpperInvariant();

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(markerPath));
        return "SewerStudio.ImportTransaction." + Convert.ToHexString(hash);
    }

    private static string MarkerPath(string projectRoot)
        => Path.Combine(projectRoot, MarkerFileName);

    private static string SafeMarkerPath(string projectRoot)
    {
        var paths = new ProjectWritePathGuard(projectRoot);
        paths.EnsureSafeDirectoryTarget(projectRoot);
        return paths.EnsureSafeFileTarget(MarkerPath(projectRoot));
    }

    private static bool IsValid(ImportTransactionMarker? marker)
        => marker is not null
           && !string.IsNullOrWhiteSpace(marker.TxId)
           && !string.IsNullOrWhiteSpace(marker.Label)
           && !string.IsNullOrWhiteSpace(marker.StagingRoot)
           && marker.PublishedTargets is not null
           && marker.PublishedTargets.All(target =>
               target is not null
               && !string.IsNullOrWhiteSpace(target.RelativePath)
               && !string.IsNullOrWhiteSpace(target.Sha256));

    /// <summary>
    /// Zu viele Ziele. Getrennt von <see cref="IsValid"/>, damit die Meldung beim
    /// Schreiben den echten Grund nennt und nicht "unvollstaendig".
    /// </summary>
    private static bool ExceedsTargetLimit(ImportTransactionMarker? marker)
        => (marker?.PublishedTargets?.Count ?? 0) > MaxPublishedTargets;

    private static ImportTransactionJournalReadResult ReadFailed()
        => ImportTransactionJournalReadResult.Failed(
            "Der Import-Wiederherstellungsmarker ist vorhanden, konnte aber nicht sicher gelesen werden.");

    private static bool IsExpectedAccessFailure(Exception ex)
        => ex is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or NotSupportedException
            or WaitHandleCannotBeOpenedException;
}
