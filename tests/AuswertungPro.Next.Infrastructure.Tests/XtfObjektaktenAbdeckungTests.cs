using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class XtfObjektaktenAbdeckungTests
{
    [Theory]
    [InlineData("bauwerksteil")]
    [InlineData("massnahme")]
    [InlineData("dichtheitspruefung")]
    [InlineData("inspektion_haltung")]
    public void Nicht_lieferbares_Objekt_wird_mit_Name_und_Feld_gemeldet(string art)
    {
        var p = XtfDssExportTests.Projekt();
        p.Objektakten.Add(new ObjektAkte { Art = art, Bezuege = [p.Data[0].Id], Werte = new()
        {
            [art + ".bezeichnung"] = new() { Text = "Pruefobjekt-4711", VonHand = true }
        }});
        var result = new XtfNeuExportService().Erzeuge(new(p, "", NurPruefen: true));
        Assert.False(result.Ok); Assert.Null(result.Datei);
        Assert.Contains("Pruefobjekt-4711", result.Bericht + result.Fehler);
        Assert.Contains(art, result.Bericht + result.Fehler);
        Assert.Contains("nicht", result.Bericht + result.Fehler, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Auch_mehr_als_zwanzig_Exportluecken_bleiben_namentlich_sichtbar()
    {
        var p = XtfDssExportTests.Projekt();
        for (var n = 0; n < 25; n++) p.Objektakten.Add(new ObjektAkte { Art = "massnahme", Bezuege = [p.Data[0].Id], Werte = new()
        { ["massnahme.bezeichnung"] = new() { Text = $"Massnahme-{n:D2}", VonHand = true } } });
        var result = new XtfNeuExportService().Erzeuge(new(p, "", NurPruefen: true));
        Assert.False(result.Ok); Assert.Null(result.Datei);
        for (var n = 0; n < 25; n++) Assert.Contains($"Massnahme-{n:D2}", result.Bericht + result.Fehler);
    }

    [Fact]
    public void Nicht_zugeordneter_Deckel_bleibt_nicht_still_ausserhalb_der_Lieferung()
    {
        var p = XtfDssExportTests.Projekt();
        p.Objektakten.Add(new ObjektAkte { Art = "deckel", Werte = new()
        { ["deckel.bezeichnung"] = new() { Text = "Deckel-ohne-Bezug", VonHand = true } } });
        var result = new XtfNeuExportService().Erzeuge(new(p, "", NurPruefen: true));
        Assert.False(result.Ok); Assert.Null(result.Datei);
        Assert.Contains("Deckel-ohne-Bezug", result.Bericht + result.Fehler);
    }

    [Fact]
    public void Fehlende_Felder_und_Quellobjekte_stehen_namentlich_im_Exportbericht()
    {
        var p = XtfDssExportTests.Projekt();
        p.Objektakten[0].Werte["haltung.unbekannte_erweiterung"] = new() { Text = "Zusatz 4711", VonHand = true };
        p.Objektakten[0].Quellen.Add(XtfDssExportTests.Quelle("Messstelle", "chTEST0000000099", new() { ["Bezeichnung"] = "Messstelle Nord" }));
        p.Objektakten.Add(new() { Art = "unterhalt", Bezuege = [p.Data[0].Id], Werte = new()
        {
            ["unterhalt.bezeichnung"] = new() { Text = "Spülung 4711" },
            ["unterhalt.auftrag_nr"] = new() { Text = "Auftrag 123" }
        } });
        var r = new XtfNeuExportService().Erzeuge(new(p, "", NurPruefen: true));
        Assert.True(r.Ok, r.Fehler);
        Assert.Contains("Zusatz 4711", r.Bericht);
        Assert.Contains("Messstelle Nord", r.Bericht);
        Assert.Contains("Spülung 4711", r.Bericht);
        Assert.Contains("Auftrag 123", r.Bericht);
        Assert.DoesNotContain("vollstaendiger Projektstand", r.Bericht);
        var v = AuswertungPro.Next.Application.UseCases.Xtf.XtfExportVorschau.AusBericht("Probe", r.Bericht);
        Assert.Contains("fehlen in dieser XTF", v.Zusammenfassung);
        Assert.Contains(v.Warnungen, w => w.Contains("Messstelle Nord"));
        Assert.Contains(v.Warnungen, w => w.Contains("Auftrag 123"));
    }
}
