using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Import;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Der Marker ist eine Datei im Projekt und damit manipulierbar. Ohne Grenzen liest
/// die Wiederherstellung beim Projektoeffnen eine beliebig grosse Datei vollstaendig
/// in den Speicher, bevor ueberhaupt etwas geprueft wird.
///
/// Grenzen: 8 MiB Dateigroesse, 10.000 Ziele. Eine Ueberschreitung ist fail-closed -
/// Lesen ergibt <see cref="ImportTransactionJournalReadOutcome.Failed"/>, Schreiben
/// wird abgelehnt, und der vorhandene Marker bleibt bytegleich liegen.
/// </summary>
public sealed class FileImportTransactionJournalLimitTests : IDisposable
{
    private readonly string _wurzel = Path.Combine(
        Path.GetTempPath(), "ifjl_" + Guid.NewGuid().ToString("N"));

    public FileImportTransactionJournalLimitTests() => Directory.CreateDirectory(_wurzel);

    private string MarkerPfad => Path.Combine(_wurzel, FileImportTransactionJournal.MarkerFileName);

    private static ImportTransactionMarker MarkerMitZielen(int anzahl)
        => new(
            "tx-grenze",
            DateTime.UtcNow,
            "Test",
            Path.Combine("egal", ".import-staging"),
            Enumerable.Range(0, anzahl)
                .Select(i => new PublishedFileInfo($"Ziel/datei_{i}.txt", new string('a', 64)))
                .ToArray(),
            RestorePointPath: null);

    // ---- Anzahl der Ziele -------------------------------------------------

    [Fact]
    public void Genau_zehntausend_ziele_werden_noch_angenommen()
    {
        var journal = new FileImportTransactionJournal();

        journal.Begin(_wurzel, MarkerMitZielen(FileImportTransactionJournal.MaxPublishedTargets));

        Assert.Equal(
            FileImportTransactionJournal.MaxPublishedTargets,
            journal.TryRead(_wurzel)?.PublishedTargets.Count);
    }

    [Fact]
    public void Ein_ziel_zu_viel_wird_beim_schreiben_abgelehnt_und_der_marker_bleibt()
    {
        var journal = new FileImportTransactionJournal();
        journal.Begin(_wurzel, MarkerMitZielen(3));
        var vorher = File.ReadAllBytes(MarkerPfad);

        Assert.Throws<ArgumentException>(
            () => journal.Begin(
                _wurzel,
                MarkerMitZielen(FileImportTransactionJournal.MaxPublishedTargets + 1)));

        Assert.Equal(vorher, File.ReadAllBytes(MarkerPfad));
    }

    [Fact]
    public void Zu_viele_ziele_in_einer_fremden_datei_gelten_als_nicht_lesbar()
    {
        // Von aussen manipulierter Marker: das Schreiben hat ihn nie gesehen.
        var json = System.Text.Json.JsonSerializer.Serialize(
            MarkerMitZielen(FileImportTransactionJournal.MaxPublishedTargets + 1));
        File.WriteAllText(MarkerPfad, json);

        var ergebnis = new FileImportTransactionJournal().Read(_wurzel);

        Assert.Equal(ImportTransactionJournalReadOutcome.Failed, ergebnis.Outcome);
        Assert.Null(ergebnis.Marker);
    }

    // ---- Dateigroesse -----------------------------------------------------

    [Fact]
    public void Marker_an_der_groessengrenze_bleibt_lesbar()
    {
        // Gueltiges JSON, dessen Laenge exakt die Grenze erreicht: der Rest wird ueber
        // ein Fuellfeld im Label aufgefuellt.
        var basis = new ImportTransactionMarker(
            "tx-fuell",
            DateTime.UtcNow,
            "x",
            Path.Combine("egal", ".import-staging"),
            new[] { new PublishedFileInfo("Ziel/a.txt", new string('a', 64)) },
            RestorePointPath: null);

        var basisJson = System.Text.Json.JsonSerializer.Serialize(basis);
        var platz = FileImportTransactionJournal.MaxMarkerBytes - Encoding.UTF8.GetByteCount(basisJson);
        Assert.True(platz > 0, "Basismarker ist bereits groesser als die Grenze.");

        var grossesLabel = new string('x', platz);
        var marker = basis with { Label = grossesLabel };
        var json = System.Text.Json.JsonSerializer.Serialize(marker);
        // Serialisierung kann durch Escaping abweichen; auf die Grenze zurechtstutzen.
        while (Encoding.UTF8.GetByteCount(json) > FileImportTransactionJournal.MaxMarkerBytes)
        {
            grossesLabel = grossesLabel[..^1];
            marker = basis with { Label = grossesLabel };
            json = System.Text.Json.JsonSerializer.Serialize(marker);
        }

        File.WriteAllText(MarkerPfad, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        Assert.True(new FileInfo(MarkerPfad).Length <= FileImportTransactionJournal.MaxMarkerBytes);

        var ergebnis = new FileImportTransactionJournal().Read(_wurzel);

        Assert.Equal(ImportTransactionJournalReadOutcome.Loaded, ergebnis.Outcome);
    }

    [Fact]
    public void Ein_byte_ueber_der_grenze_gilt_als_nicht_lesbar_und_bleibt_unangetastet()
    {
        var fuellung = new string('x', FileImportTransactionJournal.MaxMarkerBytes + 1);
        File.WriteAllText(MarkerPfad, fuellung, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        var vorher = File.ReadAllBytes(MarkerPfad);

        var ergebnis = new FileImportTransactionJournal().Read(_wurzel);

        Assert.Equal(ImportTransactionJournalReadOutcome.Failed, ergebnis.Outcome);
        Assert.Equal(vorher, File.ReadAllBytes(MarkerPfad));
    }

    public void Dispose()
    {
        try { Directory.Delete(_wurzel, recursive: true); } catch { }
    }
}
