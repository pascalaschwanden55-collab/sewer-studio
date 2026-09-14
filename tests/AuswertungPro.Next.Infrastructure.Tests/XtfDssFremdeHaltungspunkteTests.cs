using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
using static AuswertungPro.Next.Infrastructure.Tests.XtfDssExportTests;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Buerglen 14.09.2026: GeoShop fuehrt am Knoten 80475 zwei Haltungspunkte «A76157». Einer gehoert
/// zur Projekthaltung, der andere zu einer Hausanschlussleitung ausserhalb des Projekts. Ein Punkt
/// einer fremden Leitung ist kein Teil der Lieferung, solange er keine eigenen Eingaben traegt.
/// </summary>
public sealed class XtfDssFremdeHaltungspunkteTests
{
    private const string Fremd = "chTEST0000000031";

    private static (Project Projekt, ObjektAkte Akte) MitFremdemPunkt(string bezeichnung)
    {
        var p = XtfDssVerbundTests.Verbund();
        var s = p.SchaechteData[0];
        var quelle = Quelle("Haltungspunkt", Fremd, new() { ["Bezeichnung"] = bezeichnung, ["Kote"] = "448.500" },
            new() { ["AbwassernetzelementRef"] = s.Geonis!.Knoten! });
        p.Objektakten.Single(a => a.Id == s.Id).Quellen.Add(quelle);
        // So legt GeoShopHaltungspunktImport die Akte an: nur am Schacht, Anzeigen ohne Handmarke.
        var akte = new ObjektAkte { Art = "haltungspunkt", Bezuege = [s.Id], Quellen = [quelle] };
        akte.Werte["haltungspunkt.bezeichnung"] = new() { Text = bezeichnung };
        akte.Werte["haltungspunkt.hoehe"] = new() { Text = "448.500" };
        p.Objektakten.Add(akte);
        return (p, akte);
    }

    [Fact]
    public void Fremder_Punkt_ohne_eigene_Eingabe_wird_nicht_geliefert_und_sein_Namenskonflikt_sperrt_nicht()
    {
        var (p, _) = MitFremdemPunkt("A-B_von"); // gleicher Name wie der eigene Anfangspunkt
        foreach (var delta in new[] { false, true })
        {
            var r = new XtfNeuExportService().Erzeuge(new(p, "", NurPruefen: true, NurAenderungen: delta));
            Assert.True(r.Ok, r.Fehler);
            Assert.Contains("fremder Leitungen", r.Bericht);
        }
        WithExport(p, doc =>
        {
            Assert.Equal(2, doc.Descendants().Count(e => e.Name.LocalName.EndsWith(".Haltungspunkt", StringComparison.Ordinal)));
            Assert.DoesNotContain(doc.Descendants(), e => e.Attribute("TID")?.Value == Fremd);
        });
    }

    [Fact]
    public void Fremder_Punkt_mit_eigener_Eingabe_wird_mit_Originalkennung_geliefert()
    {
        var (p, akte) = MitFremdemPunkt("Zulauf X");
        akte.Werte["haltungspunkt.auslaufform"] = new() { Text = "Scharfkantig", VonHand = true };
        WithExport(p, doc =>
        {
            Assert.Equal(3, doc.Descendants().Count(e => e.Name.LocalName.EndsWith(".Haltungspunkt", StringComparison.Ordinal)));
            Assert.Equal("scharfkantig", Objekt(doc, "Haltungspunkt", Fremd).Elements().Single(e => e.Name.LocalName == "Auslaufform").Value);
        });
    }

    [Fact]
    public void Fremder_Punkt_mit_eigener_Eingabe_und_Namenskonflikt_bleibt_gesperrt()
    {
        var (p, akte) = MitFremdemPunkt("A-B_von");
        akte.Werte["haltungspunkt.auslaufform"] = new() { Text = "Scharfkantig", VonHand = true };
        var r = new XtfNeuExportService().Erzeuge(new(p, "", NurPruefen: true));
        Assert.False(r.Ok); Assert.Null(r.Datei);
        Assert.Contains(Fremd, r.Fehler); Assert.Contains(Von, r.Fehler);
    }
}
