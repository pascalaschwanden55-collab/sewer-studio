using System.Xml.Linq;
using AuswertungPro.Next.Application.Xtf;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Stammdaten der SIA405-XTF: In die Revision kommt ausschliesslich, was der Mensch
/// von Hand gesetzt hat.
/// </summary>
public sealed class XtfStammdatenPlanBuilderTests
{
    private const string Sec = """
<?xml version="1.0" encoding="utf-8"?>
<TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3">
  <HEADERSECTION VERSION="2.3" SENDER="VSA">
    <MODELS><MODEL NAME="SIA405_ABWASSER_2015_LV95" /></MODELS>
  </HEADERSECTION>
  <DATASECTION>
    <SIA405_Abwasser.SIA405_Abwasser>
      <SIA405_Abwasser.SIA405_Abwasser.Kanal TID="ch010wcsKA000001">
        <Bezeichnung>80638-80631</Bezeichnung>
        <Standortname>Utzibmattweg</Standortname>
        <Nutzungsart_Ist>Schmutzabwasser</Nutzungsart_Ist>
      </SIA405_Abwasser.SIA405_Abwasser.Kanal>
    </SIA405_Abwasser.SIA405_Abwasser>
  </DATASECTION>
</TRANSFER>
""";

    [Fact]
    public void Der_Leser_findet_den_Kanal_und_seine_Werte()
    {
        var element = Assert.Single(XtfStammdatenElementReader.Parse(XDocument.Parse(Sec)));

        Assert.Equal("ch010wcsKA000001", element.Tid);
        Assert.Equal("80638-80631", element.Bezeichnung);
        Assert.Equal("Utzibmattweg", element.Werte["Standortname"]);
        Assert.Equal("Schmutzabwasser", element.Werte["Nutzungsart_Ist"]);
    }

    [Fact]
    public void Eine_Handaenderung_kommt_in_den_Plan()
    {
        var record = Haltung("80638-80631");
        record.SetFieldValue(FieldKeys.UsageType, "Mischabwasser", FieldSource.Manual, userEdited: true);

        var position = Assert.Single(Baue(record));

        Assert.Equal("ch010wcsKA000001", position.KanalschadenTid);
        var feld = Assert.Single(position.Felder);
        Assert.Equal("Nutzungsart_Ist", feld.Name);
        Assert.Equal("Schmutzabwasser", feld.Alt);
        Assert.Equal("Mischabwasser", feld.Neu);
    }

    // Der entscheidende Schutz: Ein importierter Wert darf nicht in die Datei
    // zurueckgeschrieben werden — er stammt ja von dort.
    [Fact]
    public void Ein_nur_importierter_Wert_kommt_nicht_in_den_Plan()
    {
        var record = Haltung("80638-80631");
        record.SetFieldValue(FieldKeys.UsageType, "Mischabwasser", FieldSource.Xtf, userEdited: false);

        Assert.Empty(Baue(record));
    }

    [Fact]
    public void Ein_unveraenderter_Handwert_erzeugt_keine_Position()
    {
        var record = Haltung("80638-80631");
        record.SetFieldValue(FieldKeys.UsageType, "Schmutzabwasser", FieldSource.Manual, userEdited: true);

        Assert.Empty(Baue(record));
    }

    [Fact]
    public void Eine_fremde_Haltung_wird_nicht_zugeordnet()
    {
        var record = Haltung("99-999");
        record.SetFieldValue(FieldKeys.UsageType, "Mischabwasser", FieldSource.Manual, userEdited: true);

        Assert.Empty(Baue(record));
    }

    [Fact]
    public void Mehrere_Felder_erscheinen_gemeinsam()
    {
        var record = Haltung("80638-80631");
        record.SetFieldValue(FieldKeys.UsageType, "Mischabwasser", FieldSource.Manual, userEdited: true);
        record.SetFieldValue(FieldKeys.ConnectionType, "Steckmuffen", FieldSource.Manual, userEdited: true);

        var position = Assert.Single(Baue(record));

        Assert.Equal(2, position.Felder.Count);
        Assert.Contains(position.Felder, f => f.Name == "Verbindungsart" && f.Neu == "Steckmuffen");
    }

    // Die Strasse hat in SIA405 mit "Standortname" durchaus ein Ziel. Sie geht auf
    // ausdrueckliche Anweisung trotzdem nicht mehr hinaus (2026-09-02) — das Feld bleibt
    // im Programm, die Datei bekommt es nicht. Ohne diesen Test waere die Zeile beim
    // naechsten Aufraeumen als vergessene Luecke wieder eingebaut.
    [Fact]
    public void Die_Strasse_wird_nicht_mehr_exportiert()
    {
        var record = Haltung("80638-80631");
        record.SetFieldValue(FieldKeys.Street, "Neue Gasse", FieldSource.Manual, userEdited: true);

        Assert.Empty(Baue(record));
        Assert.DoesNotContain("Standortname", XtfStammdatenPlanBuilder.Felder.Keys);
    }

    // Die uebrigen Felder der Kataster-Infobox. Fuellgrad im Abwassernetz des Kantons:
    // Status 97,8 %, FunktionHydraulisch 93,5 %, Lagebestimmung 98,0 %,
    // Baujahr 43,0 %, Sanierungsbedarf 30,6 %, Bruttokosten 18,2 %.
    [Theory]
    [InlineData("Status", "in_Betrieb", "Status", "in_Betrieb")]
    [InlineData("Sanierungsbedarf", "mittelfristig", "Sanierungsbedarf", "mittelfristig")]
    [InlineData("FunktionHydraulisch", "Freispiegelleitung", "FunktionHydraulisch", "Freispiegelleitung")]
    [InlineData("Baujahr", "1969", "Baujahr", "1969")]
    [InlineData("Bruttokosten", "1250", "Bruttokosten", "1250.00")]
    [InlineData("Bruttokosten", "1250,50", "Bruttokosten", "1250.50")]
    public void Die_Infobox_Felder_gehen_an_den_Kanal(
        string projektFeld, string eingabe, string xtfName, string erwartet)
    {
        var record = Haltung("80638-80631");
        record.SetFieldValue(projektFeld, eingabe, FieldSource.Manual, userEdited: true);

        var feld = Assert.Single(Assert.Single(Baue(record)).Felder);

        Assert.Equal(xtfName, feld.Name);
        Assert.Equal(erwartet, feld.Neu);
    }

    // Die Lagebestimmung haengt an der physischen Klasse "Haltung", nicht am "Kanal".
    [Fact]
    public void Die_Lagebestimmung_geht_an_die_Haltung()
    {
        var record = Haltung("80638-80631");
        record.SetFieldValue(FieldKeys.PositionAccuracy, "genau", FieldSource.Manual, userEdited: true);

        var position = Assert.Single(PlanMitHaltung(record).Positionen);
        var feld = Assert.Single(position.Felder);

        Assert.Equal("ch010wcsHA000001", position.KanalschadenTid);
        Assert.Equal("Lagebestimmung", feld.Name);
        Assert.Equal("genau", feld.Neu);
    }

    // Ausserhalb des Wertebereichs wird nichts geschrieben — auch keine gerundete oder
    // gekappte Fassung. Der Bereich steht in der Norm, nicht im Programm.
    [Theory]
    [InlineData("Baujahr", "1799")]
    [InlineData("Baujahr", "2101")]
    [InlineData("Baujahr", "vor 1900")]
    [InlineData("Bruttokosten", "-5")]
    [InlineData("Status", "stillgelegt")]
    [InlineData("FunktionHydraulisch", "Freispiegel")]
    public void Ein_Wert_ausserhalb_der_Norm_wird_nicht_geschrieben(string projektFeld, string eingabe)
    {
        var record = Haltung("80638-80631");
        record.SetFieldValue(projektFeld, eingabe, FieldSource.Manual, userEdited: true);

        var plan = Plan(record);

        Assert.Empty(plan.Positionen);
        Assert.NotEmpty(plan.Hinweise);
    }

    // Die Herkunftsangaben des Katasters bleiben im Programm, gehen aber nie zurueck.
    // Der Datenherr einer Kantonsleitung ist der Kanton, nicht der Operateur.
    [Theory]
    [InlineData(FieldKeys.CadastreObjectId, "610646")]
    [InlineData(FieldKeys.DataOwner, "Acht Grad Ost AG, Altdorf")]
    [InlineData(FieldKeys.DataSupplier, "Acht Grad Ost AG, Altdorf")]
    [InlineData(FieldKeys.CadastreOrganisation, "unbekannt")]
    [InlineData(FieldKeys.CadastreLastChange, "14.02.2025")]
    [InlineData(FieldKeys.CadastreUpdatedAt, "28.08.2026")]
    [InlineData(FieldKeys.Street, "Neue Gasse")]
    public void Eine_Herkunftsangabe_geht_nie_in_die_Revision(string projektFeld, string eingabe)
    {
        var record = Haltung("80638-80631");
        record.SetFieldValue(projektFeld, eingabe, FieldSource.Manual, userEdited: true);

        Assert.Empty(PlanMitHaltung(record).Positionen);
        Assert.Contains(projektFeld, XtfStammdatenPlanBuilder.NichtExportierteFelder);
    }

    // Kein Feld darf gleichzeitig exportiert und als nicht exportiert gefuehrt werden.
    [Fact]
    public void Die_Verzichtsliste_widerspricht_dem_Export_nicht()
    {
        var exportiert = XtfStammdatenPlanBuilder.Felder.Values
            .Concat(XtfStammdatenPlanBuilder.HaltungFelder.Values)
            .Concat(XtfStammdatenPlanBuilder.RohrprofilFelder.Values)
            .Concat(XtfStammdatenPlanBuilder.EigentuemerFeldKarte.Values)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Empty(XtfStammdatenPlanBuilder.NichtExportierteFelder.Where(exportiert.Contains));
    }

    // Die sekundaere Abwasseranlage fehlte in der Auswahl ganz, obwohl der Kataster sie
    // fuehrt. Ein SAA-Wert muss deshalb ebenso hinausgehen wie ein PAA-Wert.
    [Fact]
    public void Die_funktionale_Hierarchie_kennt_auch_die_sekundaere_Anlage()
    {
        var record = Haltung("80638-80631");
        record.SetFieldValue(
            FieldKeys.HierarchicalFunction, "SAA.Liegenschaftsentwaesserung",
            FieldSource.Manual, userEdited: true);

        var feld = Assert.Single(Assert.Single(Baue(record)).Felder);

        Assert.Equal("FunktionHierarchisch", feld.Name);
        Assert.Equal("SAA.Liegenschaftsentwaesserung", feld.Neu);
    }

    // Ein Wert, den das Modell nicht kennt, wird nicht geschrieben — und der Verzicht
    // wird gemeldet, statt still zu verschwinden.
    [Fact]
    public void Eine_unbekannte_Verbindungsart_wird_nicht_geschrieben()
    {
        var record = Haltung("80638-80631");
        record.SetFieldValue(FieldKeys.ConnectionType, "Klebemuffe", FieldSource.Manual, userEdited: true);

        var plan = Plan(record);

        Assert.Empty(plan.Positionen);
        Assert.Contains(plan.Hinweise, h => h.Contains("Klebemuffe", StringComparison.Ordinal));
    }

    // Die Haltungslaenge ist eine Laenge in METERN. Wuerde sie wie eine Abmessung
    // behandelt, machte SiaAbmessung aus 45,30 m den Wert 45300 — ein Faktor-1000-Fehler,
    // der in der Datei wie eine gueltige Angabe aussieht.
    [Theory]
    [InlineData("45.30", "45.30")]
    [InlineData("45,30", "45.30")]
    [InlineData("7", "7.00")]
    public void Die_Haltungslaenge_geht_als_Meter_hinaus(string eingabe, string erwartet)
    {
        var record = Haltung("80638-80631");
        record.SetFieldValue(FieldKeys.HoldingLengthMeters, eingabe, FieldSource.Manual, userEdited: true);

        var position = Assert.Single(PlanMitHaltung(record).Positionen);
        var feld = Assert.Single(position.Felder);

        Assert.Equal("LaengeEffektiv", feld.Name);
        Assert.Equal(erwartet, feld.Neu);
    }

    // Der Profiltyp haengt nicht an der Haltung, sondern an einem eigenen Objekt hinter
    // "RohrprofilRef". Ohne diesen Weg landet er nirgends.
    [Fact]
    public void Der_Profiltyp_landet_am_verwiesenen_Rohrprofil()
    {
        var record = Haltung("80638-80631");
        record.SetFieldValue(FieldKeys.ProfileType, "Eiprofil", FieldSource.Manual, userEdited: true);

        var plan = XtfStammdatenPlanBuilder.Build(
            new[] { record },
            XtfStammdatenElementReader.Parse(XDocument.Parse(MitProfil)),
            "SIA405_ABWASSER_2020_LV95");

        var position = Assert.Single(plan.Positionen);
        var feld = Assert.Single(position.Felder);

        Assert.Equal("ch010wcsRP000001", position.KanalschadenTid);
        Assert.Equal("Profiltyp", feld.Name);
        Assert.Equal("Kreisprofil", feld.Alt);
        Assert.Equal("Eiprofil", feld.Neu);
    }

    // Zeigen zwei Haltungen auf dasselbe Rohrprofil, wuerde eine Aenderung auch die
    // fremde Haltung umschreiben. Im Kantonsexport besitzt jede Haltung ihr eigenes
    // Profil — verlassen wird sich darauf nicht.
    [Fact]
    public void Ein_geteiltes_Rohrprofil_wird_nicht_geaendert()
    {
        var geteilt = MitProfil.Replace(
            "</SIA405_Abwasser.SIA405_Abwasser>",
            """
              <SIA405_Abwasser.SIA405_Abwasser.Haltung TID="ch010wcsHA000002">
                <Bezeichnung>80631-80551</Bezeichnung>
                <RohrprofilRef REF="ch010wcsRP000001" />
              </SIA405_Abwasser.SIA405_Abwasser.Haltung>
            </SIA405_Abwasser.SIA405_Abwasser>
            """,
            StringComparison.Ordinal);

        var record = Haltung("80638-80631");
        record.SetFieldValue(FieldKeys.ProfileType, "Eiprofil", FieldSource.Manual, userEdited: true);

        var plan = XtfStammdatenPlanBuilder.Build(
            new[] { record },
            XtfStammdatenElementReader.Parse(XDocument.Parse(geteilt)),
            "SIA405_ABWASSER_2020_LV95");

        Assert.Empty(plan.Positionen);
        Assert.Contains(plan.Hinweise, h => h.Contains("gemeinsam benutzt", StringComparison.Ordinal));
    }

    // Der Eigentuemer ist in SIA405 kein Text, sondern ein Verweis. Gibt es die
    // Organisation schon, wird sie benutzt und keine zweite angelegt — die Norm verlangt
    // Bezeichnung, Typ und UID zusammen als eindeutig.
    [Fact]
    public void Ein_bekannter_Eigentuemer_verweist_auf_die_vorhandene_Organisation()
    {
        var record = Haltung("80638-80631");
        record.SetFieldValue(FieldKeys.Owner, "Privat", FieldSource.Manual, userEdited: true);

        var plan = PlanMitVerwaltung(record);
        var feld = Assert.Single(Assert.Single(plan.Positionen).Felder);

        Assert.Equal("EigentuemerRef", feld.Name);
        Assert.True(feld.IstVerweis);
        Assert.Equal("ch1000f000000002", feld.Neu);
        Assert.Equal("ch1000f000000001", feld.Alt);
        Assert.Empty(plan.Organisationen);
    }

    // Steht in der Datei schon der richtige Eigentuemer, entsteht keine Position.
    // Sonst traege jede Haltung eine Scheinaenderung.
    [Fact]
    public void Ein_unveraenderter_Eigentuemer_erzeugt_keine_Position()
    {
        var record = Haltung("80638-80631");
        record.SetFieldValue(FieldKeys.Owner, "Abwasser Uri", FieldSource.Manual, userEdited: true);

        var plan = PlanMitVerwaltung(record);

        Assert.Empty(plan.Positionen);
        Assert.Empty(plan.Organisationen);
    }

    // STANDARDOID ist in INTERLIS "OID TEXT*16" — genau sechzehn Zeichen, nur Ziffern
    // und Buchstaben. Der offizielle Pruefer (ilivalidator 1.15.0) weist eine kuerzere
    // mit "is not a valid OID" ab; genau das ist am 2026-09-03 am echten
    // Kantonsausschnitt passiert, als hier noch fuenfzehn Zeichen entstanden.
    [Fact]
    public void Die_Kennung_einer_neuen_Organisation_ist_eine_gueltige_OID()
    {
        var record = Haltung("80638-80631");
        record.SetFieldValue(FieldKeys.Owner, "Gemeinde", FieldSource.Manual, userEdited: true);

        var tid = Assert.Single(PlanMitVerwaltung(record).Organisationen).Tid;

        Assert.Equal(16, tid.Length);
        Assert.All(tid, z => Assert.True(char.IsAsciiLetterOrDigit(z), $"Ungueltiges Zeichen '{z}' in {tid}"));
    }

    [Fact]
    public void Ein_neuer_Eigentuemer_bekommt_eine_eigene_Organisation()
    {
        var record = Haltung("80638-80631");
        record.SetFieldValue(FieldKeys.Owner, "Gemeinde", FieldSource.Manual, userEdited: true);

        var plan = PlanMitVerwaltung(record);
        var organisation = Assert.Single(plan.Organisationen);
        var feld = Assert.Single(Assert.Single(plan.Positionen).Felder);

        Assert.Equal("Gemeinde", organisation.Bezeichnung);
        Assert.Equal("Gemeinde", organisation.Organisationstyp);
        Assert.Equal(organisation.Tid, feld.Neu);
        Assert.NotEqual("ch1000f000000001", organisation.Tid);
        Assert.NotEqual("ch1000f000000002", organisation.Tid);
    }

    // Zwei Haltungen mit demselben neuen Eigentuemer teilen sich eine Organisation.
    [Fact]
    public void Derselbe_neue_Eigentuemer_erzeugt_nur_eine_Organisation()
    {
        var eine = Haltung("80638-80631");
        eine.SetFieldValue(FieldKeys.Owner, "Gemeinde", FieldSource.Manual, userEdited: true);
        var andere = Haltung("80631-80551");
        andere.SetFieldValue(FieldKeys.Owner, "Gemeinde", FieldSource.Manual, userEdited: true);

        var plan = XtfStammdatenPlanBuilder.Build(
            new[] { eine, andere },
            XtfStammdatenElementReader.Parse(XDocument.Parse(MitVerwaltung)),
            "SIA405_ABWASSER_2020_LV95");

        Assert.Single(plan.Organisationen);
    }

    // Ohne Organisationen fehlt in der Datei das ganze Topic "Administration". Eines
    // anzulegen waere ein Eingriff in den Aufbau der Kundendatei.
    [Fact]
    public void Ohne_Organisationen_bleibt_der_Eigentuemer_aussen_vor()
    {
        var record = Haltung("80638-80631");
        record.SetFieldValue(FieldKeys.Owner, "Gemeinde", FieldSource.Manual, userEdited: true);

        var plan = Plan(record);

        Assert.Empty(plan.Positionen);
        Assert.Empty(plan.Organisationen);
        Assert.Contains(plan.Hinweise, h => h.Contains("keine Organisationen", StringComparison.Ordinal));
    }

    // "Organisationstyp" ist in SIA405 ein Pflichtfeld. Fuer einen Freitext gibt es
    // keinen bekannten Typ — geraten wird nicht.
    [Fact]
    public void Ein_Eigentuemer_ohne_bekannten_Typ_wird_nicht_geschrieben()
    {
        var record = Haltung("80638-80631");
        record.SetFieldValue(FieldKeys.Owner, "Familie Muster", FieldSource.Manual, userEdited: true);

        var plan = PlanMitVerwaltung(record);

        Assert.Empty(plan.Positionen);
        Assert.Empty(plan.Organisationen);
        Assert.Contains(plan.Hinweise, h => h.Contains("Organisationstyp", StringComparison.Ordinal));
    }

    private static XtfStammdatenPlan PlanMitVerwaltung(HaltungRecord record)
        => XtfStammdatenPlanBuilder.Build(
            new[] { record },
            XtfStammdatenElementReader.Parse(XDocument.Parse(MitVerwaltung)),
            "SIA405_ABWASSER_2020_LV95");

    /// <summary>
    /// Aufbau wie im echten Kantonsexport: zwei Kanaele, die beide auf die einzige
    /// Organisation im Topic <c>Administration</c> zeigen.
    /// </summary>
    private const string MitVerwaltung = """
<?xml version="1.0" encoding="utf-8"?>
<TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3">
  <HEADERSECTION VERSION="2.3" SENDER="VSA">
    <MODELS><MODEL NAME="SIA405_ABWASSER_2020_LV95" /></MODELS>
  </HEADERSECTION>
  <DATASECTION>
    <SIA405_ABWASSER_2020_LV95.SIA405_Abwasser BID="chB0000000000001">
      <SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Kanal TID="ch1000e200000000">
        <Bezeichnung>80638-80631</Bezeichnung>
        <Nutzungsart_Ist>Schmutzabwasser</Nutzungsart_Ist>
        <EigentuemerRef REF="ch1000f000000001" />
      </SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Kanal>
      <SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Kanal TID="ch1000e200000001">
        <Bezeichnung>80631-80551</Bezeichnung>
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
      <SIA405_Base_Abwasser_LV95.Administration.Organisation TID="ch1000f000000002">
        <Letzte_Aenderung>20260821</Letzte_Aenderung>
        <Bezeichnung>Privat</Bezeichnung>
        <Organisationstyp>Privat</Organisationstyp>
        <Status>aktiv</Status>
      </SIA405_Base_Abwasser_LV95.Administration.Organisation>
    </SIA405_Base_Abwasser_LV95.Administration>
  </DATASECTION>
</TRANSFER>
""";

    /// <summary>
    /// Aufbau wie im echten Kantonsexport: Die Haltung zeigt ueber <c>RohrprofilRef</c>
    /// auf ein eigenes <c>Rohrprofil</c>-Objekt.
    /// </summary>
    private const string MitProfil = """
<?xml version="1.0" encoding="utf-8"?>
<TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3">
  <HEADERSECTION VERSION="2.3" SENDER="VSA">
    <MODELS><MODEL NAME="SIA405_ABWASSER_2020_LV95" /></MODELS>
  </HEADERSECTION>
  <DATASECTION>
    <SIA405_Abwasser.SIA405_Abwasser>
      <SIA405_Abwasser.SIA405_Abwasser.Haltung TID="ch010wcsHA000001">
        <Bezeichnung>80638-80631</Bezeichnung>
        <RohrprofilRef REF="ch010wcsRP000001" />
      </SIA405_Abwasser.SIA405_Abwasser.Haltung>
      <SIA405_Abwasser.SIA405_Abwasser.Rohrprofil TID="ch010wcsRP000001">
        <Bezeichnung>Kreisprofil_0</Bezeichnung>
        <Profiltyp>Kreisprofil</Profiltyp>
      </SIA405_Abwasser.SIA405_Abwasser.Rohrprofil>
    </SIA405_Abwasser.SIA405_Abwasser>
  </DATASECTION>
</TRANSFER>
""";

    [Fact]
    public void Ein_leerer_Handwert_loescht_nichts()
    {
        var record = Haltung("80638-80631");
        record.SetFieldValue(FieldKeys.UsageType, "", FieldSource.Manual, userEdited: true);

        Assert.Empty(Baue(record));
    }

    // Die Zustandsklasse ist der Wert, der in der Praxis am haeufigsten von Hand
    // korrigiert wird. Das Projekt fuehrt sie als Ziffer, SIA405 verlangt "Z0" bis "Z4".
    [Theory]
    [InlineData("0", "Z0")]
    [InlineData("2", "Z2")]
    [InlineData("4", "Z4")]
    [InlineData("Z3", "Z3")]
    public void Die_Zustandsklasse_wird_in_die_Schreibweise_des_Modells_gebracht(string projekt, string erwartet)
    {
        var record = Haltung("80638-80631");
        record.SetFieldValue(FieldKeys.ConditionClass, projekt, FieldSource.Manual, userEdited: true);

        var position = Assert.Single(Baue(record));
        var feld = Assert.Single(position.Felder);

        Assert.Equal("BaulicherZustand", feld.Name);
        Assert.Null(feld.Alt);
        Assert.Equal(erwartet, feld.Neu);
    }

    // Fail-closed: Was nicht sicher in den Wertebereich passt, wird nicht geschrieben.
    // "3.22" ist die berechnete VSA-Note, keine Zustandsklasse.
    [Theory]
    [InlineData("n/a")]
    [InlineData("3.22")]
    [InlineData("5")]
    [InlineData("-1")]
    [InlineData("Z9")]
    public void Ein_unklarer_Zustandswert_wird_nicht_geschrieben(string projekt)
    {
        var record = Haltung("80638-80631");
        record.SetFieldValue(FieldKeys.ConditionClass, projekt, FieldSource.Manual, userEdited: true);

        Assert.Empty(Baue(record));
    }

    [Fact]
    public void Eine_unveraenderte_Zustandsklasse_erzeugt_keine_Position()
    {
        var record = Haltung("80638-80631");
        record.SetFieldValue(FieldKeys.ConditionClass, "2", FieldSource.Manual, userEdited: true);

        // Steht derselbe Wert schon in der Datei, gibt es nichts zu revidieren.
        var elemente = XtfStammdatenElementReader.Parse(XDocument.Parse(
            Sec.Replace("<Bezeichnung>", "<BaulicherZustand>Z2</BaulicherZustand><Bezeichnung>")));

        Assert.Empty(XtfStammdatenPlanBuilder.Build(new[] { record }, elemente).Positionen);
    }

    // Dieselbe Haltung heisst im Projekt "A-B" und in der XTF "B-A" — derselbe Kanal.
    [Fact]
    public void Die_Gegenrichtung_wird_zugeordnet()
    {
        var record = Haltung("80631-80638");
        record.SetFieldValue(FieldKeys.ConditionClass, "3", FieldSource.Manual, userEdited: true);

        var plan = Plan(record);

        var position = Assert.Single(plan.Positionen);
        Assert.Equal("ch010wcsKA000001", position.KanalschadenTid);
        Assert.Equal("Z3", Assert.Single(position.Felder).Neu);
        Assert.Empty(plan.Hinweise);
    }

    // Der direkte Treffer hat Vorrang: Eine echte Gegenrichtung darf ihn nicht verdraengen.
    [Fact]
    public void Ein_direkter_Treffer_geht_der_Gegenrichtung_vor()
    {
        var doppelt = Sec.Replace(
            "</SIA405_Abwasser.SIA405_Abwasser>",
            """
              <SIA405_Abwasser.SIA405_Abwasser.Kanal TID="ch010wcsKA000002">
                <Bezeichnung>80631-80638</Bezeichnung>
                <Standortname>Andere Gasse</Standortname>
              </SIA405_Abwasser.SIA405_Abwasser.Kanal>
            </SIA405_Abwasser.SIA405_Abwasser>
            """);

        var record = Haltung("80631-80638");
        record.SetFieldValue(FieldKeys.ConditionClass, "3", FieldSource.Manual, userEdited: true);

        var plan = XtfStammdatenPlanBuilder.Build(
            new[] { record },
            XtfStammdatenElementReader.Parse(XDocument.Parse(doppelt)));

        Assert.Equal("ch010wcsKA000002", Assert.Single(plan.Positionen).KanalschadenTid);
    }

    // Eine Handaenderung darf nicht still verschwinden, wenn es in der XTF kein Ziel gibt.
    [Fact]
    public void Eine_nicht_zuordenbare_Handaenderung_wird_gemeldet()
    {
        var record = Haltung("99-999");
        record.SetFieldValue(FieldKeys.ConditionClass, "2", FieldSource.Manual, userEdited: true);

        var plan = Plan(record);

        Assert.Empty(plan.Positionen);
        Assert.Contains("99-999", Assert.Single(plan.Hinweise));
    }

    // Ohne Handaenderung gibt es nichts zu melden — sonst wuerde der Bericht zurauschen.
    [Fact]
    public void Eine_fremde_Haltung_ohne_Handaenderung_erzeugt_keinen_Hinweis()
    {
        Assert.Empty(Plan(Haltung("99-999")).Hinweise);
    }

    // Der Import benennt "Schmutzabwasser" beim Lesen zu "Schmutzwasser" um. Der
    // Rueckweg muss dasselbe tun — sonst steht ein Wert in der Datei, den das Modell
    // nicht kennt und der Pruefer ablehnt.
    [Theory]
    [InlineData("Mischabwasser", "Mischabwasser")]
    [InlineData("Mischwasser", "Mischabwasser")]
    [InlineData("Reinwasser", "Reinabwasser")]
    [InlineData("unbekannt", "unbekannt")]
    public void Die_Nutzungsart_wird_in_die_Schreibweise_des_Modells_gebracht(string projekt, string erwartet)
    {
        var record = Haltung("80638-80631");
        record.SetFieldValue(FieldKeys.UsageType, projekt, FieldSource.Manual, userEdited: true);

        var position = Assert.Single(Plan(record).Positionen);
        Assert.Equal(erwartet, Assert.Single(position.Felder).Neu);
    }

    // Der Hin- und Rueckweg schliesst sich: Aus "Schmutzabwasser" wird beim Import
    // "Schmutzwasser", und daraus wieder "Schmutzabwasser". Frueher entstand hier eine
    // Scheinaenderung, die einen im Modell unbekannten Wert in die Datei geschrieben haette.
    [Fact]
    public void Der_zurueckuebersetzte_Importwert_ist_keine_Aenderung()
    {
        var record = Haltung("80638-80631");
        record.SetFieldValue(FieldKeys.UsageType, "Schmutzwasser", FieldSource.Manual, userEdited: true);

        var plan = Plan(record);

        Assert.Empty(plan.Positionen);
        Assert.Empty(plan.Hinweise);
    }

    // Nur beim Regenwasser entscheidet die Modellfassung: 2015 kennt "Regenabwasser",
    // 2020 stattdessen "Niederschlagsabwasser" — keine kennt den Wert der anderen.
    [Theory]
    [InlineData("SIA405_ABWASSER_2015_LV95", "Regenabwasser")]
    [InlineData("SIA405_ABWASSER_2020_LV95", "Niederschlagsabwasser")]
    public void Das_Regenwasser_richtet_sich_nach_der_Modellfassung(string modell, string erwartet)
    {
        var record = Haltung("80638-80631");
        record.SetFieldValue(FieldKeys.UsageType, "Regenwasser", FieldSource.Manual, userEdited: true);

        var plan = XtfStammdatenPlanBuilder.Build(
            new[] { record },
            XtfStammdatenElementReader.Parse(XDocument.Parse(Sec)),
            modell);

        Assert.Equal(erwartet, Assert.Single(Assert.Single(plan.Positionen).Felder).Neu);
    }

    // Ohne erkennbare Fassung waere jede Wahl ein Ratespiel — lieber eine Luecke.
    [Fact]
    public void Ohne_erkennbare_Modellfassung_wird_das_Regenwasser_nicht_geschrieben()
    {
        var record = Haltung("80638-80631");
        record.SetFieldValue(FieldKeys.UsageType, "Regenwasser", FieldSource.Manual, userEdited: true);

        var plan = XtfStammdatenPlanBuilder.Build(
            new[] { record },
            XtfStammdatenElementReader.Parse(XDocument.Parse(Sec)),
            modell: "IRGENDEIN_MODELL");

        Assert.Empty(plan.Positionen);
        Assert.Contains("Regenwasser", Assert.Single(plan.Hinweise));
    }

    [Fact]
    public void Eine_unbekannte_Nutzungsart_wird_gemeldet_statt_geschrieben()
    {
        var record = Haltung("80638-80631");
        record.SetFieldValue(FieldKeys.UsageType, "Kuehlwasser", FieldSource.Manual, userEdited: true);

        var plan = Plan(record);

        Assert.Empty(plan.Positionen);
        Assert.Contains("Kuehlwasser", Assert.Single(plan.Hinweise));
    }

    [Fact]
    public void Der_Modellname_wird_aus_dem_Dateikopf_gelesen()
    {
        Assert.Equal(
            "SIA405_ABWASSER_2015_LV95",
            XtfStammdatenElementReader.ParseModelName(XDocument.Parse(Sec)));
    }

    // ---------------------------------------------------------------------------
    // Klasse "Haltung": Material und Lichte_Hoehe haengen nicht am Kanal.
    //
    // Gemessen am Kantonsexport von Abwasser Uri: Alle 109871 Kanal-Objekte tragen
    // weder Material noch Lichte_Hoehe. Beide gehoeren zur physischen Klasse Haltung,
    // die dieselbe Bezeichnung fuehrt — in allen 109871 Faellen identisch.
    // ---------------------------------------------------------------------------

    private const string MitHaltung = """
<?xml version="1.0" encoding="utf-8"?>
<TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3">
  <HEADERSECTION VERSION="2.3" SENDER="VSA">
    <MODELS><MODEL NAME="SIA405_ABWASSER_2020_LV95" /></MODELS>
  </HEADERSECTION>
  <DATASECTION>
    <SIA405_Abwasser.SIA405_Abwasser>
      <SIA405_Abwasser.SIA405_Abwasser.Kanal TID="ch010wcsKA000001">
        <Bezeichnung>80638-80631</Bezeichnung>
        <Nutzungsart_Ist>Schmutzabwasser</Nutzungsart_Ist>
      </SIA405_Abwasser.SIA405_Abwasser.Kanal>
      <SIA405_Abwasser.SIA405_Abwasser.Haltung TID="ch010wcsHA000001">
        <Bezeichnung>80638-80631</Bezeichnung>
        <Lichte_Hoehe>0</Lichte_Hoehe>
        <Material>unbekannt</Material>
      </SIA405_Abwasser.SIA405_Abwasser.Haltung>
    </SIA405_Abwasser.SIA405_Abwasser>
  </DATASECTION>
</TRANSFER>
""";

    [Fact]
    public void Der_Leser_findet_auch_die_Haltung_und_kennzeichnet_die_Klasse()
    {
        var elemente = XtfStammdatenElementReader.Parse(XDocument.Parse(MitHaltung));

        Assert.Equal(2, elemente.Count);
        var kanal = Assert.Single(elemente, e => e.Klasse == "Kanal");
        var haltung = Assert.Single(elemente, e => e.Klasse == "Haltung");

        Assert.Equal("ch010wcsKA000001", kanal.Tid);
        Assert.Equal("ch010wcsHA000001", haltung.Tid);
        Assert.Equal("80638-80631", haltung.Bezeichnung);
        Assert.Equal("unbekannt", haltung.Werte["Material"]);
        Assert.Equal("0", haltung.Werte["Lichte_Hoehe"]);
    }

    [Fact]
    public void Ein_handgesetztes_Material_geht_an_die_Haltung_nicht_an_den_Kanal()
    {
        var record = Haltung("80638-80631");
        record.SetFieldValue(FieldKeys.PipeMaterial, "Steinzeug", FieldSource.Manual, userEdited: true);

        var position = Assert.Single(PlanMitHaltung(record).Positionen);

        // Die TID der Haltung, nicht die des Kanals — sonst landet Material an einem
        // Objekt, dessen Klasse das Feld gar nicht kennt.
        Assert.Equal("ch010wcsHA000001", position.KanalschadenTid);
        var feld = Assert.Single(position.Felder);
        Assert.Equal("Material", feld.Name);
        Assert.Equal("unbekannt", feld.Alt);
        Assert.Equal("Steinzeug", feld.Neu);
    }

    [Fact]
    public void Ein_handgesetzter_Durchmesser_geht_als_Lichte_Hoehe_in_Millimeter()
    {
        var record = Haltung("80638-80631");
        record.SetFieldValue(FieldKeys.NominalDiameterMm, "300", FieldSource.Manual, userEdited: true);

        var position = Assert.Single(PlanMitHaltung(record).Positionen);
        var feld = Assert.Single(position.Felder);

        // DOMAIN Lichte_Hoehe = 0 .. 99999 [Units.mm] laut SIA405_Abwasser_2020_2_d_LV95.
        Assert.Equal("Lichte_Hoehe", feld.Name);
        Assert.Equal("300", feld.Neu);
    }

    [Fact]
    public void Kanal_und_Haltung_ergeben_zwei_getrennte_Positionen()
    {
        var record = Haltung("80638-80631");
        record.SetFieldValue(FieldKeys.UsageType, "Mischabwasser", FieldSource.Manual, userEdited: true);
        record.SetFieldValue(FieldKeys.PipeMaterial, "Steinzeug", FieldSource.Manual, userEdited: true);

        var positionen = PlanMitHaltung(record).Positionen;

        Assert.Equal(2, positionen.Count);
        Assert.Equal(
            new[] { "ch010wcsHA000001", "ch010wcsKA000001" },
            positionen.Select(p => p.KanalschadenTid).OrderBy(t => t, StringComparer.Ordinal));
    }

    [Fact]
    public void Ein_nur_importiertes_Material_kommt_nicht_in_den_Plan()
    {
        var record = Haltung("80638-80631");
        record.SetFieldValue(FieldKeys.PipeMaterial, "Steinzeug", FieldSource.Xtf, userEdited: false);

        Assert.Empty(PlanMitHaltung(record).Positionen);
    }

    [Fact]
    public void Ein_Material_ohne_belegte_2015_Schreibweise_wird_gemeldet_statt_geschrieben()
    {
        var record = Haltung("80638-80631");
        record.SetFieldValue(FieldKeys.PipeMaterial, "Normalbeton", FieldSource.Manual, userEdited: true);

        var plan = XtfStammdatenPlanBuilder.Build(
            new[] { record },
            XtfStammdatenElementReader.Parse(XDocument.Parse(MitHaltung)),
            "SIA405_ABWASSER_2015_LV95");

        Assert.Empty(plan.Positionen);
        Assert.Contains("Normalbeton", Assert.Single(plan.Hinweise));
    }

    [Theory]
    // 0 heisst in dieser Datei "unbekannt" und ist keine Angabe. Negatives und alles
    // ueber der Modellgrenze 99999 mm ist keine Rohrweite.
    [InlineData("0")]
    [InlineData("-100")]
    [InlineData("100000")]
    [InlineData("keine Ahnung")]
    public void Eine_unbrauchbare_Rohrweite_wird_nicht_geschrieben(string wert)
    {
        var record = Haltung("80638-80631");
        record.SetFieldValue(FieldKeys.NominalDiameterMm, wert, FieldSource.Manual, userEdited: true);

        var plan = PlanMitHaltung(record);

        Assert.Empty(plan.Positionen);
        Assert.Single(plan.Hinweise);
    }

    [Fact]
    public void Eine_bearbeitete_Bemerkung_kommt_in_den_Plan()
    {
        var record = Haltung("80638-80631");
        record.SetFieldValue(
            FieldKeys.Remarks, "Wurzeln bei Meter 12 entfernt", FieldSource.Manual, userEdited: true);

        var position = Assert.Single(Baue(record));

        var feld = Assert.Single(position.Felder);
        Assert.Equal("Bemerkung", feld.Name);
        Assert.Equal("Wurzeln bei Meter 12 entfernt", feld.Neu);
    }

    [Fact]
    public void Eine_nicht_bearbeitete_Bemerkung_bleibt_draussen()
    {
        var record = Haltung("80638-80631");
        record.SetFieldValue(FieldKeys.Remarks, "aus dem Import", FieldSource.Xtf, userEdited: false);

        Assert.Empty(Baue(record));
    }

    [Fact]
    public void Ein_Zeilenumbruch_wird_zu_einem_Leerzeichen()
    {
        // TEXT ist in INTERLIS einzeilig; mehrzeilig waere MTEXT.
        var record = Haltung("80638-80631");
        record.SetFieldValue(
            FieldKeys.Remarks, "Erste Zeile\r\nZweite Zeile", FieldSource.Manual, userEdited: true);

        var feld = Assert.Single(Assert.Single(Baue(record)).Felder);

        Assert.Equal("Erste Zeile Zweite Zeile", feld.Neu);
        Assert.DoesNotContain('\n', feld.Neu!);
    }

    [Fact]
    public void Genau_achtzig_Zeichen_passen_noch()
    {
        var record = Haltung("80638-80631");
        var text = new string('a', 80);
        record.SetFieldValue(FieldKeys.Remarks, text, FieldSource.Manual, userEdited: true);

        var feld = Assert.Single(Assert.Single(Baue(record)).Felder);

        Assert.Equal(text, feld.Neu);
    }

    [Fact]
    public void Eine_zu_lange_Bemerkung_wird_nicht_geschrieben_und_der_Bericht_nennt_die_Zeichenzahl()
    {
        // Das Modell laesst TEXT*80 zu. Kuerzen wuerde Inhalt unsichtbar verlieren.
        var record = Haltung("80638-80631");
        record.SetFieldValue(FieldKeys.Remarks, new string('a', 81), FieldSource.Manual, userEdited: true);

        var plan = Plan(record);

        Assert.Empty(plan.Positionen);
        var hinweis = Assert.Single(plan.Hinweise);
        Assert.Contains("81 Zeichen", hinweis, StringComparison.Ordinal);
        Assert.Contains("80", hinweis, StringComparison.Ordinal);
    }

    [Fact]
    public void Die_Bemerkung_zieht_mehrfache_Leerzeichen_zusammen_und_trimmt()
    {
        Assert.Equal("Riss quer", XtfStammdatenPlanBuilder.AlsBemerkung("  Riss \t\n quer  "));
        Assert.Null(XtfStammdatenPlanBuilder.AlsBemerkung("   "));
        Assert.Null(XtfStammdatenPlanBuilder.AlsBemerkung(null));
    }

    // Die Breite einer Haltung hat in SIA405 kein eigenes Feld: Sie geht als
    // Hoehen-Breiten-Verhaeltnis ans Rohrprofil, das die Haltung verweist.
    [Fact]
    public void Die_Breite_geht_als_Verhaeltnis_ans_Rohrprofil()
    {
        var record = Haltung("80638-80631");
        record.SetFieldValue(FieldKeys.ProfileType, "Rechteckprofil", FieldSource.Manual, userEdited: true);
        record.SetFieldValue(FieldKeys.NominalDiameterMm, "1000", FieldSource.Xtf, userEdited: false);
        record.SetFieldValue(FieldKeys.ClearWidthMm, "600", FieldSource.Manual, userEdited: true);

        var plan = XtfStammdatenPlanBuilder.Build(
            new[] { record },
            XtfStammdatenElementReader.Parse(XDocument.Parse(MitProfil)),
            "SIA405_ABWASSER_2020_LV95");

        var position = Assert.Single(plan.Positionen);
        Assert.Equal("ch010wcsRP000001", position.KanalschadenTid);
        var verhaeltnis = Assert.Single(position.Felder, f => f.Name == "HoehenBreitenverhaeltnis");
        Assert.Null(verhaeltnis.Alt);
        Assert.Equal("1.66667", verhaeltnis.Neu);
        Assert.Equal("Rechteckprofil", Assert.Single(position.Felder, f => f.Name == "Profiltyp").Neu);
    }

    [Fact]
    public void Eine_Breite_am_Kreisprofil_wird_gemeldet_statt_geschrieben()
    {
        var record = Haltung("80638-80631");
        record.SetFieldValue(FieldKeys.NominalDiameterMm, "1000", FieldSource.Xtf, userEdited: false);
        record.SetFieldValue(FieldKeys.ClearWidthMm, "600", FieldSource.Manual, userEdited: true);

        var plan = XtfStammdatenPlanBuilder.Build(
            new[] { record },
            XtfStammdatenElementReader.Parse(XDocument.Parse(MitProfil)),
            "SIA405_ABWASSER_2020_LV95");

        Assert.Empty(plan.Positionen);
        Assert.Contains(plan.Hinweise, h => h.Contains("Kreisprofil", StringComparison.Ordinal));
    }

    [Fact]
    public void Eine_runde_Breite_aendert_nichts()
    {
        var record = Haltung("80638-80631");
        record.SetFieldValue(FieldKeys.NominalDiameterMm, "300", FieldSource.Xtf, userEdited: false);
        record.SetFieldValue(FieldKeys.ClearWidthMm, "300", FieldSource.Manual, userEdited: true);

        var plan = XtfStammdatenPlanBuilder.Build(
            new[] { record },
            XtfStammdatenElementReader.Parse(XDocument.Parse(MitProfil)),
            "SIA405_ABWASSER_2020_LV95");

        Assert.Empty(plan.Positionen);
        Assert.Empty(plan.Hinweise);
    }

    [Theory]
    [InlineData("1000")]
    [InlineData("")]
    public void Der_Wechsel_auf_rund_entfernt_das_alte_Verhaeltnis(string breite)
    {
        var mitVerhaeltnis = MitProfil.Replace(
            "<Profiltyp>Kreisprofil</Profiltyp>",
            "<HoehenBreitenverhaeltnis>1.66667</HoehenBreitenverhaeltnis>\n        <Profiltyp>Rechteckprofil</Profiltyp>",
            StringComparison.Ordinal);
        var record = Haltung("80638-80631");
        record.SetFieldValue(FieldKeys.NominalDiameterMm, "1000", FieldSource.Xtf, userEdited: false);
        record.SetFieldValue(FieldKeys.ClearWidthMm, breite, FieldSource.Manual, userEdited: true);

        var plan = XtfStammdatenPlanBuilder.Build(
            new[] { record },
            XtfStammdatenElementReader.Parse(XDocument.Parse(mitVerhaeltnis)),
            "SIA405_ABWASSER_2020_LV95");

        var position = Assert.Single(plan.Positionen);
        var verhaeltnis = Assert.Single(position.Felder);
        Assert.Equal("HoehenBreitenverhaeltnis", verhaeltnis.Name);
        Assert.Equal("1.66667", verhaeltnis.Alt);
        Assert.Null(verhaeltnis.Neu);
        Assert.Equal(XtfRevisionFeldAktion.Entfernen, verhaeltnis.Aktion);
    }

    [Fact]
    public void Nur_der_Profiltyp_Kreis_mit_zwei_verschiedenen_Massen_wird_gemeldet()
    {
        var mitVerhaeltnis = MitProfil.Replace(
            "<Profiltyp>Kreisprofil</Profiltyp>",
            "<HoehenBreitenverhaeltnis>1.66667</HoehenBreitenverhaeltnis>\n        <Profiltyp>Rechteckprofil</Profiltyp>",
            StringComparison.Ordinal);
        var record = Haltung("80638-80631");
        record.SetFieldValue(FieldKeys.NominalDiameterMm, "1000", FieldSource.Xtf, userEdited: false);
        record.SetFieldValue(FieldKeys.ClearWidthMm, "600", FieldSource.Xtf, userEdited: false);
        record.SetFieldValue(FieldKeys.ProfileType, "Kreisprofil", FieldSource.Manual, userEdited: true);

        var plan = XtfStammdatenPlanBuilder.Build(
            new[] { record },
            XtfStammdatenElementReader.Parse(XDocument.Parse(mitVerhaeltnis)),
            "SIA405_ABWASSER_2020_LV95");

        Assert.Empty(plan.Positionen);
        Assert.Contains(plan.Hinweise, h =>
            h.Contains("Kreisprofil", StringComparison.Ordinal)
            && h.Contains("1000 x 600", StringComparison.Ordinal));
    }

    [Fact]
    public void Ein_Kreisprofil_Masskonflikt_schreibt_keine_halbe_Hoehenaenderung()
    {
        var mitKanalUndVerhaeltnis = MitProfil
            .Replace(
                "      <SIA405_Abwasser.SIA405_Abwasser.Haltung TID=\"ch010wcsHA000001\">",
                """
                      <SIA405_Abwasser.SIA405_Abwasser.Kanal TID="ch010wcsKA000001">
                        <Bezeichnung>80638-80631</Bezeichnung>
                        <Nutzungsart_Ist>Schmutzabwasser</Nutzungsart_Ist>
                      </SIA405_Abwasser.SIA405_Abwasser.Kanal>
                      <SIA405_Abwasser.SIA405_Abwasser.Haltung TID="ch010wcsHA000001">
                """,
                StringComparison.Ordinal)
            .Replace(
                "<Profiltyp>Kreisprofil</Profiltyp>",
                "<HoehenBreitenverhaeltnis>1.66667</HoehenBreitenverhaeltnis>\n        <Profiltyp>Rechteckprofil</Profiltyp>",
                StringComparison.Ordinal);
        var record = Haltung("80638-80631");
        record.SetFieldValue(FieldKeys.UsageType, "Mischabwasser", FieldSource.Manual, userEdited: true);
        record.SetFieldValue(FieldKeys.NominalDiameterMm, "1200", FieldSource.Manual, userEdited: true);
        record.SetFieldValue(FieldKeys.ClearWidthMm, "600", FieldSource.Xtf, userEdited: false);
        record.SetFieldValue(FieldKeys.ProfileType, "Kreisprofil", FieldSource.Manual, userEdited: true);

        var plan = XtfStammdatenPlanBuilder.Build(
            new[] { record },
            XtfStammdatenElementReader.Parse(XDocument.Parse(mitKanalUndVerhaeltnis)),
            "SIA405_ABWASSER_2020_LV95");

        var kanal = Assert.Single(plan.Positionen);
        Assert.Equal("ch010wcsKA000001", kanal.KanalschadenTid);
        Assert.Equal("Mischabwasser", Assert.Single(kanal.Felder, f => f.Name == "Nutzungsart_Ist").Neu);
        Assert.DoesNotContain(plan.Positionen.SelectMany(p => p.Felder), f => f.Name == "Lichte_Hoehe");
        Assert.DoesNotContain(plan.Positionen, p => p.KanalschadenTid == "ch010wcsRP000001");
        Assert.Contains(plan.Hinweise, h =>
            h.Contains("Kreisprofil", StringComparison.Ordinal)
            && h.Contains("1200 x 600", StringComparison.Ordinal));
    }

    [Fact]
    public void Nur_der_Profiltyp_Kreis_entfernt_ein_altes_Verhaeltnis_wenn_die_Masse_nicht_widersprechen()
    {
        var mitVerhaeltnis = MitProfil.Replace(
            "<Profiltyp>Kreisprofil</Profiltyp>",
            "<HoehenBreitenverhaeltnis>1.66667</HoehenBreitenverhaeltnis>\n        <Profiltyp>Rechteckprofil</Profiltyp>",
            StringComparison.Ordinal);
        var record = Haltung("80638-80631");
        record.SetFieldValue(FieldKeys.NominalDiameterMm, "1000", FieldSource.Xtf, userEdited: false);
        record.SetFieldValue(FieldKeys.ClearWidthMm, "", FieldSource.Xtf, userEdited: false);
        record.SetFieldValue(FieldKeys.ProfileType, "Kreisprofil", FieldSource.Manual, userEdited: true);

        var plan = XtfStammdatenPlanBuilder.Build(
            new[] { record },
            XtfStammdatenElementReader.Parse(XDocument.Parse(mitVerhaeltnis)),
            "SIA405_ABWASSER_2020_LV95");

        var position = Assert.Single(plan.Positionen);
        Assert.Equal("ch010wcsRP000001", position.KanalschadenTid);
        Assert.Equal("Kreisprofil", Assert.Single(position.Felder, f => f.Name == "Profiltyp").Neu);
        var loeschung = Assert.Single(position.Felder, f => f.Name == "HoehenBreitenverhaeltnis");
        Assert.Equal(XtfRevisionFeldAktion.Entfernen, loeschung.Aktion);
    }

    [Fact]
    public void Nur_die_Hoehe_mit_unbearbeiteter_leerer_Breite_loescht_das_Verhaeltnis_nicht()
    {
        var mitVerhaeltnis = MitProfil.Replace(
            "<Profiltyp>Kreisprofil</Profiltyp>",
            "<HoehenBreitenverhaeltnis>1.66667</HoehenBreitenverhaeltnis>\n        <Profiltyp>Rechteckprofil</Profiltyp>",
            StringComparison.Ordinal);
        var record = Haltung("80638-80631");
        record.SetFieldValue(FieldKeys.NominalDiameterMm, "1200", FieldSource.Manual, userEdited: true);
        record.SetFieldValue(FieldKeys.ClearWidthMm, "", FieldSource.Xtf, userEdited: false);

        var plan = XtfStammdatenPlanBuilder.Build(
            new[] { record },
            XtfStammdatenElementReader.Parse(XDocument.Parse(mitVerhaeltnis)),
            "SIA405_ABWASSER_2020_LV95");

        Assert.Contains(plan.Positionen, p =>
            p.KanalschadenTid == "ch010wcsHA000001"
            && p.Felder.Any(f => f.Name == "Lichte_Hoehe" && f.Neu == "1200"));
        Assert.DoesNotContain(plan.Positionen, p => p.KanalschadenTid == "ch010wcsRP000001");
    }

    [Fact]
    public void Eine_unlesbare_Breite_loescht_das_alte_Verhaeltnis_nicht()
    {
        var mitVerhaeltnis = MitProfil.Replace(
            "<Profiltyp>Kreisprofil</Profiltyp>",
            "<HoehenBreitenverhaeltnis>1.66667</HoehenBreitenverhaeltnis>\n        <Profiltyp>Rechteckprofil</Profiltyp>",
            StringComparison.Ordinal);
        var record = Haltung("80638-80631");
        record.SetFieldValue(FieldKeys.NominalDiameterMm, "1000", FieldSource.Xtf, userEdited: false);
        record.SetFieldValue(FieldKeys.ClearWidthMm, "unlesbar", FieldSource.Manual, userEdited: true);

        var plan = XtfStammdatenPlanBuilder.Build(
            new[] { record },
            XtfStammdatenElementReader.Parse(XDocument.Parse(mitVerhaeltnis)),
            "SIA405_ABWASSER_2020_LV95");

        Assert.Empty(plan.Positionen);
    }

    [Fact]
    public void Der_Wechsel_auf_rund_aendert_kein_geteiltes_Profil()
    {
        var geteilt = MitProfil
            .Replace(
                "<Profiltyp>Kreisprofil</Profiltyp>",
                "<HoehenBreitenverhaeltnis>1.66667</HoehenBreitenverhaeltnis>\n        <Profiltyp>Rechteckprofil</Profiltyp>",
                StringComparison.Ordinal)
            .Replace(
                "</SIA405_Abwasser.SIA405_Abwasser>",
                """
                  <SIA405_Abwasser.SIA405_Abwasser.Haltung TID="ch010wcsHA000002">
                    <Bezeichnung>80631-80551</Bezeichnung>
                    <RohrprofilRef REF="ch010wcsRP000001" />
                  </SIA405_Abwasser.SIA405_Abwasser.Haltung>
                </SIA405_Abwasser.SIA405_Abwasser>
                """,
                StringComparison.Ordinal);
        var record = Haltung("80638-80631");
        record.SetFieldValue(FieldKeys.NominalDiameterMm, "1000", FieldSource.Xtf, userEdited: false);
        record.SetFieldValue(FieldKeys.ClearWidthMm, "1000", FieldSource.Manual, userEdited: true);

        var plan = XtfStammdatenPlanBuilder.Build(
            new[] { record },
            XtfStammdatenElementReader.Parse(XDocument.Parse(geteilt)),
            "SIA405_ABWASSER_2020_LV95");

        Assert.Empty(plan.Positionen);
        Assert.Contains(plan.Hinweise, h => h.Contains("gemeinsam benutzt", StringComparison.Ordinal));
    }

    [Fact]
    public void Ein_unveraendertes_Verhaeltnis_wird_nicht_erneut_geschrieben()
    {
        var mitVerhaeltnis = MitProfil.Replace(
            "<Profiltyp>Kreisprofil</Profiltyp>",
            "<HoehenBreitenverhaeltnis>1.66667</HoehenBreitenverhaeltnis>\n        <Profiltyp>Rechteckprofil</Profiltyp>",
            StringComparison.Ordinal);
        var record = Haltung("80638-80631");
        record.SetFieldValue(FieldKeys.NominalDiameterMm, "1000", FieldSource.Xtf, userEdited: false);
        record.SetFieldValue(FieldKeys.ClearWidthMm, "600", FieldSource.Manual, userEdited: true);

        var plan = XtfStammdatenPlanBuilder.Build(
            new[] { record },
            XtfStammdatenElementReader.Parse(XDocument.Parse(mitVerhaeltnis)),
            "SIA405_ABWASSER_2020_LV95");

        Assert.Empty(plan.Positionen);
    }

    private static XtfStammdatenPlan PlanMitHaltung(HaltungRecord record)
        => XtfStammdatenPlanBuilder.Build(
            new[] { record },
            XtfStammdatenElementReader.Parse(XDocument.Parse(MitHaltung)),
            "SIA405_ABWASSER_2020_LV95");

    private static XtfStammdatenPlan Plan(HaltungRecord record)
        => XtfStammdatenPlanBuilder.Build(
            new[] { record },
            XtfStammdatenElementReader.Parse(XDocument.Parse(Sec)),
            "SIA405_ABWASSER_2015_LV95");

    private static IReadOnlyList<XtfRevisionPosition> Baue(HaltungRecord record)
        => Plan(record).Positionen;

    private static HaltungRecord Haltung(string name)
    {
        var record = new HaltungRecord();
        record.SetFieldValue(FieldKeys.HoldingName, name, FieldSource.Xtf, userEdited: false);
        return record;
    }
}
