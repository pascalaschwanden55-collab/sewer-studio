using System.Xml.Linq;
using AuswertungPro.Next.Application.Xtf.Lieferung;
using AuswertungPro.Next.Infrastructure.Import.Xtf.Lieferung;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class XtfLieferungsAblageTests : IDisposable
{
    private readonly string _ordner = Path.Combine(Path.GetTempPath(), "SewerStudio-Lieferung-" + Guid.NewGuid().ToString("N"));
    private readonly IXtfLieferungsAblage _ablage = new XtfLieferungsAblage();
    private string Datei(string name) => Path.Combine(_ordner, name);
    private const string Modell = "DSS_2020_1_LV95.Siedlungsentwaesserung.";
    private const string Org = "chTEST0000000005", Knoten = "chTEST0000000010";
    private static readonly XNamespace Ns = "http://www.interlis.ch/INTERLIS2.3";

    [Fact]
    public void Freier_Import_erhaelt_auch_Knoten_ohne_Bauwerk_und_alle_Beschriftungen()
    {
        Quelle(); var original = File.ReadAllBytes(Datei("original.xtf"));
        var info = _ablage.Importiere(Datei("original.xtf"), Datei("arbeit.ssxtf"));
        Assert.Equal(5, info.Anzahl); Assert.Equal(0, info.Geaendert);
        Assert.Equal(5, _ablage.Oeffne(info.Datei).Anzahl);
        var knot = _ablage.Suche(info.Datei, "Abwasserknoten", "Knoten ohne Bauwerk").Zeilen.Single();
        Assert.Equal(Knoten, knot.Kennung);
        Assert.Equal(original, File.ReadAllBytes(Datei("original.xtf")));
        Assert.Equal(new[] { "Top", "Cap", "Half", "Base", "Bottom" },
            Text(info.Datei).Felder.Single(f => f.Schluessel == "TextVAli").Optionen.Where(x => x.Length > 0));
    }

    [Fact]
    public void Bearbeiten_Speichern_Wiederoeffnen_und_neue_XTF_erhalten_Original_TIDs_und_Referenzen()
    {
        Quelle(); var original = File.ReadAllBytes(Datei("original.xtf"));
        var info = _ablage.Importiere(Datei("original.xtf"), Datei("arbeit.ssxtf"));
        var a = Text(info.Datei);
        _ablage.Speichere(info.Datei, a.Id, a.Version, new Dictionary<string, string>
            { ["Textinhalt"] = "Neu beschriftet", ["TextHAli"] = "Right", ["TextPos.C1"] = "2690001,25" });
        a = Text(info.Datei);
        Assert.Equal("Neu beschriftet", a.Felder.Single(f => f.Schluessel == "Textinhalt").Wert);
        Assert.Equal("2690001.250", a.Felder.Single(f => f.Schluessel == "TextPos.C1").Wert);
        Assert.Equal(1, _ablage.Oeffne(info.Datei).Geaendert);
        var pruefung = _ablage.Exportiere(info.Datei, Datei("neu.xtf"));
        Assert.Equal(0, pruefung.FehlerhafteObjekte);
        var doc = XDocument.Load(Datei("neu.xtf"));
        var text = doc.Descendants().Single(e => e.Name.LocalName.EndsWith(".Haltung_Text"));
        Assert.Equal("chTEST0000000040", text.Attribute("TID")!.Value);
        Assert.Equal("chTEST0000000001", text.Element(Ns + "HaltungRef")!.Attribute("REF")!.Value);
        Assert.Equal("Neu beschriftet", text.Element(Ns + "Textinhalt")!.Value);
        Assert.Equal(original, File.ReadAllBytes(Datei("original.xtf")));
    }

    [Fact]
    public void Doppelte_Originalkennungen_bleiben_getrennt_sichtbar_und_sperren_den_Export()
    {
        Quelle(); var doc = XDocument.Load(Datei("original.xtf"));
        var knoten = doc.Descendants().Single(e => e.Name.LocalName.EndsWith(".Abwasserknoten"));
        var doppelt = new XElement(knoten); doppelt.Element(Ns + "Bezeichnung")!.Value = "Zweiter Knoten"; knoten.Parent!.Add(doppelt); doc.Save(Datei("original.xtf"));
        var info = _ablage.Importiere(Datei("original.xtf"), Datei("arbeit.ssxtf"));
        Assert.Equal(2, _ablage.Suche(info.Datei, "Abwasserknoten", Knoten).Gesamt);
        var r = _ablage.Exportiere(info.Datei, Datei("gesperrt.xtf"));
        Assert.Equal(2, r.FehlerhafteObjekte); Assert.Contains("doppelt", r.Bericht, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Datei("gesperrt.xtf")));
        Assert.Equal(2, _ablage.Suche(info.Datei, null, "", nurProbleme: true).Gesamt);
    }

    [Fact]
    public void Veraltete_Ansicht_ungueltige_Werte_und_Fremdkennungen_werden_nicht_geschrieben()
    {
        Quelle(); var info = _ablage.Importiere(Datei("original.xtf"), Datei("arbeit.ssxtf")); var a = Text(info.Datei);
        _ablage.Speichere(info.Datei, a.Id, a.Version, new Dictionary<string, string> { ["Textinhalt"] = "Aktuell" });
        Assert.Throws<InvalidOperationException>(() => _ablage.Speichere(info.Datei, a.Id, a.Version, new Dictionary<string, string> { ["Textinhalt"] = "Veraltet" }));
        a = Text(info.Datei);
        foreach (var (feld, wert) in new[] { ("Textinhalt", ""), ("TextHAli", "999"), ("TextPos.C1", "123"), ("HaltungRef", Knoten), ("TID", "chTEST0000000099") })
            Assert.Throws<InvalidOperationException>(() => _ablage.Speichere(info.Datei, a.Id, a.Version, new Dictionary<string, string> { [feld] = wert }));
        Assert.Equal("Aktuell", Text(info.Datei).Felder.Single(f => f.Schluessel == "Textinhalt").Wert);
    }

    [Fact]
    public void Abbruch_und_vorhandene_Ziele_lassen_keine_halbe_Arbeitsdatei_zurueck()
    {
        Quelle(); var original = File.ReadAllBytes(Datei("original.xtf"));
        Assert.Throws<OperationCanceledException>(() => _ablage.Importiere(Datei("original.xtf"), Datei("arbeit.ssxtf"), token: new CancellationToken(true)));
        Assert.False(File.Exists(Datei("arbeit.ssxtf")));
        Assert.Throws<IOException>(() => _ablage.Importiere(Datei("original.xtf"), Datei("original.xtf")));
        Assert.Equal(original, File.ReadAllBytes(Datei("original.xtf")));
        File.WriteAllText(Datei("fremd.ssxtf"), "Keine SewerStudio-Lieferung");
        Assert.ThrowsAny<Exception>(() => _ablage.Oeffne(Datei("fremd.ssxtf")));
    }

    private XtfLieferungsObjekt Text(string datei) => _ablage.Lies(datei, _ablage.Suche(datei, "Haltung_Text", "").Zeilen.Single().Id);
    [Fact]
    public void Mehrere_Koerbe_Beziehungen_ohne_TID_und_originaler_Linienverlauf_bleiben_erhalten()
    {
        Quelle(); var doc = XDocument.Load(Datei("original.xtf")); var daten = doc.Root!.Element(Ns + "DATASECTION")!;
        var fach = daten.Elements().Single();
        var verlauf = new XElement(Ns + "Verlauf", new XElement(Ns + "POLYLINE",
            new XElement(Ns + "COORD", new XElement(Ns + "C1", "2690000.000"), new XElement(Ns + "C2", "1190000.000")),
            new XElement(Ns + "COORD", new XElement(Ns + "C1", "2690020.000"), new XElement(Ns + "C2", "1190030.000"))));
        fach.Element(Ns + Modell + "Haltung")!.Add(verlauf);
        fach.Add(Objekt("Kanal", "chTEST0000000002", new() { ["Bezeichnung"] = "Bauwerk", ["Letzte_Aenderung"] = "20260101" },
            new() { ["DatenherrRef"] = Org, ["DatenlieferantRef"] = Org, ["EigentuemerRef"] = Org }));
        fach.Add(Objekt("Unterhalt", "chTEST0000000020", new() { ["Bezeichnung"] = "Unterhalt 1", ["Letzte_Aenderung"] = "20260101" },
            new() { ["DatenherrRef"] = Org, ["DatenlieferantRef"] = Org }));
        var assoc = new XElement(Ns + Modell + "Erhaltungsereignis_AbwasserbauwerkAssoc",
            new XElement(Ns + "AbwasserbauwerkRef", new XAttribute("REF", "chTEST0000000002")),
            new XElement(Ns + "Erhaltungsereignis_AbwasserbauwerkAssocRef", new XAttribute("REF", "chTEST0000000020")));
        fach.Add(assoc);
        daten.Add(new XElement(Ns + "SIA405_Base_Abwasser_1_LV95.Administration", new XAttribute("BID", "chB0000000000002"),
            new XElement(Ns + "SIA405_Base_Abwasser_1_LV95.Administration.Organisation", new XAttribute("TID", Org), new XElement(Ns + "Letzte_Aenderung", "20260101"),
                new XElement(Ns + "Bezeichnung", "Testorganisation"), new XElement(Ns + "Organisationstyp", "Privat"), new XElement(Ns + "Status", "aktiv"))));
        doc.Save(Datei("original.xtf"));
        var info = _ablage.Importiere(Datei("original.xtf"), Datei("arbeit.ssxtf")); Assert.Equal(9, info.Anzahl);
        var holding = _ablage.Suche(info.Datei, "Haltung", "").Zeilen.Single(); var o = _ablage.Lies(info.Datei, holding.Id);
        Assert.False(o.Felder.Single(f => f.Schluessel == "Verlauf").Bearbeitbar);
        _ablage.Speichere(info.Datei, o.Id, o.Version, new Dictionary<string, string> { ["Bemerkung"] = "Prüfung" });
        var r = _ablage.Exportiere(info.Datei, Datei("vollstaendig.xtf")); Assert.True(r.FehlerhafteObjekte == 0, r.Bericht); Assert.Equal(0, r.ExterneOrganisationen);
        var neu = XDocument.Load(Datei("vollstaendig.xtf")); Assert.Equal(2, neu.Root!.Element(Ns + "DATASECTION")!.Elements().Count());
        Assert.True(XNode.DeepEquals(verlauf, neu.Descendants(Ns + "Verlauf").Single()));
        var neueBeziehung = neu.Descendants(Ns + Modell + "Erhaltungsereignis_AbwasserbauwerkAssoc").Single();
        Assert.Null(neueBeziehung.Attribute("TID"));
        Assert.Equal(assoc.Elements().Select(e => (e.Name, e.Attribute("REF")!.Value)),
            neueBeziehung.Elements().Select(e => (e.Name, e.Attribute("REF")!.Value)));
    }

    [Fact]
    public void Messstelle_und_Beschriftung_bieten_alle_originalen_Normoptionen()
    {
        Quelle(); var doc = XDocument.Load(Datei("original.xtf"));
        doc.Root!.Element(Ns + "DATASECTION")!.Elements().Single().Add(Objekt("Messstelle", "chTEST0000000030", new(), new())); doc.Save(Datei("original.xtf"));
        var info = _ablage.Importiere(Datei("original.xtf"), Datei("arbeit.ssxtf")); var text = Text(info.Datei);
        Assert.Equal(new[] { "Left", "Center", "Right" }, text.Felder.Single(f => f.Schluessel == "TextHAli").Optionen);
        Assert.Equal(new[] { "Leitungskataster", "Werkplan", "Uebersichtsplan.UeP10", "Uebersichtsplan.UeP2", "Uebersichtsplan.UeP5" }, text.Felder.Single(f => f.Schluessel == "Plantyp").Optionen);
        var o = _ablage.Lies(info.Datei, _ablage.Suche(info.Datei, "Messstelle", "").Zeilen.Single().Id);
        Assert.Equal(new[] { "", "beides", "Kostenverteilung", "technischer_Zweck", "unbekannt" }, o.Felder.Single(f => f.Schluessel == "Zweck").Optionen);
        Assert.Equal(new[] { "", "andere", "keiner", "Ueberfallwehr", "unbekannt", "Venturieinschnuerung" }, o.Felder.Single(f => f.Schluessel == "Staukoerper").Optionen);
    }

    [Fact]
    public void Zusatzfelder_und_ungueltige_Altwerte_bleiben_sichtbar_und_sperren_die_Ausgabe()
    {
        Quelle(); var doc = XDocument.Load(Datei("original.xtf")); var text = doc.Descendants(Ns + Modell + "Haltung_Text").Single();
        text.SetElementValue(Ns + "TextVAli", "Altwert"); text.Add(new XElement(Ns + "Zusatzfeld", "Nicht verlieren")); doc.Save(Datei("original.xtf"));
        var info = _ablage.Importiere(Datei("original.xtf"), Datei("arbeit.ssxtf")); var o = Text(info.Datei);
        Assert.Equal("Altwert", o.Felder.Single(f => f.Schluessel == "TextVAli").Wert);
        Assert.False(o.Felder.Single(f => f.Schluessel == "Zusatzfeld").Bearbeitbar);
        Assert.Equal("Nicht verlieren", o.Felder.Single(f => f.Schluessel == "Zusatzfeld").Wert);
        Assert.Equal(1, _ablage.Exportiere(info.Datei, Datei("verboten.xtf")).FehlerhafteObjekte); Assert.False(File.Exists(Datei("verboten.xtf")));
    }

    [Fact]
    public void Weitere_Punktangaben_werden_beim_Bearbeiten_nicht_still_entfernt()
    {
        Quelle(); var doc = XDocument.Load(Datei("original.xtf")); doc.Descendants(Ns + "COORD").Single().Add(new XElement(Ns + "C3", "450.000")); doc.Save(Datei("original.xtf"));
        var info = _ablage.Importiere(Datei("original.xtf"), Datei("arbeit.ssxtf")); var o = Text(info.Datei);
        Assert.Throws<InvalidOperationException>(() => _ablage.Speichere(info.Datei, o.Id, o.Version, new Dictionary<string, string> { ["TextPos.C1"] = "2690001.000" }));
        Assert.Equal(0, _ablage.Oeffne(info.Datei).Geaendert);
    }

    [Fact]
    public void Bericht_veraltet_nach_Aenderungen_und_vorhandene_Ausgaben_werden_nicht_ueberschrieben()
    {
        Quelle(); var info = _ablage.Importiere(Datei("original.xtf"), Datei("arbeit.ssxtf"));
        _ablage.Pruefe(info.Datei); _ablage.SichereBericht(info.Datei, Datei("bericht.txt"));
        Assert.Contains("5", File.ReadAllText(Datei("bericht.txt")));
        Assert.Throws<IOException>(() => _ablage.SichereBericht(info.Datei, Datei("bericht.txt")));
        File.WriteAllText(Datei("neu.xtf"), "Vorhandene Ausgabe");
        Assert.Throws<IOException>(() => _ablage.Exportiere(info.Datei, Datei("neu.xtf"))); Assert.Equal("Vorhandene Ausgabe", File.ReadAllText(Datei("neu.xtf")));
        var o = Text(info.Datei); _ablage.Speichere(info.Datei, o.Id, o.Version, new Dictionary<string, string> { ["Textinhalt"] = "Geändert" });
        Assert.Throws<InvalidOperationException>(() => _ablage.SichereBericht(info.Datei, Datei("veralteter-bericht.txt")));
        Assert.False(File.Exists(Datei("veralteter-bericht.txt")));
    }

    [Fact]
    public void Abbruch_mitten_im_Import_und_defektes_XML_veroeffentlichen_keine_Arbeitsdatei()
    {
        Quelle(); var doc = XDocument.Load(Datei("original.xtf")); var fach = doc.Root!.Element(Ns + "DATASECTION")!.Elements().Single(); var vorlage = fach.Elements().First();
        for (var i = 100; i < 5110; i++) { var e = new XElement(vorlage); e.SetAttributeValue("TID", "chTEST" + i.ToString("0000000000")); fach.Add(e); }
        doc.Save(Datei("original.xtf")); using var cts = new CancellationTokenSource();
        Assert.Throws<OperationCanceledException>(() => _ablage.Importiere(Datei("original.xtf"), Datei("abbruch.ssxtf"), new Rueckmeldung(_ => cts.Cancel()), cts.Token));
        Assert.False(File.Exists(Datei("abbruch.ssxtf"))); Assert.Empty(Directory.GetFiles(_ordner, ".lieferung-*"));
        File.AppendAllText(Datei("original.xtf"), "<defekt>");
        Assert.Throws<System.Xml.XmlException>(() => _ablage.Importiere(Datei("original.xtf"), Datei("defekt.ssxtf")));
        Assert.False(File.Exists(Datei("defekt.ssxtf"))); Assert.Empty(Directory.GetFiles(_ordner, ".lieferung-*"));
    }

    [Fact]
    public void Gleiches_Unterhaltsdatum_in_unterschiedlicher_Schreibweise_ergibt_denselben_Namensschluessel()
    {
        var a = new AuswertungPro.Next.Domain.Models.ObjektQuellbeleg { Klasse = "Unterhalt" };
        a.Werte["Bezeichnung"] = "Kontrolle"; a.Werte["Zeitpunkt"] = "20260102"; var key = XtfLieferungsNorm.Namensschluessel(a);
        a.Werte["Zeitpunkt"] = "02.01.2026"; Assert.Equal(key, XtfLieferungsNorm.Namensschluessel(a));
    }
    private sealed class Rueckmeldung(Action<string> action) : IProgress<string> { public void Report(string value) => action(value); }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Falsche_XML_Themen_und_Feldnamensraeume_fuehren_nicht_zu_stillem_Datenverlust(bool feld)
    {
        Quelle(); var doc = XDocument.Load(Datei("original.xtf")); var text = doc.Descendants(Ns + Modell + "Haltung_Text").Single();
        if (feld) text.Element(Ns + "Textinhalt")!.Name = XName.Get("Textinhalt", "urn:fremd");
        else text.Name = Ns + "DSS_2020_1_LV95.FremdesThema.Haltung_Text";
        doc.Save(Datei("original.xtf"));
        Assert.Throws<InvalidDataException>(() => _ablage.Importiere(Datei("original.xtf"), Datei("arbeit.ssxtf")));
        Assert.False(File.Exists(Datei("arbeit.ssxtf")));
    }

    [Fact]
    public void Organisation_gehoert_zum_Basismodell_und_TID_lose_Beziehungen_erhalten_keine_eigene_Kennung()
    {
        var q = new AuswertungPro.Next.Domain.Models.ObjektQuellbeleg { Klasse = "Organisation", Modell = "DSS_2020_1_LV95", Kennung = Org };
        Assert.Contains("Modell", XtfLieferungsNorm.Problem(q, _ => null));
        q.Klasse = "Erhaltungsereignis_AbwasserbauwerkAssoc";
        Assert.Contains("keine eigene TID", XtfLieferungsNorm.Problem(q, _ => null));
    }
    private void Quelle()
    {
        Directory.CreateDirectory(_ordner);
        // Eigenständige Objekte; externe Organisationen bleiben ausdrücklich als solche erhalten.
        var daten = new XElement(Ns + "DSS_2020_1_LV95.Siedlungsentwaesserung", new XAttribute("BID", "chB0000000000001"),
            Objekt("Abwasserknoten", Knoten, new() { ["Bezeichnung"] = "Knoten ohne Bauwerk", ["Letzte_Aenderung"] = "20260101" }, new() { ["DatenherrRef"] = Org, ["DatenlieferantRef"] = Org }),
            Objekt("Haltung", "chTEST0000000001", new() { ["Bezeichnung"] = "Haltung", ["Letzte_Aenderung"] = "20260101" }, new() { ["DatenherrRef"] = Org, ["DatenlieferantRef"] = Org,
                ["vonHaltungspunktRef"] = "chTEST0000000003", ["nachHaltungspunktRef"] = "chTEST0000000004" }),
            Objekt("Haltung_Text", "chTEST0000000040", new() { ["Plantyp"] = "Leitungskataster", ["Textinhalt"] = "Originaltext", ["TextOri"] = "0.0", ["TextHAli"] = "Left", ["TextVAli"] = "Top" },
                new() { ["HaltungRef"] = "chTEST0000000001" }));
        daten.Elements().Last().AddFirst(new XElement(Ns + "TextPos", new XElement(Ns + "COORD", new XElement(Ns + "C1", "2690000.000"), new XElement(Ns + "C2", "1190000.000"))));
        // Die Endpunkte ergänzen wir für Normprüfungen, zählen sie im Importtest mit.
        foreach (var tid in new[] { "chTEST0000000003", "chTEST0000000004" })
            daten.Add(Objekt("Haltungspunkt", tid, new() { ["Bezeichnung"] = tid, ["Letzte_Aenderung"] = "20260101" }, new() { ["DatenherrRef"] = Org, ["DatenlieferantRef"] = Org }));
        var doc = new XDocument(new XElement(Ns + "TRANSFER", new XElement(Ns + "HEADERSECTION", new XAttribute("VERSION", "2.3"), new XAttribute("SENDER", "Test"),
            new XElement(Ns + "MODELS", new XElement(Ns + "MODEL", new XAttribute("NAME", "DSS_2020_1_LV95"), new XAttribute("VERSION", "18.10.2023"), new XAttribute("URI", "http://www.vsa.ch/models")))),
            new XElement(Ns + "DATASECTION", daten)));
        doc.Save(Datei("original.xtf"));
    }
    private static XElement Objekt(string klasse, string tid, Dictionary<string, string> werte, Dictionary<string, string> refs)
        => new(Ns + (Modell + klasse), new XAttribute("TID", tid), werte.Select(w => new XElement(Ns + w.Key, w.Value)),
            refs.Select(r => new XElement(Ns + r.Key, new XAttribute("REF", r.Value))));
    public void Dispose() { if (Directory.Exists(_ordner)) Directory.Delete(_ordner, true); }
}
