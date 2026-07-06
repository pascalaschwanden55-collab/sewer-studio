using AuswertungPro.Next.UI.Mapping;

namespace AuswertungPro.Next.UI.Tests;

public sealed class KarteBasemapWahlTests
{
    [Fact]
    public void Naechste_reihum_wenn_alle_verfuegbar()
    {
        Assert.Equal(KarteBasemapAuswahl.AvKarte, KarteBasemapWahl.Naechste(KarteBasemapAuswahl.Satellit, hatSatellit: true, hatAv: true));
        Assert.Equal(KarteBasemapAuswahl.OpenStreetMap, KarteBasemapWahl.Naechste(KarteBasemapAuswahl.AvKarte, hatSatellit: true, hatAv: true));
        Assert.Equal(KarteBasemapAuswahl.Satellit, KarteBasemapWahl.Naechste(KarteBasemapAuswahl.OpenStreetMap, hatSatellit: true, hatAv: true));
    }

    [Fact]
    public void Naechste_ueberspringt_fehlende_offline_karten()
    {
        // Kein AV-Ordner -> von Satellit direkt zu OSM.
        Assert.Equal(KarteBasemapAuswahl.OpenStreetMap, KarteBasemapWahl.Naechste(KarteBasemapAuswahl.Satellit, hatSatellit: true, hatAv: false));
        // Weder Satellit noch AV -> immer OSM.
        Assert.Equal(KarteBasemapAuswahl.OpenStreetMap, KarteBasemapWahl.Naechste(KarteBasemapAuswahl.OpenStreetMap, hatSatellit: false, hatAv: false));
        Assert.Equal(KarteBasemapAuswahl.OpenStreetMap, KarteBasemapWahl.Naechste(KarteBasemapAuswahl.Satellit, hatSatellit: false, hatAv: false));
    }

    [Theory]
    [InlineData(KarteBasemapAuswahl.Satellit, true, true, true)]
    [InlineData(KarteBasemapAuswahl.Satellit, false, true, false)]
    [InlineData(KarteBasemapAuswahl.AvKarte, true, false, false)]
    [InlineData(KarteBasemapAuswahl.OpenStreetMap, false, false, true)]  // OSM immer verfuegbar
    public void IstVerfuegbar_haengt_an_den_ordnern(KarteBasemapAuswahl wahl, bool hatSat, bool hatAv, bool expected)
        => Assert.Equal(expected, KarteBasemapWahl.IstVerfuegbar(wahl, hatSat, hatAv));
}
