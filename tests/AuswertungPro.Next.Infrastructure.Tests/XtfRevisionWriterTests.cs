using System.Xml.Linq;
using AuswertungPro.Next.Application.Xtf;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Der Ausfuehrer schreibt ausschliesslich den Plan. Das Original bleibt unberuehrt,
/// eine vorhandene Zieldatei wird nie ueberschrieben, und alles nicht Geplante bleibt stehen.
/// </summary>
public sealed class XtfRevisionWriterTests : IDisposable
{
    private readonly string _dir;

    public XtfRevisionWriterTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"xtf-revision-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* Aufraeumen ist Nebensache */ }
    }

    private const string Original = """
<?xml version="1.0" encoding="UTF-8"?>
<TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3">
  <HEADERSECTION SENDER="Test" VERSION="2.3">
    <MODELS><MODEL NAME="VSA_KEK_2020_LV95" /></MODELS>
  </HEADERSECTION>
  <DATASECTION>
    <VSA_KEK_2020_LV95.KEK BID="B1">
      <VSA_KEK_2020_LV95.KEK.Untersuchung TID="U1">
        <Bezeichnung>06-001</Bezeichnung>
      </VSA_KEK_2020_LV95.KEK.Untersuchung>
      <VSA_KEK_2020_LV95.KEK.Kanalschaden TID="ch1000A1">
        <Letzte_Aenderung>20260114</Letzte_Aenderung>
        <UntersuchungRef REF="U1" />
        <KanalSchadencode>BAB</KanalSchadencode>
        <Distanz>5.00</Distanz>
      </VSA_KEK_2020_LV95.KEK.Kanalschaden>
      <VSA_KEK_2020_LV95.KEK.Kanalschaden TID="ch1000A2">
        <Letzte_Aenderung>20260114</Letzte_Aenderung>
        <UntersuchungRef REF="U1" />
        <KanalSchadencode>BBC</KanalSchadencode>
        <Distanz>9.00</Distanz>
      </VSA_KEK_2020_LV95.KEK.Kanalschaden>
      <VSA_KEK_2020_LV95.KEK.Datei TID="D1">
        <Bezeichnung>foto.jpg</Bezeichnung>
      </VSA_KEK_2020_LV95.KEK.Datei>
    </VSA_KEK_2020_LV95.KEK>
  </DATASECTION>
</TRANSFER>
""";

    [Fact]
    public void Eine_Aenderung_wird_geschrieben_und_datiert()
    {
        var (quelle, ziel) = Dateien();
        var plan = Plan(Position(XtfRevisionAenderung.Geaendert, "ch1000A1", "BAC",
            new XtfRevisionFeld("KanalSchadencode", "BAB", "BAC")));

        var ergebnis = XtfRevisionWriter.Schreibe(quelle, plan, ziel, new DateOnly(2026, 8, 13));

        Assert.True(ergebnis.Ok, ergebnis.Fehler);
        Assert.Equal(1, ergebnis.Geaendert);

        var schaden = Kanalschaden(ziel, "ch1000A1");
        Assert.Equal("BAC", Kindwert(schaden, "KanalSchadencode"));
        Assert.Equal("20260813", Kindwert(schaden, "Letzte_Aenderung"));
    }

    [Fact]
    public void Das_Original_bleibt_bytegleich()
    {
        var (quelle, ziel) = Dateien();
        var vorher = File.ReadAllBytes(quelle);
        var plan = Plan(Position(XtfRevisionAenderung.Geaendert, "ch1000A1", "BAC",
            new XtfRevisionFeld("KanalSchadencode", "BAB", "BAC")));

        Assert.True(XtfRevisionWriter.Schreibe(quelle, plan, ziel).Ok);

        Assert.Equal(vorher, File.ReadAllBytes(quelle));
    }

    [Fact]
    public void Nicht_geplante_Elemente_bleiben_unveraendert_stehen()
    {
        var (quelle, ziel) = Dateien();
        var plan = Plan(Position(XtfRevisionAenderung.Geaendert, "ch1000A1", "BAC",
            new XtfRevisionFeld("KanalSchadencode", "BAB", "BAC")));

        Assert.True(XtfRevisionWriter.Schreibe(quelle, plan, ziel).Ok);

        // Der zweite Schaden und der Dateiverweis stehen unveraendert im Ergebnis.
        Assert.Equal("BBC", Kindwert(Kanalschaden(ziel, "ch1000A2"), "KanalSchadencode"));
        Assert.Equal("20260114", Kindwert(Kanalschaden(ziel, "ch1000A2"), "Letzte_Aenderung"));
        Assert.Contains(
            XDocument.Load(ziel).Descendants().Where(e => e.Name.LocalName.EndsWith("Datei", StringComparison.Ordinal)),
            e => (string?)e.Attribute("TID") == "D1");
    }

    [Fact]
    public void Ein_entfernter_Schaden_verschwindet_aus_der_Revision()
    {
        var (quelle, ziel) = Dateien();
        var plan = Plan(Position(XtfRevisionAenderung.Entfernt, "ch1000A2", "BBC"));

        var ergebnis = XtfRevisionWriter.Schreibe(quelle, plan, ziel);

        Assert.True(ergebnis.Ok, ergebnis.Fehler);
        Assert.Equal(1, ergebnis.Entfernt);
        Assert.Null(KanalschadenOderNull(ziel, "ch1000A2"));
        Assert.NotNull(KanalschadenOderNull(ziel, "ch1000A1"));
    }

    [Fact]
    public void Ein_neuer_Schaden_bekommt_eine_freie_Kennung_und_die_richtige_Untersuchung()
    {
        var (quelle, ziel) = Dateien();
        var plan = Plan(Position(XtfRevisionAenderung.Neu, null, "BAF",
            new XtfRevisionFeld("KanalSchadencode", null, "BAF"),
            new XtfRevisionFeld("Distanz", null, "12.50")));

        var ergebnis = XtfRevisionWriter.Schreibe(quelle, plan, ziel, new DateOnly(2026, 8, 13));

        Assert.True(ergebnis.Ok, ergebnis.Fehler);
        Assert.Equal(1, ergebnis.Neu);

        var alle = Kanalschaeden(ziel);
        Assert.Equal(3, alle.Count);
        var neu = alle.Single(e => Kindwert(e, "KanalSchadencode") == "BAF");
        var tid = (string?)neu.Attribute("TID");
        Assert.False(string.IsNullOrWhiteSpace(tid));
        Assert.NotEqual("ch1000A1", tid);
        Assert.NotEqual("ch1000A2", tid);
        Assert.Equal("12.50", Kindwert(neu, "Distanz"));
        Assert.Equal("20260813", Kindwert(neu, "Letzte_Aenderung"));
        Assert.Equal(
            "U1",
            (string?)neu.Elements().First(e => e.Name.LocalName == "UntersuchungRef").Attribute("REF"));
    }

    [Fact]
    public void Das_Original_darf_nicht_ueberschrieben_werden()
    {
        var (quelle, _) = Dateien();

        var ergebnis = XtfRevisionWriter.Schreibe(quelle, Plan(), quelle);

        Assert.False(ergebnis.Ok);
        Assert.Contains("Original", ergebnis.Fehler!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Eine_vorhandene_Zieldatei_wird_nicht_ueberschrieben()
    {
        var (quelle, ziel) = Dateien();
        File.WriteAllText(ziel, "schon da");

        var ergebnis = XtfRevisionWriter.Schreibe(quelle, Plan(), ziel);

        Assert.False(ergebnis.Ok);
        Assert.Equal("schon da", File.ReadAllText(ziel));
    }

    [Fact]
    public void Ein_Plan_mit_offenen_Faellen_wird_nicht_geschrieben()
    {
        var (quelle, ziel) = Dateien();
        var plan = new XtfRevisionPlan("original.xtf", Array.Empty<XtfRevisionPosition>(), new[] { "unklar" });

        var ergebnis = XtfRevisionWriter.Schreibe(quelle, plan, ziel);

        Assert.False(ergebnis.Ok);
        Assert.False(File.Exists(ziel));
    }

    // Fail-closed: Was der Plan verlangt, muss angewandt worden sein. Sonst entstuende eine
    // Datei, die wie eine Revision aussieht und keine ist — genau der Fall vom 2026-08-13.
    [Fact]
    public void Ein_nicht_anwendbarer_Plan_schreibt_nichts()
    {
        var (quelle, ziel) = Dateien();
        var plan = new XtfRevisionPlan(
            "original.xtf",
            new[] { new XtfRevisionPosition(XtfRevisionAenderung.Neu, null, "GIBT-ES-NICHT", "99-999", "BAF", null, Array.Empty<XtfRevisionFeld>()) },
            Array.Empty<string>());

        var ergebnis = XtfRevisionWriter.Schreibe(quelle, plan, ziel);

        Assert.False(ergebnis.Ok);
        Assert.False(File.Exists(ziel));
        Assert.Contains("nicht vollstaendig", ergebnis.Fehler!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Eine_unbekannte_Kennung_bei_Aenderung_schreibt_ebenfalls_nichts()
    {
        var (quelle, ziel) = Dateien();
        var plan = Plan(Position(XtfRevisionAenderung.Geaendert, "ch1000UNBEKANNT", "BAC",
            new XtfRevisionFeld("KanalSchadencode", "BAB", "BAC")));

        var ergebnis = XtfRevisionWriter.Schreibe(quelle, plan, ziel);

        Assert.False(ergebnis.Ok);
        Assert.False(File.Exists(ziel));
    }

    [Fact]
    public void Eine_fehlende_Originaldatei_meldet_einen_Fehler_statt_zu_werfen()
    {
        var ergebnis = XtfRevisionWriter.Schreibe(
            Path.Combine(_dir, "gibt-es-nicht.xtf"),
            Plan(),
            Path.Combine(_dir, "ziel.xtf"));

        Assert.False(ergebnis.Ok);
        Assert.NotNull(ergebnis.Fehler);
    }

    [Fact]
    public void Ein_Plan_ohne_Aenderung_erzeugt_eine_inhaltsgleiche_Revision()
    {
        var (quelle, ziel) = Dateien();

        Assert.True(XtfRevisionWriter.Schreibe(quelle, Plan(), ziel).Ok);

        Assert.Equal(Kanalschaeden(quelle).Count, Kanalschaeden(ziel).Count);
        Assert.Equal("BAB", Kindwert(Kanalschaden(ziel, "ch1000A1"), "KanalSchadencode"));
    }

    // ── Hilfen ──────────────────────────────────────────────────────────

    // Die SIA405-Stammdaten: ein Kanal ohne "Letzte_Aenderung" und ohne "BaulicherZustand".
    private const string Stammdaten = """
<?xml version="1.0" encoding="UTF-8"?>
<TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3">
  <HEADERSECTION SENDER="Test" VERSION="2.3">
    <MODELS><MODEL NAME="SIA405_ABWASSER_2015_LV95" /></MODELS>
  </HEADERSECTION>
  <DATASECTION>
    <SIA405_Abwasser.SIA405_Abwasser BID="B1">
      <SIA405_Abwasser.SIA405_Abwasser.Kanal TID="ch010wcsKA000001">
        <Bezeichnung>80638-80631</Bezeichnung>
        <Standortname>Utzibmattweg</Standortname>
        <Nutzungsart_Ist>Schmutzabwasser</Nutzungsart_Ist>
      </SIA405_Abwasser.SIA405_Abwasser.Kanal>
    </SIA405_Abwasser.SIA405_Abwasser>
  </DATASECTION>
</TRANSFER>
""";

    // INTERLIS gibt die Feldreihenfolge vor. "BaulicherZustand" steht im Modell vor
    // "Bezeichnung" und darf deshalb nicht hinten angehaengt werden.
    [Fact]
    public void Ein_neues_Feld_wird_an_die_vom_Modell_verlangte_Stelle_gesetzt()
    {
        var quelle = Path.Combine(_dir, "stammdaten.xtf");
        File.WriteAllText(quelle, Stammdaten);
        var ziel = Path.Combine(_dir, "stammdaten-revision.xtf");

        var ergebnis = XtfRevisionWriter.Schreibe(
            quelle,
            Plan(Position(XtfRevisionAenderung.Geaendert, "ch010wcsKA000001", "",
                new XtfRevisionFeld("BaulicherZustand", null, "Z2"))),
            ziel,
            new DateOnly(2026, 8, 13));

        Assert.True(ergebnis.Ok, ergebnis.Fehler);

        var kanal = XDocument.Load(ziel).Descendants()
            .Single(e => e.Name.LocalName.EndsWith(".Kanal", StringComparison.Ordinal));

        Assert.Equal(
            new[] { "BaulicherZustand", "Bezeichnung", "Standortname", "Nutzungsart_Ist" },
            kanal.Elements().Select(e => e.Name.LocalName).ToArray());
        Assert.Equal("Z2", Kindwert(kanal, "BaulicherZustand"));
    }

    // In der SIA405-XTF gehoert "Letzte_Aenderung" in die Struktur "Metaattribute".
    // Direkt am Kanal waere es ein erfundenes Feld — die Datei wuerde die Pruefung nicht bestehen.
    [Fact]
    public void Ein_Aenderungsdatum_wird_nicht_erfunden()
    {
        var quelle = Path.Combine(_dir, "stammdaten.xtf");
        File.WriteAllText(quelle, Stammdaten);
        var ziel = Path.Combine(_dir, "stammdaten-revision.xtf");

        var ergebnis = XtfRevisionWriter.Schreibe(
            quelle,
            Plan(Position(XtfRevisionAenderung.Geaendert, "ch010wcsKA000001", "",
                new XtfRevisionFeld("Standortname", "Utzibmattweg", "Neue Gasse"))),
            ziel,
            new DateOnly(2026, 8, 13));

        Assert.True(ergebnis.Ok, ergebnis.Fehler);

        var kanal = XDocument.Load(ziel).Descendants()
            .Single(e => e.Name.LocalName.EndsWith(".Kanal", StringComparison.Ordinal));

        Assert.Null(Kindwert(kanal, "Letzte_Aenderung"));
        Assert.Equal("Neue Gasse", Kindwert(kanal, "Standortname"));
    }

    // ---------------------------------------------------------------------------
    // Ein fehlendes Feld muss an die richtige Stelle. INTERLIS gibt die Reihenfolge
    // vor; hinten anhaengen macht die Datei ungueltig.
    //
    // Eine feste Liste je Klasse reicht dafuer nicht: Gemessen an drei echten Dateien
    // ordnen sie die Haltung verschieden - Zone 1.15 setzt AbwasserbauwerkRef direkt
    // hinter die Bezeichnung, der Kantonsexport ganz ans Ende. Innerhalb einer Datei
    // ist die Reihenfolge dagegen konsistent. Die Datei weiss es also selbst.
    // ---------------------------------------------------------------------------

    private const string MitHaltungen = """
<?xml version="1.0" encoding="UTF-8"?>
<TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3">
  <HEADERSECTION SENDER="Test" VERSION="2.3">
    <MODELS><MODEL NAME="SIA405_ABWASSER_2020_LV95" /></MODELS>
  </HEADERSECTION>
  <DATASECTION>
    <SIA405_Abwasser.SIA405_Abwasser BID="B1">
      <SIA405_Abwasser.SIA405_Abwasser.Haltung TID="HA1">
        <Bezeichnung>80638-80631</Bezeichnung>
        <LaengeEffektiv>21.40</LaengeEffektiv>
        <Lichte_Hoehe>300</Lichte_Hoehe>
        <Material>Steinzeug</Material>
        <Lagebestimmung>genau</Lagebestimmung>
      </SIA405_Abwasser.SIA405_Abwasser.Haltung>
      <SIA405_Abwasser.SIA405_Abwasser.Haltung TID="HA2">
        <Bezeichnung>80631-80551</Bezeichnung>
        <LaengeEffektiv>18.00</LaengeEffektiv>
        <Lagebestimmung>genau</Lagebestimmung>
      </SIA405_Abwasser.SIA405_Abwasser.Haltung>
    </SIA405_Abwasser.SIA405_Abwasser>
  </DATASECTION>
</TRANSFER>
""";

    [Fact]
    public void Ein_fehlendes_Feld_lernt_seinen_Platz_vom_Geschwister_Objekt()
    {
        var quelle = Path.Combine(_dir, "haltungen.xtf");
        File.WriteAllText(quelle, MitHaltungen);
        var ziel = Path.Combine(_dir, "haltungen-revision.xtf");

        var plan = new XtfRevisionPlan(
            "haltungen.xtf",
            [new XtfRevisionPosition(
                XtfRevisionAenderung.Geaendert, "HA2", "", "80631-80551", "", null,
                [new XtfRevisionFeld("Material", null, "Beton_Normalbeton")])],
            Array.Empty<string>());

        var ergebnis = XtfRevisionWriter.Schreibe(quelle, plan, ziel);
        Assert.True(ergebnis.Ok, ergebnis.Fehler);

        var ha2 = XDocument.Load(ziel).Descendants()
            .Single(e => (string?)e.Attribute("TID") == "HA2");

        // HA1 fuehrt Material zwischen Lichte_Hoehe und Lagebestimmung. HA2 hat kein
        // Lichte_Hoehe, also muss Material dort vor Lagebestimmung landen - nicht
        // hinter ihr und nicht am Ende.
        Assert.Equal(
            new[] { "Bezeichnung", "LaengeEffektiv", "Material", "Lagebestimmung" },
            ha2.Elements().Select(e => e.Name.LocalName));
        Assert.Equal("Beton_Normalbeton", Kindwert(ha2, "Material"));
    }

    [Fact]
    public void Ein_vorhandenes_Feld_bleibt_an_seinem_Platz()
    {
        var quelle = Path.Combine(_dir, "haltungen.xtf");
        File.WriteAllText(quelle, MitHaltungen);
        var ziel = Path.Combine(_dir, "haltungen-revision.xtf");

        var plan = new XtfRevisionPlan(
            "haltungen.xtf",
            [new XtfRevisionPosition(
                XtfRevisionAenderung.Geaendert, "HA1", "", "80638-80631", "", null,
                [new XtfRevisionFeld("Material", "Steinzeug", "Faserzement")])],
            Array.Empty<string>());

        Assert.True(XtfRevisionWriter.Schreibe(quelle, plan, ziel).Ok);

        var ha1 = XDocument.Load(ziel).Descendants()
            .Single(e => (string?)e.Attribute("TID") == "HA1");

        Assert.Equal(
            new[] { "Bezeichnung", "LaengeEffektiv", "Lichte_Hoehe", "Material", "Lagebestimmung" },
            ha1.Elements().Select(e => e.Name.LocalName));
        Assert.Equal("Faserzement", Kindwert(ha1, "Material"));
    }

    // ---------------------------------------------------------------------------
    // Die Datei bestimmt auch die SCHREIBWEISE, nicht nur die Reihenfolge.
    //
    // Gemessen an zwei echten Lieferungen: Der GEP-Export Zone 1.15 schreibt
    // "BaulicherZustand" wie das Modell, Zone 1.17 dagegen "Baulicherzustand" mit
    // kleinem z - an Kanal (446 Objekte) und an Normschacht (295). Ein zeichengenauer
    // Vergleich findet das vorhandene Feld dann nicht und legt ein zweites daneben.
    // Die Haltung traegt danach beide, und die Datei ist kaputt.
    // ---------------------------------------------------------------------------

    private const string KleinGeschrieben = """
<?xml version="1.0" encoding="UTF-8"?>
<TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3">
  <HEADERSECTION SENDER="Test" VERSION="2.3">
    <MODELS><MODEL NAME="SIA405_ABWASSER_2020_LV95" /></MODELS>
  </HEADERSECTION>
  <DATASECTION>
    <SIA405_Abwasser.SIA405_Abwasser BID="B1">
      <SIA405_Abwasser.SIA405_Abwasser.Kanal TID="KA1">
        <Bezeichnung>80638-80631</Bezeichnung>
        <Nutzungsart_Ist>Schmutzabwasser</Nutzungsart_Ist>
        <Baulicherzustand>unbekannt</Baulicherzustand>
      </SIA405_Abwasser.SIA405_Abwasser.Kanal>
    </SIA405_Abwasser.SIA405_Abwasser>
  </DATASECTION>
</TRANSFER>
""";

    [Fact]
    public void Ein_Feld_wird_in_der_Schreibweise_der_Datei_geaendert()
    {
        var quelle = Path.Combine(_dir, "klein.xtf");
        File.WriteAllText(quelle, KleinGeschrieben);
        var ziel = Path.Combine(_dir, "klein-revision.xtf");

        var plan = new XtfRevisionPlan(
            "klein.xtf",
            [new XtfRevisionPosition(
                XtfRevisionAenderung.Geaendert, "KA1", "", "80638-80631", "", null,
                [new XtfRevisionFeld("BaulicherZustand", "unbekannt", "Z2")])],
            Array.Empty<string>());

        Assert.True(XtfRevisionWriter.Schreibe(quelle, plan, ziel).Ok);

        var kanal = XDocument.Load(ziel).Descendants().Single(e => (string?)e.Attribute("TID") == "KA1");

        // Genau ein Zustandsfeld, und zwar das der Datei - kein zweites daneben.
        var zustaende = kanal.Elements()
            .Where(e => e.Name.LocalName.Equals("BaulicherZustand", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Equal(new[] { "Baulicherzustand" }, zustaende.Select(e => e.Name.LocalName));
        Assert.Equal("Z2", Assert.Single(zustaende).Value);
    }

    private (string Quelle, string Ziel) Dateien()
    {
        var quelle = Path.Combine(_dir, "original.xtf");
        File.WriteAllText(quelle, Original);
        return (quelle, Path.Combine(_dir, "revision.xtf"));
    }

    /// <summary>
    /// Aufbau wie im echten Kantonsexport: Der Kanal verweist ueber <c>EigentuemerRef</c>
    /// auf eine Organisation im eigenen Topic <c>Administration</c>.
    /// </summary>
    private const string MitVerwaltung = """
<?xml version="1.0" encoding="UTF-8"?>
<TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3">
  <HEADERSECTION SENDER="Test" VERSION="2.3">
    <MODELS><MODEL NAME="SIA405_ABWASSER_2020_LV95" /></MODELS>
  </HEADERSECTION>
  <DATASECTION>
    <SIA405_ABWASSER_2020_LV95.SIA405_Abwasser BID="chB0000000000001">
      <SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Kanal TID="ch1000e200000000">
        <Bezeichnung>80638-80631</Bezeichnung>
        <Nutzungsart_Ist>Schmutzabwasser</Nutzungsart_Ist>
        <EigentuemerRef REF="ch1000f000000001" />
      </SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Kanal>
    </SIA405_ABWASSER_2020_LV95.SIA405_Abwasser>
    <SIA405_Base_Abwasser_LV95.Administration BID="chB0000000000002">
      <SIA405_Base_Abwasser_LV95.Administration.Organisation TID="ch1000f000000001">
        <Letzte_Aenderung>20260821</Letzte_Aenderung>
        <Bezeichnung>Abwasser Uri</Bezeichnung>
        <Organisationstyp>Kanton</Organisationstyp>
        <Status>aktiv</Status>
      </SIA405_Base_Abwasser_LV95.Administration.Organisation>
    </SIA405_Base_Abwasser_LV95.Administration>
  </DATASECTION>
</TRANSFER>
""";

    // In INTERLIS stehen die Rollenverweise hinter den Attributen. Ein neues Attribut
    // darf deshalb nie hinter einem Ref landen — im Echtlauf am Kantonsausschnitt stand
    // "Verbindungsart" genau dort, weil weder ein Geschwister noch die Modellliste einen
    // Nachfolger kannten.
    [Fact]
    public void Ein_neues_Attribut_landet_vor_den_Verweisen()
    {
        var quelle = Path.Combine(_dir, "verwaltung.xtf");
        File.WriteAllText(quelle, MitVerwaltung);
        var ziel = Path.Combine(_dir, "verwaltung-revision.xtf");

        var ergebnis = XtfRevisionWriter.Schreibe(
            quelle,
            Plan(Position(XtfRevisionAenderung.Geaendert, "ch1000e200000000", "",
                new XtfRevisionFeld("Verbindungsart", null, "Steckmuffen"))),
            ziel,
            new DateOnly(2026, 9, 2));

        Assert.True(ergebnis.Ok, ergebnis.Fehler);

        var kanal = XDocument.Load(ziel).Descendants()
            .Single(e => e.Name.LocalName.EndsWith(".Kanal", StringComparison.Ordinal));
        var namen = kanal.Elements().Select(e => e.Name.LocalName).ToList();

        Assert.True(
            namen.IndexOf("Verbindungsart") < namen.IndexOf("EigentuemerRef"),
            $"Reihenfolge: {string.Join(", ", namen)}");
    }

    // Ein Verweis selbst gehoert dagegen zu den Verweisen und wird nicht vorgezogen.
    [Fact]
    public void Ein_neuer_Verweis_wird_nicht_vor_die_Attribute_gezogen()
    {
        var ohneEigentuemer = MitVerwaltung.Replace(
            "        <EigentuemerRef REF=\"ch1000f000000001\" />\n", "", StringComparison.Ordinal);

        var quelle = Path.Combine(_dir, "ohne-eigentuemer.xtf");
        File.WriteAllText(quelle, ohneEigentuemer);
        var ziel = Path.Combine(_dir, "ohne-eigentuemer-revision.xtf");

        var ergebnis = XtfRevisionWriter.Schreibe(
            quelle,
            Plan(Position(XtfRevisionAenderung.Geaendert, "ch1000e200000000", "",
                new XtfRevisionFeld("EigentuemerRef", null, "ch1000f000000001", IstVerweis: true))),
            ziel,
            new DateOnly(2026, 9, 2));

        Assert.True(ergebnis.Ok, ergebnis.Fehler);

        var kanal = XDocument.Load(ziel).Descendants()
            .Single(e => e.Name.LocalName.EndsWith(".Kanal", StringComparison.Ordinal));
        var namen = kanal.Elements().Select(e => e.Name.LocalName).ToList();

        Assert.True(
            namen.IndexOf("EigentuemerRef") > namen.IndexOf("Nutzungsart_Ist"),
            $"Reihenfolge: {string.Join(", ", namen)}");
    }

    // Ein Verweis traegt seinen Wert im Attribut REF, nicht im Text. Wuerde er wie ein
    // gewoehnliches Feld geschrieben, staende die Kennung als Elementtext da und der
    // alte REF bliebe unveraendert stehen — die Datei zeigte weiterhin auf den falschen
    // Eigentuemer und saehe dabei geaendert aus.
    [Fact]
    public void Ein_Verweis_wird_ins_Attribut_geschrieben()
    {
        var quelle = Path.Combine(_dir, "verwaltung.xtf");
        File.WriteAllText(quelle, MitVerwaltung);
        var ziel = Path.Combine(_dir, "verwaltung-revision.xtf");

        var ergebnis = XtfRevisionWriter.Schreibe(
            quelle,
            Plan(Position(XtfRevisionAenderung.Geaendert, "ch1000e200000000", "",
                new XtfRevisionFeld("EigentuemerRef", "ch1000f000000001", "chORG000O000001", IstVerweis: true))),
            ziel,
            new DateOnly(2026, 9, 2));

        Assert.True(ergebnis.Ok, ergebnis.Fehler);

        var verweis = XDocument.Load(ziel).Descendants()
            .Single(e => e.Name.LocalName == "EigentuemerRef");

        Assert.Equal("chORG000O000001", (string?)verweis.Attribute("REF"));
        Assert.Equal("", verweis.Value);
    }

    [Fact]
    public void Eine_fehlende_Organisation_wird_im_Verwaltungs_Topic_angelegt()
    {
        var quelle = Path.Combine(_dir, "verwaltung.xtf");
        File.WriteAllText(quelle, MitVerwaltung);
        var ziel = Path.Combine(_dir, "verwaltung-revision.xtf");

        var plan = Plan(Position(XtfRevisionAenderung.Geaendert, "ch1000e200000000", "",
            new XtfRevisionFeld("EigentuemerRef", "ch1000f000000001", "chORG000O000001", IstVerweis: true)))
            with { NeueOrganisationen = new[] { new XtfNeueOrganisation("chORG000O000001", "Privat", "Privat") } };

        var ergebnis = XtfRevisionWriter.Schreibe(quelle, plan, ziel, new DateOnly(2026, 9, 2));
        Assert.True(ergebnis.Ok, ergebnis.Fehler);

        var doc = XDocument.Load(ziel);
        var organisationen = doc.Descendants()
            .Where(e => e.Name.LocalName.EndsWith(".Organisation", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(2, organisationen.Count);

        var neu = organisationen.Single(e => (string?)e.Attribute("TID") == "chORG000O000001");
        Assert.Equal("Privat", Kindwert(neu, "Bezeichnung"));
        Assert.Equal("Privat", Kindwert(neu, "Organisationstyp"));
        Assert.Equal("aktiv", Kindwert(neu, "Status"));
        Assert.Equal("20260902", Kindwert(neu, "Letzte_Aenderung"));

        // Sie gehoert in dasselbe Topic wie ihr Vorbild, nicht neben die Kanaele.
        Assert.Equal(
            "SIA405_Base_Abwasser_LV95.Administration",
            neu.Parent!.Name.LocalName);
    }

    // Eine schon vergebene Kennung waere ein zweites Objekt mit derselben Identitaet.
    // Lieber gar nichts schreiben als eine Datei, die INTERLIS ablehnt.
    [Fact]
    public void Eine_Organisation_mit_belegter_Kennung_schreibt_nichts()
    {
        var quelle = Path.Combine(_dir, "verwaltung.xtf");
        File.WriteAllText(quelle, MitVerwaltung);
        var ziel = Path.Combine(_dir, "verwaltung-revision.xtf");

        var plan = Plan(Position(XtfRevisionAenderung.Geaendert, "ch1000e200000000", "",
            new XtfRevisionFeld("EigentuemerRef", null, "ch1000f000000001", IstVerweis: true)))
            with { NeueOrganisationen = new[] { new XtfNeueOrganisation("ch1000f000000001", "Privat", "Privat") } };

        var ergebnis = XtfRevisionWriter.Schreibe(quelle, plan, ziel);

        Assert.False(ergebnis.Ok);
        Assert.False(File.Exists(ziel));
    }

    // Ohne vorhandene Organisation fehlt das Topic "Administration". Der Ausfuehrer
    // erfindet es nicht — er bricht ab.
    [Fact]
    public void Ohne_Vorbild_wird_keine_Organisation_erfunden()
    {
        var quelle = Path.Combine(_dir, "stammdaten.xtf");
        File.WriteAllText(quelle, Stammdaten);
        var ziel = Path.Combine(_dir, "stammdaten-revision.xtf");

        var plan = Plan(Position(XtfRevisionAenderung.Geaendert, "ch010wcsKA000001", "",
            new XtfRevisionFeld("EigentuemerRef", null, "chORG000O000001", IstVerweis: true)))
            with { NeueOrganisationen = new[] { new XtfNeueOrganisation("chORG000O000001", "Privat", "Privat") } };

        var ergebnis = XtfRevisionWriter.Schreibe(quelle, plan, ziel);

        Assert.False(ergebnis.Ok);
        Assert.False(File.Exists(ziel));
    }

    private static XtfRevisionPlan Plan(params XtfRevisionPosition[] positionen)
        => new("original.xtf", positionen, Array.Empty<string>());

    private static XtfRevisionPosition Position(
        XtfRevisionAenderung art,
        string? tid,
        string code,
        params XtfRevisionFeld[] felder)
        => new(art, tid, "U1", "06-001", code, null, felder);

    private static List<XElement> Kanalschaeden(string pfad)
        => XDocument.Load(pfad).Descendants()
            .Where(e => e.Name.LocalName.EndsWith("Kanalschaden", StringComparison.Ordinal))
            .ToList();

    private static XElement? KanalschadenOderNull(string pfad, string tid)
        => Kanalschaeden(pfad).FirstOrDefault(e => (string?)e.Attribute("TID") == tid);

    private static XElement Kanalschaden(string pfad, string tid)
        => KanalschadenOderNull(pfad, tid) ?? throw new InvalidOperationException($"Kanalschaden {tid} fehlt.");

    private static string? Kindwert(XElement node, string name)
        => node.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value;
}
