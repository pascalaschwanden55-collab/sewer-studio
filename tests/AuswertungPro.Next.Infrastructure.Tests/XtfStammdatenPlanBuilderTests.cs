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

    private static IReadOnlyList<XtfRevisionPosition> Baue(HaltungRecord record)
        => XtfStammdatenPlanBuilder.Build(
            new[] { record },
            XtfStammdatenElementReader.Parse(XDocument.Parse(Sec)));

    private static HaltungRecord Haltung(string name)
    {
        var record = new HaltungRecord();
        record.SetFieldValue(FieldKeys.HoldingName, name, FieldSource.Xtf, userEdited: false);
        return record;
    }
}
