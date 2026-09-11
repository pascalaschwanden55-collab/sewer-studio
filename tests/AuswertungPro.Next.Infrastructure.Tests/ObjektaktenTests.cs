using AuswertungPro.Next.Application.UseCases.Objektakten;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Projects;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Infrastructure.Lookup;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class ObjektaktenTests
{
    [Fact]
    public void Ausfuehrende_Firma_folgt_nur_der_belegten_Ereignisbeziehung()
    {
        var p = new Project(); var h = new HaltungRecord(); p.Data.Add(h);
        var eventQuelle = new ObjektQuellbeleg { Klasse = "Unterhalt", Kennung = "ereignis", Werte = new() { ["Art"] = "Sanierung_Reparatur", ["Ausfuehrender"] = "Operateur" } };
        var quelle = new GeoShopBauteil("A-B", KatasterKennung.FuerHaltung("A-B", null, "haltung", "kanal", null, null, null, null, null, null),
            new Dictionary<string, string>(), Quellen: [eventQuelle,
                new() { Klasse = "Erhaltungsereignis_Ausfuehrende_FirmaAssoc", Kennung = "lokal:beleg", IstLokaleKennung = true,
                    Referenzen = new() { ["Erhaltungsereignis_Ausfuehrende_FirmaAssocRef"] = "ereignis", ["Ausfuehrende_FirmaRef"] = "firma" } },
                new() { Klasse = "Organisation", Kennung = "firma", Werte = new() { ["Bezeichnung"] = "Ausführende Firma" } }]);
        GeoShopObjektaktenImport.Uebernehme(p, h.Id, "haltung", quelle, false);
        var ereignis = Assert.Single(p.Objektakten.Where(a => a.Art == "sanierung"));
        Assert.Equal("Ausführende Firma", ereignis.Werte["sanierung.firma"].Text);
        Assert.Equal("Operateur", ereignis.Werte["sanierung.ausfuehrender"].Text);
        Assert.False(ereignis.Werte.ContainsKey("sanierung.s_manufacturer"));
        Assert.Equal(3, ereignis.Quellen.Count);
        GeoShopObjektaktenImport.Uebernehme(p, h.Id, "haltung", quelle, false);
        Assert.Equal(3, ereignis.Quellen.Count);
    }
    [Fact]
    public void Eigentuemernamen_nur_aus_expliziter_Datei_und_Reinigung_ist_keine_Sanierung()
    {
        var p = new Project(); var s = new SchachtRecord(); p.SchaechteData.Add(s);
        var bw = new ObjektQuellbeleg { Klasse = "Normschacht", Kennung = "chTEST00B0000001",
            Referenzen = new() { ["EigentuemerRef"] = "chTEST00O0000001" } };
        var quelle = new GeoShopBauteil("A", KatasterKennung.FuerSchacht("A", null, "chTEST00K0000001", bw.Kennung),
            new Dictionary<string, string>(), Quellen: [bw, new() { Klasse = "Unterhalt", Kennung = "chTEST00U0000001", Werte = new() { ["Art"] = "Reinigung" } }]);
        GeoShopObjektaktenImport.Uebernehme(p, s.Id, "schacht", quelle, false);
        Assert.DoesNotContain(p.Objektakten, a => a.Art == "sanierung");
        Assert.Equal("", s.GetFieldValue(FieldKeys.Owner));
        var bestand = new GeoShopBestand(BauteilArt.Schacht, "quelle.xtf", [quelle]);
        var neu = GeoShopEigentuemerErgaenzung.Ergaenze(bestand, new Dictionary<string, string> { ["chTEST00O0000001"] = "Firma" }, "ausgewählt.json");
        Assert.Equal("Firma", neu.Bauteile[0].Felder[FieldKeys.Owner]);
        Assert.Contains(neu.Bauteile[0].Quellen!, q => q.Datei == "ausgewählt.json");
        Assert.DoesNotContain(neu.Bauteile[0].Felder.Keys, k => k.Contains("Betreiber"));
        Assert.Empty(quelle.Felder);
    }

    [Fact]
    public void Doppelte_Eigentuemerschluessel_werden_nicht_still_ueberschrieben()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            File.WriteAllText(path, "{\"chTEST00O0000001\":\"Erste\",\"chTEST00O0000001\":\"Zweite\"}");
            Assert.Throws<InvalidDataException>(() => GeoShopEigentuemerDatei.Lies(path));
        }
        finally { File.Delete(path); }
    }
    [Fact]
    public void Quellenumfang_ohne_Beispielwerte_und_eindeutige_Verweise()
    {
        var k = FieldCatalog.Objektfelder;
        k.Pruefe();

        // Die Zahlen gehoeren zu den vier Masken des WebGIS-Plans vom 10.09.2026. Der
        // Katalog darf um Detailmasken wachsen; dieser Bestand muss unveraendert bleiben.
        string[] bestandsarten = ["haltung", "schacht", "deckel", "sanierung"];
        var bestand = k.Felder.Where(f => bestandsarten.Contains(f.Art)).ToArray();
        Assert.Equal(217, bestand.Length);
        Assert.Equal(204, bestand.Sum(f => f.Quellanzeigen));
        Assert.Equal(200, bestand.Count(f => f.Quellanzeigen > 0));
        Assert.Equal(91, bestand.Where(f => f.KatalogId is not null).Sum(f => f.Quellanzeigen));
        Assert.Equal(78, bestand.Select(f => f.KatalogId).Where(id => id is not null).Distinct().Count());

        Assert.Equal(24, k.Unterlisten.Count);
        Assert.All(k.Kataloge.Where(c => !c.CodesBestaetigt), c => Assert.All(c.Eintraege, e => Assert.Null(e.OriginalCode)));

        // Fuer den ganzen Katalog gilt: jeder Verweis loest auf, kein Katalog ist leer,
        // und keine zwei Felder derselben Objektart tragen dieselbe Beschriftung.
        Assert.All(k.Felder.Where(f => f.KatalogId is not null), f => Assert.NotNull(k.Auswahl(f.KatalogId)));
        Assert.All(k.Kataloge, c => Assert.NotEmpty(c.Eintraege));
        foreach (var gruppe in k.Felder.GroupBy(f => f.Art))
        {
            var doppelt = gruppe.GroupBy(f => f.Label).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
            Assert.True(doppelt.Length <= 2, $"{gruppe.Key}: mehrfach vergebene Beschriftungen {string.Join(", ", doppelt)}");
        }
    }

    [Fact]
    public void Abhaengige_Felder_haben_einen_Katalog_je_Elternwert_und_der_Bestand_bleibt()
    {
        var k = FieldCatalog.Objektfelder;

        // Die zwei abhaengigen Bestandsfelder: alter Katalog unveraendert, voller Katalog dazu.
        var material = k.Feld("haltung.material");
        Assert.Equal("haltung-C06", material.KatalogId);
        Assert.Equal(14, k.Auswahl("haltung-C06")!.Eintraege.Count);
        Assert.Equal("haltung.material-je-eltern", material.KatalogIdJeEltern);
        Assert.Equal(43, k.Auswahl(material.KatalogIdJeEltern)!.Eintraege.Count);
        Assert.Equal(6, k.Auswahl(material.KatalogIdJeEltern)!.Eintraege.Select(e => e.Eltern).Distinct().Count());

        var detail = k.Feld("schacht.materialdetail");
        Assert.Equal("schacht-C07", detail.KatalogId);
        Assert.Single(k.Auswahl("schacht-C07")!.Eintraege);
        Assert.Equal(25, k.Auswahl(detail.KatalogIdJeEltern)!.Eintraege.Count);

        // Jeder Katalog je Elternwert zeigt nur auf Codes, die der Eltern-Katalog kennt.
        foreach (var feld in k.Felder.Where(f => f.KatalogIdJeEltern is not null))
        {
            Assert.NotNull(feld.Elternfeld);
            var eltern = k.Feld(feld.Elternfeld!);
            var codes = k.Auswahl(eltern.KatalogId)!.Eintraege.Select(e => e.OriginalCode).ToHashSet();
            Assert.All(k.Auswahl(feld.KatalogIdJeEltern)!.Eintraege, e => Assert.Contains(e.Eltern, codes));
        }
        // Die Beton-Gruppe des vollen Katalogs ist genau der alte Bestandskatalog.
        var beton = k.Auswahl(material.KatalogIdJeEltern)!.Eintraege.Where(e => e.Eltern == "1")
            .Select(e => (e.OriginalCode, e.Label));
        Assert.Equal(k.Auswahl("haltung-C06")!.Eintraege.Select(e => (e.OriginalCode, e.Label)), beton);
    }

    [Fact]
    public void Jede_Aufklappliste_nennt_ihr_Ziel_und_ihre_Rechte()
    {
        var listen = FieldCatalog.Objektfelder.Unterlisten;
        Assert.Equal(24, listen.Count);
        Assert.All(listen, l => Assert.NotEqual("", l.ZeigtAufObjektart));

        // Ein Einlauf ist die anschliessende Haltung, kein zweites Objekt daneben.
        foreach (var titel in new[] { "Einläufe", "Ausläufe" })
            Assert.All(listen.Where(l => l.Label == titel), l =>
            {
                Assert.Equal("haltung", l.ZeigtAufObjektart);
                Assert.False(l.EigeneObjektart);
                Assert.False(l.DarfAnlegen);
            });

        // Neun Listeneintraege sind im WebGIS nur lesend; dort wird nichts angelegt.
        Assert.Equal(9, listen.Count(l => l.NurLesen));
        Assert.All(listen.Where(l => l.NurLesen), l => Assert.False(l.DarfAnlegen));
        Assert.Equal(15, listen.Count(l => l.DarfAnlegen));

        // Deckel und Sanierung bleiben eigene Akten und weiterhin anlegbar.
        Assert.All(listen.Where(l => l.Label is "Deckel" or "Hauptdeckel"), l =>
        {
            Assert.Equal("deckel", l.ZeigtAufObjektart);
            Assert.Equal("schacht", l.Art);
            Assert.True(l.DarfAnlegen);
        });
        Assert.All(listen.Where(l => l.Label == "Sanierungsmassnahmen"), l =>
        {
            Assert.Equal("sanierung", l.ZeigtAufObjektart);
            Assert.True(l.DarfAnlegen);
        });
    }

    [Fact]
    public void Oeffnen_aendert_nichts_Handwerte_und_Umlautnamen_bleiben_ein_Feld()
    {
        var p = new Project(); var s = new SchachtRecord(); p.SchaechteData.Add(s);
        s.SetFieldValue("Eigentümer", "Alte Firma", FieldSource.Manual, true);
        var b = new ObjektaktenBearbeitung(p, s.Id, "schacht");
        var f = FieldCatalog.Objektfelder.Feld("schacht.eigentuemer");
        Assert.Equal("Alte Firma", b.Lies(b.Wurzel, f));
        Assert.Empty(p.Objektakten); Assert.False(p.Dirty);
        b.Schreibe(b.Wurzel, f, "Alte Firma", "Neue Firma");
        Assert.Equal("Neue Firma", s.GetFieldValue("Eigentümer"));
        Assert.False(s.Fields.ContainsKey("Eigentuemer"));
        Assert.True(s.FieldMeta["Eigentümer"].UserEdited);
        Assert.Equal(3, p.Version);
    }

    [Fact]
    public void Zwei_Deckel_zwei_Ereignisse_und_unbekannte_Codes_ueberstehen_Dateirunde()
    {
        var p = new Project(); var s = new SchachtRecord(); p.SchaechteData.Add(s);
        var b = new ObjektaktenBearbeitung(p, s.Id, "schacht");
        var d1 = b.Neu("deckel"); var d2 = b.Neu("deckel");
        b.Schreibe(d1, FieldCatalog.Objektfelder.Feld("deckel.hoehe"), "", "450.94");
        b.Schreibe(d2, FieldCatalog.Objektfelder.Feld("deckel.hoehe"), "", "451.00");
        b.SetzeHauptdeckel(d1);
        b.Schreibe(b.Wurzel, FieldCatalog.Objektfelder.Feld("schacht.sohlenhoehe"), "", "448.34");
        var e1 = b.Neu("sanierung"); b.Neu("sanierung");
        e1.Werte["fremdes.feld"] = new() { Text = "unbekannt", Originalcode = "X-999" };
        s.SetFieldValue("Sanieren_JaNein", "Nein", FieldSource.Manual, true);
        Assert.Contains("2", b.BerechneteTiefe());
        var repo = new JsonProjectRepository();
        var path = Path.Combine(Path.GetTempPath(), $"objektakte-{Guid.NewGuid():N}.json");
        try
        {
            Assert.True(repo.Save(p, path).Ok);
            var loaded = repo.Load(path); Assert.True(loaded.Ok, loaded.ErrorMessage);
            var copy = loaded.Value!;
            Assert.Equal(2, copy.Objektakten.Count(a => a.Art == "deckel"));
            Assert.Equal(2, copy.Objektakten.Count(a => a.Art == "sanierung"));
            Assert.Equal("X-999", copy.Objektakten.Single(a => a.Id == e1.Id).Werte["fremdes.feld"].Originalcode);
            Assert.Equal(d1.Id, copy.Objektakten.Single(a => a.Id == s.Id).HauptdeckelId);
            Assert.All(copy.Objektakten.Where(a => a.Id != s.Id), a => Assert.Contains(s.Id, a.Bezuege));
        }
        finally { File.Delete(path); File.Delete(path + ".bak"); }
    }

    [Fact]
    public void Neuere_Aenderung_wird_nicht_ueberschrieben_und_Bemerkung_erzeugt_keine_Historie()
    {
        var p = new Project(); var h = new HaltungRecord(); p.Data.Add(h);
        var b = new ObjektaktenBearbeitung(p, h.Id, "haltung");
        h.SetFieldValue(FieldKeys.Remarks, "Saniert 2018", FieldSource.Manual, true);
        var f = FieldCatalog.Objektfelder.Feld("haltung.remarks");
        Assert.Throws<InvalidOperationException>(() => b.Schreibe(b.Wurzel, f, "Alt", "Meine Eingabe"));
        Assert.Equal("Saniert 2018", h.GetFieldValue(FieldKeys.Remarks));
        Assert.Empty(p.Objektakten);
    }
}
