using AuswertungPro.Next.UI.Mapping;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>Nennweite -> Linienbreite auf der Karte (dickere Rohre = dickere Linien).</summary>
public sealed class DnLineWidthMapperTests
{
    [Fact]
    public void Hausanschluss_ist_duenn()
    {
        Assert.Equal(2.5, DnLineWidthMapper.Breite(150));
        Assert.Equal(2.5, DnLineWidthMapper.Breite(249));
    }

    [Fact]
    public void Standardrohr_ist_mittel()
    {
        Assert.Equal(3.5, DnLineWidthMapper.Breite(250));
        Assert.Equal(3.5, DnLineWidthMapper.Breite(300));
        Assert.Equal(3.5, DnLineWidthMapper.Breite(399));
    }

    [Fact]
    public void Grosse_rohre_sind_dick()
    {
        Assert.Equal(4.5, DnLineWidthMapper.Breite(400));
        Assert.Equal(4.5, DnLineWidthMapper.Breite(699));
        Assert.Equal(6.0, DnLineWidthMapper.Breite(700));
        Assert.Equal(6.0, DnLineWidthMapper.Breite(1200));
    }

    [Fact]
    public void Unbekannte_nennweite_bleibt_heutige_breite()
    {
        Assert.Equal(4.0, DnLineWidthMapper.Breite(null));
        Assert.Equal(4.0, DnLineWidthMapper.Breite(0));
        Assert.Equal(4.0, DnLineWidthMapper.Breite(-50));
    }
}
