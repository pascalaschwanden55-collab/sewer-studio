using System.IO;
using System.Linq;
using System.Text.Json;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>
/// Datei-basierter Transaktions-Marker <c>.import-transaction.json</c> im Projekt-Root,
/// atomar geschrieben ueber <see cref="AtomicTextFileWriter"/>. Ein fehlender Marker ist
/// eindeutig von einem vorhandenen, aber nicht sicher lesbaren Marker getrennt; Recovery
/// blockiert im zweiten Fall.
/// </summary>
public sealed class FileImportTransactionJournal : IImportTransactionJournal
{
    public const string MarkerFileName = ".import-transaction.json";

    private static readonly JsonSerializerOptions Opt = new() { WriteIndented = true };

    public void Begin(string projectRoot, ImportTransactionMarker marker)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectRoot);
        ArgumentNullException.ThrowIfNull(marker);

        var json = JsonSerializer.Serialize(marker, Opt);
        AtomicTextFileWriter.WriteAllText(MarkerPath(projectRoot), json);
    }

    public ImportTransactionJournalReadResult Read(string projectRoot)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
            return ImportTransactionJournalReadResult.Missing();

        string path;
        try
        {
            path = MarkerPath(projectRoot);
            _ = File.GetAttributes(path);
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
            var json = File.ReadAllText(path);
            var marker = JsonSerializer.Deserialize<ImportTransactionMarker>(json, Opt);
            return IsValid(marker)
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

    public ImportTransactionMarker? TryRead(string projectRoot)
        => Read(projectRoot) is
        {
            Outcome: ImportTransactionJournalReadOutcome.Loaded,
            Marker: not null
        } result
            ? result.Marker
            : null;

    public void Clear(string projectRoot)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
            return;

        try
        {
            var path = MarkerPath(projectRoot);
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Bleibt der Marker liegen, wird er beim naechsten Laden erneut geprueft — kein harter Fehler.
        }
    }

    private static string MarkerPath(string projectRoot)
        => Path.Combine(projectRoot, MarkerFileName);

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

    private static ImportTransactionJournalReadResult ReadFailed()
        => ImportTransactionJournalReadResult.Failed(
            "Der Import-Wiederherstellungsmarker ist vorhanden, konnte aber nicht sicher gelesen werden.");
}
