using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Tests.Backup;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Security-Waechter fuer den Absturzfall: Der Wiederherstellungs-Marker
/// (<c>.import-transaction.json</c>) ist eine Datei im Projekt und damit manipulierbar.
/// Diese Tests nageln fest, dass weder ein manipulierter Marker noch ein Absturz
/// mitten in der Journal-Operation zu Loeschungen ausserhalb des vorgesehenen
/// Rahmens fuehren kann. Sie werden rot, sobald eine spaetere Aenderung eine dieser
/// Grenzen aufweicht - auch dann, wenn "alles noch funktioniert".
/// </summary>
public sealed class ImportTransactionRecoverySecurityGuardTests
{
    private sealed class TempDir : IDisposable
    {
        public string Path { get; } =
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "itrsec_" + Guid.NewGuid().ToString("N"));
        public TempDir() => Directory.CreateDirectory(Path);
        public void Dispose() { try { Directory.Delete(Path, recursive: true); } catch { } }
    }

    private static string Sha256Hex(string path)
        => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private static ImportTransactionMarker Marker(
        string txId, string root, params PublishedFileInfo[] ziele) => new(
        TxId: txId,
        StartedUtc: new DateTime(2026, 8, 20, 9, 0, 0, DateTimeKind.Utc),
        Label: "PDF",
        StagingRoot: Path.Combine(root, ".import-staging"),
        PublishedTargets: ziele,
        RestorePointPath: null);

    /// <summary>
    /// Der Sperrname ist eine private Implementierungsdetails des Journals. Ueber
    /// Reflexion gekoppelt, damit dieser Test bei einer Umbenennung laut ausfaellt
    /// statt still eine Sperre zu testen, die das Journal gar nicht mehr nutzt.
    /// </summary>
    private static string JournalSperrName(string projectRoot)
    {
        var methode = typeof(FileImportTransactionJournal).GetMethod(
            "BuildSynchronizationName", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(methode);
        return (string)methode!.Invoke(null, new object[] { projectRoot })!;
    }

    /// <summary>
    /// Absturz mitten in der Journal-Operation: Ein Thread (statt Prozess) erwirbt die
    /// benannte Projektsperre und endet, ohne sie freizugeben - der Mutex verwaist.
    /// Die naechste Transaktion muss die verwaiste Sperre uebernehmen koennen
    /// (AbandonedMutexException) und normal arbeiten; die Sperre darf das Projekt
    /// nach einem Absturz nicht dauerhaft lahmlegen.
    /// </summary>
    [Fact]
    public void Verwaiste_journalsperre_nach_absturz_laesst_naechste_transaktion_zu()
    {
        using var dir = new TempDir();
        var sperrName = JournalSperrName(dir.Path);
        using var bereit = new ManualResetEventSlim();
        var sterbender = new Thread(() =>
        {
            var mutex = new Mutex(initiallyOwned: false, sperrName);
            mutex.WaitOne();
            bereit.Set();
            // Bewusst KEIN ReleaseMutex und kein Dispose: Genau so sieht es aus,
            // wenn der Prozess zwischen Pruefung und Schreiben des Markers stirbt.
        });
        sterbender.Start();
        Assert.True(bereit.Wait(TimeSpan.FromSeconds(5)));
        sterbender.Join();

        var journal = new FileImportTransactionJournal();
        journal.Begin(dir.Path, Marker("tx-nach-absturz", dir.Path));

        Assert.Equal("tx-nach-absturz", journal.TryRead(dir.Path)?.TxId);
        Assert.True(journal.ClearIfOwned(dir.Path, "tx-nach-absturz"));
        Assert.Null(journal.TryRead(dir.Path));
    }

    /// <summary>
    /// Stromausfall zwischen Datei-Anlage und Inhalt: Die Marker-Datei existiert mit
    /// 0 Bytes. Ein fehlender Marker ist harmlos, ein VORHANDENER unleserlicher ist es
    /// nicht - die Recovery muss sperren, ohne ein einziges Byte anzufassen.
    /// </summary>
    [Fact]
    public void Absturz_beim_marker_schreiben_leere_datei_sperrt_ohne_mutation()
    {
        using var dir = new TempDir();
        var markerPfad = Path.Combine(dir.Path, FileImportTransactionJournal.MarkerFileName);
        File.WriteAllBytes(markerPfad, []);
        var unbeteiligt = Path.Combine(dir.Path, "projekt.txt");
        File.WriteAllText(unbeteiligt, "gehoert-nicht-dem-import");

        var journal = new FileImportTransactionJournal();
        var ergebnis = new ImportTransactionRecoveryService(journal)
            .RecoverIfNeeded(dir.Path, committedImportTxId: null);

        Assert.Equal(ImportRecoveryOutcome.Blocked, ergebnis.Outcome);
        Assert.False(ergebnis.ProjectFolderModified);
        Assert.Empty(File.ReadAllBytes(markerPfad));
        Assert.Equal("gehoert-nicht-dem-import", File.ReadAllText(unbeteiligt));
    }

    /// <summary>
    /// Am Marker-Pfad liegt ein Verzeichnis (Angriff oder Unfall). Lesen muss als
    /// "nicht sicher lesbar" gelten - nicht als "kein Marker". Sonst liesse sich die
    /// Wiederherstellung durch Umformen des Markers unter den Tisch wischen.
    /// </summary>
    [Fact]
    public void Markerpfad_als_verzeichnis_sperrt_recovery_ohne_mutation()
    {
        using var dir = new TempDir();
        var markerPfad = Path.Combine(dir.Path, FileImportTransactionJournal.MarkerFileName);
        Directory.CreateDirectory(markerPfad);
        var unbeteiligt = Path.Combine(dir.Path, "projekt.txt");
        File.WriteAllText(unbeteiligt, "inhalt");

        var journal = new FileImportTransactionJournal();
        Assert.Equal(ImportTransactionJournalReadOutcome.Failed, journal.Read(dir.Path).Outcome);

        var ergebnis = new ImportTransactionRecoveryService(journal)
            .RecoverIfNeeded(dir.Path, committedImportTxId: null);

        Assert.Equal(ImportRecoveryOutcome.Blocked, ergebnis.Outcome);
        Assert.False(ergebnis.ProjectFolderModified);
        Assert.True(Directory.Exists(markerPfad));
        Assert.Equal("inhalt", File.ReadAllText(unbeteiligt));
    }

    /// <summary>
    /// Am Marker-Pfad liegt eine Verknuepfung auf ein fremdes Verzeichnis. Das Journal
    /// darf dem Link weder lesend folgen noch ihn als "kein Marker" behandeln; die
    /// Recovery sperrt und das Fremdziel bleibt unangetastet.
    /// </summary>
    [JunctionFact]
    public void Markerpfad_als_verknuepfung_sperrt_recovery_und_behaelt_fremdziel()
    {
        using var projektDir = new TempDir();
        using var fremdDir = new TempDir();
        var fremdDatei = Path.Combine(fremdDir.Path, "wichtig.txt");
        File.WriteAllText(fremdDatei, "nicht loeschen");
        var markerPfad = Path.Combine(projektDir.Path, FileImportTransactionJournal.MarkerFileName);
        JunctionTestSupport.CreateDirectoryLink(markerPfad, fremdDir.Path);

        var journal = new FileImportTransactionJournal();
        var ergebnis = new ImportTransactionRecoveryService(journal)
            .RecoverIfNeeded(projektDir.Path, committedImportTxId: null);

        Assert.Equal(ImportRecoveryOutcome.Blocked, ergebnis.Outcome);
        Assert.False(ergebnis.ProjectFolderModified);
        Assert.True(File.Exists(fremdDatei));
        Assert.True(Directory.Exists(markerPfad));
    }

    /// <summary>
    /// Der schwerste Staging-Angriff: Ein manipulierter, als "gespeichert"
    /// behauptender Marker nennt den Projekt-Root selbst als Arbeitsordner. Der
    /// Committed-Pfad loescht den Arbeitsordner REKURSIV - ohne die Ortspruefung
    /// waere das das gesamte Projekt. Muss sperren, bevor irgendetwas passiert.
    /// </summary>
    [Fact]
    public void Committed_marker_mit_projektroot_als_arbeitsordner_loescht_kein_projekt()
    {
        using var dir = new TempDir();
        var projektDatei = Path.Combine(dir.Path, "projekt.txt");
        File.WriteAllText(projektDatei, "projektinhalt");
        var journal = new FileImportTransactionJournal();
        journal.Begin(dir.Path, new ImportTransactionMarker(
            TxId: "tx-root-staging",
            StartedUtc: new DateTime(2026, 8, 20, 9, 0, 0, DateTimeKind.Utc),
            Label: "PDF",
            StagingRoot: dir.Path,
            PublishedTargets: [],
            RestorePointPath: null));

        var ergebnis = new ImportTransactionRecoveryService(journal)
            .RecoverIfNeeded(dir.Path, committedImportTxId: "tx-root-staging");

        Assert.Equal(ImportRecoveryOutcome.Blocked, ergebnis.Outcome);
        Assert.False(ergebnis.ProjectFolderModified);
        Assert.Equal("projektinhalt", File.ReadAllText(projektDatei));
        Assert.NotNull(journal.TryRead(dir.Path));
    }

    /// <summary>
    /// Manipulierter Markereintrag: der Projekt-Root selbst als "Zieldatei". Die
    /// Grenzpruefung verlangt strikt UNTERHALB des Roots - der Root ist kein
    /// loeschbares Ziel, egal welcher Hash im Marker steht.
    /// </summary>
    [Fact]
    public void Manipulierter_marker_mit_projektroot_als_zieldatei_loescht_nichts()
    {
        using var dir = new TempDir();
        var projektDatei = Path.Combine(dir.Path, "projekt.txt");
        File.WriteAllText(projektDatei, "projektinhalt");
        var journal = new FileImportTransactionJournal();
        journal.Begin(dir.Path, Marker(
            "tx-root-ziel", dir.Path, new PublishedFileInfo(".", new string('0', 64))));

        var ergebnis = new ImportTransactionRecoveryService(journal)
            .RecoverIfNeeded(dir.Path, committedImportTxId: null);

        Assert.Equal(ImportRecoveryOutcome.Blocked, ergebnis.Outcome);
        Assert.False(ergebnis.ProjectFolderModified);
        Assert.Equal("projektinhalt", File.ReadAllText(projektDatei));
        Assert.NotNull(journal.TryRead(dir.Path));
    }

    /// <summary>
    /// Der TxId-Vergleich entscheidet, ob aufgeraeumt (Import gespeichert) oder
    /// zurueckgenommen (Import abgebrochen) wird. Er muss EXAKT bleiben: Eine
    /// Aufweichung (z.B. Gross-/Kleinschreibung ignorieren) wuerde einen fremden,
    /// offenen Import als "gespeichert" behandeln und seinen Beweis wegraumen.
    /// Im Zweifel gilt fail-closed: zuruecknehmen statt aufraeumen.
    /// </summary>
    [Fact]
    public void Txid_vergleich_ist_exakt_und_rollt_im_zweifel_zurueck_statt_aufzuraeumen()
    {
        using var dir = new TempDir();
        var relative = "Bilder/neu.jpg";
        var full = Path.Combine(dir.Path, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, "importierter-inhalt");
        var journal = new FileImportTransactionJournal();
        journal.Begin(dir.Path, Marker(
            "TX-GROSS", dir.Path, new PublishedFileInfo(relative, Sha256Hex(full))));

        var ergebnis = new ImportTransactionRecoveryService(journal)
            .RecoverIfNeeded(dir.Path, committedImportTxId: "tx-gross");

        Assert.Equal(ImportRecoveryOutcome.RolledBack, ergebnis.Outcome);
        Assert.False(File.Exists(full));
    }
}
