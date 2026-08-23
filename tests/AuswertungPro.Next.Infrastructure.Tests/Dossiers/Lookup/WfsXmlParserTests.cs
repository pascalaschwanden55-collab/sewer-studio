using System;
using System.IO;
using System.Linq;

using AuswertungPro.Next.Infrastructure.Dossiers.Lookup;

using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Dossiers.Lookup;

public sealed class WfsXmlParserTests
{
    private static string Lade(string dateiname)
        => File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory, "Fixtures", "DossierLookup", dateiname));

    [Fact]
    public void Liest_die_Parzelle_mit_Umriss()
    {
        var parzellen = ParcelWfsXmlParser.Parse(Lade("wfs_parzelle.xml"));

        var parzelle = Assert.Single(parzellen);
        Assert.Equal("439", parzelle.Number);
        Assert.Equal(1206, parzelle.BfsNr);
        Assert.Equal("Musterdorf", parzelle.Municipality);
        Assert.Equal(1139, parzelle.AreaSqm);
        Assert.Equal("CH114627077847", parzelle.Egrid);
        Assert.Contains("grundbuchauskunft", parzelle.LandRegistryUrl, StringComparison.Ordinal);

        // Der Umriss wird als WKT gebraucht, weil die raeumliche Suche ihn so erwartet.
        Assert.StartsWith("POLYGON((2692400.5 1185800.25,", parzelle.OutlineWkt, StringComparison.Ordinal);
        Assert.EndsWith("))", parzelle.OutlineWkt, StringComparison.Ordinal);
    }

    [Fact]
    public void Liest_die_Haltungen_mit_Eigentuemer_und_Linie()
    {
        var haltungen = SewerNetworkWfsXmlParser.Parse(Lade("wfs_haltungen.xml"));

        Assert.Equal(2, haltungen.Count);

        Assert.Equal("36051-36329", haltungen[0].Designation);
        Assert.Equal(11.46, haltungen[0].LengthMeters);
        Assert.True(haltungen[0].IsPrivate);
        Assert.Equal("LINESTRING(2692462.471 1185860.503,2692458.291 1185862.403)", haltungen[0].GeometryWkt);

        Assert.Equal("Abwasser Uri", haltungen[1].Owner);
        Assert.False(haltungen[1].IsPrivate);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("kein XML")]
    [InlineData("<html><body>Fehler</body></html>")]
    public void Unlesbares_ergibt_eine_leere_Liste_statt_eines_Absturzes(string? xml)
    {
        Assert.Empty(ParcelWfsXmlParser.Parse(xml));
        Assert.Empty(SewerNetworkWfsXmlParser.Parse(xml));
    }
}
