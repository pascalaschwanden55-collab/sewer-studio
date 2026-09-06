using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Arbeitspaket 8 des Uebergabeplans vom 2026-09-05: Schacht-Sammelprotokolle
/// automatisch anschliessen.
///
/// Die Verknuepfungsregel lag bis dahin allein im Export-ViewModel. Der Ein-Knopf-Import
/// braucht sie ebenfalls — eine zweite Umsetzung waere die Stelle, an der beide Wege
/// spaeter auseinanderlaufen.
/// </summary>
public sealed class SchachtProtokollVerknuepfungTests
{
    private const string Wurzel = @"C:\Projekt";

    [Fact]
    public void JederTeil_WirdSeinemSchachtRelativVerknuepft()
    {
        var projekt = ProjektMitSchaechten("3133", "3060");

        var ergebnis = SchachtProtokollVerknuepfung.Verknuepfe(
        [
            (@"C:\Projekt\Schächte_Verteilt\3133\20200703_3133.pdf", @"C:\Projekt\Schächte_Verteilt\3133", "sammel.pdf"),
            (@"C:\Projekt\Schächte_Verteilt\3060\20200703_3060.pdf", @"C:\Projekt\Schächte_Verteilt\3060", "sammel.pdf")
        ], projekt, Wurzel);

        Assert.Equal(2, ergebnis.Verknuepft);
        Assert.Empty(ergebnis.Meldungen);
        foreach (var schacht in projekt.SchaechteData)
        {
            var pfad = schacht.GetFieldValue("PDF_Path");
            Assert.False(Path.IsPathRooted(pfad), $"Verweis muss relativ sein: {pfad}");
            Assert.Contains(schacht.GetFieldValue("Schachtnummer"), pfad, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void UnbekannteSchachtnummer_LegtKeinenSchachtAnUndWirdGemeldet()
    {
        // Einen Schacht allein aus einer PDF-Seite zu erfinden, waere fachlich nicht belegt.
        var projekt = ProjektMitSchaechten("3133");

        var ergebnis = SchachtProtokollVerknuepfung.Verknuepfe(
        [
            (@"C:\Projekt\Schächte_Verteilt\9999\20200703_9999.pdf", @"C:\Projekt\Schächte_Verteilt\9999", "sammel.pdf")
        ], projekt, Wurzel);

        Assert.Equal(0, ergebnis.Verknuepft);
        Assert.Single(projekt.SchaechteData);
        var meldung = Assert.Single(ergebnis.Meldungen);
        Assert.Contains("9999", meldung, StringComparison.Ordinal);
        Assert.Contains("nicht bekannt", meldung, StringComparison.Ordinal);
        Assert.Contains("sammel.pdf", meldung, StringComparison.Ordinal);
    }

    [Fact]
    public void VorhandenerVerweis_WirdNichtErsetzt()
    {
        var projekt = ProjektMitSchaechten("3133");
        projekt.SchaechteData[0].SetFieldValue(
            "PDF_Path", @"Schächte_Verteilt\3133\von_hand.pdf");

        var ergebnis = SchachtProtokollVerknuepfung.Verknuepfe(
        [
            (@"C:\Projekt\Schächte_Verteilt\3133\20200703_3133.pdf", @"C:\Projekt\Schächte_Verteilt\3133", "sammel.pdf")
        ], projekt, Wurzel);

        Assert.Equal(0, ergebnis.Verknuepft);
        Assert.Equal(@"Schächte_Verteilt\3133\von_hand.pdf", projekt.SchaechteData[0].GetFieldValue("PDF_Path"));
        Assert.Contains(ergebnis.Meldungen,
            m => m.Contains("hat bereits ein Protokoll", StringComparison.Ordinal));
    }

    [Fact]
    public void ZielbaumMitGemeindeUndJahr_FindetDenSchachtTrotzdem()
    {
        var projekt = ProjektMitSchaechten("3133");

        var ergebnis = SchachtProtokollVerknuepfung.Verknuepfe(
        [
            (@"C:\Projekt\Schächte_Verteilt\Andermatt\2020\3133\20200703_3133.pdf",
             @"C:\Projekt\Schächte_Verteilt\Andermatt\2020\3133", "sammel.pdf")
        ], projekt, Wurzel);

        Assert.Equal(1, ergebnis.Verknuepft);
    }

    [Fact]
    public void ZweiterIdentischerLauf_MeldetKeinenKonflikt()
    {
        var projekt = ProjektMitSchaechten("3133");
        var teile = new List<(string, string, string)>
        {
            (@"C:\Projekt\Schächte_Verteilt\3133\20200703_3133.pdf", @"C:\Projekt\Schächte_Verteilt\3133", "sammel.pdf")
        };

        SchachtProtokollVerknuepfung.Verknuepfe(teile, projekt, Wurzel);
        var zweiter = SchachtProtokollVerknuepfung.Verknuepfe(teile, projekt, Wurzel);

        // Derselbe Zielpfad ist kein Konflikt — er ist derselbe Verweis.
        Assert.Empty(zweiter.Meldungen);
    }

    // ---------------------------------------------------------------------

    private static Project ProjektMitSchaechten(params string[] nummern)
    {
        var projekt = new Project();
        foreach (var nummer in nummern)
        {
            var schacht = new SchachtRecord();
            schacht.SetFieldValue("Schachtnummer", nummer);
            projekt.SchaechteData.Add(schacht);
        }

        return projekt;
    }
}
