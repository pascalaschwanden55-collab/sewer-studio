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
[Fact]
    public void Eine_zweiteilige_Parzelle_verliert_ihren_zweiten_Teil_nicht()
    {
        var xml = """
            <wfs:FeatureCollection xmlns:wfs="http://www.opengis.net/wfs/2.0"
                                   xmlns:gml="http://www.opengis.net/gml/3.2"
                                   xmlns:av="http://geo.ur.ch/av">
              <wfs:member>
                <av:ch059_liegenschaften_flaechen>
                  <av:nummer>500</av:nummer>
                  <av:bfsnr>1206</av:bfsnr>
                  <av:wkb_geometry>
                    <gml:MultiSurface>
                      <gml:surfaceMember><gml:Polygon><gml:exterior><gml:LinearRing>
                        <gml:posList>0 0 10 0 10 10 0 0</gml:posList>
                      </gml:LinearRing></gml:exterior></gml:Polygon></gml:surfaceMember>
                      <gml:surfaceMember><gml:Polygon><gml:exterior><gml:LinearRing>
                        <gml:posList>20 20 30 20 30 30 20 20</gml:posList>
                      </gml:LinearRing></gml:exterior></gml:Polygon></gml:surfaceMember>
                    </gml:MultiSurface>
                  </av:wkb_geometry>
                </av:ch059_liegenschaften_flaechen>
              </wfs:member>
            </wfs:FeatureCollection>
            """;

        var parzelle = Assert.Single(ParcelWfsXmlParser.Parse(xml));

        Assert.Equal(
            "MULTIPOLYGON(((0 0,10 0,10 10,0 0)),((20 20,30 20,30 30,20 20)))",
            parzelle.OutlineWkt);
    }

    [Fact]
    public void Eine_zweiteilige_Leitung_wird_zur_Mehrfachlinie()
    {
        var xml = """
            <wfs:FeatureCollection xmlns:wfs="http://www.opengis.net/wfs/2.0"
                                   xmlns:gml="http://www.opengis.net/gml/3.2"
                                   xmlns:leitungen="http://geo.ur.ch/leitungen">
              <wfs:member>
                <leitungen:abw_haltungen>
                  <leitungen:ne_bezeichnung>A-B</leitungen:ne_bezeichnung>
                  <leitungen:org_eigentuemer>Privat</leitungen:org_eigentuemer>
                  <leitungen:wkb_geometry>
                    <gml:MultiCurve>
                      <gml:curveMember><gml:LineString>
                        <gml:posList>0 0 1 1</gml:posList>
                      </gml:LineString></gml:curveMember>
                      <gml:curveMember><gml:LineString>
                        <gml:posList>5 5 6 6</gml:posList>
                      </gml:LineString></gml:curveMember>
                    </gml:MultiCurve>
                  </leitungen:wkb_geometry>
                </leitungen:abw_haltungen>
              </wfs:member>
            </wfs:FeatureCollection>
            """;

        var haltung = Assert.Single(SewerNetworkWfsXmlParser.Parse(xml));

        Assert.Equal("MULTILINESTRING((0 0,1 1),(5 5,6 6))", haltung.GeometryWkt);
    }

    [Fact]
    public void Datenmuell_in_den_Koordinaten_ergibt_keine_Geometrie()
    {
        var xml = """
            <wfs:FeatureCollection xmlns:wfs="http://www.opengis.net/wfs/2.0"
                                   xmlns:gml="http://www.opengis.net/gml/3.2"
                                   xmlns:leitungen="http://geo.ur.ch/leitungen">
              <wfs:member>
                <leitungen:abw_haltungen>
                  <leitungen:ne_bezeichnung>A-B</leitungen:ne_bezeichnung>
                  <leitungen:wkb_geometry><gml:MultiCurve><gml:curveMember><gml:LineString>
                    <gml:posList>0 0 spam eggs</gml:posList>
                  </gml:LineString></gml:curveMember></gml:MultiCurve></leitungen:wkb_geometry>
                </leitungen:abw_haltungen>
              </wfs:member>
            </wfs:FeatureCollection>
            """;

        var haltung = Assert.Single(SewerNetworkWfsXmlParser.Parse(xml));

        // Die Angaben bleiben nutzbar, die Geometrie gilt als nicht lesbar.
        Assert.Equal("A-B", haltung.Designation);
        Assert.Equal("", haltung.GeometryWkt);
    }

    [Fact]
    public void Ein_Loch_in_der_Parzelle_wird_zur_Aussparung_nicht_zur_zweiten_Flaeche()
    {
        var xml = """
            <wfs:FeatureCollection xmlns:wfs="http://www.opengis.net/wfs/2.0"
                                   xmlns:gml="http://www.opengis.net/gml/3.2"
                                   xmlns:av="http://geo.ur.ch/av">
              <wfs:member>
                <av:ch059_liegenschaften_flaechen>
                  <av:nummer>905</av:nummer>
                  <av:wkb_geometry><gml:MultiSurface><gml:surfaceMember><gml:Polygon>
                    <gml:exterior><gml:LinearRing>
                      <gml:posList>0 0 100 0 100 100 0 100 0 0</gml:posList>
                    </gml:LinearRing></gml:exterior>
                    <gml:interior><gml:LinearRing>
                      <gml:posList>40 40 60 40 60 60 40 60 40 40</gml:posList>
                    </gml:LinearRing></gml:interior>
                  </gml:Polygon></gml:surfaceMember></gml:MultiSurface></av:wkb_geometry>
                </av:ch059_liegenschaften_flaechen>
              </wfs:member>
            </wfs:FeatureCollection>
            """;

        var parzelle = Assert.Single(ParcelWfsXmlParser.Parse(xml));

        Assert.Equal(
            "POLYGON((0 0,100 0,100 100,0 100,0 0),(40 40,60 40,60 60,40 60,40 40))",
            parzelle.OutlineWkt);
    }

    [Fact]
    public void Eine_Flaeche_aus_zwei_Punkten_ist_kein_Ring()
    {
        var xml = """
            <wfs:FeatureCollection xmlns:wfs="http://www.opengis.net/wfs/2.0"
                                   xmlns:gml="http://www.opengis.net/gml/3.2"
                                   xmlns:av="http://geo.ur.ch/av">
              <wfs:member>
                <av:ch059_liegenschaften_flaechen>
                  <av:nummer>501</av:nummer>
                  <av:wkb_geometry><gml:MultiSurface><gml:surfaceMember><gml:Polygon>
                    <gml:exterior><gml:LinearRing>
                      <gml:posList>0 0 10 10</gml:posList>
                    </gml:LinearRing></gml:exterior>
                  </gml:Polygon></gml:surfaceMember></gml:MultiSurface></av:wkb_geometry>
                </av:ch059_liegenschaften_flaechen>
              </wfs:member>
            </wfs:FeatureCollection>
            """;

        Assert.Equal("", Assert.Single(ParcelWfsXmlParser.Parse(xml)).OutlineWkt);
    }
}
