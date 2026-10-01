using System.IO;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Tests.Backup;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

public sealed class FileImportTransactionJournalTests
{
    private sealed class TempDir : IDisposable
    {
        public string Path { get; } =
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "itj_" + Guid.NewGuid().ToString("N"));
        public TempDir() => Directory.CreateDirectory(Path);
        public void Dispose() { try { Directory.Delete(Path, recursive: true); } catch { } }
    }

    private static ImportTransactionMarker SampleMarker(string txId = "tx-123") => new(
        TxId: txId,
        StartedUtc: new DateTime(2026, 7, 21, 10, 0, 0, DateTimeKind.Utc),
        Label: "PDF",
        StagingRoot: @"C:\P\.import-staging\abc",
        PublishedTargets: [new PublishedFileInfo("Bilder/1.jpg", "AABB"), new PublishedFileInfo("x.pdf", "CCDD")],
        RestorePointPath: @"C:\P\__RESTORE_POINTS\projekt\rp.json");

    [Fact]
    public void Begin_dann_TryRead_liefert_denselben_Marker()
    {
        using var dir = new TempDir();
        var journal = new FileImportTransactionJournal();
        var marker = SampleMarker();

        journal.Begin(dir.Path, marker);
        var read = journal.TryRead(dir.Path);

        Assert.NotNull(read);
        Assert.Equal(marker.TxId, read!.TxId);
        Assert.Equal(marker.Label, read.Label);
        Assert.Equal(marker.StagingRoot, read.StagingRoot);
        Assert.Equal(marker.RestorePointPath, read.RestorePointPath);
        Assert.Equal(2, read.PublishedTargets.Count);
        Assert.Equal("Bilder/1.jpg", read.PublishedTargets[0].RelativePath);
        Assert.Equal("AABB", read.PublishedTargets[0].Sha256);
    }

    [Fact]
    public void Clear_entfernt_den_Marker()
    {
        using var dir = new TempDir();
        var journal = new FileImportTransactionJournal();
        journal.Begin(dir.Path, SampleMarker());

        journal.Clear(dir.Path);

        Assert.Null(journal.TryRead(dir.Path));
    }

    [Fact]
    public void TryRead_ohne_Marker_liefert_null()
    {
        using var dir = new TempDir();
        Assert.Null(new FileImportTransactionJournal().TryRead(dir.Path));
    }

    [Fact]
    public void TryRead_bei_kaputtem_Json_liefert_null_statt_Wurf()
    {
        using var dir = new TempDir();
        File.WriteAllText(Path.Combine(dir.Path, ".import-transaction.json"), "{ kein gueltiges json");

        Assert.Null(new FileImportTransactionJournal().TryRead(dir.Path));
    }

    [Fact]
    public void Read_unterscheidet_fehlenden_von_beschaedigtem_Marker()
    {
        using var dir = new TempDir();
        var journal = new FileImportTransactionJournal();

        var missing = journal.Read(dir.Path);
        File.WriteAllText(Path.Combine(dir.Path, ".import-transaction.json"), "{ kein gueltiges json");
        var failed = journal.Read(dir.Path);

        Assert.Equal(ImportTransactionJournalReadOutcome.Missing, missing.Outcome);
        Assert.Null(missing.Marker);
        Assert.Equal(ImportTransactionJournalReadOutcome.Failed, failed.Outcome);
        Assert.Null(failed.Marker);
        Assert.False(string.IsNullOrWhiteSpace(failed.ErrorMessage));
    }

    [Fact]
    public void Read_lehnt_strukturell_unvollstaendigen_Marker_ab()
    {
        using var dir = new TempDir();
        File.WriteAllText(Path.Combine(dir.Path, ".import-transaction.json"), "{}");

        var result = new FileImportTransactionJournal().Read(dir.Path);

        Assert.Equal(ImportTransactionJournalReadOutcome.Failed, result.Outcome);
        Assert.Null(result.Marker);
    }

    [Fact]
    public void Clear_ohne_Marker_wirft_nicht()
    {
        using var dir = new TempDir();
        new FileImportTransactionJournal().Clear(dir.Path);   // idempotent
    }

    [Fact]
    public void BeginIfMissingOrOwned_ueberschreibt_nur_den_eigenen_Marker()
    {
        using var dir = new TempDir();
        var journal = new FileImportTransactionJournal();
        var ersterStand = SampleMarker("tx-eigen");
        var zweiterStand = ersterStand with
        {
            Label = "PDF veroeffentlicht",
            PublishedTargets = [new PublishedFileInfo("Bilder/2.jpg", "EEFF")]
        };

        journal.BeginIfMissingOrOwned(dir.Path, ersterStand);
        journal.BeginIfMissingOrOwned(dir.Path, zweiterStand);

        var gelesen = journal.Read(dir.Path);
        Assert.Equal(ImportTransactionJournalReadOutcome.Loaded, gelesen.Outcome);
        Assert.Equal("tx-eigen", gelesen.Marker?.TxId);
        Assert.Equal("PDF veroeffentlicht", gelesen.Marker?.Label);
        Assert.Equal("Bilder/2.jpg", Assert.Single(gelesen.Marker!.PublishedTargets).RelativePath);
    }

    [Fact]
    public void BeginIfMissingOrOwned_laesst_fremden_Marker_bytegleich()
    {
        using var dir = new TempDir();
        var journal = new FileImportTransactionJournal();
        var markerPath = Path.Combine(dir.Path, FileImportTransactionJournal.MarkerFileName);
        journal.BeginIfMissingOrOwned(dir.Path, SampleMarker("tx-alt"));
        var vorher = File.ReadAllBytes(markerPath);

        Assert.Throws<InvalidOperationException>(
            () => journal.BeginIfMissingOrOwned(dir.Path, SampleMarker("tx-neu")));

        Assert.Equal(vorher, File.ReadAllBytes(markerPath));
        Assert.Equal("tx-alt", journal.TryRead(dir.Path)?.TxId);
    }

    [Fact]
    public void BeginIfMissingOrOwned_laesst_beschaedigten_Marker_bytegleich()
    {
        using var dir = new TempDir();
        var journal = new FileImportTransactionJournal();
        var markerPath = Path.Combine(dir.Path, FileImportTransactionJournal.MarkerFileName);
        byte[] kaputt = [0xFF, 0x00, 0x7B, 0x13, 0x0A];
        File.WriteAllBytes(markerPath, kaputt);

        Assert.Throws<InvalidOperationException>(
            () => journal.BeginIfMissingOrOwned(dir.Path, SampleMarker("tx-neu")));

        Assert.Equal(kaputt, File.ReadAllBytes(markerPath));
    }

    [Fact]
    public void ClearIfOwned_loescht_nur_den_eigenen_Marker()
    {
        using var dir = new TempDir();
        var journal = new FileImportTransactionJournal();
        var markerPath = Path.Combine(dir.Path, FileImportTransactionJournal.MarkerFileName);
        journal.BeginIfMissingOrOwned(dir.Path, SampleMarker("tx-eigen"));
        var vorher = File.ReadAllBytes(markerPath);

        Assert.False(journal.ClearIfOwned(dir.Path, "tx-fremd"));
        Assert.Equal(vorher, File.ReadAllBytes(markerPath));

        Assert.True(journal.ClearIfOwned(dir.Path, "tx-eigen"));
        Assert.Equal(ImportTransactionJournalReadOutcome.Missing, journal.Read(dir.Path).Outcome);
    }

    [Fact]
    public void ClearIfOwned_laesst_beschaedigten_Marker_bytegleich()
    {
        using var dir = new TempDir();
        var journal = new FileImportTransactionJournal();
        var markerPath = Path.Combine(dir.Path, FileImportTransactionJournal.MarkerFileName);
        byte[] kaputt = [0x00, 0x01, 0x02, 0xFF];
        File.WriteAllBytes(markerPath, kaputt);

        Assert.False(journal.ClearIfOwned(dir.Path, "tx-eigen"));
        Assert.Equal(kaputt, File.ReadAllBytes(markerPath));
    }

    [Fact]
    public async Task Parallele_BeginIfMissingOrOwned_Aufrufer_gewinnen_genau_einmal()
    {
        using var dir = new TempDir();
        const int anzahl = 8;
        using var start = new Barrier(anzahl + 1);
        var aufgaben = Enumerable.Range(0, anzahl)
            .Select(index => Task.Factory.StartNew(
                () =>
                {
                    start.SignalAndWait();
                    try
                    {
                        new FileImportTransactionJournal().BeginIfMissingOrOwned(
                            dir.Path,
                            SampleMarker($"tx-{index}"));
                        return true;
                    }
                    catch (InvalidOperationException)
                    {
                        return false;
                    }
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default))
            .ToArray();

        start.SignalAndWait();
        var ergebnisse = await Task.WhenAll(aufgaben);

        Assert.Single(ergebnisse.Where(erfolg => erfolg));
        Assert.Equal(
            ImportTransactionJournalReadOutcome.Loaded,
            new FileImportTransactionJournal().Read(dir.Path).Outcome);
    }

    [Fact]
    public async Task Clear_von_A_kann_erfolgreichen_Begin_von_B_nicht_nachtraeglich_loeschen()
    {
        using var dir = new TempDir();
        var journal = new FileImportTransactionJournal();
        journal.BeginIfMissingOrOwned(dir.Path, SampleMarker("tx-a"));

        using var clearHatAErkannt = new ManualResetEventSlim();
        using var clearDarfFortfahren = new ManualResetEventSlim();
        var clearJournal = new FileImportTransactionJournal(() =>
        {
            clearHatAErkannt.Set();
            Assert.True(clearDarfFortfahren.Wait(TimeSpan.FromSeconds(5)));
        });

        var clearTask = Task.Factory.StartNew(
            () => clearJournal.ClearIfOwned(dir.Path, "tx-a"),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        Assert.True(clearHatAErkannt.Wait(TimeSpan.FromSeconds(5)));

        using var beginHatGestartet = new ManualResetEventSlim();
        var beginTask = Task.Factory.StartNew(
            () =>
            {
                beginHatGestartet.Set();
                journal.BeginIfMissingOrOwned(dir.Path, SampleMarker("tx-b"));
                return true;
            },
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        Assert.True(beginHatGestartet.Wait(TimeSpan.FromSeconds(5)));

        // Clear haelt die gemeinsame Projektsperre genau zwischen Besitzpruefung und
        // Loeschung. Begin B darf diesen kritischen Abschnitt nicht betreten.
        var vorFreigabeBeendet = await Task.WhenAny(beginTask, Task.Delay(250));
        var beginWarGesperrt = !ReferenceEquals(vorFreigabeBeendet, beginTask);
        clearDarfFortfahren.Set();

        Assert.True(await clearTask);
        Assert.True(await beginTask);
        Assert.True(beginWarGesperrt);
        Assert.Equal("tx-b", journal.TryRead(dir.Path)?.TxId);
    }

    [JunctionFact]
    public void Read_meldet_Projektroot_Verknuepfung_als_Failed_und_laesst_Marker_unveraendert()
    {
        using var dir = new TempDir();
        var externalRoot = Path.Combine(dir.Path, "extern");
        var linkedRoot = Path.Combine(dir.Path, "projekt-link");
        Directory.CreateDirectory(externalRoot);
        var journal = new FileImportTransactionJournal();
        journal.Begin(externalRoot, SampleMarker("tx-extern"));
        var externalMarker = Path.Combine(externalRoot, FileImportTransactionJournal.MarkerFileName);
        var before = File.ReadAllBytes(externalMarker);
        JunctionTestSupport.CreateDirectoryLink(linkedRoot, externalRoot);

        try
        {
            var result = journal.Read(linkedRoot);

            Assert.Equal(ImportTransactionJournalReadOutcome.Failed, result.Outcome);
            Assert.Null(result.Marker);
            Assert.Equal(before, File.ReadAllBytes(externalMarker));
        }
        finally
        {
            if (Directory.Exists(linkedRoot))
                Directory.Delete(linkedRoot);
        }
    }

    [JunctionFact]
    public void Begin_blockiert_Projektroot_Verknuepfung_ohne_externen_Marker_anzulegen()
    {
        using var dir = new TempDir();
        var externalRoot = Path.Combine(dir.Path, "extern");
        var linkedRoot = Path.Combine(dir.Path, "projekt-link");
        Directory.CreateDirectory(externalRoot);
        JunctionTestSupport.CreateDirectoryLink(linkedRoot, externalRoot);

        try
        {
            var error = Assert.Throws<InvalidOperationException>(() =>
                new FileImportTransactionJournal().Begin(linkedRoot, SampleMarker()));

            Assert.Contains("nicht sicher", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.False(File.Exists(Path.Combine(
                externalRoot,
                FileImportTransactionJournal.MarkerFileName)));
        }
        finally
        {
            if (Directory.Exists(linkedRoot))
                Directory.Delete(linkedRoot);
        }
    }

    [JunctionFact]
    public void ClearIfOwned_blockiert_Projektroot_Verknuepfung_und_laesst_Marker_bytegleich()
    {
        using var dir = new TempDir();
        var externalRoot = Path.Combine(dir.Path, "extern");
        var linkedRoot = Path.Combine(dir.Path, "projekt-link");
        Directory.CreateDirectory(externalRoot);
        var journal = new FileImportTransactionJournal();
        journal.Begin(externalRoot, SampleMarker("tx-extern"));
        var externalMarker = Path.Combine(externalRoot, FileImportTransactionJournal.MarkerFileName);
        var before = File.ReadAllBytes(externalMarker);
        JunctionTestSupport.CreateDirectoryLink(linkedRoot, externalRoot);

        try
        {
            Assert.False(journal.ClearIfOwned(linkedRoot, "tx-extern"));
            Assert.Equal(before, File.ReadAllBytes(externalMarker));
        }
        finally
        {
            if (Directory.Exists(linkedRoot))
                Directory.Delete(linkedRoot);
        }
    }

    [JunctionFact]
    public void Marker_Dateiverknuepfung_bleibt_beim_Lesen_Schreiben_und_Loeschen_unveraendert()
    {
        using var dir = new TempDir();
        var externalRoot = Path.Combine(dir.Path, "extern");
        Directory.CreateDirectory(externalRoot);
        var journal = new FileImportTransactionJournal();
        journal.Begin(externalRoot, SampleMarker("tx-extern"));
        var externalMarker = Path.Combine(externalRoot, FileImportTransactionJournal.MarkerFileName);
        var before = File.ReadAllBytes(externalMarker);
        var markerLink = Path.Combine(dir.Path, FileImportTransactionJournal.MarkerFileName);
        File.CreateSymbolicLink(markerLink, externalMarker);

        try
        {
            Assert.Equal(
                ImportTransactionJournalReadOutcome.Failed,
                journal.Read(dir.Path).Outcome);
            Assert.Throws<InvalidOperationException>(() =>
                journal.Begin(dir.Path, SampleMarker("tx-neu")));
            Assert.False(journal.ClearIfOwned(dir.Path, "tx-extern"));
            Assert.Equal(before, File.ReadAllBytes(externalMarker));
            Assert.True(File.Exists(markerLink));
        }
        finally
        {
            if (File.Exists(markerLink))
                File.Delete(markerLink);
        }
    }
}
