namespace AuswertungPro.Next.Application.Import;

/// <summary>Eine vom Import veroeffentlichte Zieldatei (Pfad relativ zum Projekt-Root + Inhalts-Hash).</summary>
public sealed record PublishedFileInfo(string RelativePath, string Sha256);

/// <summary>
/// Persistenter Marker einer laufenden Import-Transaktion. Existiert er beim naechsten
/// Projekt-Laden noch, ist der Prozess mitten in der Transaktion gestorben (siehe
/// <see cref="IImportTransactionJournal"/>).
/// </summary>
public sealed record ImportTransactionMarker(
    string TxId,
    DateTime StartedUtc,
    string Label,
    string StagingRoot,
    IReadOnlyList<PublishedFileInfo> PublishedTargets,
    string? RestorePointPath);

/// <summary>Unterscheidet sicher zwischen fehlendem, gelesenem und unlesbarem Marker.</summary>
public enum ImportTransactionJournalReadOutcome
{
    Missing,
    Loaded,
    Failed
}

/// <summary>Typisiertes Leseergebnis des Import-Journals.</summary>
public sealed record ImportTransactionJournalReadResult(
    ImportTransactionJournalReadOutcome Outcome,
    ImportTransactionMarker? Marker,
    string? ErrorMessage)
{
    public static ImportTransactionJournalReadResult Missing()
        => new(ImportTransactionJournalReadOutcome.Missing, null, null);

    public static ImportTransactionJournalReadResult Loaded(ImportTransactionMarker marker)
        => new(ImportTransactionJournalReadOutcome.Loaded, marker, null);

    public static ImportTransactionJournalReadResult Failed(string message)
        => new(ImportTransactionJournalReadOutcome.Failed, null, message);
}

/// <summary>
/// Schreibt/liest/loescht den Transaktions-Marker (<c>.import-transaction.json</c>) im
/// Projekt-Root. Kern der Absturz-Atomaritaet: der Marker existiert nur zwischen dem
/// Beginn der Datei-Veroeffentlichung und dem sauberen Abschluss der Transaktion.
/// </summary>
public interface IImportTransactionJournal
{
    void Begin(string projectRoot, ImportTransactionMarker marker);

    /// <summary>
    /// Liest den Marker mit eindeutigem Ergebnis. Die Standardimplementierung bewahrt
    /// bestehende Journal-Fassaden; Datei-Journale sollen Lesefehler explizit melden.
    /// </summary>
    ImportTransactionJournalReadResult Read(string projectRoot)
    {
        var marker = TryRead(projectRoot);
        return marker is null
            ? ImportTransactionJournalReadResult.Missing()
            : ImportTransactionJournalReadResult.Loaded(marker);
    }

    ImportTransactionMarker? TryRead(string projectRoot);

    void Clear(string projectRoot);
}
