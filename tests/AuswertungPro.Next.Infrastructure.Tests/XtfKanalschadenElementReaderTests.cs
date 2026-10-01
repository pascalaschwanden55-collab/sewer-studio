using System.Xml.Linq;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Der Leser holt die Kanalschaden-Elemente aus der Original-XTF. Er ist ausschliesslich
/// lesend — die Datei darf danach byteweise unveraendert sein.
/// </summary>
public sealed class XtfKanalschadenElementReaderTests
{
    private const string Beispiel = """
<?xml version="1.0" encoding="UTF-8"?>
<TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3">
  <HEADERSECTION SENDER="Test" VERSION="2.3">
    <MODELS><MODEL NAME="VSA_KEK_2020_LV95" /></MODELS>
  </HEADERSECTION>
  <DATASECTION>
    <VSA_KEK_2020_LV95.KEK BID="B1">
      <VSA_KEK_2020_LV95.KEK.Untersuchung TID="ch100000004EB182">
        <Bezeichnung>59220-10.1036545</Bezeichnung>
      </VSA_KEK_2020_LV95.KEK.Untersuchung>
      <VSA_KEK_2020_LV95.KEK.Kanalschaden TID="ch100000004EB1AB">
        <UntersuchungRef REF="ch100000004EB182" />
        <KanalSchadencode>BCD</KanalSchadencode>
        <Distanz>0.00</Distanz>
        <Videozaehlerstand>00:00:15:00</Videozaehlerstand>
      </VSA_KEK_2020_LV95.KEK.Kanalschaden>
      <VSA_KEK_2020_LV95.KEK.Kanalschaden TID="ch100000004EB1AC">
        <UntersuchungRef REF="ch100000004EB182" />
        <KanalSchadencode>BAF</KanalSchadencode>
        <Distanz>12.34</Distanz>
      </VSA_KEK_2020_LV95.KEK.Kanalschaden>
    </VSA_KEK_2020_LV95.KEK>
  </DATASECTION>
</TRANSFER>
""";

    [Fact]
    public void Liest_alle_Kanalschaeden_mit_ihren_Angaben()
    {
        var elemente = XtfKanalschadenElementReader.Parse(XDocument.Parse(Beispiel));

        Assert.Equal(2, elemente.Count);

        var erster = elemente[0];
        Assert.Equal("ch100000004EB1AB", erster.KanalschadenTid);
        Assert.Equal("ch100000004EB182", erster.UntersuchungTid);
        Assert.Equal("59220-10.1036545", erster.HaltungBezeichnung);
        Assert.Equal("BCD", erster.Code);
        Assert.Equal(0.00, erster.Distanz);
        Assert.Equal("00:00:15:00", erster.Videozaehlerstand);
    }

    [Fact]
    public void Traegt_die_Haltungsbezeichnung_aus_der_Untersuchung_nach()
    {
        var elemente = XtfKanalschadenElementReader.Parse(XDocument.Parse(Beispiel));

        Assert.All(elemente, e => Assert.Equal("59220-10.1036545", e.HaltungBezeichnung));
    }

    [Fact]
    public void Ein_fehlender_Videozaehlerstand_bleibt_leer_statt_erfunden()
    {
        var elemente = XtfKanalschadenElementReader.Parse(XDocument.Parse(Beispiel));

        Assert.Null(elemente[1].Videozaehlerstand);
        Assert.Equal(12.34, elemente[1].Distanz);
    }

    [Fact]
    public void Eine_fehlende_Datei_liefert_eine_leere_Liste_statt_zu_werfen()
    {
        var pfad = Path.Combine(Path.GetTempPath(), $"gibt-es-nicht-{Guid.NewGuid():N}.xtf");

        Assert.Empty(XtfKanalschadenElementReader.Read(pfad));
    }

    [Fact]
    public void Die_Originaldatei_bleibt_nach_dem_Lesen_bytegleich()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"xtf-leser-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var pfad = Path.Combine(dir, "original.xtf");
        File.WriteAllText(pfad, Beispiel);
        var vorher = File.ReadAllBytes(pfad);
        var zeitVorher = File.GetLastWriteTimeUtc(pfad);

        try
        {
            Assert.Equal(2, XtfKanalschadenElementReader.Read(pfad).Count);

            Assert.Equal(vorher, File.ReadAllBytes(pfad));
            Assert.Equal(zeitVorher, File.GetLastWriteTimeUtc(pfad));
        }
        finally { try { Directory.Delete(dir, recursive: true); } catch { } }
    }

    [Fact]
    public void Ein_Kanalschaden_ohne_Kennung_wird_uebersprungen()
    {
        var ohneTid = Beispiel.Replace(" TID=\"ch100000004EB1AC\"", "", StringComparison.Ordinal);

        var elemente = XtfKanalschadenElementReader.Parse(XDocument.Parse(ohneTid));

        Assert.Equal("ch100000004EB1AB", Assert.Single(elemente).KanalschadenTid);
    }
}
