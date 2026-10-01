using System;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Infrastructure.Map;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Der Kataster fuehrt die Fachdaten am Normschacht, die Koordinaten aber am
/// gleichnamigen Abwasserknoten. Beides muss zusammenfinden — und die Lage
/// eines Deckels darf dabei nie als Schachtlage durchgehen.
/// </summary>
public sealed class SchachtCadastreTableFileTests
{
    private const string XtfAusschnitt = """
<?xml version="1.0" encoding="UTF-8"?>
<TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3">
<DATASECTION>
<SIA405_ABWASSER_2020_LV95.SIA405_Abwasser BID="chB0000000000001">
<SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Normschacht TID="ch1000a00000c3e1">
<Bezeichnung>80401</Bezeichnung>
<Funktion>Kontroll_Einsteigschacht</Funktion>
<Material>Beton</Material>
<Dimension1>1000</Dimension1>
<Dimension2>1000</Dimension2>
<Status>in_Betrieb</Status>
</SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Normschacht>
<SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Abwasserknoten TID="ch1000b00000c3e1">
<Bezeichnung>80401</Bezeichnung>
<AbwasserbauwerkRef REF="ch1000a00000c3e1" />
<Lage><COORD><C1>2692606.892</C1><C2>1192380.717</C2></COORD></Lage>
</SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Abwasserknoten>
<SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Deckel TID="ch1000c00000c3e1">
<Bezeichnung>DE_80401</Bezeichnung>
<Lage><COORD><C1>9999999.999</C1><C2>8888888.888</C2></COORD></Lage>
</SIA405_ABWASSER_2020_LV95.SIA405_Abwasser.Deckel>
</SIA405_ABWASSER_2020_LV95.SIA405_Abwasser>
</DATASECTION>
</TRANSFER>
""";

    [Fact]
    public void Extract_LiestFachdatenUndLageDesselbenSchachts()
    {
        var xtf = Path.Combine(Path.GetTempPath(), $"schacht_{Guid.NewGuid():N}.xtf");
        File.WriteAllText(xtf, XtfAusschnitt);
        try
        {
            var store = new SchachtCadastreTableFileStore();

            var schacht = store.Extract(xtf).Single();

            Assert.Equal("80401", schacht.Bezeichnung);
            Assert.Equal("Kontroll_Einsteigschacht", schacht.Funktion);
            Assert.Equal("Beton", schacht.Material);
            Assert.Equal("in_Betrieb", schacht.Status);
            Assert.Equal(2692606.892, schacht.Ost!.Value, 3);
            Assert.Equal(1192380.717, schacht.Nord!.Value, 3);
        }
        finally { File.Delete(xtf); }
    }

    [Fact]
    public void Extract_UebernimmtNiemalsDieLageDesDeckels()
    {
        var xtf = Path.Combine(Path.GetTempPath(), $"schacht_{Guid.NewGuid():N}.xtf");
        File.WriteAllText(xtf, XtfAusschnitt);
        try
        {
            var schacht = new SchachtCadastreTableFileStore().Extract(xtf).Single();

            // Der Deckel traegt eine andere, absichtlich auffaellige Lage.
            Assert.NotEqual(9999999.999, schacht.Ost!.Value, 3);
        }
        finally { File.Delete(xtf); }
    }

    [Fact]
    public void BuildTable_UndReadTable_LiefernDenselbenStand()
    {
        var xtf = Path.Combine(Path.GetTempPath(), $"schacht_{Guid.NewGuid():N}.xtf");
        var tabelle = Path.Combine(Path.GetTempPath(), $"schacht_{Guid.NewGuid():N}.tsv");
        File.WriteAllText(xtf, XtfAusschnitt);
        try
        {
            var store = new SchachtCadastreTableFileStore();

            var anzahl = store.BuildTable(xtf, tabelle);
            var gelesen = store.ReadTable(tabelle);

            Assert.Equal(1, anzahl);
            Assert.Equal("80401", gelesen.Single().Bezeichnung);
            Assert.Equal("Kontroll_Einsteigschacht", gelesen.Single().Funktion);
            Assert.Equal(2692606.892, gelesen.Single().Ost!.Value, 3);
            Assert.True(store.IsTableFresh(tabelle, xtf));
        }
        finally { File.Delete(xtf); File.Delete(tabelle); }
    }

    [Fact]
    public void IsTableFresh_ErkenntEineGeaenderteQuelle()
    {
        var xtf = Path.Combine(Path.GetTempPath(), $"schacht_{Guid.NewGuid():N}.xtf");
        var tabelle = Path.Combine(Path.GetTempPath(), $"schacht_{Guid.NewGuid():N}.tsv");
        File.WriteAllText(xtf, XtfAusschnitt);
        try
        {
            var store = new SchachtCadastreTableFileStore();
            store.BuildTable(xtf, tabelle);

            File.WriteAllText(xtf, XtfAusschnitt + "<!-- geaendert -->");

            Assert.False(store.IsTableFresh(tabelle, xtf));
        }
        finally { File.Delete(xtf); File.Delete(tabelle); }
    }
}
