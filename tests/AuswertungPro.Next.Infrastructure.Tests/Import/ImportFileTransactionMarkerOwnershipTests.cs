using System;
using System.IO;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Application.UseCases.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Der Wiederherstellungs-Marker liegt einmal je Projekt. Er gehoert der Transaktion,
/// die ihn geschrieben hat - eine spaetere Transaktion darf ihn weder loeschen noch
/// ueberschreiben.
///
/// Dieser Fall braucht KEINEN Absturz: Nach einem Speicherfehler von Import A bleibt
/// dessen Marker liegen (richtig so). Wird danach ein zweiter Import gestartet und
/// abgebrochen, raeumte dessen Cleanup den fremden Marker mit weg - und damit den
/// Beweis, welche Dateien von A noch zurueckgenommen werden muessen.
/// </summary>
public sealed class ImportFileTransactionMarkerOwnershipTests : IDisposable
{
    private readonly string _wurzel = Path.Combine(
        Path.GetTempPath(), "iftx_" + Guid.NewGuid().ToString("N"));

    private readonly string _projektDatei;

    public ImportFileTransactionMarkerOwnershipTests()
    {
        _projektDatei = Path.Combine(_wurzel, "Projektdateien", "projekt.json");
        Directory.CreateDirectory(Path.GetDirectoryName(_projektDatei)!);
        File.WriteAllText(_projektDatei, "{}");
    }

    private string QuelleAnlegen(string name, string inhalt)
    {
        var quellOrdner = Path.Combine(_wurzel, "quelle");
        Directory.CreateDirectory(quellOrdner);
        var pfad = Path.Combine(quellOrdner, name);
        File.WriteAllText(pfad, inhalt);
        return pfad;
    }

    /// <summary>Import A: veroeffentlicht, Projekt uebernommen, Speichern scheitert.</summary>
    private string ImportAMitSpeicherfehler(FileImportTransactionJournal journal)
    {
        using var staging = new ImportFileStagingService().Begin(_projektDatei)!;
        var transaktion = new ImportFileTransaction("Import A", staging, journal);

        staging.StageCopy(
            QuelleAnlegen("a.txt", "inhalt-a"),
            Path.Combine(_wurzel, "Ziel"));

        transaktion.Publish();
        transaktion.StampProject(new Project());
        transaktion.MarkProjectCommitted();
        // MarkProjectSaved() bleibt bewusst aus: der Save ist fehlgeschlagen.
        transaktion.Cleanup();

        return transaktion.TxId;
    }

    [Fact]
    public void Abgebrochener_zweiter_import_loescht_den_fremden_marker_nicht()
    {
        var journal = new FileImportTransactionJournal();
        var txIdA = ImportAMitSpeicherfehler(journal);

        // Vorbedingung: Marker A liegt - genau so soll es nach einem Speicherfehler sein.
        Assert.Equal(txIdA, journal.TryRead(_wurzel)?.TxId);

        // Import B wird gestartet und ganz normal abgebrochen (kein Publish).
        using (var stagingB = new ImportFileStagingService().Begin(_projektDatei)!)
        {
            var transaktionB = new ImportFileTransaction("Import B", stagingB, journal);
            stagingB.StageCopy(
                QuelleAnlegen("b.txt", "inhalt-b"),
                Path.Combine(_wurzel, "Ziel"));
            transaktionB.Cleanup();
        }

        var markerNachB = journal.TryRead(_wurzel);
        Assert.NotNull(markerNachB);
        Assert.Equal(txIdA, markerNachB!.TxId);
    }

    [Fact]
    public void Zweiter_import_ueberschreibt_den_fremden_marker_nicht()
    {
        var journal = new FileImportTransactionJournal();
        var txIdA = ImportAMitSpeicherfehler(journal);

        using var stagingB = new ImportFileStagingService().Begin(_projektDatei)!;
        var transaktionB = new ImportFileTransaction("Import B", stagingB, journal);
        stagingB.StageCopy(
            QuelleAnlegen("b.txt", "inhalt-b"),
            Path.Combine(_wurzel, "Ziel"));

        // Ein zweiter Import darf den offenen Beweis von A nicht ueberschreiben.
        Assert.Throws<InvalidOperationException>(() => transaktionB.Publish());
        Assert.Equal(txIdA, journal.TryRead(_wurzel)?.TxId);
    }

    [Fact]
    public void Eigener_marker_wird_weiterhin_aufgeraeumt()
    {
        var journal = new FileImportTransactionJournal();

        using (var staging = new ImportFileStagingService().Begin(_projektDatei)!)
        {
            var transaktion = new ImportFileTransaction("Import", staging, journal);
            staging.StageCopy(
                QuelleAnlegen("c.txt", "inhalt-c"),
                Path.Combine(_wurzel, "Ziel"));
            transaktion.Publish();
            transaktion.StampProject(new Project());
            transaktion.MarkProjectCommitted();
            transaktion.MarkProjectSaved();
            transaktion.Cleanup();
        }

        Assert.Null(journal.TryRead(_wurzel));
    }

    [Fact]
    public void Beschaedigter_marker_sperrt_publish_und_bleibt_bytegleich()
    {
        var journal = new FileImportTransactionJournal();
        var markerPath = Path.Combine(_wurzel, FileImportTransactionJournal.MarkerFileName);
        byte[] kaputterMarker = [0xFF, 0x00, 0x7B, 0x13, 0x0A];
        File.WriteAllBytes(markerPath, kaputterMarker);

        using var staging = new ImportFileStagingService().Begin(_projektDatei)!;
        var transaktion = new ImportFileTransaction("Import", staging, journal);
        var zielOrdner = Path.Combine(_wurzel, "Ziel");
        staging.StageCopy(QuelleAnlegen("kaputt.txt", "inhalt"), zielOrdner);

        Assert.Throws<InvalidOperationException>(() => transaktion.Publish());

        Assert.Equal(kaputterMarker, File.ReadAllBytes(markerPath));
        Assert.False(File.Exists(Path.Combine(zielOrdner, "kaputt.txt")));
    }

    public void Dispose()
    {
        try { Directory.Delete(_wurzel, recursive: true); } catch { }
    }
}
