using AuswertungPro.Next.Application.Xtf;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class RedesignSiaExportTests
{
    [Fact]
    public void Reiner_Erstexport_verspricht_keine_nicht_mitgelieferten_Zusatzangaben()
    {
        var project = new Project();
        var shaft = XtfBauwerksartenExportTests.Schacht("Normschacht", "Kein Normwert");
        project.SchaechteData.Add(shaft);
        var result = new AuswertungPro.Next.Infrastructure.Import.Xtf.XtfNeuExportService()
            .Erzeuge(new(project, "", NurPruefen: true, MitZusatzangaben: false));
        Assert.True(result.Ok, result.Fehler);
        Assert.DoesNotContain("siehe Zusatzangaben", result.Bericht);
        Assert.Contains("Originalwert verbleibt im Projekt", result.Bericht);
    }

    [Fact]
    public void Fettabscheider_behaelt_seine_genaue_Funktion()
        => Assert.Equal("Fettabscheider", SchachtFunktionVokabular.NachNorm("Fettabscheider"));

    [Theory]
    [InlineData("10 mm", 10)]
    [InlineData("10mm", 10)]
    [InlineData("12 m", 12000)]
    public void Ausdrueckliche_Einheiten_haben_Vorrang(string wert, int erwartet)
        => Assert.Equal(erwartet, SiaAbmessung.NachMillimeter(wert));

    [Theory]
    [InlineData("4500")]
    [InlineData("-1")]
    [InlineData("keine Zahl")]
    public void Ungueltige_Schachtmasse_werden_gemeldet_und_nicht_exportiert(string wert)
    {
        var record = XtfBauwerksartenExportTests.Schacht("Normschacht", "Kontrollschacht");
        record.SetFieldValue(FieldKeys.ShaftDimension1Mm, wert, FieldSource.Manual, true);
        var plan = XtfNeuPlanBuilder.Build([], [record]);
        var shaft = Assert.Single(plan.Objekte, o => o.Klasse == "Normschacht");
        Assert.DoesNotContain(shaft.Felder, f => f.Key is "Dimension1" or "Dimension2");
        Assert.Contains(plan.Hinweise, h => h.Contains("4000", StringComparison.Ordinal));
    }

    [Fact]
    public void Kleine_Werte_im_Millimeterfeld_bleiben_Millimeter()
    {
        var record = new SchachtRecord();
        record.SetFieldValue(FieldKeys.ShaftDimension1Mm, "10");
        Assert.Equal(("10", "10"), XtfSchachtPlanBuilder.Masse(record, "Test"));
    }

    [Fact]
    public void Einheit_am_Ende_eines_Paars_gilt_fuer_beide_Masse()
        => Assert.Equal(("2", "3"), XtfSchachtPlanBuilder.Abmessungen("2 x 3 mm"));
}
