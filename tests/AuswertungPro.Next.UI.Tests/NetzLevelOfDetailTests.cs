using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.UI.Mapping;

namespace AuswertungPro.Next.UI.Tests;

public sealed class NetzLevelOfDetailTests
{
    [Fact]
    public void Thin_gibt_alles_zurueck_wenn_unter_dem_limit()
    {
        var alle = Enumerable.Range(0, 500).ToList();
        var (features, ausgeduennt) = NetzLevelOfDetail.Thin(alle, 8000);
        Assert.False(ausgeduennt);
        Assert.Equal(500, features.Count);
        Assert.Same(alle, features);
    }

    [Fact]
    public void Thin_duennt_grosse_mengen_auf_hoechstens_das_limit_aus()
    {
        var alle = Enumerable.Range(0, 110224).ToList();
        var (features, ausgeduennt) = NetzLevelOfDetail.Thin(alle, 8000);
        Assert.True(ausgeduennt);
        Assert.True(features.Count <= 8000, $"erwartet <= 8000, war {features.Count}");
        // Verteilt ueber den ganzen Bereich (nicht nur die ersten): erstes + spaetes Element dabei.
        Assert.Equal(0, features[0]);
        Assert.Contains(features, v => v > 100000);
    }

    [Fact]
    public void Thin_leere_liste_bleibt_leer()
    {
        var (features, ausgeduennt) = NetzLevelOfDetail.Thin(new List<int>(), 8000);
        Assert.False(ausgeduennt);
        Assert.Empty(features);
    }

    [Fact]
    public void Thin_limit_null_oder_kleiner_gibt_alles_zurueck()
    {
        var alle = Enumerable.Range(0, 50).ToList();
        var (features, ausgeduennt) = NetzLevelOfDetail.Thin(alle, 0);
        Assert.False(ausgeduennt);
        Assert.Equal(50, features.Count);
    }
}
