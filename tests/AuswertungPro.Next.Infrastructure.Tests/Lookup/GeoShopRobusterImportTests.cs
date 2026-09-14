using System.Text.Json;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Application.UseCases;
using AuswertungPro.Next.Application.UseCases.Objektakten;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Lookup;

namespace AuswertungPro.Next.Infrastructure.Tests.Lookup;

public sealed class GeoShopRobusterImportTests
{
    [Theory]
    [InlineData("OBJID")]
    [InlineData("OBJECTID")]
    [InlineData("objid")]
    public void Explizit_gelieferte_Objektid_wird_mit_fuehrenden_Nullen_importiert(string attribut)
    {
        var (p, s, b) = Beispiel();
        b.Bauteile[0].Quellen!.Single(q => q.Klasse == "Normschacht").Werte[attribut] = "009123";
        Anwenden(Plane(p, s, b), p, s);
        var akte = p.Objektakten.Single(a => a.Id == s.Id);
        Assert.Equal("009123", akte.Werte["schacht.objectid"].Text);
        Assert.Equal(Knoten, s.Geonis!.Knoten);
        Assert.Empty(Plane(p, s, b).Positionen);
    }

    [Fact]
    public void Widersprechende_Objektkennungen_werden_nicht_willkuerlich_ausgewaehlt()
    {
        var (p, s, b) = Beispiel();
        var q = b.Bauteile[0].Quellen!.Single(q => q.Klasse == "Normschacht");
        q.Werte["OBJID"] = "123"; q.Werte["OBJECTID"] = "456";
        var plan = Plane(p, s, b);
        Assert.Empty(plan.Positionen);
        Assert.Contains(plan.Hinweise, h => h.Contains("OBJID und OBJECTID"));
        Assert.Empty(p.Objektakten);
    }

    private const string Knoten = "chTEST00A0000001", Bauwerk = "chTEST00C0000001";
    private static (Project Projekt, SchachtRecord Schacht, GeoShopBestand Bestand) Beispiel()
    {
        var p = new Project(); var s = new SchachtRecord(); p.SchaechteData.Add(s);
        s.SetFieldValue("Schachtnummer", "60248", FieldSource.Legacy, false);
        s.SetFieldValue("Funktion", "NOD", FieldSource.Legacy, false);
        s.SetFieldValue("Material", "GFK-Liner", FieldSource.Legacy, false);
        var quellen = new List<ObjektQuellbeleg>
        {
            Quelle("Abwasserknoten", Knoten, new() { ["Bezeichnung"] = "60248", ["Sohlenkote"] = "503.680" },
                new() { ["AbwasserbauwerkRef"] = Bauwerk }),
            Quelle("Normschacht", Bauwerk, new() { ["Bezeichnung"] = "60248", ["Material"] = "Beton", ["Funktion"] = "Kontroll_Einsteigschacht", ["Baujahr"] = "1974" }, new()),
            Quelle("Deckel", "chTEST00D0000001", new() { ["Kote"] = "505.920" }, new() { ["AbwasserbauwerkRef"] = Bauwerk })
        };
        quellen[0].Strukturen["Lage"] = "<Lage><COORD><C1>2692748.532</C1><C2>1192136.855</C2></COORD></Lage>";
        var teil = new GeoShopBauteil("60248", KatasterKennung.FuerSchacht("60248", null, Knoten, Bauwerk),
            new Dictionary<string, string> { ["Funktion"] = "Kontrollschacht", ["Material"] = "Beton", ["Baujahr"] = "1974" }, Quellen: quellen);
        return (p, s, new(BauteilArt.Schacht, "synthetisch.xtf", [teil]));
    }
    private static ObjektQuellbeleg Quelle(string klasse, string id, Dictionary<string, string> werte, Dictionary<string, string> refs)
        => new() { System = "GeoShop-XTF", Modell = "DSS_2020_1_LV95", Klasse = klasse, Kennung = id, Werte = werte, Referenzen = refs };
    private static GeoShopPlan Plane(Project p, SchachtRecord s, GeoShopBestand b)
        => GeoShopAbgleichPlanBuilder.Baue([GeoShopZiel.Fuer(s, p)], b, mitVergleich: true);
    private static void Anwenden(GeoShopPlan plan, Project p, SchachtRecord s)
        => GeoShopAbgleichAnwender.WendeAn(plan, [GeoShopZiel.Fuer(s, p)]);

    [Fact]
    public void Alte_Importwerte_sind_waehlbar_Handwerte_und_bewusste_Leere_bleiben_geschuetzt()
    {
        var (p, s, b) = Beispiel();
        s.SetFieldValue("Baujahr", "", FieldSource.Manual, true);
        var vorher = JsonSerializer.Serialize(p); var plan = Plane(p, s, b);
        Assert.Equal(vorher, JsonSerializer.Serialize(p));
        var wahlen = Assert.Single(plan.Positionen).Vergleich!.Felder;
        var funktion = Assert.Single(wahlen.Where(w => w.Feld == "Funktion"));
        Assert.Equal("Alter Import", funktion.Herkunft); Assert.False(funktion.Uebernehmen);
        funktion.Uebernehmen = true;
        var jahr = Assert.Single(wahlen.Where(w => w.Feld == "Baujahr"));
        jahr.Uebernehmen = true; Assert.False(jahr.Uebernehmen);
        Anwenden(plan, p, s);
        Assert.Equal("Kontrollschacht", s.GetFieldValue("Funktion"));
        Assert.Equal("GFK-Liner", s.GetFieldValue("Material"));
        Assert.Equal("", s.GetFieldValue("Baujahr")); Assert.True(s.IsUserEdited("Baujahr"));
        Assert.Empty(Plane(p, s, b).Positionen);
    }

    [Fact]
    public void Koordinaten_und_ein_Deckel_liefern_Hoehe_und_berechnete_Tiefe_ohne_Hauptdeckelmarkierung()
    {
        var (p, s, b) = Beispiel(); Anwenden(Plane(p, s, b), p, s);
        var akte = p.Objektakten.Single(a => a.Id == s.Id);
        Assert.Equal("2692748.532", akte.Werte["schacht.rechtswert"].Text);
        Assert.Equal("1192136.855", akte.Werte["schacht.hochwert"].Text);
        Assert.Null(akte.HauptdeckelId);
        var bearbeitung = new ObjektaktenBearbeitung(p, s.Id, "schacht");
        Assert.Equal("505.920", bearbeitung.Lies(akte, FieldCatalog.Objektfelder.Feld("schacht.deckelhoehe")));
        Assert.Equal($"Berechnete Tiefe: {2.24m.ToString("0.00", System.Globalization.CultureInfo.CurrentCulture)} m (Deckel − Sohle)", bearbeitung.BerechneteTiefe());
        p.Objektakten.Add(new() { Art = "deckel", Bezuege = [s.Id], Werte = new() { ["deckel.hoehe"] = new() { Text = "506.2" } } });
        Assert.Equal("", bearbeitung.Lies(akte, FieldCatalog.Objektfelder.Feld("schacht.deckelhoehe")));
        var erster = p.Objektakten.First(a => a.Art == "deckel"); bearbeitung.SetzeHauptdeckel(erster);
        Assert.Equal("505.920", bearbeitung.Lies(akte, FieldCatalog.Objektfelder.Feld("schacht.deckelhoehe")));
    }

    [Fact]
    public void Neue_Lieferung_vergleicht_Deckelhoehen_und_erzeugt_keine_doppelten_Quellversionen()
    {
        var (p, s, b) = Beispiel(); Anwenden(Plane(p, s, b), p, s);
        b.Bauteile[0].Quellen!.Single(q => q.Klasse == "Deckel").Werte["Kote"] = "506.000";
        var plan = Plane(p, s, b);
        var hoehe = Assert.Single(plan.Positionen[0].Vergleich!.Felder.Where(w => w.Feld == "deckel.hoehe"));
        Assert.Equal("505.920", hoehe.Vorher); Assert.False(hoehe.Uebernehmen); hoehe.Uebernehmen = true;
        Anwenden(plan, p, s);
        var deckel = Assert.Single(p.Objektakten.Where(a => a.Art == "deckel"));
        Assert.Single(deckel.Quellen); Assert.Equal("506.000", deckel.Werte["deckel.hoehe"].Text);
        Assert.Empty(Plane(p, s, b).Positionen);
    }

    [Fact]
    public void Schreibfehler_nach_erster_Aenderung_setzt_ganzen_Lauf_zurueck()
    {
        var (p, s, b) = Beispiel(); var plan = Plane(p, s, b); var vorher = JsonSerializer.Serialize(p);
        s.PropertyChanged += (_, _) => throw new InvalidOperationException("Test-Schreibfehler");
        Assert.Throws<InvalidOperationException>(() => Anwenden(plan, p, s));
        Assert.Equal(vorher, JsonSerializer.Serialize(p));
    }

    [Fact]
    public void Projektwechsel_oder_neuere_Eingabe_sperrt_vor_erster_Aenderung()
    {
        var (p, s, b) = Beispiel(); var plan = Plane(p, s, b);
        p.Metadata["Gemeinde"] = "Neue Eingabe"; var vorher = JsonSerializer.Serialize(p);
        Assert.Throws<InvalidOperationException>(() => Anwenden(plan, p, s));
        Assert.Equal(vorher, JsonSerializer.Serialize(p));
    }

    [Fact]
    public void Sicherungsfehler_verhindert_Uebernahme_und_echte_Sicherung_enthaelt_ungespeicherte_Werte()
    {
        var (p, s, b) = Beispiel(); var plan = Plane(p, s, b); var vorher = JsonSerializer.Serialize(p);
        Assert.Throws<IOException>(() => GeoShopGesicherteUebernahme.WendeAn(plan, [GeoShopZiel.Fuer(s, p)], new DefekteSicherung()));
        Assert.Equal(vorher, JsonSerializer.Serialize(p));
        var dir = Path.Combine(Path.GetTempPath(), "geoshop-sicherung-" + Guid.NewGuid().ToString("N"));
        try
        {
            var pfad = new GeoShopSicherungsdatei(dir).Sichere(p);
            var kopie = JsonSerializer.Deserialize<Project>(File.ReadAllText(pfad))!;
            Assert.Equal(vorher, JsonSerializer.Serialize(kopie));
            Assert.NotEqual(pfad, new GeoShopSicherungsdatei(dir).Sichere(p));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Theory]
    [InlineData(FieldSource.Pdf)]
    [InlineData(FieldSource.Legacy)]
    [InlineData(FieldSource.Spro)]
    public void Spaeterer_Protokollimport_setzt_bestaetigte_Katasterwerte_nicht_zurueck(FieldSource quelle)
    {
        var (p, s, b) = Beispiel(); var plan = Plane(p, s, b);
        plan.Positionen[0].Vergleich!.Felder.Single(w => w.Feld == "Funktion").Uebernehmen = true;
        Anwenden(plan, p, s); s.SetFieldValue("Funktion", "NOD", quelle, false);
        Assert.Equal("Kontrollschacht", s.GetFieldValue("Funktion"));
        Assert.NotNull(s.FieldMeta["Funktion"].Conflict);
        s.SetFieldValue("Funktion", "Meine Korrektur", FieldSource.Manual, true);
        Assert.Equal("Meine Korrektur", s.GetFieldValue("Funktion"));
    }
    [Fact]
    public void Materialgruppe_folgt_nur_dem_gewaehlten_Material_und_schuetzt_bewusstes_Leeren()
    {
        var (p, s, b) = Beispiel(); var plan = Plane(p, s, b);
        var bearbeitung = new ObjektaktenBearbeitung(p, s.Id, "schacht");
        var feld = FieldCatalog.Objektfelder.Feld("schacht.materialgruppe");
        Assert.Equal("", bearbeitung.Lies(bearbeitung.Wurzel, feld));
        plan.Positionen[0].Vergleich!.Felder.Single(w => w.Feld == "Material").Uebernehmen = true;
        Anwenden(plan, p, s);
        Assert.Equal("Beton", bearbeitung.Lies(bearbeitung.Wurzel, feld));
        Assert.Equal("Beton", s.GetFieldValue("Material")); // Kein erfundenes Fertigteil.
        bearbeitung.Schreibe(bearbeitung.Wurzel, feld, "Beton", "");
        Assert.Equal("", bearbeitung.Lies(bearbeitung.Wurzel, feld));
    }

    [Fact]
    public void Abgelehnte_Koordinate_wird_mit_geaendertem_Partner_erneut_zusammen_angeboten()
    {
        var (p, s, b) = Beispiel();
        p.Objektakten.Add(new() { Id = s.Id, Art = "schacht", Werte = new()
        {
            ["schacht.rechtswert"] = new() { Text = "2692700.000" },
            ["schacht.hochwert"] = new() { Text = "1192100.000" }
        } });
        Anwenden(Plane(p, s, b), p, s);
        b.Bauteile[0].Quellen![0].Strukturen["Lage"] =
            "<Lage><COORD><C1>2692748.532</C1><C2>1192200.000</C2></COORD></Lage>";
        var plan = Plane(p, s, b); var felder = plan.Positionen[0].Vergleich!.Felder;
        var x = Assert.Single(felder.Where(w => w.Feld == "schacht.rechtswert"));
        var y = Assert.Single(felder.Where(w => w.Feld == "schacht.hochwert"));
        Assert.False(x.Uebernehmen); y.Uebernehmen = true; Assert.True(x.Uebernehmen);
        Anwenden(plan, p, s);
        Assert.Equal("2692748.532", p.Objektakten[0].Werte[x.Feld].Text);
        Assert.Equal("1192200.000", p.Objektakten[0].Werte[y.Feld].Text);
    }

    [Theory]
    [InlineData("<Lage><COORD><C1>2692748.532</C1></COORD></Lage>")]
    [InlineData("<Lage><COORD><C1>2692748.5321</C1><C2>1192136.855</C2></COORD></Lage>")]
    [InlineData("<Lage><COORD><C1>692748.532</C1><C2>192136.855</C2></COORD></Lage>")]
    [InlineData("<!DOCTYPE Lage [<!ENTITY x '2692748.532'>]><Lage><COORD><C1>&x;</C1><C2>1192136.855</C2></COORD></Lage>")]
    public void Unvollstaendige_oder_ungeeignete_Koordinaten_bleiben_nur_im_Originalbeleg(string lage)
    {
        var (p, s, b) = Beispiel(); b.Bauteile[0].Quellen![0].Strukturen["Lage"] = lage;
        var plan = Plane(p, s, b);
        Assert.DoesNotContain(plan.Positionen[0].Vergleich!.Felder, w => w.Feld is "schacht.rechtswert" or "schacht.hochwert");
        Assert.Contains(plan.Positionen[0].Vergleich!.Hinweise, h => h.Contains("Rechtswert"));
        Anwenden(plan, p, s);
        Assert.Equal(lage, p.Objektakten[0].Quellen.Single(q => q.Klasse == "Abwasserknoten").Strukturen["Lage"]);
    }

    [Fact]
    public void Gespeicherte_Entscheidungen_werden_nach_neuem_Laden_respektiert_und_beschaedigte_abgewiesen()
    {
        var (p, s, b) = Beispiel(); Anwenden(Plane(p, s, b), p, s);
        var neu = JsonSerializer.Deserialize<Project>(JsonSerializer.Serialize(p))!;
        Assert.Empty(Plane(neu, neu.SchaechteData.Single(), b).Positionen);
        neu.Metadata[$"GeoShop.Vergleich.{s.Id:N}"] = "{\"kaputt\":null}";
        var vorher = JsonSerializer.Serialize(neu);
        var gesperrt = Plane(neu, neu.SchaechteData.Single(), b);
        Assert.Empty(gesperrt.Positionen);
        Assert.Contains(gesperrt.Hinweise, h => h.Contains("unlesbar"));
        Assert.Equal(vorher, JsonSerializer.Serialize(neu));
    }

    [Fact]
    public void Defekte_Zuordnung_wird_ausgelassen_waehrend_der_naechste_Schacht_importierbar_bleibt()
    {
        var (p, s, b) = Beispiel();
        p.Objektakten.Add(new() { Id = s.Id, Art = "sanierung" });
        var zweiter = new SchachtRecord(); zweiter.SetFieldValue("Schachtnummer", "60249", FieldSource.Legacy, false);
        p.SchaechteData.Add(zweiter);
        b = b with { Bauteile = [b.Bauteile[0], new("60249",
            KatasterKennung.FuerSchacht("60249", null, "chTEST00A0000002", "chTEST00C0000002"),
            new Dictionary<string, string> { ["Baujahr"] = "2000" })] };
        var ziele = new[] { GeoShopZiel.Fuer(s, p), GeoShopZiel.Fuer(zweiter, p) };
        var plan = GeoShopAbgleichPlanBuilder.Baue(ziele, b, mitVergleich: true);
        Assert.Equal("60249", Assert.Single(plan.Positionen).Ziel.Name);
        Assert.Contains(plan.Hinweise, h => h.Contains("60248") && h.Contains("Objektart"));
        GeoShopAbgleichAnwender.WendeAn(plan, ziele);
        Assert.Equal("2000", zweiter.GetFieldValue("Baujahr"));
        Assert.Equal("NOD", s.GetFieldValue("Funktion"));
    }

    [Fact]
    public void Spaeterer_Xtf_Export_respektiert_behaltenes_Katasterjahr_und_Koordinaten()
    {
        var (p, s, b) = Beispiel(); s.SetFieldValue("Material", "", FieldSource.Legacy, false);
        s.SetFieldValue("Funktion", "", FieldSource.Legacy, false);
        var punkt = Quelle("Haltungspunkt", "chTEST00P0000001", new() { ["Bezeichnung"] = "A1" },
            new() { ["AbwassernetzelementRef"] = Knoten });
        punkt.Strukturen["Lage"] = "<Lage xmlns=\"http://www.interlis.ch/INTERLIS2.3\"><COORD><C1>2692700.000</C1><C2>1192100.000</C2></COORD></Lage>";
        b = b with { Bauteile = [b.Bauteile[0] with { Quellen = b.Bauteile[0].Quellen!.Append(punkt).ToArray() }] };
        foreach (var q in b.Bauteile[0].Quellen!)
        {
            q.Werte["Letzte_Aenderung"] = "20260101";
            q.Referenzen["DatenherrRef"] = XtfDssExportTests.Owner;
            q.Referenzen["DatenlieferantRef"] = XtfDssExportTests.Owner;
            if (q.Klasse == "Normschacht") q.Referenzen["EigentuemerRef"] = XtfDssExportTests.Owner;
        }
        var org = XtfDssExportTests.Projekt().Objektakten[0].Quellen.Single(q => q.Klasse == "Organisation");
        b = b with { Bauteile = [b.Bauteile[0] with { Quellen = b.Bauteile[0].Quellen!.Append(org).ToArray() }] };
        Anwenden(Plane(p, s, b), p, s);
        b.Bauteile[0].Quellen!.Single(q => q.Klasse == "Normschacht").Werte["Baujahr"] = "1980";
        b.Bauteile[0].Quellen![0].Strukturen["Lage"] =
            "<Lage xmlns=\"http://www.interlis.ch/INTERLIS2.3\"><COORD><C1>2692800.000</C1><C2>1192200.000</C2></COORD></Lage>";
        punkt.Strukturen["Lage"] = b.Bauteile[0].Quellen![0].Strukturen["Lage"];
        b = b with { Bauteile = [b.Bauteile[0] with { Felder = new Dictionary<string, string>(b.Bauteile[0].Felder) { ["Baujahr"] = "1980" } }] };
        Anwenden(Plane(p, s, b), p, s); // Abweichungen bleiben standardmässig unangetastet.
        Assert.Equal("1974", s.GetFieldValue("Baujahr"));
        XtfDssExportTests.WithExport(p, doc =>
        {
            Assert.Equal("1974", XtfDssExportTests.Wert(doc, "Normschacht", "Baujahr"));
            var knoten = XtfDssExportTests.Objekt(doc, "Abwasserknoten");
            Assert.Equal("2692748.532", knoten.Descendants().Single(e => e.Name.LocalName == "C1").Value);
            Assert.Equal("1192136.855", knoten.Descendants().Single(e => e.Name.LocalName == "C2").Value);
            var anschluss = XtfDssExportTests.Objekt(doc, "Haltungspunkt");
            Assert.Equal("2692700.000", anschluss.Descendants().Single(e => e.Name.LocalName == "C1").Value);
            Assert.Equal("1192100.000", anschluss.Descendants().Single(e => e.Name.LocalName == "C2").Value);
        });
    }

    private sealed class DefekteSicherung : IGeoShopSicherung
    { public string Sichere(Project projekt) => throw new IOException("Sicherung nicht möglich"); }
}
