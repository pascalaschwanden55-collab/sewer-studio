using System.Xml.Linq;
using AuswertungPro.Next.Infrastructure.Import.Xtf.Sia405;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Die Beziehungsaufloesung des SIA405-Haltungsimports fuer sich: Ein fehlender oder ins
/// Leere zeigender Verweis bleibt als solcher erkennbar und wird nicht durch einen
/// geratenen Wert ersetzt. Grundlage ist die synthetische Referenzdatei aus AP08.
/// </summary>
public sealed class Sia405BeziehungenTests
{
    private static readonly IReadOnlyList<Sia405HaltungMitBezuegen> Haltungen = Sia405Beziehungen.Loese(
        Sia405ObjektLeser.Lies(XDocument.Load(TestRepoPaths.RepoFile("tests", "Fixtures", "XtfReferenz", "sia405-referenz.xtf"))));

    private static Sia405HaltungMitBezuegen Haltung(string tid)
        => Assert.Single(Haltungen, h => h.Haltung.Tid == tid);

    [Fact]
    public void Ein_gueltiger_Verweis_loest_Kanal_Profil_Organisationen_und_Schaechte_auf()
    {
        var h = Haltung("refHALTUNG1");

        Assert.Equal(Sia405KanalBezug.Verweis, h.KanalBezug);
        Assert.Equal("refKANAL1", h.Kanal?.Tid);
        Assert.Equal(new Sia405Rohrprofil("Rechteckprofil", "0.6"), h.Rohrprofil);
        Assert.False(h.RohrprofilVerweisOhneZiel);
        Assert.Equal("Gemeinde Referenz", h.EigentuemerAusVerweis);
        Assert.Equal("Kanton Referenz", h.Datenherr);
        Assert.Equal("Inspektion Muster AG", h.Datenlieferant);
        Assert.Equal("100", h.SchachtOben);   // ueber den Abwasserknoten
        Assert.Equal("101", h.SchachtUnten);  // aus "100-101_nach" und dem Haltungsnamen
    }

    [Fact]
    public void Fehlende_Verweise_bleiben_leer_und_werden_nicht_geraten()
    {
        var h = Haltung("refHALTUNG3");

        Assert.Equal(Sia405KanalBezug.Fehlt, h.KanalBezug);
        Assert.Null(h.Kanal);
        Assert.Null(h.Rohrprofil);
        Assert.True(h.RohrprofilVerweisOhneZiel);
        Assert.Null(h.EigentuemerAusVerweis);
        Assert.Null(h.Datenherr);
        Assert.Null(h.SchachtOben);
        Assert.Null(h.SchachtUnten);
    }

    [Fact]
    public void Ein_Verweis_auf_eine_fehlende_Organisation_bleibt_leer()
    {
        var h = Haltung("refHALTUNG2");

        Assert.Equal("unbekannt", h.Datenherr);
        Assert.Null(h.Datenlieferant);
        Assert.Equal("P-201", h.SchachtOben);
        Assert.Null(h.SchachtUnten);
    }

    [Fact]
    public void Ohne_Verweis_findet_die_Bezeichnung_den_Kanal_und_ohne_Namen_faellt_die_Haltung_weg()
    {
        Assert.Equal(Sia405KanalBezug.Bezeichnung, Haltung("refHALTUNG6").KanalBezug);
        Assert.Equal("103-104", Haltung("refHALTUNG4").Haltungsname);
        Assert.DoesNotContain(Haltungen, h => h.Haltung.Tid == "refHALTUNG5");
    }
}
