using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Application.UseCases.Objektakten;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
using static AuswertungPro.Next.Infrastructure.Tests.XtfDssExportTests;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class XtfUnterhaltTests
{
    private const string Ereignis = "chTEST0000000030";

    [Fact]
    public void Reinigung_wird_bearbeitbar_importiert_ohne_Duplikat_und_mit_Originalkennung_exportiert()
    {
        var p = Projekt();
        var h = p.Data[0];
        var eventQuelle = Quelle("Unterhalt", Ereignis, new()
        {
            ["Bezeichnung"] = "Reinigung A-B", ["Art"] = "Reinigung", ["Status"] = "geplant",
            ["Zeitpunkt"] = "20260912", ["Dauer"] = "2", ["Kosten"] = "120.50", ["Bemerkung"] = "Original"
        });
        var quelle = new GeoShopBauteil("A-B", KatasterKennung.FuerHaltung("A-B", null, Haltung, Kanal,
            Von, Nach, null, null, null, null), new Dictionary<string, string>(), Quellen: [eventQuelle]);
        GeoShopObjektaktenImport.Uebernehme(p, h.Id, "haltung", quelle, false);
        var akte = Assert.Single(p.Objektakten.Where(a => a.Art == "unterhalt"));
        Assert.Equal("Reinigung A-B", akte.Werte["unterhalt.bezeichnung"].Text);
        Assert.Equal("120.50", akte.Werte["unterhalt.kosten"].Text);
        Assert.Equal("Geplant", akte.Werte["unterhalt.status"].Text);
        var b = new ObjektaktenBearbeitung(p, h.Id, "haltung");
        b.Schreibe(akte, FieldCatalog.Objektfelder.Feld("unterhalt.bemerkung"), "Original", "Gespült");
        GeoShopObjektaktenImport.Uebernehme(p, h.Id, "haltung", quelle, false);
        Assert.Single(p.Objektakten.Where(a => a.Art == "unterhalt"));
        Assert.Equal("Gespült", akte.Werte["unterhalt.bemerkung"].Text);
        var liste = FieldCatalog.Objektfelder.Unterlisten.Single(l => l.Art == "haltung" && l.ZeigtAufObjektart == "unterhalt");
        Assert.Single(ObjektaktenListen.Akten(b, b.Wurzel, liste));
        Assert.Empty(ObjektaktenListen.Zeilen(b, b.Wurzel, liste));
        WithExport(p, doc =>
        {
            var o = Objekt(doc, "Unterhalt", Ereignis);
            Assert.Equal("Gespült", o.Elements().Single(e => e.Name.LocalName == "Bemerkung").Value);
            Assert.Equal("120.50", Wert(doc, "Unterhalt", "Kosten"));
            var assoc = Objekt(doc, "Erhaltungsereignis_AbwasserbauwerkAssoc");
            Assert.Equal(Kanal, assoc.Elements().Single(e => e.Name.LocalName == "AbwasserbauwerkRef").Attribute("REF")!.Value);
        });
        Assert.Equal("Original", eventQuelle.Werte["Bemerkung"]);
    }

    [Theory]
    [InlineData("Reinigung", "Ausgeführt", "Reinigung", "ausgefuehrt")]
    [InlineData("Reparatur", "Geplant", "Sanierung_Reparatur", "geplant")]
    [InlineData("Untersuchung", "Nicht möglich", "Untersuchung", "nicht_moeglich")]
    public void Neu_erfasster_Unterhalt_liefert_alle_belegten_Felder(string art, string status, string normArt, string normStatus)
    {
        var p = Projekt();
        p.Objektakten.Add(new() { Art = "unterhalt", Bezuege = [p.Data[0].Id], Werte = new()
        {
            ["unterhalt.bezeichnung"] = new() { Text = "Spülung 2026", VonHand = true },
            ["unterhalt.art"] = new() { Text = art, VonHand = true },
            ["unterhalt.status"] = new() { Text = status, VonHand = true },
            ["unterhalt.zeitpunkt"] = new() { Text = "12.09.2026", VonHand = true },
            ["unterhalt.dauer_t"] = new() { Text = "2", VonHand = true },
            ["unterhalt.ausfuehrender"] = new() { Text = "Equipe A", VonHand = true }
        } });
        WithExport(p, doc =>
        {
            Assert.Equal(normArt, Wert(doc, "Unterhalt", "Art"));
            Assert.Equal(normStatus, Wert(doc, "Unterhalt", "Status"));
            Assert.Equal("20260912", Wert(doc, "Unterhalt", "Zeitpunkt"));
            Assert.Equal("2", Wert(doc, "Unterhalt", "Dauer"));
            Assert.Equal("Equipe A", Wert(doc, "Unterhalt", "Ausfuehrender"));
        });
    }

    [Theory]
    [InlineData("unterhalt.art", "Begehung")]
    [InlineData("unterhalt.status", "Beauftragt")]
    public void Ungeklaerter_Normwert_sperrt_statt_einen_anderen_Wert_einzusetzen(string feld, string text)
    {
        var p = Projekt();
        p.Objektakten.Add(new() { Art = "unterhalt", Bezuege = [p.Data[0].Id], Werte = new()
        { [feld] = new() { Text = text, VonHand = true }, ["unterhalt.bezeichnung"] = new() { Text = "Ereignis 42" } } });
        var result = new XtfNeuExportService().Erzeuge(new(p, "", NurPruefen: true));
        Assert.False(result.Ok);
        Assert.Contains(text, result.Fehler);
    }
}
