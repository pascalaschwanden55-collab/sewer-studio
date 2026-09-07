using AuswertungPro.Next.Application.UseCases.Uebersicht;
using AuswertungPro.Next.Domain.Models;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class ProjektUebersichtKennzahlenTests
{
    private static Project Projekt()
    {
        var p = new Project { Name = "Test" };
        p.EnsureMetadataDefaults();
        void H(string name, string status, string zk, string laenge, string material, string link)
        {
            var r = p.CreateNewRecord();
            r.SetFieldValue(FieldKeys.HoldingName, name, FieldSource.Manual, false);
            r.SetFieldValue(FieldKeys.WorkflowStatus, status, FieldSource.Manual, false);
            r.SetFieldValue(FieldKeys.ConditionClass, zk, FieldSource.Manual, false);
            r.SetFieldValue(FieldKeys.HoldingLengthMeters, laenge, FieldSource.Manual, false);
            r.SetFieldValue(FieldKeys.PipeMaterial, material, FieldSource.Manual, false);
            r.SetFieldValue(FieldKeys.NominalDiameterMm, "300", FieldSource.Manual, false);
            r.SetFieldValue(FieldKeys.Link, link, FieldSource.Manual, false);
            p.AddRecord(r);
        }
        H("1-2", "abgeschlossen", "0", "10", "Beton", "a.mp4");
        H("2-3", "", "1", "20.5", "", "b.mp4");
        H("3-4", "", "4", "", "Beton", "");
        var s = new SchachtRecord(); s.Fields["Schachtnummer"] = "1"; s.Fields["Zustandsklasse"] = "1"; s.Fields["PDF_Path"] = "x.pdf";
        p.SchaechteData.Add(s);
        var s2 = new SchachtRecord(); s2.Fields["Schachtnummer"] = "2"; s2.Fields["Zustandsklasse"] = "3";
        p.SchaechteData.Add(s2);
        return p;
    }

    [Fact]
    public void Zaehlt_Pruefstand_Laenge_Protokolle_und_Dringende()
    {
        var k = ProjektUebersichtRechner.Berechne(Projekt());
        Assert.Equal(3, k.Haltungen);
        Assert.Equal(1, k.Geprueft);
        Assert.Equal(0, k.KiAnalysiert);
        Assert.Equal(2, k.Offen);
        Assert.Equal(30.5, k.GesamtlaengeM, 3);
        Assert.Equal(2, k.Schaechte);
        Assert.Equal(1, k.SchaechteMitProtokoll);
        Assert.Equal(2, k.DringendHaltungen);
        Assert.Equal(1, k.DringendSchaechte);
    }

    [Fact]
    public void Stammdaten_Vollstaendigkeit_je_Feld_mit_Stufe()
    {
        var k = ProjektUebersichtRechner.Berechne(Projekt());
        var material = Assert.Single(k.Stammdaten, s => s.Feld == "Material");
        Assert.Equal(2, material.Gefuellt); Assert.Equal(3, material.Gesamt); Assert.Equal("2", material.Stufe);
        var dn = Assert.Single(k.Stammdaten, s => s.Feld == "DN");
        Assert.Equal("4", dn.Stufe);
    }

    [Fact]
    public void Hero_Text_nennt_alle_Zahlen()
    {
        var k = ProjektUebersichtRechner.Berechne(Projekt());
        Assert.Equal("1 von 3 Haltungen fachlich geprüft (33.3 %). 0 von der KI analysiert und noch nicht geprüft, 2 ohne Analyse. 2 Haltungen dringend (Z0 oder Z1). Bestand mit 3 Haltungen und 2 Schächten.",
            ProjektUebersichtRechner.HeroText(k));
    }
}
