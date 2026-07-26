using System.IO;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Tests.Backup;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Schutz der Recovery-Loeschung: der Marker (.import-transaction.json) ist eine
/// manipulierbare Datei im Projekt — sein StagingRoot darf nur geloescht werden,
/// wenn er einem erwarteten Arbeitsordner neben der Projektdatei entspricht.
/// </summary>
public sealed class ImportTransactionRecoveryStagingGuardTests
{
    private sealed class TempDir : IDisposable
    {
        public string Path { get; } =
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "itrsg_" + Guid.NewGuid().ToString("N"));
        public TempDir() => Directory.CreateDirectory(Path);
        public void Dispose() { try { Directory.Delete(Path, recursive: true); } catch { } }
    }

    private static FileImportTransactionJournal WriteMarker(
        string projectRoot, string txId, string stagingRoot)
    {
        var journal = new FileImportTransactionJournal();
        journal.Begin(projectRoot, new ImportTransactionMarker(
            TxId: txId,
            StartedUtc: new DateTime(2026, 7, 21, 9, 0, 0, DateTimeKind.Utc),
            Label: "PDF",
            StagingRoot: stagingRoot,
            PublishedTargets: [],
            RestorePointPath: null));
        return journal;
    }

    [Fact]
    public void Fremder_stagingpfad_im_marker_wird_nicht_geloescht()
    {
        using var dir = new TempDir();
        var projectRoot = Path.Combine(dir.Path, "projekt");
        Directory.CreateDirectory(projectRoot);
        // Fremder Ordner ausserhalb des Projekts mit Inhalt, der keinesfalls weg darf.
        var foreign = Path.Combine(dir.Path, "fremd");
        Directory.CreateDirectory(foreign);
        var foreignFile = Path.Combine(foreign, "wichtig.txt");
        File.WriteAllText(foreignFile, "nicht loeschen");

        var journal = WriteMarker(projectRoot, "tx-1", stagingRoot: foreign);
        var service = new ImportTransactionRecoveryService(journal);

        var result = service.RecoverIfNeeded(projectRoot, committedImportTxId: "tx-1");

        Assert.Equal(ImportRecoveryOutcome.Blocked, result.Outcome);
        Assert.True(Directory.Exists(foreign));
        Assert.True(File.Exists(foreignFile));
        Assert.Contains("nicht geloescht", result.Message!);
        Assert.NotNull(journal.TryRead(projectRoot));
    }

    [Fact]
    public void Legitimer_stagingpfad_wird_weiterhin_geloescht()
    {
        using var dir = new TempDir();
        var stagingRoot = Path.Combine(dir.Path, ".import-staging");
        Directory.CreateDirectory(stagingRoot);
        File.WriteAllText(Path.Combine(stagingRoot, "rest.stage"), "rest");

        var journal = WriteMarker(dir.Path, "tx-2", stagingRoot);
        var service = new ImportTransactionRecoveryService(journal);

        var result = service.RecoverIfNeeded(dir.Path, committedImportTxId: "tx-2");

        Assert.Equal(ImportRecoveryOutcome.CompletedCleanup, result.Outcome);
        Assert.False(Directory.Exists(stagingRoot));
        Assert.DoesNotContain("Hinweis", result.Message!);
    }

    [Fact]
    public void Kanonischer_stagingpfad_unter_Projektdateien_wird_geloescht()
    {
        using var dir = new TempDir();
        var stagingRoot = Path.Combine(dir.Path, "Projektdateien", ".import-staging");
        Directory.CreateDirectory(stagingRoot);
        File.WriteAllText(Path.Combine(stagingRoot, "rest.stage"), "rest");

        var journal = WriteMarker(dir.Path, "tx-canonical", stagingRoot);
        var service = new ImportTransactionRecoveryService(journal);

        var result = service.RecoverIfNeeded(dir.Path, committedImportTxId: "tx-canonical");

        Assert.Equal(ImportRecoveryOutcome.CompletedCleanup, result.Outcome);
        Assert.False(Directory.Exists(stagingRoot));
        Assert.Null(journal.TryRead(dir.Path));
    }

    [Fact]
    public void Staging_unterordner_der_session_wird_geloescht()
    {
        using var dir = new TempDir();
        // Die Import-Session arbeitet in GUID-Unterordnern von .import-staging.
        var sessionDir = Path.Combine(dir.Path, ".import-staging", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sessionDir);
        File.WriteAllText(Path.Combine(sessionDir, "rest.stage"), "rest");

        var journal = WriteMarker(dir.Path, "tx-3", sessionDir);
        var service = new ImportTransactionRecoveryService(journal);

        var result = service.RecoverIfNeeded(dir.Path, committedImportTxId: "tx-3");

        Assert.Equal(ImportRecoveryOutcome.CompletedCleanup, result.Outcome);
        Assert.False(Directory.Exists(sessionDir));
        Assert.True(Directory.Exists(Path.Combine(dir.Path, ".import-staging")));   // Eltern bleibt
    }

    [Fact]
    public void Beliebiger_oder_verschachtelter_Unterordner_wird_nicht_als_Session_geloescht()
    {
        using var dir = new TempDir();
        var nested = Path.Combine(dir.Path, ".import-staging", "kein-guid", "wichtig");
        Directory.CreateDirectory(nested);
        var important = Path.Combine(nested, "behalten.txt");
        File.WriteAllText(important, "nicht loeschen");
        var journal = WriteMarker(dir.Path, "tx-nested", nested);
        var service = new ImportTransactionRecoveryService(journal);

        var result = service.RecoverIfNeeded(dir.Path, committedImportTxId: "tx-nested");

        Assert.Equal(ImportRecoveryOutcome.Blocked, result.Outcome);
        Assert.True(File.Exists(important));
        Assert.NotNull(journal.TryRead(dir.Path));
    }

    [JunctionFact]
    public void Junction_in_der_staging_elternkette_wird_nicht_rekursiv_geloescht()
    {
        using var projectDir = new TempDir();
        using var foreignDir = new TempDir();
        var foreignSession = Path.Combine(foreignDir.Path, "session");
        Directory.CreateDirectory(foreignSession);
        var foreignFile = Path.Combine(foreignSession, "wichtig.txt");
        File.WriteAllText(foreignFile, "nicht loeschen");

        var stagingLink = Path.Combine(projectDir.Path, ".import-staging");
        JunctionTestSupport.CreateDirectoryLink(stagingLink, foreignDir.Path);
        var journal = WriteMarker(
            projectDir.Path,
            "tx-junction",
            Path.Combine(stagingLink, "session"));
        var service = new ImportTransactionRecoveryService(journal);

        var result = service.RecoverIfNeeded(projectDir.Path, committedImportTxId: "tx-junction");

        Assert.Equal(ImportRecoveryOutcome.Blocked, result.Outcome);
        Assert.True(File.Exists(foreignFile));
        Assert.Contains("nicht geloescht", result.Message, StringComparison.Ordinal);
        Assert.NotNull(journal.TryRead(projectDir.Path));
    }
}
