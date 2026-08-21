using System.IO;
using System.Security.Cryptography;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Tests.Backup;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

public sealed class ImportTransactionRecoveryServiceTests
{
    private sealed class UnclearableJournal(ImportTransactionMarker marker)
        : IImportTransactionJournal
    {
        public void Begin(string projectRoot, ImportTransactionMarker newMarker)
        {
        }

        public ImportTransactionMarker? TryRead(string projectRoot) => marker;

        public void Clear(string projectRoot)
        {
            // Simuliert einen Marker, der wegen eines Datei-/Rechtefehlers liegen bleibt.
        }
    }

    private sealed class ReplacingJournal(
        ImportTransactionMarker initialMarker,
        ImportTransactionMarker replacementMarker) : IImportTransactionJournal
    {
        private ImportTransactionMarker _currentMarker = initialMarker;

        public string? ExpectedClearTxId { get; private set; }

        public void Begin(string projectRoot, ImportTransactionMarker newMarker)
        {
        }

        public ImportTransactionMarker? TryRead(string projectRoot) => _currentMarker;

        public void Clear(string projectRoot)
            => throw new InvalidOperationException("Clear ohne Besitzpruefung darf nicht laufen.");

        public bool ClearIfOwned(string projectRoot, string expectedTxId)
        {
            ExpectedClearTxId = expectedTxId;
            _currentMarker = replacementMarker;
            return false;
        }
    }

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

        Assert.Equal(ImportRecoveryOutcome.Blocked, result.Outcome);
        Assert.True(File.Exists(published));   // bleibt wegen SHA-Abweichung
        Assert.NotNull(journal.TryRead(dir.Path));
        Assert.Contains("unvollstaendig", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rollback_sperrt_wenn_am_erwarteten_Dateipfad_ein_Ordner_liegt()
    {
        using var dir = new TempDir();
        var (journal, published) = Arrange(dir.Path, "tx-ziel-ist-ordner");
        File.Delete(published);
        Directory.CreateDirectory(published);
        var service = new ImportTransactionRecoveryService(journal);

        var result = service.RecoverIfNeeded(dir.Path, committedImportTxId: null);

        Assert.Equal(ImportRecoveryOutcome.Blocked, result.Outcome);
        Assert.True(Directory.Exists(published));
        Assert.NotNull(journal.TryRead(dir.Path));
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
    public void Beschaedigter_marker_sperrt_recovery_und_bleibt_zur_pruefung_erhalten()
    {
        using var dir = new TempDir();
        var markerPath = Path.Combine(dir.Path, FileImportTransactionJournal.MarkerFileName);
        File.WriteAllText(markerPath, "{ kaputt");
        var service = new ImportTransactionRecoveryService(new FileImportTransactionJournal());

        var result = service.RecoverIfNeeded(dir.Path, committedImportTxId: null);

        Assert.Equal(ImportRecoveryOutcome.Blocked, result.Outcome);
        Assert.Contains("nicht sicher gelesen", result.Message, StringComparison.Ordinal);
        Assert.True(File.Exists(markerPath));
        Assert.Equal("{ kaputt", File.ReadAllText(markerPath));
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

    // Schreibt einen Marker mit frei waehlbaren Zielen (simuliert manipulierte Marker).
    private static FileImportTransactionJournal ArrangeMarker(
        string root, string txId, params PublishedFileInfo[] targets)
    {
        var journal = new FileImportTransactionJournal();
        journal.Begin(root, new ImportTransactionMarker(
            TxId: txId,
            StartedUtc: new DateTime(2026, 7, 25, 9, 0, 0, DateTimeKind.Utc),
            Label: "PDF",
            StagingRoot: Path.Combine(root, ".import-staging"),
            PublishedTargets: targets,
            RestorePointPath: null));
        return journal;
    }

    [Fact]
    public void Rollback_loescht_relativen_markereintrag_ausserhalb_des_projekts_nicht()
    {
        using var projectDir = new TempDir();
        using var outsideDir = new TempDir();
        var outsideFile = Path.Combine(outsideDir.Path, "fremd.txt");
        File.WriteAllText(outsideFile, "fremde-datei");

        // Manipulierter Marker: relativer Ausbruch "../...", Hash stimmt trotzdem.
        var escapeRelative = $"../{Path.GetFileName(outsideDir.Path)}/fremd.txt";
        var journal = ArrangeMarker(projectDir.Path, "tx-esc",
            new PublishedFileInfo(escapeRelative, Sha256Hex(outsideFile)));
        var service = new ImportTransactionRecoveryService(journal);

        var result = service.RecoverIfNeeded(projectDir.Path, committedImportTxId: null);

        Assert.Equal(ImportRecoveryOutcome.Blocked, result.Outcome);
        Assert.True(File.Exists(outsideFile));   // trotz passendem Hash NICHT geloescht
        Assert.Contains("nicht angefasst", result.Message);
        Assert.NotNull(journal.TryRead(projectDir.Path));
    }

    [Fact]
    public void Rollback_loescht_absoluten_markereintrag_ausserhalb_des_projekts_nicht()
    {
        using var projectDir = new TempDir();
        using var outsideDir = new TempDir();
        var outsideFile = Path.Combine(outsideDir.Path, "fremd.txt");
        File.WriteAllText(outsideFile, "fremde-datei");

        // Manipulierter Marker: absoluter Pfad (Path.Combine laesst ihn unveraendert durch).
        var journal = ArrangeMarker(projectDir.Path, "tx-abs",
            new PublishedFileInfo(outsideFile, Sha256Hex(outsideFile)));
        var service = new ImportTransactionRecoveryService(journal);

        var result = service.RecoverIfNeeded(projectDir.Path, committedImportTxId: null);

        Assert.Equal(ImportRecoveryOutcome.Blocked, result.Outcome);
        Assert.True(File.Exists(outsideFile));
        Assert.Contains("nicht angefasst", result.Message);
        Assert.NotNull(journal.TryRead(projectDir.Path));
    }

    [Fact]
    public void Rollback_sperrt_markerpfad_mit_nullzeichen_ohne_mutation()
    {
        using var projectDir = new TempDir();
        var legitRelative = "Bilder/neu.jpg";
        var legitFull = Path.Combine(
            projectDir.Path,
            legitRelative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(legitFull)!);
        File.WriteAllText(legitFull, "importierter-inhalt");

        var journal = ArrangeMarker(
            projectDir.Path,
            "tx-nullzeichen",
            new PublishedFileInfo("Bilder/ungueltig\0.jpg", new string('0', 64)),
            new PublishedFileInfo(legitRelative, Sha256Hex(legitFull)));
        var service = new ImportTransactionRecoveryService(journal);

        var result = service.RecoverIfNeeded(projectDir.Path, committedImportTxId: null);

        Assert.Equal(ImportRecoveryOutcome.Blocked, result.Outcome);
        Assert.False(result.ProjectFolderModified);
        Assert.True(File.Exists(legitFull));
        Assert.Contains("nicht angefasst", result.Message, StringComparison.Ordinal);
        Assert.NotNull(journal.TryRead(projectDir.Path));
    }

    [JunctionFact]
    public void Rollback_sperrt_datei_symlink_und_behaelt_link_ziel_und_marker()
    {
        using var outsideDir = new TempDir();
        using var projectDir = new TempDir();
        var outsideFile = Path.Combine(outsideDir.Path, "kundenoriginal.txt");
        File.WriteAllText(outsideFile, "unveraendertes-kundenoriginal");

        var relativeLink = "Bilder/import-link.txt";
        var linkPath = Path.Combine(
            projectDir.Path,
            relativeLink.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(linkPath)!);
        File.CreateSymbolicLink(linkPath, outsideFile);

        var journal = ArrangeMarker(
            projectDir.Path,
            "tx-datei-symlink",
            new PublishedFileInfo(relativeLink, Sha256Hex(linkPath)));
        var service = new ImportTransactionRecoveryService(journal);

        var result = service.RecoverIfNeeded(projectDir.Path, committedImportTxId: null);

        Assert.Equal(ImportRecoveryOutcome.Blocked, result.Outcome);
        Assert.False(result.ProjectFolderModified);
        Assert.True(File.Exists(linkPath));
        Assert.True(File.Exists(outsideFile));
        Assert.Equal("unveraendertes-kundenoriginal", File.ReadAllText(outsideFile));
        Assert.Contains("Verknuepfung", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(journal.TryRead(projectDir.Path));
    }

    [Fact]
    public void Preflight_sperrt_zwei_ziele_wenn_das_zweite_schreibgeschuetzt_ist()
    {
        using var projectDir = new TempDir();
        var firstRelative = "Bilder/erstes.jpg";
        var secondRelative = "Bilder/zweites.jpg";
        var firstPath = Path.Combine(projectDir.Path, firstRelative);
        var secondPath = Path.Combine(projectDir.Path, secondRelative);
        Directory.CreateDirectory(Path.GetDirectoryName(firstPath)!);
        File.WriteAllText(firstPath, "erstes-importziel");
        File.WriteAllText(secondPath, "zweites-importziel");
        File.SetAttributes(secondPath, File.GetAttributes(secondPath) | FileAttributes.ReadOnly);

        var journal = ArrangeMarker(
            projectDir.Path,
            "tx-readonly",
            new PublishedFileInfo(firstRelative, Sha256Hex(firstPath)),
            new PublishedFileInfo(secondRelative, Sha256Hex(secondPath)));
        var service = new ImportTransactionRecoveryService(journal);

        try
        {
            var result = service.RecoverIfNeeded(projectDir.Path, committedImportTxId: null);

            Assert.Equal(ImportRecoveryOutcome.Blocked, result.Outcome);
            Assert.False(result.ProjectFolderModified);
            Assert.Equal("erstes-importziel", File.ReadAllText(firstPath));
            Assert.Equal("zweites-importziel", File.ReadAllText(secondPath));
            Assert.Contains("schreibgeschuetzt", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(journal.TryRead(projectDir.Path));
        }
        finally
        {
            if (File.Exists(secondPath))
            {
                File.SetAttributes(
                    secondPath,
                    File.GetAttributes(secondPath) & ~FileAttributes.ReadOnly);
            }
        }
    }

    [Fact]
    public void Preflight_sperrt_zwei_ziele_wenn_das_zweite_nicht_exklusiv_geoeffnet_werden_kann()
    {
        using var projectDir = new TempDir();
        var firstRelative = "Bilder/erstes.jpg";
        var secondRelative = "Bilder/zweites.jpg";
        var firstPath = Path.Combine(projectDir.Path, firstRelative);
        var secondPath = Path.Combine(projectDir.Path, secondRelative);
        Directory.CreateDirectory(Path.GetDirectoryName(firstPath)!);
        File.WriteAllText(firstPath, "erstes-importziel");
        File.WriteAllText(secondPath, "zweites-importziel");

        var journal = ArrangeMarker(
            projectDir.Path,
            "tx-delete-gesperrt",
            new PublishedFileInfo(firstRelative, Sha256Hex(firstPath)),
            new PublishedFileInfo(secondRelative, Sha256Hex(secondPath)));
        var service = new ImportTransactionRecoveryService(journal);
        using var deleteSperre = new FileStream(
            secondPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        var result = service.RecoverIfNeeded(projectDir.Path, committedImportTxId: null);

        Assert.Equal(ImportRecoveryOutcome.Blocked, result.Outcome);
        Assert.False(result.ProjectFolderModified);
        Assert.Equal("erstes-importziel", File.ReadAllText(firstPath));
        Assert.Equal("zweites-importziel", File.ReadAllText(secondPath));
        Assert.Contains("verwendet", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(journal.TryRead(projectDir.Path));
    }

    /// <summary>
    /// Frueher hiess dieser Fall "nimmt gueltige Ziele mit und sperrt nur die Ausbrueche":
    /// die sicheren Ziele wurden sofort geloescht und erst danach fiel auf, dass der
    /// Rollback unvollstaendig bleibt. Der Benutzer bekam dann eine Box, die im selben
    /// Atemzug "3 Datei(en) zurueckgenommen" UND "nicht veraendert" sagte.
    ///
    /// Eine Ruecknahme ist alles oder nichts: Ein No-op kann man wiederholen, eine
    /// Loeschung nicht. Steht ein einziges Ziel im Weg, bleibt der Projektordner
    /// unangetastet und der Marker liegen.
    /// </summary>
    [Fact]
    public void Rollback_loescht_nichts_wenn_ein_einziges_ziel_unsicher_ist()
    {
        using var projectDir = new TempDir();
        using var outsideDir = new TempDir();
        var outsideFile = Path.Combine(outsideDir.Path, "fremd.txt");
        File.WriteAllText(outsideFile, "fremde-datei");

        var legitRelative = "Bilder/neu.jpg";
        var legitFull = Path.Combine(projectDir.Path, legitRelative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(legitFull)!);
        File.WriteAllText(legitFull, "importierter-inhalt");

        var journal = ArrangeMarker(projectDir.Path, "tx-mix",
            new PublishedFileInfo(legitRelative, Sha256Hex(legitFull)),
            new PublishedFileInfo($"../{Path.GetFileName(outsideDir.Path)}/fremd.txt", Sha256Hex(outsideFile)));
        var service = new ImportTransactionRecoveryService(journal);

        var result = service.RecoverIfNeeded(projectDir.Path, committedImportTxId: null);

        Assert.Equal(ImportRecoveryOutcome.Blocked, result.Outcome);
        Assert.True(File.Exists(legitFull));     // NICHTS geloescht, auch nicht das gueltige Ziel
        Assert.True(File.Exists(outsideFile));   // Ausbruch ohnehin gesperrt
        Assert.False(result.ProjectFolderModified);
        Assert.NotNull(journal.TryRead(projectDir.Path));
    }

    [Fact]
    public void Gesperrte_ruecknahme_nennt_die_blockierende_datei_und_den_weg_hinaus()
    {
        using var projectDir = new TempDir();
        var (journal, published) = Arrange(projectDir.Path, "tx-hinweis");

        // Der Benutzer hat die Datei nach dem Import bearbeitet -> Hash passt nicht mehr.
        File.WriteAllText(published, "vom benutzer geaendert");
        var service = new ImportTransactionRecoveryService(journal);

        var result = service.RecoverIfNeeded(projectDir.Path, committedImportTxId: null);

        Assert.Equal(ImportRecoveryOutcome.Blocked, result.Outcome);
        Assert.True(File.Exists(published));
        Assert.False(result.ProjectFolderModified);
        // Ohne Namen und ohne Ausweg steht der Benutzer vor einem Projekt, das nicht
        // mehr aufgeht: beide Oeffnen-Wege enden bei Blocked.
        Assert.Contains(Path.GetFileName(published), result.Message);
        Assert.Contains(FileImportTransactionJournal.MarkerFileName, result.Message);
        Assert.DoesNotContain("nichts veraendert", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("nicht veraendert", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Vollstaendig_moegliche_ruecknahme_laeuft_weiterhin_durch()
    {
        using var projectDir = new TempDir();
        var (journal, published) = Arrange(projectDir.Path, "tx-sauber");
        var service = new ImportTransactionRecoveryService(journal);

        var result = service.RecoverIfNeeded(projectDir.Path, committedImportTxId: null);

        Assert.Equal(ImportRecoveryOutcome.RolledBack, result.Outcome);
        Assert.False(File.Exists(published));
        Assert.True(result.ProjectFolderModified);
    }

    /// <summary>
    /// Der Arbeitsordner gehoert in denselben Preflight wie die Zieldateien. Sonst
    /// werden erst alle Ziele geloescht und danach faellt auf, dass ".import-staging"
    /// eine Datei oder eine Junction ist - genau der Teilzustand, den der Preflight
    /// verhindern soll.
    /// </summary>
    [Fact]
    public void Unsicherer_arbeitsordner_verhindert_jede_loeschung()
    {
        using var dir = new TempDir();
        var (journal, published) = Arrange(dir.Path, "tx-staging");

        var service = new ImportTransactionRecoveryService(
            journal,
            inspectStaging: (_, _) => "Am erwarteten Arbeitsordnerpfad liegt eine Datei.",
            cleanupStaging: (_, _) => null);

        var result = service.RecoverIfNeeded(dir.Path, committedImportTxId: null);

        Assert.Equal(ImportRecoveryOutcome.Blocked, result.Outcome);
        Assert.True(File.Exists(published));   // kein einziges Ziel angefasst
        Assert.False(result.ProjectFolderModified);
        Assert.DoesNotContain("Im Weg: .", result.Message, StringComparison.Ordinal);
        Assert.Contains(
            "Am erwarteten Arbeitsordnerpfad liegt eine Datei.",
            result.Message,
            StringComparison.Ordinal);
        Assert.NotNull(journal.TryRead(dir.Path));
    }

    [Fact]
    public void Staging_preflight_sperrt_vor_jeder_loeschung_wenn_kinddatei_verwendet_wird()
    {
        using var dir = new TempDir();
        var (journal, published) = Arrange(dir.Path, "tx-staging-gesperrt", "behalten.stage");
        var stagingRoot = Path.Combine(dir.Path, ".import-staging");
        var nestedRoot = Path.Combine(stagingRoot, "tiefer");
        Directory.CreateDirectory(nestedRoot);
        var lockedStagingFile = Path.Combine(nestedRoot, "gesperrt.stage");
        File.WriteAllText(lockedStagingFile, "gesperrter-rest");
        var service = new ImportTransactionRecoveryService(journal);
        using var deleteSperre = new FileStream(
            lockedStagingFile,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);

        var result = service.RecoverIfNeeded(dir.Path, committedImportTxId: null);

        Assert.Equal(ImportRecoveryOutcome.Blocked, result.Outcome);
        Assert.False(result.ProjectFolderModified);
        Assert.Equal("importierter-inhalt", File.ReadAllText(published));
        Assert.Equal("rest", File.ReadAllText(Path.Combine(stagingRoot, "behalten.stage")));
        Assert.Equal("gesperrter-rest", File.ReadAllText(lockedStagingFile));
        Assert.Contains("verwendet", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(journal.TryRead(dir.Path));
    }

    [Fact]
    public void Staging_preflight_sperrt_vor_jeder_loeschung_bei_schreibgeschuetztem_kind()
    {
        using var dir = new TempDir();
        var (journal, published) = Arrange(dir.Path, "tx-staging-readonly", "behalten.stage");
        var stagingRoot = Path.Combine(dir.Path, ".import-staging");
        var readOnlyStagingFile = Path.Combine(stagingRoot, "schreibgeschuetzt.stage");
        File.WriteAllText(readOnlyStagingFile, "schreibgeschuetzter-rest");
        File.SetAttributes(
            readOnlyStagingFile,
            File.GetAttributes(readOnlyStagingFile) | FileAttributes.ReadOnly);
        var service = new ImportTransactionRecoveryService(journal);

        try
        {
            var result = service.RecoverIfNeeded(dir.Path, committedImportTxId: null);

            Assert.Equal(ImportRecoveryOutcome.Blocked, result.Outcome);
            Assert.False(result.ProjectFolderModified);
            Assert.Equal("importierter-inhalt", File.ReadAllText(published));
            Assert.Equal("rest", File.ReadAllText(Path.Combine(stagingRoot, "behalten.stage")));
            Assert.Equal("schreibgeschuetzter-rest", File.ReadAllText(readOnlyStagingFile));
            Assert.Contains("schreibgeschuetzt", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(journal.TryRead(dir.Path));
        }
        finally
        {
            if (File.Exists(readOnlyStagingFile))
            {
                File.SetAttributes(
                    readOnlyStagingFile,
                    File.GetAttributes(readOnlyStagingFile) & ~FileAttributes.ReadOnly);
            }
        }
    }

    [JunctionFact]
    public void Staging_preflight_sperrt_verknuepftes_kind_ohne_link_oder_ziel_anzufassen()
    {
        using var dir = new TempDir();
        using var outsideDir = new TempDir();
        var (journal, published) = Arrange(dir.Path, "tx-staging-link", "behalten.stage");
        var outsideFile = Path.Combine(outsideDir.Path, "kundenoriginal.txt");
        File.WriteAllText(outsideFile, "unveraendertes-kundenoriginal");
        var stagingRoot = Path.Combine(dir.Path, ".import-staging");
        var linkPath = Path.Combine(stagingRoot, "original-link.txt");
        File.CreateSymbolicLink(linkPath, outsideFile);
        var service = new ImportTransactionRecoveryService(journal);

        var result = service.RecoverIfNeeded(dir.Path, committedImportTxId: null);

        Assert.Equal(ImportRecoveryOutcome.Blocked, result.Outcome);
        Assert.False(result.ProjectFolderModified);
        Assert.Equal("importierter-inhalt", File.ReadAllText(published));
        Assert.Equal("rest", File.ReadAllText(Path.Combine(stagingRoot, "behalten.stage")));
        Assert.True(File.Exists(linkPath));
        Assert.Equal("unveraendertes-kundenoriginal", File.ReadAllText(outsideFile));
        Assert.Contains("Verknuepfung", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(journal.TryRead(dir.Path));
    }

    [Fact]
    public void Fehlender_externer_stagingpfad_ist_nicht_automatisch_sicher()
    {
        using var dir = new TempDir();
        using var outsideDir = new TempDir();
        var publishedRelative = "Bilder/neu.jpg";
        var published = Path.Combine(dir.Path, publishedRelative);
        Directory.CreateDirectory(Path.GetDirectoryName(published)!);
        File.WriteAllText(published, "importierter-inhalt");
        var externalStaging = Path.Combine(outsideDir.Path, "erscheint-vielleicht-spaeter");
        var journal = new FileImportTransactionJournal();
        journal.Begin(dir.Path, new ImportTransactionMarker(
            TxId: "tx-staging-extern-fehlt",
            StartedUtc: new DateTime(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc),
            Label: "PDF",
            StagingRoot: externalStaging,
            PublishedTargets: [new PublishedFileInfo(publishedRelative, Sha256Hex(published))],
            RestorePointPath: null));
        var service = new ImportTransactionRecoveryService(journal);

        var result = service.RecoverIfNeeded(dir.Path, committedImportTxId: null);

        Assert.Equal(ImportRecoveryOutcome.Blocked, result.Outcome);
        Assert.False(result.ProjectFolderModified);
        Assert.Equal("importierter-inhalt", File.ReadAllText(published));
        Assert.False(Directory.Exists(externalStaging));
        Assert.Contains("erlaubten Projektort", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(journal.TryRead(dir.Path));
    }

    [Fact]
    public void Teilweise_gescheitertes_staging_cleanup_meldet_projektordner_als_veraendert()
    {
        using var dir = new TempDir();
        var stagingRoot = Path.Combine(dir.Path, ".import-staging");
        Directory.CreateDirectory(stagingRoot);
        var bereitsEntfernterRest = Path.Combine(stagingRoot, "bereits-entfernt.stage");
        var gebliebenerRest = Path.Combine(stagingRoot, "geblieben.stage");
        File.WriteAllText(bereitsEntfernterRest, "rest-a");
        File.WriteAllText(gebliebenerRest, "rest-b");
        var journal = ArrangeMarker(dir.Path, "tx-staging-teilweise");

        var service = new ImportTransactionRecoveryService(
            journal,
            inspectStaging: (_, _) => null,
            cleanupStaging: (_, _) =>
            {
                File.Delete(bereitsEntfernterRest);
                return "Der Arbeitsordner konnte nur teilweise entfernt werden.";
            });

        var result = service.RecoverIfNeeded(dir.Path, committedImportTxId: null);

        Assert.Equal(ImportRecoveryOutcome.Blocked, result.Outcome);
        Assert.True(result.ProjectFolderModified);
        Assert.False(File.Exists(bereitsEntfernterRest));
        Assert.True(File.Exists(gebliebenerRest));
        Assert.NotNull(journal.TryRead(dir.Path));
    }

    /// <summary>
    /// Gespeicherter Import: Der Arbeitsordner verschwindet erfolgreich, danach
    /// scheitert das Loeschen des Markers. Der Projektordner IST damit veraendert -
    /// die Oberflaeche darf hier nicht "nicht veraendert" melden.
    /// </summary>
    [Fact]
    public void Committed_cleanup_meldet_den_projektordner_als_veraendert()
    {
        using var dir = new TempDir();
        var (_, published) = Arrange(dir.Path, "tx-committed");
        var marker = new ImportTransactionMarker(
            "tx-committed",
            DateTime.UtcNow,
            "Test",
            Path.Combine(dir.Path, ".import-staging"),
            new[] { new PublishedFileInfo("egal.txt", "00") },
            RestorePointPath: null);

        var service = new ImportTransactionRecoveryService(
            new UnclearableJournal(marker),
            inspectStaging: (_, _) => null,
            cleanupStaging: (_, _) => null);

        var result = service.RecoverIfNeeded(dir.Path, committedImportTxId: "tx-committed");

        Assert.Equal(ImportRecoveryOutcome.Blocked, result.Outcome);
        Assert.True(result.ProjectFolderModified);
        Assert.True(File.Exists(published));
    }

    [Fact]
    public void Recovery_loescht_keinen_zwischenzeitlich_ersetzten_marker()
    {
        using var dir = new TempDir();
        var stagingRoot = Path.Combine(dir.Path, ".import-staging");
        var eigenerMarker = new ImportTransactionMarker(
            "tx-eigen",
            new DateTime(2026, 8, 20, 9, 0, 0, DateTimeKind.Utc),
            "Test",
            stagingRoot,
            [],
            RestorePointPath: null);
        var fremderMarker = eigenerMarker with { TxId = "tx-fremd" };
        var journal = new ReplacingJournal(eigenerMarker, fremderMarker);
        var service = new ImportTransactionRecoveryService(
            journal,
            inspectStaging: (_, _) => null,
            cleanupStaging: (_, _) => null);

        var result = service.RecoverIfNeeded(dir.Path, committedImportTxId: eigenerMarker.TxId);

        Assert.Equal(ImportRecoveryOutcome.Blocked, result.Outcome);
        Assert.Equal(eigenerMarker.TxId, journal.ExpectedClearTxId);
        Assert.Same(fremderMarker, journal.TryRead(dir.Path));
    }

    [Fact]
    public void Aufraeumfehler_sperrt_recovery_und_marker_bleibt_fuer_naechsten_lauf()
    {
        using var dir = new TempDir();
        var (journal, published) = Arrange(dir.Path, "tx-cleanup", "rest.stage");
        var service = new ImportTransactionRecoveryService(
            journal,
            (_, _) => "Arbeitsordner konnte testweise nicht entfernt werden.");

        var result = service.RecoverIfNeeded(dir.Path, committedImportTxId: "tx-cleanup");

        Assert.Equal(ImportRecoveryOutcome.Blocked, result.Outcome);
        Assert.True(File.Exists(published));
        Assert.NotNull(journal.TryRead(dir.Path));
        Assert.Contains("nicht entfernt", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Arbeitsordnerpfad_als_Datei_sperrt_Recovery_und_behaelt_Marker()
    {
        using var dir = new TempDir();
        var stagingPath = Path.Combine(dir.Path, ".import-staging");
        File.WriteAllText(stagingPath, "kein Ordner");
        var journal = ArrangeMarker(dir.Path, "tx-staging-ist-datei");
        var service = new ImportTransactionRecoveryService(journal);

        var result = service.RecoverIfNeeded(dir.Path, committedImportTxId: null);

        Assert.Equal(ImportRecoveryOutcome.Blocked, result.Outcome);
        Assert.True(File.Exists(stagingPath));
        Assert.NotNull(journal.TryRead(dir.Path));
    }

    [Fact]
    public void Nicht_loeschbarer_marker_wird_nicht_als_erfolgreiche_recovery_gemeldet()
    {
        using var dir = new TempDir();
        var marker = new ImportTransactionMarker(
            TxId: "tx-marker-bleibt",
            StartedUtc: new DateTime(2026, 7, 26, 9, 0, 0, DateTimeKind.Utc),
            Label: "PDF",
            StagingRoot: Path.Combine(dir.Path, ".import-staging"),
            PublishedTargets: [],
            RestorePointPath: null);
        var service = new ImportTransactionRecoveryService(
            new UnclearableJournal(marker),
            (_, _) => null);

        var result = service.RecoverIfNeeded(
            dir.Path,
            committedImportTxId: marker.TxId);

        Assert.Equal(ImportRecoveryOutcome.Blocked, result.Outcome);
        Assert.Contains("Marker", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("nicht entfernt", result.Message, StringComparison.OrdinalIgnoreCase);
    }
}
