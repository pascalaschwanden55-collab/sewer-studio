using System.Xml.Linq;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Application.UseCases.Objektakten;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
using AuswertungPro.Next.Infrastructure.Lookup;
using static AuswertungPro.Next.Infrastructure.Tests.XtfDssExportTests;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class XtfEinbautenTests
{
    internal const string Knoten = "chTEST0000000010", Bauwerk = "chTEST0000000011";

    [Theory]
    [InlineData("FoerderAggregat", "pumpe", "foerderstrom_max_einzel_l_s", "12,345", "FoerderstromMax_einzel", "12.345")]
    [InlineData("Absperr_Drosselorgan", "absperr_drossel", "oeffnung_ist_mm", "120", "Drosselorgan_Oeffnung_Ist", "120")]
    [InlineData("Leapingwehr", "ueberlauf", "oeffnungsform", "Rechteck", "Oeffnungsform", "Rechteck")]
    [InlineData("Streichwehr", "ueberlauf", "bauwerksart", "Streichwehr, niedrig", "Wehr_Art", "niedrig")]
    [InlineData("Trockenwetterfallrohr", "bauwerksteil", "durchmesser", "200", "Durchmesser", "200")]
    [InlineData("Einstiegshilfe", "bauwerksteil", "instandstellung", "Notwendig", "Instandstellung", "notwendig")]
    public void Original_Einbau_wird_bearbeitbar_und_mit_Originalkennung_und_Beziehung_exportiert(
        string klasse, string art, string feld, string text, string attribut, string norm)
    {
        var p = Probe(); Importiere(p);
        var a = Assert.Single(p.Objektakten.Where(a => a.Art == art && a.Quellen.Any(q => q.Klasse == klasse)));
        var original = a.Quellen.Single(q => q.Klasse == klasse);
        var vorher = original.Werte.GetValueOrDefault(attribut);
        var b = new ObjektaktenBearbeitung(p, p.SchaechteData[0].Id, "schacht");
        var f = FieldCatalog.Objektfelder.Feld(art + "." + feld);
        b.Schreibe(a, f, a.Werte.GetValueOrDefault(f.Id)?.Text ?? "", text, b.ErlaubteEintraege(a, f).SingleOrDefault(e => e.Label == text));
        Importiere(p);
        Assert.Equal(text, a.Werte[f.Id].Text);
        Assert.Equal(vorher, original.Werte.GetValueOrDefault(attribut));
        Assert.Single(p.Objektakten.Where(x => x.Art == art && x.Quellen.Any(q => q.Kennung == original.Kennung)));
        WithExport(p, doc =>
        {
            var o = Objekt(doc, klasse, original.Kennung);
            Assert.Equal(norm, o.Elements().Single(e => e.Name.LocalName == attribut).Value);
            var rolle = klasse is "Einstiegshilfe" or "Trockenwetterfallrohr" ? "AbwasserbauwerkRef" : "AbwasserknotenRef";
            Assert.Equal(original.Referenzen[rolle], o.Elements().Single(e => e.Name.LocalName == rolle).Attribute("REF")!.Value);
        });
    }

    [Fact]
    public void Abgleich_findet_fehlende_Einbauakten_auch_bei_bereits_gespeicherten_Quellen()
    {
        var p = Probe(); Importiere(p);
        Assert.False(GeoShopObjektaktenImport.HatNeueQuellen(p, p.SchaechteData[0].Id, Bauteil(p)));
        p.Objektakten.RemoveAll(a => a.Art == "pumpe");
        Assert.True(GeoShopObjektaktenImport.HatNeueQuellen(p, p.SchaechteData[0].Id, Bauteil(p)));
    }

    [Theory]
    [InlineData("Streichwehr", "Leapingwehr")]
    [InlineData("Leapingwehr", "Streichwehr, niedrig")]
    public void Eine_Originalkennung_darf_nicht_unbemerkt_die_Wehrklasse_wechseln(string klasse, string text)
    {
        var p = Probe(); Importiere(p);
        var a = p.Objektakten.Single(a => a.Art == "ueberlauf" && a.Quellen.Any(q => q.Klasse == klasse));
        a.Werte["ueberlauf.bauwerksart"] = new() { Text = text, VonHand = true };
        var r = new XtfNeuExportService().Erzeuge(new(p, "", NurPruefen: true));
        Assert.False(r.Ok); Assert.Contains("Objektklasse", r.Fehler);
    }

    [Fact]
    public void Nicht_zugeordnetes_Feld_und_unbelegte_Neuanlage_werden_deutlich_gemeldet()
    {
        var p = Probe(); Importiere(p);
        var a = p.Objektakten.Single(a => a.Art == "pumpe");
        a.Werte["pumpe.anzahl"] = new() { Text = "2", VonHand = true };
        var r = new XtfNeuExportService().Erzeuge(new(p, "", NurPruefen: true));
        Assert.True(r.Ok, r.Fehler); Assert.Contains("Anzahl = „2“ fehlt in der XTF", r.Bericht);
        a.Quellen.Clear();
        r = new XtfNeuExportService().Erzeuge(new(p, "", NurPruefen: true));
        Assert.False(r.Ok); Assert.Contains("Original", r.Fehler);
    }

    [Theory]
    [InlineData("", "Pflichtverweis")]
    [InlineData("chTEST0000000098", "Bezugsobjekt")]
    [InlineData(Kanal, "zeigt auf Kanal")]
    public void Unpassende_Anschlusskennung_sperrt_die_Lieferung(string tid, string fehler)
    {
        var p = Probe(); Importiere(p);
        p.Objektakten.Single(a => a.Art == "pumpe").Werte["pumpe.knoten"] = new() { Text = tid, VonHand = true };
        var r = new XtfNeuExportService().Erzeuge(new(p, "", NurPruefen: true));
        Assert.False(r.Ok); Assert.Contains(fehler, r.Fehler);
    }

    [Fact]
    public void Gemeinsamer_Einbau_erscheint_an_beiden_Projektobjekten_aber_nur_einmal_in_der_XTF()
    {
        var p = Probe(); Importiere(p);
        var q = Bauteil(p);
        GeoShopObjektaktenImport.Uebernehme(p, p.Data[0].Id, "haltung", q, false);
        var a = Assert.Single(p.Objektakten.Where(a => a.Art == "pumpe"));
        Assert.Contains(p.Data[0].Id, a.Bezuege); Assert.Contains(p.SchaechteData[0].Id, a.Bezuege);
        WithExport(p, doc => Assert.Single(doc.Descendants().Where(e => e.Name.LocalName.EndsWith(".FoerderAggregat"))));
    }

    [Fact]
    public void Widerspruechliche_Bearbeitung_derselben_Kennung_wird_nicht_ueberschrieben()
    {
        var p = Probe(); Importiere(p);
        var a = p.Objektakten.Single(a => a.Art == "pumpe");
        a.Werte["pumpe.fabrikat"] = new() { Text = "Erste Angabe", VonHand = true };
        p.Objektakten.Add(new() { Art = "pumpe", Quellen = a.Quellen, Bezuege = a.Bezuege,
            Werte = new() { ["pumpe.fabrikat"] = new() { Text = "Zweite Angabe", VonHand = true } } });
        var r = new XtfNeuExportService().Erzeuge(new(p, "", NurPruefen: true));
        Assert.False(r.Ok); Assert.Contains("widersprüchliche aktuelle Angaben", r.Fehler);
    }

    [Theory]
    [InlineData(BauteilArt.Schacht, "B")]
    [InlineData(BauteilArt.Haltung, "A-B")]
    public void Leser_nimmt_nur_zugehoerige_Einbauten_mit_und_bewahrt_ihre_Rohwerte(BauteilArt art, string name)
    {
        var p = Probe(); var q = p.Objektakten.SelectMany(a => a.Quellen).DistinctBy(q => q.Kennung).ToList();
        if (art == BauteilArt.Haltung)
        {
            // Einbauten dürfen an einem Netzknoten ohne eigenes Bauwerk hängen.
            q.Single(q => q.Kennung == Knoten).Referenzen.Remove("AbwasserbauwerkRef");
            foreach (var punkt in q.Where(q => q.Klasse == "Haltungspunkt")) punkt.Referenzen["AbwassernetzelementRef"] = Knoten;
            foreach (var teil in q.Where(q => q.Klasse is "Einstiegshilfe" or "Trockenwetterfallrohr")) teil.Referenzen["AbwasserbauwerkRef"] = Kanal;
            q.Single(q => q.Kennung == Haltung).Referenzen["RohrprofilRef"] = "chTEST0000000088";
            q.Add(Quelle("Rohrprofil", "chTEST0000000088", new() { ["Bezeichnung"] = "Kreis", ["Profiltyp"] = "Kreisprofil", ["HoehenBreitenverhaeltnis"] = "1.00" }));
        }
        q.Add(Quelle("FoerderAggregat", "chTEST0000000099", new() { ["Bezeichnung"] = "Fremde Pumpe" },
            new() { ["AbwasserknotenRef"] = "chTEST0000000098" }));
        XNamespace ns = "http://www.interlis.ch/INTERLIS2.3";
        var doc = new XDocument(new XElement(ns + "TRANSFER", new XElement(ns + "DATASECTION",
            new XElement(ns + "DSS_2020_1_LV95.Siedlungsentwaesserung", new XAttribute("BID", "b1"), q.Select(o =>
                new XElement(ns + (o.Modell + ".Siedlungsentwaesserung." + o.Klasse), new XAttribute("TID", o.Kennung),
                    o.Werte.Select(f => new XElement(ns + f.Key, f.Value)),
                    o.Referenzen.Select(f => new XElement(ns + f.Key, new XAttribute("REF", f.Value)))))))));
        var datei = Path.Combine(Path.GetTempPath(), "SewerStudio-Einbauten-" + Guid.NewGuid() + ".xtf");
        try
        {
            doc.Save(datei);
            var b = Assert.Single(new GeoShopXtfLeser().Lies(datei, art, [name]).Bauteile);
            Assert.Null(b.Fehler);
            Assert.DoesNotContain(b.Quellen!, q => q.Kennung == "chTEST0000000099");
            foreach (var klasse in new[] { "FoerderAggregat", "Absperr_Drosselorgan", "Streichwehr", "Leapingwehr", "Trockenwetterfallrohr", "Einstiegshilfe" })
                Assert.Single(b.Quellen!.Where(q => q.Klasse == klasse));
        }
        finally { File.Delete(datei); }
    }

    internal static Project Probe()
    {
        var p = XtfDssVerbundTests.Verbund();
        var q = p.Objektakten[1].Quellen;
        q.AddRange(new[]
        {
            Quelle("FoerderAggregat", "chTEST0000000030", new() { ["Bezeichnung"] = "Pumpe B", ["Bauart"] = "Kreiselpumpe", ["AufstellungAntrieb"] = "nass", ["FoerderstromMax_einzel"] = "10.000" }, new() { ["AbwasserknotenRef"] = Knoten }),
            Quelle("Absperr_Drosselorgan", "chTEST0000000031", new() { ["Bezeichnung"] = "Drossel B", ["Art"] = "Rueckstauklappe" }, new() { ["AbwasserknotenRef"] = Knoten }),
            Quelle("Leapingwehr", "chTEST0000000032", new() { ["Bezeichnung"] = "Leaping B", ["Oeffnungsform"] = "Kreis" }, new() { ["AbwasserknotenRef"] = Knoten }),
            Quelle("Streichwehr", "chTEST0000000033", new() { ["Bezeichnung"] = "Streich B", ["Wehr_Art"] = "hochgezogen" }, new() { ["AbwasserknotenRef"] = Knoten }),
            Quelle("Trockenwetterfallrohr", "chTEST0000000034", new() { ["Bezeichnung"] = "Fallrohr B", ["Durchmesser"] = "150" }, new() { ["AbwasserbauwerkRef"] = Bauwerk })
        });
        return p;
    }
    internal static void Importiere(Project p) => GeoShopObjektaktenImport.Uebernehme(p, p.SchaechteData[0].Id, "schacht", Bauteil(p), false);
    private static GeoShopBauteil Bauteil(Project p) => new("B", KatasterKennung.FuerSchacht("B", null, Knoten, Bauwerk),
        new Dictionary<string, string>(), Quellen: p.Objektakten[1].Quellen.ToArray());
}
