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

/// <summary>
/// Schreibt/liest/loescht den Transaktions-Marker (<c>.import-transaction.json</c>) im
/// Projekt-Root. Kern der Absturz-Atomaritaet: der Marker existiert nur zwischen dem
/// Beginn der Datei-Veroeffentlichung und dem sauberen Abschluss der Transaktion.
/// </summary>
public interface IImportTransactionJournal
{
    void Begin(string projectRoot, ImportTransactionMarker marker);

    ImportTransactionMarker? TryRead(string projectRoot);

    void Clear(string projectRoot);
}
