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
        record.SetFieldValue(FieldKeys.Street, "Neue Gasse", FieldSource.Manual, userEdited: true);

        var position = Assert.Single(Baue(record));

        Assert.Equal(2, position.Felder.Count);
        Assert.Contains(position.Felder, f => f.Name == "Standortname" && f.Neu == "Neue Gasse");
    }

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
