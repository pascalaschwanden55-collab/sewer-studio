using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Application.UseCases.Objektakten;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
using static AuswertungPro.Next.Infrastructure.Tests.XtfDssExportTests;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class XtfHaltungspunktAkteTests
{
    internal static void Importiere(Project p)
    {
        var h = p.Data[0];
        var q = new GeoShopBauteil("A-B", KatasterKennung.FuerHaltung("A-B", null, Haltung, Kanal,
            Von, Nach, null, null, null, null), new Dictionary<string, string>(), Quellen: p.Objektakten[0].Quellen.ToArray());
        GeoShopObjektaktenImport.Uebernehme(p, h.Id, "haltung", q, false);
    }

    [Fact]
    public void Importierte_Punkte_sind_einzeln_bearbeitbar_ohne_Originalaenderung_oder_Duplikate()
    {
        var p = Projekt(); Importiere(p);
        var a = Assert.Single(p.Objektakten.Where(a => a.Art == "haltungspunkt" && a.Quellen.Any(q => q.Kennung == Von)));
        var b = new ObjektaktenBearbeitung(p, p.Data[0].Id, "haltung");
        var f = FieldCatalog.Objektfelder.Feld("haltungspunkt.auslaufform");
        var wahl = b.ErlaubteEintraege(a, f).Single(e => e.OriginalCode == "104");
        b.Schreibe(a, f, "", wahl.Label, wahl);
        Importiere(p);
        Assert.Equal(2, p.Objektakten.Count(a => a.Art == "haltungspunkt"));
        Assert.Equal("Scharfkantig", a.Werte[f.Id].Text);
        Assert.False(a.Quellen[0].Werte.ContainsKey("Auslaufform"));
        WithExport(p, doc =>
        {
            Assert.Equal("scharfkantig", Objekt(doc, "Haltungspunkt", Von).Elements().Single(e => e.Name.LocalName == "Auslaufform").Value);
            Assert.Equal(2, doc.Descendants().Count(e => e.Name.LocalName.EndsWith(".Haltungspunkt")));
        });
    }

    [Fact]
    public void Bereits_abgeglichene_Projekte_erhalten_beim_naechsten_Abgleich_die_fehlenden_Punktakten()
    {
        var p = Projekt(); var h = p.Data[0];
        var q = new GeoShopBauteil("A-B", KatasterKennung.FuerHaltung("A-B", null, Haltung, Kanal,
            Von, Nach, null, null, null, null), new Dictionary<string, string>(), Quellen: p.Objektakten[0].Quellen.ToArray());
        Assert.True(GeoShopObjektaktenImport.HatNeueQuellen(p, h.Id, q));
        Importiere(p);
        Assert.False(GeoShopObjektaktenImport.HatNeueQuellen(p, h.Id, q));
    }

    [Theory]
    [InlineData("Unbekannt", "unbekannt")]
    [InlineData("> 6 cm", "groesser_6cm")]
    [InlineData("+/- 1 cm", "plusminus_1cm")]
    [InlineData("+/- 3 cm", "plusminus_3cm")]
    [InlineData("+/- 6 cm", "plusminus_6cm")]
    public void Jede_Hoehengenauigkeit_aus_der_Auswahl_hat_das_belegte_Normziel(string text, string norm)
    {
        var p = Projekt(); Importiere(p);
        var a = p.Objektakten.Single(a => a.Art == "haltungspunkt" && a.Quellen.Any(q => q.Kennung == Von));
        var b = new ObjektaktenBearbeitung(p, p.Data[0].Id, "haltung");
        var f = FieldCatalog.Objektfelder.Feld("haltungspunkt.hoehengenauigkeit");
        b.Schreibe(a, f, "", text, b.ErlaubteEintraege(a, f).Single(e => e.Label == text));
        WithExport(p, doc => Assert.Equal(norm, Objekt(doc, "Haltungspunkt", Von).Elements().Single(e => e.Name.LocalName == "Hoehengenauigkeit").Value));
    }

    [Fact]
    public void Normwerte_erscheinen_beim_Import_als_passende_Dropdowntexte()
    {
        var p = Projekt();
        var q = p.Objektakten[0].Quellen.Single(q => q.Kennung == Von);
        q.Werte["Hoehengenauigkeit"] = "plusminus_3cm";
        q.Werte["Auslaufform"] = "keine_Querschnittsaenderung";
        Importiere(p);
        var a = p.Objektakten.Single(a => a.Art == "haltungspunkt" && a.Quellen.Any(q => q.Kennung == Von));
        Assert.Equal("+/- 3 cm", a.Werte["haltungspunkt.hoehengenauigkeit"].Text);
        Assert.Equal("Keine Querschnittsänderung", a.Werte["haltungspunkt.auslaufform"].Text);
    }

    [Fact]
    public void Nicht_zugeordnete_Punktfelder_werden_im_Exportbericht_genannt()
    {
        var p = Projekt(); Importiere(p);
        var a = p.Objektakten.First(a => a.Art == "haltungspunkt");
        var b = new ObjektaktenBearbeitung(p, p.Data[0].Id, "haltung");
        var f = FieldCatalog.Objektfelder.Feld("haltungspunkt.lagebestimmung");
        b.Schreibe(a, f, "", "Genau", b.ErlaubteEintraege(a, f).Single(e => e.Label == "Genau"));
        var result = new XtfNeuExportService().Erzeuge(new(p, "", NurPruefen: true));
        Assert.True(result.Ok, result.Fehler);
        Assert.Contains("Lagebestimmung = „Genau“ fehlt in der XTF", result.Bericht);
    }

    [Fact]
    public void Widerspruch_zwischen_zwei_manuellen_Anzeigen_desselben_Punkts_sperrt_den_Export()
    {
        var p = Projekt(); Importiere(p);
        var a = p.Objektakten.Single(a => a.Art == "haltungspunkt" && a.Quellen.Any(q => q.Kennung == Von));
        a.Werte["haltungspunkt.hoehe"] = new() { Text = "450", VonHand = true };
        p.Objektakten[0].Werte["haltung.fromlevel"] = new() { Text = "449", VonHand = true };
        var result = new XtfNeuExportService().Erzeuge(new(p, "", NurPruefen: true));
        Assert.False(result.Ok);
        Assert.Contains("widersprüchliche aktuelle Angaben", result.Fehler);
    }
}
