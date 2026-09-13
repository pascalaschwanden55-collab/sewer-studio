using System.Xml.Linq;
using AuswertungPro.Next.Application.UseCases.Objektakten;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Application.UseCases;
using AuswertungPro.Next.Application.Xtf;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Lookup;

namespace AuswertungPro.Next.Infrastructure.Tests.Lookup;

public sealed class GeoShopAbgleichTests : IDisposable
{
    private readonly string _datei = Path.Combine(Path.GetTempPath(), $"geoshop-test-{Guid.NewGuid():N}.xtf");
    private const string H = "chTEST00H0000001", K = "chTEST00K0000001", P = "chTEST00P0000001";
    private const string V = "chTEST00V0000001", N = "chTEST00N0000001", A = "chTEST00A0000001", B = "chTEST00B0000001";
    private const string BA = "chTEST00C0000001", BB = "chTEST00D0000001";
    private static readonly XNamespace Ns = "http://www.interlis.ch/INTERLIS2.3";

    public void Dispose() => File.Delete(_datei);

    [Fact]
    public void Originalverbund_und_Leerfelder_werden_uebernommen_und_exportiert()
    {
        Schreibe();
        var original = File.ReadAllBytes(_datei);
        var h = Haltung();
        h.SetFieldValue(FieldKeys.PipeMaterial, "Steinzeug", FieldSource.Manual, true);
        h.SetFieldValue(FieldKeys.CadastreObjectId, "1234", FieldSource.Kataster, false);
        h.Geonis = new GeonisKennungen { Haltung = "chALT000H0000001", Kanal = "chALT000K0000001" };
        var ziel = GeoShopZiel.Fuer(h);
        var bestand = Lies(BauteilArt.Haltung, "A-B");
        var plan = GeoShopAbgleichPlanBuilder.Baue([ziel], bestand);
        Assert.Equal("1234", h.GetFieldValue(FieldKeys.CadastreObjectId));
        Assert.Contains("1234", GeoShopAbgleichBericht.Schreibe(plan));
        Assert.Equal(1, GeoShopAbgleichAnwender.WendeAn(plan, [ziel]));
        Assert.Equal(H, h.Geonis!.Haltung);
        Assert.Equal(K, h.Geonis.Kanal);
        Assert.Equal(V, h.Geonis.VonPunkt);
        Assert.Null(h.Geonis.GeonisGeaendert);
        Assert.Equal("Steinzeug", h.GetFieldValue(FieldKeys.PipeMaterial));
        Assert.True(h.FieldMeta[FieldKeys.PipeMaterial].UserEdited);
        Assert.Equal("300", h.GetFieldValue(FieldKeys.NominalDiameterMm));
        Assert.Equal(H, h.GetFieldValue(FieldKeys.CadastreObjectId));
        Assert.False(h.FieldMeta[FieldKeys.NominalDiameterMm].UserEdited);
        Assert.Equal(FieldSource.Kataster, h.FieldMeta[FieldKeys.NominalDiameterMm].Source);
        Assert.Empty(GeoShopAbgleichPlanBuilder.Baue([ziel], bestand).Positionen);
        var schacht = Schacht();
        var sz = GeoShopZiel.Fuer(schacht);
        GeoShopAbgleichAnwender.WendeAn(GeoShopAbgleichPlanBuilder.Baue([sz], Lies(BauteilArt.Schacht, "A")), [sz]);
        var export = XtfNeuPlanBuilder.Build([h], [schacht]);
        Assert.Contains(export.Objekte, o => o.Klasse == "Haltung" && o.Tid == H);
        Assert.Contains(export.Objekte, o => o.Klasse == "Kanal" && o.Tid == K);
        Assert.Contains(export.Objekte, o => o.Klasse == "Abwasserknoten" && o.Tid == A);
        Assert.Contains(export.Objekte, o => o.Klasse == "Normschacht" && o.Tid == BA);
        Assert.Equal(original, File.ReadAllBytes(_datei));
    }

    [Fact]
    public void Gegenrichtung_tauscht_Punkte_und_Schachtfelder()
    {
        Schreibe();
        var h = Haltung("B-A"); var ziel = GeoShopZiel.Fuer(h);
        var plan = GeoShopAbgleichPlanBuilder.Baue([ziel], Lies(BauteilArt.Haltung, "B-A"));
        Assert.True(Assert.Single(plan.Positionen).Gedreht);
        GeoShopAbgleichAnwender.WendeAn(plan, [ziel]);
        Assert.Equal(N, h.Geonis!.VonPunkt); Assert.Equal(V, h.Geonis.NachPunkt);
        Assert.Equal("B", h.GetFieldValue("Schacht_oben"));
        Assert.Equal("A", h.GetFieldValue("Schacht_unten"));
    }

    [Fact]
    public void Schacht_Umlautfeld_und_expliziter_Zustand_bleiben_erhalten()
    {
        Schreibe(); var s = Schacht();
        s.SetFieldValue("Eigentümer", "Mein Eigentümer", FieldSource.Manual, true);
        s.SetFieldValue(FieldKeys.ConditionClass, "4", FieldSource.Manual, true);
        var z = GeoShopZiel.Fuer(s);
        var plan = GeoShopAbgleichPlanBuilder.Baue([z], Lies(BauteilArt.Schacht, "A"));
        GeoShopAbgleichAnwender.WendeAn(plan, [z]);
        Assert.Equal("Mein Eigentümer", s.GetFieldValue("Eigentümer"));
        Assert.Equal("4", s.GetFieldValue(FieldKeys.ConditionClass));
        Assert.Equal("800", s.GetFieldValue(FieldKeys.ShaftDimension1Mm));
        Assert.Equal(A, s.Geonis!.Knoten); Assert.Equal(BA, s.Geonis.Bauwerk);
    }

    [Theory]
    [InlineData("Name")]
    [InlineData("TID")]
    [InlineData("Referenz")]
    [InlineData("Klasse")]
    public void Unklare_oder_defekte_Quelle_wird_ausgelassen(string defekt)
    {
        Schreibe(doc =>
        {
            var h = doc.Descendants().Single(e => (string?)e.Attribute("TID") == H);
            if (defekt is "Name" or "TID")
            {
                var kopie = new XElement(h);
                if (defekt == "Name") kopie.SetAttributeValue("TID", "chTEST00H0000002");
                h.AddAfterSelf(kopie);
            }
            if (defekt == "Referenz") h.Element(Ns + "RohrprofilRef")!.SetAttributeValue("REF", "chTEST00F0000001");
            if (defekt == "Klasse") doc.Descendants().Single(e => (string?)e.Attribute("TID") == K).Name = Ns + "DSS_2020_1_LV95.Siedlungsentwaesserung.Normschacht";
        });
        var z = GeoShopZiel.Fuer(Haltung());
        var plan = GeoShopAbgleichPlanBuilder.Baue([z], Lies(BauteilArt.Haltung, "A-B"));
        Assert.Empty(plan.Positionen); Assert.NotEmpty(plan.Hinweise);
    }

    [Fact]
    public void Widersprechende_Endpunkte_und_doppelte_Projektzeilen_werden_nicht_geraten()
    {
        Schreibe(); var h = Haltung(); h.SetFieldValue("Schacht_oben", "Fremd", FieldSource.Manual, true);
        var bestand = Lies(BauteilArt.Haltung, "A-B");
        Assert.Empty(GeoShopAbgleichPlanBuilder.Baue([GeoShopZiel.Fuer(h)], bestand).Positionen);
        Assert.Empty(GeoShopAbgleichPlanBuilder.Baue([GeoShopZiel.Fuer(Haltung()), GeoShopZiel.Fuer(Haltung())], bestand).Positionen);
    }

    [Fact]
    public void Projektveraenderung_oder_Entfernen_nach_Vorschau_schreibt_nichts()
    {
        Schreibe(); var h = Haltung(); var z = GeoShopZiel.Fuer(h);
        var plan = GeoShopAbgleichPlanBuilder.Baue([z], Lies(BauteilArt.Haltung, "A-B"));
        Assert.Throws<InvalidOperationException>(() => GeoShopAbgleichAnwender.WendeAn(plan, []));
        h.SetFieldValue(FieldKeys.NominalDiameterMm, "500", FieldSource.Manual, true);
        Assert.Throws<InvalidOperationException>(() => GeoShopAbgleichAnwender.WendeAn(plan, [z]));
        Assert.Null(h.Geonis); Assert.Equal("500", h.GetFieldValue(FieldKeys.NominalDiameterMm));
    }

    [Fact]
    public void Handgeschuetzte_Kennung_wird_nicht_still_ersetzt()
    {
        Schreibe(); var h = Haltung();
        h.SetFieldValue(FieldKeys.CadastreObjectId, "Handkennung", FieldSource.Manual, true);
        var plan = GeoShopAbgleichPlanBuilder.Baue([GeoShopZiel.Fuer(h)], Lies(BauteilArt.Haltung, "A-B"));
        Assert.Empty(plan.Positionen); Assert.Contains(plan.Hinweise, h => h.Contains("geschützt"));
    }

    [Fact]
    public void Fehlende_Organisation_und_unbekannter_Zustand_werden_nicht_erfunden()
    {
        Schreibe(doc =>
        {
            doc.Descendants().Single(e => (string?)e.Attribute("TID") == "chTEST00O0000001").Remove();
            doc.Descendants().Single(e => (string?)e.Attribute("TID") == BA)
                .Element(Ns + "BaulicherZustand")!.Value = "unbekannt";
        });
        var s = Schacht(); var z = GeoShopZiel.Fuer(s);
        var plan = GeoShopAbgleichPlanBuilder.Baue([z], Lies(BauteilArt.Schacht, "A"));
        Assert.Contains(plan.Hinweise, h => h.Contains("Organisationsobjekt"));
        GeoShopAbgleichAnwender.WendeAn(plan, [z]);
        Assert.Equal("", s.GetFieldValue(FieldKeys.Owner));
        Assert.Equal("", s.GetFieldValue(FieldKeys.ConditionClass));
        Assert.Equal(BA, s.Geonis!.Bauwerk);
    }

    [Fact]
    public void Neue_Projektzeile_nach_Vorschau_erfordert_neuen_Abgleich()
    {
        Schreibe(); var h = Haltung(); var z = GeoShopZiel.Fuer(h);
        var plan = GeoShopAbgleichPlanBuilder.Baue([z], Lies(BauteilArt.Haltung, "A-B"));
        Assert.Throws<InvalidOperationException>(() => GeoShopAbgleichAnwender.WendeAn(plan, [z, GeoShopZiel.Fuer(Haltung())]));
        Assert.Null(h.Geonis);
    }

    [Fact]
    public void Handkorrigierte_Hoehe_wird_nicht_mit_alter_Breite_vermischt()
    {
        Schreibe(); var h = Haltung(); var z = GeoShopZiel.Fuer(h);
        h.SetFieldValue(FieldKeys.NominalDiameterMm, "100", FieldSource.Manual, true);
        var plan = GeoShopAbgleichPlanBuilder.Baue([z], Lies(BauteilArt.Haltung, "A-B"));
        GeoShopAbgleichAnwender.WendeAn(plan, [z]);
        Assert.Equal("100", h.GetFieldValue(FieldKeys.NominalDiameterMm));
        Assert.Equal("", h.GetFieldValue(FieldKeys.ClearWidthMm));
        Assert.Contains(plan.Hinweise, h => h.Contains("Breite bleibt leer"));
    }

    [Theory]
    [InlineData("Spezialbauwerk")]
    [InlineData("Versickerungsanlage")]
    [InlineData("Einleitstelle")]
    public void Bauwerksklasse_wird_erhalten_und_Widerspruch_gesperrt(string klasse)
    {
        Schreibe(doc => doc.Descendants().Single(e => (string?)e.Attribute("TID") == BA)
            .Name = Ns + "DSS_2020_1_LV95.Siedlungsentwaesserung." + klasse);
        var s = Schacht(); var z = GeoShopZiel.Fuer(s); var bestand = Lies(BauteilArt.Schacht, "A");
        GeoShopAbgleichAnwender.WendeAn(GeoShopAbgleichPlanBuilder.Baue([z], bestand), [z]);
        Assert.Equal(klasse, s.GetFieldValue(FieldKeys.ShaftStructureType));
        s.SetFieldValue(FieldKeys.ShaftStructureType, "Normschacht", FieldSource.Manual, true);
        Assert.Empty(GeoShopAbgleichPlanBuilder.Baue([z], bestand).Positionen);
    }

    [Fact]
    public void Doppelte_TID_in_anderer_Klasse_wird_erkannt()
    {
        Schreibe(doc => doc.Descendants().Single(e => (string?)e.Attribute("TID") == BB).SetAttributeValue("TID", H));
        var plan = GeoShopAbgleichPlanBuilder.Baue([GeoShopZiel.Fuer(Haltung())], Lies(BauteilArt.Haltung, "A-B"));
        Assert.Empty(plan.Positionen);
        Assert.Contains(plan.Hinweise, h => h.Contains("doppelte TID"));
    }

    [Fact]
    public void Abbruch_und_DTD_werden_abgewiesen()
    {
        Schreibe();
        Assert.Throws<OperationCanceledException>(() => new GeoShopXtfLeser().Lies(_datei,
            BauteilArt.Haltung, ["A-B"], new CancellationToken(true)));
        File.WriteAllText(_datei, "<!DOCTYPE TRANSFER [<!ENTITY x SYSTEM 'file:///nichtlesen'>]><TRANSFER>&x;</TRANSFER>");
        Assert.Throws<System.Xml.XmlException>(() => Lies(BauteilArt.Haltung, "A-B"));
    }

    [Fact]
    public void Objektverbund_liest_zwei_Deckel_und_Assoziation_ohne_Tid_ohne_Duplikate()
    {
        Schreibe(doc =>
        {
            var basket = doc.Descendants().Single(e => e.Attribute("BID") is not null);
            const string prefix = "DSS_2020_1_LV95.Siedlungsentwaesserung.";
            foreach (var (tid, hoehe) in new[] { ("chTEST00X0000001", "450.94"), ("chTEST00X0000002", "451.10") })
                basket.Add(new XElement(Ns + prefix + "Deckel", new XAttribute("TID", tid),
                    new XElement(Ns + "AbwasserbauwerkRef", new XAttribute("REF", BA)),
                    new XElement(Ns + "Kote", hoehe), new XElement(Ns + "Deckelform", "rund")));
            basket.Add(new XElement(Ns + prefix + "Erhaltungsereignis_AbwasserbauwerkAssoc",
                new XElement(Ns + "AbwasserbauwerkRef", new XAttribute("REF", BA)),
                new XElement(Ns + "Erhaltungsereignis_AbwasserbauwerkAssocRef", new XAttribute("REF", "chTEST00E0000001"))));
            basket.Add(new XElement(Ns + prefix + "Unterhalt", new XAttribute("TID", "chTEST00E0000001"),
                new XElement(Ns + "Art", "Sanierung_Renovierung"), new XElement(Ns + "Bezeichnung", "Renovierung 2018")));
            doc.Descendants().Single(e => (string?)e.Attribute("TID") == A).Add(new XElement(Ns + "Sohlenkote", "448.34"));
            doc.Descendants().Single(e => (string?)e.Attribute("TID") == V).Add(new XElement(Ns + "Kote", "448.36"));
        });
        var p = new Project(); var s = Schacht(); p.SchaechteData.Add(s);
        var z = GeoShopZiel.Fuer(s, p); var bestand = Lies(BauteilArt.Schacht, "A");
        var plan = GeoShopAbgleichPlanBuilder.Baue([z], bestand);
        Assert.Equal(1, GeoShopAbgleichAnwender.WendeAn(plan, [z]));
        Assert.Equal(2, p.Objektakten.Count(a => a.Art == "deckel"));
        var ereignis = Assert.Single(p.Objektakten.Where(a => a.Art == "sanierung"));
        Assert.Equal("Renovierung 2018", ereignis.Werte["sanierung.s_name"].Text);
        var root = p.Objektakten.Single(a => a.Id == s.Id);
        Assert.Contains(root.Quellen, q => q.IstLokaleKennung && q.Klasse.EndsWith("Assoc"));
        Assert.Contains(root.Quellen, q => q.Klasse == "Haltungspunkt" && q.Werte.GetValueOrDefault("Kote") == "448.36");
        Assert.Equal("448.34", root.Werte["schacht.sohlenhoehe"].Text);
        var b = new ObjektaktenBearbeitung(p, s.Id, "schacht");
        b.SetzeHauptdeckel(p.Objektakten.Single(a => a.Art == "deckel" && a.Werte["deckel.hoehe"].Text == "450.94"));
        Assert.Equal("450.94", b.Lies(b.Wurzel, FieldCatalog.Objektfelder.Feld("schacht.deckelhoehe")));
        Assert.Empty(GeoShopAbgleichPlanBuilder.Baue([z], bestand).Positionen);
        Assert.Equal(2, p.Objektakten.Count(a => a.Art == "haltungspunkt"));
        Assert.Equal(6, p.Objektakten.Count);
    }

    [Fact]
    public void Quellstand_Aenderung_in_Akte_macht_Vorschau_ungueltig()
    {
        Schreibe(); var p = new Project(); var h = Haltung(); p.Data.Add(h);
        var z = GeoShopZiel.Fuer(h, p); var plan = GeoShopAbgleichPlanBuilder.Baue([z], Lies(BauteilArt.Haltung, "A-B"));
        var b = new ObjektaktenBearbeitung(p, h.Id, "haltung");
        b.Schreibe(b.Wurzel, FieldCatalog.Objektfelder.Feld("haltung.haltungsbemerkung"), "", "Nach Vorschau geändert");
        Assert.Throws<InvalidOperationException>(() => GeoShopAbgleichAnwender.WendeAn(plan, [z]));
        Assert.Null(h.Geonis);
    }

    [Fact]
    public void Haltungslaenge_kommt_immer_aus_der_XTF_auch_wenn_von_Hand_gesetzt()
    {
        Schreibe();
        var h = Haltung();
        h.SetFieldValue(FieldKeys.HoldingLengthMeters, "11.9", FieldSource.Manual, true);
        h.SetFieldValue(FieldKeys.PipeMaterial, "Steinzeug", FieldSource.Manual, true);
        var ziel = GeoShopZiel.Fuer(h);
        var plan = GeoShopAbgleichPlanBuilder.Baue([ziel], Lies(BauteilArt.Haltung, "A-B"));
        var laenge = Assert.Single(plan.Positionen).Felder.Single(f => f.Feld == FieldKeys.HoldingLengthMeters);
        Assert.True(laenge.Ersetzen); Assert.Equal("11.9", laenge.Vorher); Assert.Equal("12.5", laenge.Nachher);
        Assert.Contains("1 Haltungslängen aus der XTF ersetzen", GeoShopAbgleichBericht.Schreibe(plan));
        Assert.Contains("11.9 → 12.5 (ersetzt", GeoShopAbgleichBericht.Schreibe(plan));
        GeoShopAbgleichAnwender.WendeAn(plan, [ziel]);
        Assert.Equal("12.5", h.GetFieldValue(FieldKeys.HoldingLengthMeters));
        Assert.False(h.FieldMeta[FieldKeys.HoldingLengthMeters].UserEdited);
        Assert.Equal(FieldSource.Kataster, h.FieldMeta[FieldKeys.HoldingLengthMeters].Source);
        Assert.Equal("Steinzeug", h.GetFieldValue(FieldKeys.PipeMaterial)); // alle anderen Felder: nur wenn leer

        // Derselbe Wert in anderer Schreibweise ist keine Aenderung.
        h.SetFieldValue(FieldKeys.HoldingLengthMeters, "12.50", FieldSource.Manual, true);
        var erneut = GeoShopAbgleichPlanBuilder.Baue([GeoShopZiel.Fuer(h)], Lies(BauteilArt.Haltung, "A-B"));
        Assert.DoesNotContain(erneut.Positionen.SelectMany(p => p.Felder), f => f.Ersetzen);
        Assert.Equal("12.50", h.GetFieldValue(FieldKeys.HoldingLengthMeters));
    }

    [Fact]
    public void Einzelergaenzung_liest_nur_dieses_Bauteil_und_schreibt_erst_beim_Anwenden()
    {
        Schreibe(); var p = new Project(); var h = Haltung(); p.Data.Add(h);
        h.SetFieldValue(FieldKeys.HoldingLengthMeters, "11.9", FieldSource.Manual, true);
        var leser = new ZaehlenderLeser(_datei);
        var ergebnis = GeoShopEinzelErgaenzung.Plane(GeoShopZiel.Fuer(h, p), leser, _datei);
        Assert.Equal(["A-B"], leser.Angefragt);
        Assert.True(ergebnis.HatAenderungen);
        Assert.Contains("Haltung «A-B»", ergebnis.Text);
        Assert.Contains("(leer) → 300", ergebnis.Text);
        Assert.Contains("11.9 → 12.5  (kommt immer aus der XTF)", ergebnis.Text);
        Assert.Contains("Kennungen übernehmen", ergebnis.Text);
        Assert.True(string.IsNullOrEmpty(h.GetFieldValue(FieldKeys.NominalDiameterMm))); // Planen schreibt nichts
        Assert.Equal(1, GeoShopEinzelErgaenzung.WendeAn(ergebnis, GeoShopZiel.Fuer(h, p)));
        Assert.Equal("300", h.GetFieldValue(FieldKeys.NominalDiameterMm));
        Assert.Equal("12.5", h.GetFieldValue(FieldKeys.HoldingLengthMeters));
        Assert.Equal(H, h.Geonis!.Haltung);
        Assert.NotEmpty(p.Objektakten);

        var fremd = Haltung("X-Y"); p.Data.Add(fremd);
        var nichts = GeoShopEinzelErgaenzung.Plane(GeoShopZiel.Fuer(fremd, p), leser, _datei);
        Assert.False(nichts.HatAenderungen);
        Assert.Contains("nichts zu übernehmen", nichts.Text);
        Assert.Contains("Nicht in der XTF gefunden", nichts.Text);

        // Nach dem Planen geaendert: der Anwender schreibt nicht.
        p.Data.Remove(h);
        var h2 = Haltung(); p.Data.Add(h2);
        var plan2 = GeoShopEinzelErgaenzung.Plane(GeoShopZiel.Fuer(h2, p), leser, _datei);
        h2.SetFieldValue(FieldKeys.PipeMaterial, "Steinzeug", FieldSource.Manual, true);
        Assert.Throws<InvalidOperationException>(() => GeoShopEinzelErgaenzung.WendeAn(plan2, GeoShopZiel.Fuer(h2, p)));
        Assert.Null(h2.Geonis);
    }

    private sealed class ZaehlenderLeser(string datei) : IGeoShopLeser
    {
        public List<string> Angefragt { get; } = new();
        public GeoShopBestand Lies(string d, BauteilArt art, IReadOnlyCollection<string> namen, CancellationToken ct = default)
        { Angefragt.AddRange(namen); return new GeoShopXtfLeser().Lies(datei, art, namen, ct); }
    }

    private GeoShopBestand Lies(BauteilArt art, string name) => new GeoShopXtfLeser().Lies(_datei, art, [name]);
    [Theory]
    [InlineData("SIA405_Base_Abwasser_1_LV95")]
    [InlineData("SIA405_Base_Abwasser_LV95")]
    public void Organisation_im_Basismodell_wird_ueber_ihre_Originalkennung_gelesen(string modell)
    {
        Schreibe(doc => doc.Descendants().Single(e => (string?)e.Attribute("TID") == "chTEST00O0000001")
            .Name = Ns + modell + ".Administration.Organisation");
        var bestand = Lies(BauteilArt.Schacht, "A");
        Assert.Equal("Privat", Assert.Single(bestand.Bauteile).Felder[FieldKeys.Owner]);
        Assert.Contains(bestand.Bauteile[0].Quellen!, q => q.Klasse == "Organisation" && q.Modell == modell);
    }

    private static HaltungRecord Haltung(string name = "A-B")
    {
        var h = new HaltungRecord(); h.SetFieldValue(FieldKeys.HoldingName, name, FieldSource.Manual, true); return h;
    }
    private static SchachtRecord Schacht()
    {
        var s = new SchachtRecord(); s.SetFieldValue("Schachtnummer", "A", FieldSource.Manual, true); return s;
    }
    private void Schreibe(Action<XDocument>? aendere = null)
    {
        XElement Objekt(string klasse, string tid, params (string, string)[] felder) =>
            new(Ns + "DSS_2020_1_LV95.Siedlungsentwaesserung." + klasse, new XAttribute("TID", tid),
                felder.Select(f => f.Item1.EndsWith("Ref", StringComparison.Ordinal)
                    ? new XElement(Ns + f.Item1, new XAttribute("REF", f.Item2)) : new XElement(Ns + f.Item1, f.Item2)));
        var doc = new XDocument(new XElement(Ns + "TRANSFER", new XElement(Ns + "HEADERSECTION"),
            new XElement(Ns + "DATASECTION", new XElement(Ns + "DSS_2020_1_LV95.Siedlungsentwaesserung", new XAttribute("BID", "1"),
                Objekt("Haltung", H, ("Bezeichnung", "A-B"), ("AbwasserbauwerkRef", K), ("RohrprofilRef", P),
                    ("vonHaltungspunktRef", V), ("nachHaltungspunktRef", N), ("Lichte_Hoehe", "300"),
                    ("Material", "Beton"), ("LaengeEffektiv", "12.5"), ("Letzte_Aenderung", "20260829")),
                Objekt("Kanal", K, ("Nutzungsart_Ist", "Mischabwasser"), ("BaulicherZustand", "Z2"), ("EigentuemerRef", "chTEST00O0000001")),
                Objekt("Rohrprofil", P, ("Profiltyp", "Kreisprofil"), ("HoehenBreitenverhaeltnis", "1")),
                Objekt("Haltungspunkt", V, ("Bezeichnung", "A1"), ("AbwassernetzelementRef", A)),
                Objekt("Haltungspunkt", N, ("Bezeichnung", "E1"), ("AbwassernetzelementRef", B)),
                Objekt("Abwasserknoten", A, ("Bezeichnung", "A"), ("AbwasserbauwerkRef", BA)),
                Objekt("Abwasserknoten", B, ("Bezeichnung", "B"), ("AbwasserbauwerkRef", BB)),
                Objekt("Normschacht", BA, ("Bezeichnung", "A"), ("Dimension1", "800"), ("Dimension2", "800"),
                    ("Material", "Beton"), ("BaulicherZustand", "Z1"), ("EigentuemerRef", "chTEST00O0000001")),
                Objekt("Organisation", "chTEST00O0000001", ("Bezeichnung", "Privat")),
                Objekt("Normschacht", BB, ("Bezeichnung", "B"))))));
        aendere?.Invoke(doc); doc.Save(_datei);
    }
}
