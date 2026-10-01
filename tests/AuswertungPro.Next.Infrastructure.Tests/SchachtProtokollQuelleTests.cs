using AuswertungPro.Next.Application.UseCases.NaechsteAufgabe;
using AuswertungPro.Next.Domain.Models;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Nova-Etappe 2b, Task 6 (Fix-Runde 1): Der Protokollknopf der Schachtliste darf genau dann
/// erscheinen, wenn der Oeffner auch wirklich etwas oeffnen wuerde. Vorher entschied die
/// Sichtbarkeit nach <c>PDF_Path | PDF_Eigen | PDF_All</c>, der Oeffner
/// (<c>SchachtFileTargetPathResolver</c>) dagegen nach <c>PDF_Path</c> und ersatzweise
/// <c>Link</c> mit Endung .pdf — zwei Fehlfaelle in beide Richtungen.
///
/// Diese Regel ist die gemeinsame Quelle beider Wege. Sie liest nur Felder und fasst keine
/// Datei an; ob ein Pfad wirklich existiert, entscheidet weiterhin der Oeffner.
/// </summary>
public sealed class SchachtProtokollQuelleTests
{
    [Fact]
    public void Ein_PDF_Pfad_ist_die_erste_Quelle()
    {
        var record = Mit((FieldKeys.PdfPath, @"D:\Projekt\Schaechte\78998.pdf"));

        Assert.True(SchachtProtokollQuelle.Vorhanden(record));
        Assert.Equal([@"D:\Projekt\Schaechte\78998.pdf"], SchachtProtokollQuelle.Kandidaten(record));
    }

    /// <summary>Fehlfall 1: Nur PDF_Eigen (oder PDF_All) — der Oeffner findet nichts, also kein Knopf.</summary>
    [Theory]
    [InlineData(FieldKeys.PdfEigen)]
    [InlineData(FieldKeys.PdfAll)]
    public void Ein_anderes_PDF_Feld_allein_ist_keine_Protokollquelle(string feld)
    {
        var record = Mit((feld, @"D:\Projekt\Schaechte\78998.pdf"));

        Assert.False(SchachtProtokollQuelle.Vorhanden(record));
        Assert.Empty(SchachtProtokollQuelle.Kandidaten(record));
    }

    /// <summary>Fehlfall 2: Nur ein Link auf eine PDF — der Oeffner nimmt ihn, also gehoert der Knopf hin.</summary>
    [Fact]
    public void Ein_Link_auf_eine_PDF_ist_die_zweite_Quelle()
    {
        var record = Mit((FieldKeys.Link, @" D:\Projekt\Schaechte\78998.PDF "));

        Assert.True(SchachtProtokollQuelle.Vorhanden(record));
        Assert.Equal([@"D:\Projekt\Schaechte\78998.PDF"], SchachtProtokollQuelle.Kandidaten(record));
    }

    [Fact]
    public void Ein_Link_auf_ein_Video_ist_keine_Protokollquelle()
        => Assert.False(SchachtProtokollQuelle.Vorhanden(Mit((FieldKeys.Link, @"D:\Medien\78998.mp4"))));

    /// <summary>Auch der PDF-Pfad zaehlt nur mit Endung .pdf — genau wie beim Oeffnen.</summary>
    [Fact]
    public void Ein_Pfad_ohne_PDF_Endung_zaehlt_nicht()
        => Assert.False(SchachtProtokollQuelle.Vorhanden(Mit((FieldKeys.PdfPath, @"D:\Projekt\Schaechte"))));

    [Fact]
    public void Beide_Quellen_stehen_in_Pruefreihenfolge()
    {
        var record = Mit(
            (FieldKeys.PdfPath, @"D:\a.pdf"),
            (FieldKeys.Link, @"D:\b.pdf"));

        Assert.Equal([@"D:\a.pdf", @"D:\b.pdf"], SchachtProtokollQuelle.Kandidaten(record));
    }

    /// <summary>
    /// Der Datensatz fuehrt das Feld unter der Schreibweise der Excel-Kopfzeile. Ein reiner
    /// Ordinalvergleich haette den Knopf still ausgeblendet.
    /// </summary>
    [Fact]
    public void Eine_abweichende_Schreibweise_wird_gefunden()
        => Assert.True(SchachtProtokollQuelle.Vorhanden(Mit(("PDF Path", @"D:\Projekt\Schaechte\78998.pdf"))));

    [Fact]
    public void Ohne_jedes_Feld_gibt_es_keine_Quelle()
    {
        Assert.False(SchachtProtokollQuelle.Vorhanden(new SchachtRecord()));
        Assert.False(SchachtProtokollQuelle.Vorhanden(Mit((FieldKeys.PdfPath, "   "))));
    }

    private static SchachtRecord Mit(params (string Feld, string Wert)[] felder)
    {
        var record = new SchachtRecord();
        foreach (var (feld, wert) in felder)
            record.SetFieldValue(feld, wert, FieldSource.Manual, userEdited: false);
        return record;
    }
}
