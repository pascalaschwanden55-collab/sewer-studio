using System.Xml.Linq;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Lookup;
using static AuswertungPro.Next.Infrastructure.Tests.XtfDssExportTests;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class XtfDssGeometrieTests
{
    private const string Ns = "http://www.interlis.ch/INTERLIS2.3";
    private const string Verlauf = "<Verlauf xmlns='http://www.interlis.ch/INTERLIS2.3'><POLYLINE><COORD><C1>2680000.000</C1><C2>1200000.000</C2></COORD><ARC><C1>2680020.000</C1><C2>1200000.000</C2><A1>2680010.000</A1><A2>1200005.000</A2></ARC></POLYLINE></Verlauf>";
    [Fact]
    public void Gegenrichtung_dreht_Punkte_Koten_und_Kreisbogen_zusammen()
    {
        var p = Projekt(); var h = p.Data[0];
        h.Geonis!.RichtungGedreht = true; h.Geonis.VonPunkt = Nach; h.Geonis.NachPunkt = Von;
        h.SetFieldValue(FieldKeys.HoldingName, "B-A", FieldSource.Manual, true);
        p.Objektakten[0].Quellen.Single(q => q.Klasse == "Haltung").Strukturen["Verlauf"] = Verlauf;
        p.Objektakten[0].Werte["haltung.fromlevel"] = new() { Text = "449.123", VonHand = true };
        WithExport(p, doc =>
        {
            XNamespace ns = Ns;
            var o = Objekt(doc, "Haltung");
            Assert.Equal(Nach, o.Element(ns + "vonHaltungspunktRef")!.Attribute("REF")!.Value);
            Assert.Equal("449.123", Objekt(doc, "Haltungspunkt", Nach).Element(ns + "Kote")!.Value);
            var line = o.Element(ns + "Verlauf")!.Element(ns + "POLYLINE")!;
            Assert.Equal("2680020.000", line.Element(ns + "COORD")!.Element(ns + "C1")!.Value);
            Assert.Equal("2680000.000", line.Element(ns + "ARC")!.Element(ns + "C1")!.Value);
            Assert.Equal("2680010.000", line.Element(ns + "ARC")!.Element(ns + "A1")!.Value);
        });
    }
    [Fact]
    public void GeoShop_Leser_bewahrt_Kreisbogen_fuer_spaetere_Neu_Lieferung()
    {
        var p = Projekt();
        p.Objektakten[0].Quellen.Single(q => q.Klasse == "Haltung").Strukturen["Verlauf"] = Verlauf;
        WithExport(p, doc =>
        {
            var path = Path.Combine(Path.GetTempPath(), "SewerStudio-Dss-Leser-" + Guid.NewGuid() + ".xtf");
            try
            {
                doc.Save(path);
                var bestand = new GeoShopXtfLeser().Lies(path, BauteilArt.Haltung, ["A-B"]);
                var raw = bestand.Bauteile.Single().Quellen!.Single(q => q.Klasse == "Haltung").Strukturen["Verlauf"];
                Assert.True(XNode.DeepEquals(XElement.Parse(Verlauf), XElement.Parse(raw)));
            }
            finally { File.Delete(path); }
        });
    }
    [Fact]
    public void Eine_geaenderte_Koordinate_bewahrt_die_andere_aus_dem_Import()
    {
        var p = XtfDssVerbundTests.Verbund();
        var deckel = p.Objektakten.Single(a => a.Art == "deckel");
        deckel.Quellen[0].Strukturen["Lage"] = "<Lage xmlns='" + Ns + "'><COORD><C1>2680000.000</C1><C2>1200000.000</C2></COORD></Lage>";
        deckel.Werte["deckel.rechtswert"] = new() { Text = "2680001,125", VonHand = true };
        WithExport(p, doc =>
        {
            XNamespace ns = Ns;
            var coord = Objekt(doc, "Deckel").Element(ns + "Lage")!.Element(ns + "COORD")!;
            Assert.Equal("2680001.125", coord.Element(ns + "C1")!.Value);
            Assert.Equal("1200000.000", coord.Element(ns + "C2")!.Value);
        });
    }
}
