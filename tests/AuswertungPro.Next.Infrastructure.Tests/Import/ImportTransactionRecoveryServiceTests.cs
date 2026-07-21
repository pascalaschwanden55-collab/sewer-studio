using System.IO;
using System.Security.Cryptography;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Import;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

public sealed class ImportTransactionRecoveryServiceTests
{
    private sealed class TempDir : IDisposable
    {
        public string Path { get; } =
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "itr_" + Guid.NewGuid().ToString("N"));
        public TempDir() => Directory.CreateDirectory(Path);
        public void Dispose() { try { Directory.Delete(Path, recursive: true); } catch { } }
    }

    private static string Sha256Hex(string path)
        => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    // Legt eine veroeffentlichte Datei an und schreibt einen Marker, der sie referenziert.
    private static (FileImportTransactionJournal journal, string publishedFile) Arrange(
        string root, string txId, params string[] extraStagingFiles)
    {
        var relative = "Bilder/neu.jpg";
        var full = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, "importierter-inhalt");

        var stagingRoot = Path.Combine(root, ".import-staging");
        Directory.CreateDirectory(stagingRoot);
        foreach (var f in extraStagingFiles)
            File.WriteAllText(Path.Combine(stagingRoot, f), "rest");

        var journal = new FileImportTransactionJournal();
        journal.Begin(root, new ImportTransactionMarker(
            TxId: txId,
            StartedUtc: new DateTime(2026, 7, 21, 9, 0, 0, DateTimeKind.Utc),
            Label: "PDF",
            StagingRoot: stagingRoot,
            PublishedTargets: [new PublishedFileInfo(relative, Sha256Hex(full))],
            RestorePointPath: null));
        return (journal, full);
    }

    [Fact]
    public void Committed_marker_raeumt_nur_auf_und_behaelt_dateien()
    {
        using var dir = new TempDir();
        var (journal, published) = Arrange(dir.Path, "tx-1", "rest.stage");
        var service = new ImportTransactionRecoveryService(journal);

        // Commit-Beweis == Marker-TxId -> abgeschlossen.
        var result = service.RecoverIfNeeded(dir.Path, committedImportTxId: "tx-1");

        Assert.Equal(ImportRecoveryOutcome.CompletedCleanup, result.Outcome);
        Assert.True(File.Exists(published));                                   // Datei bleibt
        Assert.False(Directory.Exists(Path.Combine(dir.Path, ".import-staging")));  // Staging weg
        Assert.Null(journal.TryRead(dir.Path));                               // Marker weg
    }

    [Fact]
    public void Nicht_committed_rollt_veroeffentlichte_dateien_zurueck()
    {
        using var dir = new TempDir();
        var (journal, published) = Arrange(dir.Path, "tx-2");
        var service = new ImportTransactionRecoveryService(journal);

        // Projekt traegt eine ANDERE (aeltere) TxId -> Import lief nicht durch.
        var result = service.RecoverIfNeeded(dir.Path, committedImportTxId: "alt-99");

        Assert.Equal(ImportRecoveryOutcome.RolledBack, result.Outcome);
        Assert.False(File.Exists(published));                                 // Datei zurueckgerollt
        Assert.False(Directory.Exists(Path.Combine(dir.Path, ".import-staging")));
        Assert.Null(journal.TryRead(dir.Path));
    }

    [Fact]
    public void Rollback_loescht_datei_mit_abweichendem_hash_nicht()
    {
        using var dir = new TempDir();
        var (journal, published) = Arrange(dir.Path, "tx-3");
        // Datei wurde nach dem Import anderweitig veraendert -> darf nicht geloescht werden.
        File.WriteAllText(published, "vom-nutzer-geaendert");
        var service = new ImportTransactionRecoveryService(journal);

        var result = service.RecoverIfNeeded(dir.Path, committedImportTxId: null);

        Assert.Equal(ImportRecoveryOutcome.RolledBack, result.Outcome);
        Assert.True(File.Exists(published));   // bleibt wegen SHA-Abweichung
    }

    [Fact]
    public void Kein_marker_liefert_none()
    {
        using var dir = new TempDir();
        var service = new ImportTransactionRecoveryService(new FileImportTransactionJournal());

        var result = service.RecoverIfNeeded(dir.Path, committedImportTxId: "egal");

        Assert.Equal(ImportRecoveryOutcome.None, result.Outcome);
    }

    [Fact]
    public void Zweiter_lauf_nach_rollback_ist_none_idempotent()
    {
        using var dir = new TempDir();
        var (journal, _) = Arrange(dir.Path, "tx-4");
        var service = new ImportTransactionRecoveryService(journal);

        service.RecoverIfNeeded(dir.Path, committedImportTxId: null);
        var second = service.RecoverIfNeeded(dir.Path, committedImportTxId: null);

        Assert.Equal(ImportRecoveryOutcome.None, second.Outcome);
    }
}
