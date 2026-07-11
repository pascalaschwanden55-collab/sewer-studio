using AuswertungPro.Next.UI.Mapping;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>Eine Stelle entscheidet, was ab welcher Zoomstufe erscheint (m/px in WebMercator).</summary>
public sealed class KarteZoomStufenPolicyTests
{
    [Fact]
    public void Weit_draussen_ist_nur_das_netz_sichtbar()
    {
        var sicht = KarteZoomStufenPolicy.Fuer(12d, schaechteEingeschaltet: true);
        Assert.False(sicht.SchaechteSichtbar);
        Assert.False(sicht.LabelsSichtbar);
        Assert.False(sicht.SchaedenSichtbar);
        Assert.False(sicht.PfeileSichtbar);
    }

    [Fact]
    public void Mittlerer_zoom_zeigt_schaechte_und_schaeden()
    {
        var sicht = KarteZoomStufenPolicy.Fuer(4d, schaechteEingeschaltet: true);
        Assert.True(sicht.SchaechteSichtbar);
        Assert.True(sicht.SchaedenSichtbar);
        Assert.False(sicht.LabelsSichtbar);
        Assert.False(sicht.PfeileSichtbar);
    }

    [Fact]
    public void Detail_zoom_zeigt_alles()
    {
        var sicht = KarteZoomStufenPolicy.Fuer(2d, schaechteEingeschaltet: true);
        Assert.True(sicht.SchaechteSichtbar);
        Assert.True(sicht.LabelsSichtbar);
        Assert.True(sicht.SchaedenSichtbar);
        Assert.True(sicht.PfeileSichtbar);
    }

    [Fact]
    public void Schaechte_schalter_wirkt_nur_auf_schaechte()
    {
        var sicht = KarteZoomStufenPolicy.Fuer(2d, schaechteEingeschaltet: false);
        Assert.False(sicht.SchaechteSichtbar);
        Assert.True(sicht.LabelsSichtbar);
    }

    [Fact]
    public void Ungueltige_aufloesung_zeigt_nichts_extra()
    {
        var sicht = KarteZoomStufenPolicy.Fuer(0d, schaechteEingeschaltet: true);
        Assert.False(sicht.SchaechteSichtbar);
        Assert.False(sicht.LabelsSichtbar);
        Assert.False(sicht.SchaedenSichtbar);
        Assert.False(sicht.PfeileSichtbar);
    }

    [Fact]
    public void Schacht_schwelle_bleibt_kompatibel_zur_bestehenden_policy()
    {
        // Gleiche Schwelle wie SchachtSichtbarkeitPolicy (5 m/px), damit sich nichts verschiebt.
        Assert.True(KarteZoomStufenPolicy.Fuer(5d, true).SchaechteSichtbar);
        Assert.False(KarteZoomStufenPolicy.Fuer(5.01d, true).SchaechteSichtbar);
    }
}
