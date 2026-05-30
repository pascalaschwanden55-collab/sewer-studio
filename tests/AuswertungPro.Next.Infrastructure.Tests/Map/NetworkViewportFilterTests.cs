using AuswertungPro.Next.Infrastructure.Map;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Map;

public class NetworkViewportFilterTests
{
    [Fact]
    public void FilterByViewport_GibtNurHaltungenImSichtbarenAusschnittZurueck()
    {
        var visible = new ProjectedHaltungGeometry(
            "sichtbar",
            new[] { (10.0, 10.0), (20.0, 20.0) },
            new MapBounds(10, 10, 20, 20));
        var outside = new ProjectedHaltungGeometry(
            "ausserhalb",
            new[] { (100.0, 100.0), (120.0, 120.0) },
            new MapBounds(100, 100, 120, 120));

        var result = NetworkViewportFilter.FilterByViewport(
            new[] { visible, outside },
            new MapBounds(0, 0, 50, 50));

        var item = Assert.Single(result);
        Assert.Equal("sichtbar", item.Haltungsname);
    }

    [Fact]
    public void MapBounds_Grow_DecktKleinesVerschiebenMitAb()
    {
        var loaded = new MapBounds(0, 0, 100, 100).Grow(50, 50);

        Assert.True(loaded.Contains(new MapBounds(20, 20, 120, 120)));
        Assert.False(loaded.Contains(new MapBounds(80, 80, 180, 180)));
    }
}
