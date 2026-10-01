using AuswertungPro.Next.Application.UseCases.NaechsteAufgabe;
using AuswertungPro.Next.Domain.Models;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Nova-Fixwelle 2b (F1): Die gemeinsame Kandidatenregel der Haltungs-Protokollspalte.
/// Kein Dateizugriff — nur hinterlegte Pfade.
/// </summary>
public sealed class HaltungProtokollQuelleTests
{
    private static HaltungRecord Haltung(params (string Feld, string Wert)[] felder)
    {
        var record = new HaltungRecord();
        foreach (var (feld, wert) in felder)
            record.SetFieldValue(feld, wert, FieldSource.Manual, false);
        return record;
    }

    [Fact]
    public void Ohne_jeden_Pfad_gibt_es_keinen_Kandidaten()
    {
        Assert.Empty(HaltungProtokollQuelle.Kandidaten(Haltung()));
        Assert.False(HaltungProtokollQuelle.Vorhanden(Haltung()));
    }

    [Theory]
    [InlineData(FieldKeys.PdfPath)]
    [InlineData(FieldKeys.PdfEigen)]
    [InlineData(FieldKeys.PdfAll)]
    public void Jedes_der_drei_Protokollfelder_genuegt(string feld)
    {
        var record = Haltung((feld, @"D:\Protokolle\10001-10002.pdf"));

        Assert.True(HaltungProtokollQuelle.Vorhanden(record));
        Assert.Equal(@"D:\Protokolle\10001-10002.pdf", Assert.Single(HaltungProtokollQuelle.Kandidaten(record)));
    }

    [Fact]
    public void Ein_Link_zaehlt_nur_wenn_dort_wirklich_eine_PDF_steht()
    {
        Assert.False(HaltungProtokollQuelle.Vorhanden(Haltung((FieldKeys.Link, @"D:\Medien\10001-10002.mp4"))));
        Assert.True(HaltungProtokollQuelle.Vorhanden(Haltung((FieldKeys.Link, @"D:\Medien\10001-10002.pdf"))));
    }

    [Fact]
    public void Ein_Wert_ohne_pdf_Endung_ist_keine_Protokollquelle()
        => Assert.False(HaltungProtokollQuelle.Vorhanden(Haltung((FieldKeys.PdfPath, "siehe Ordner Protokolle"))));

    [Fact]
    public void Die_Reihenfolge_ist_PdfPath_PdfEigen_PdfAll_Link()
    {
        var record = Haltung(
            (FieldKeys.Link, "d.pdf"),
            (FieldKeys.PdfAll, "c.pdf"),
            (FieldKeys.PdfEigen, "b.pdf"),
            (FieldKeys.PdfPath, "a.pdf"));

        Assert.Equal(["a.pdf", "b.pdf", "c.pdf", "d.pdf"], HaltungProtokollQuelle.Kandidaten(record));
    }

    [Fact]
    public void Leerzeichen_allein_sind_kein_Pfad()
        => Assert.False(HaltungProtokollQuelle.Vorhanden(Haltung((FieldKeys.PdfPath, "   "))));

    /// <summary>
    /// Die Spaltenhinweise stehen an der Regel, nicht im XAML: Zelle und Doku muessen dieselbe
    /// Aussage tragen — hinterlegt ist nicht dasselbe wie vorhanden.
    /// </summary>
    [Fact]
    public void Die_beiden_Hinweistexte_nennen_den_zweiten_Weg()
    {
        Assert.Contains("Kontextmenü sucht im Projekt", HaltungProtokollQuelle.OhneProtokollHinweis);
        Assert.Contains("sucht im Ordner", HaltungProtokollQuelle.OhneVideoHinweis);
    }
}
