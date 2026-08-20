using System.Linq;
using AuswertungPro.Next.Application.Kostenanalyse;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Kostenanalyse;

public sealed class KostenfallAehnlichkeitTests
{
    private static KostenfallMerkmale M(int dn, double laenge, params string[] arten) => new()
    {
        DnMm = dn,
        LaengeM = laenge,
        Schaeden = [.. arten.Select(a => new SchadensMerkmal(a, 1, false))]
    };

    private static Kostenfall F(string name, int dn, params string[] arten) => new()
    {
        Haltung = name,
        Merkmale = M(dn, 40, arten),
        Positionen = [new MassnahmePosition("SCHLAUCHLINER_GFK", 40m, "m")]
    };

    [Fact]
    public void Gleiche_Schadensarten_ergeben_volle_Aehnlichkeit()
    {
        Assert.Equal(1.0, KostenfallAehnlichkeit.SchadensAehnlichkeit(M(300, 40, "BAF", "BAJ"), M(300, 40, "BAJ", "BAF")));
    }

    [Fact]
    public void Teilweise_Ueberschneidung_wird_anteilig_bewertet()
    {
        // gemeinsam {BAF, BAJ} = 2, insgesamt {BAF, BAJ, BBC} = 3
        var wert = KostenfallAehnlichkeit.SchadensAehnlichkeit(M(300, 40, "BAF", "BAJ"), M(300, 40, "BAF", "BAJ", "BBC"));

        Assert.Equal(2d / 3d, wert, 5);
    }

    [Fact]
    public void Ohne_Ueberschneidung_null()
    {
        Assert.Equal(0d, KostenfallAehnlichkeit.SchadensAehnlichkeit(M(300, 40, "BAF"), M(300, 40, "BBC")));
    }

    [Fact]
    public void Der_Durchmesser_Abstand_zaehlt_Katalogstufen()
    {
        Assert.Equal(0, KostenfallAehnlichkeit.DnStufenAbstand(300, 300));
        Assert.Equal(1, KostenfallAehnlichkeit.DnStufenAbstand(250, 300));
        Assert.Equal(2, KostenfallAehnlichkeit.DnStufenAbstand(200, 300));
    }

    [Fact]
    public void Ein_unbekannter_Durchmesser_hat_keinen_Abstand()
    {
        Assert.Null(KostenfallAehnlichkeit.DnStufenAbstand(333, 300));
    }

    [Fact]
    public void Nachbarn_ausserhalb_einer_Durchmesserstufe_fallen_weg()
    {
        var faelle = new[] { F("nah", 250, "BAF"), F("fern", 150, "BAF") };

        var nachbarn = KostenfallAehnlichkeit.FindeNachbarn(M(300, 40, "BAF"), faelle, 7);

        Assert.Equal("nah", Assert.Single(nachbarn).Haltung);
    }

    [Fact]
    public void Die_aehnlichsten_Faelle_stehen_vorn()
    {
        var faelle = new[]
        {
            F("halb", 300, "BAF", "BBC", "BAB"),
            F("genau", 300, "BAF", "BAJ"),
            F("teil", 300, "BAF")
        };

        var nachbarn = KostenfallAehnlichkeit.FindeNachbarn(M(300, 40, "BAF", "BAJ"), faelle, 7);

        Assert.Equal("genau", nachbarn[0].Haltung);
    }

    [Fact]
    public void Mehr_als_das_Maximum_kommt_nicht_zurueck()
    {
        var faelle = Enumerable.Range(0, 10).Select(i => F($"H{i}", 300, "BAF")).ToList();

        Assert.Equal(7, KostenfallAehnlichkeit.FindeNachbarn(M(300, 40, "BAF"), faelle, 7).Count);
    }

    [Fact]
    public void Faelle_ohne_gemeinsame_Schadensart_zaehlen_nicht_als_Nachbarn()
    {
        var faelle = new[] { F("anders", 300, "BBC") };

        Assert.Empty(KostenfallAehnlichkeit.FindeNachbarn(M(300, 40, "BAF"), faelle, 7));
    }

    [Fact]
    public void Bei_gleichem_Rang_entscheidet_die_naehere_Schadenszahl()
    {
        var viele = new Kostenfall
        {
            Haltung = "viele",
            Merkmale = new KostenfallMerkmale
            {
                DnMm = 300, LaengeM = 40,
                Schaeden = [new SchadensMerkmal("BAF", 9, false)]
            },
            Positionen = [new MassnahmePosition("SCHLAUCHLINER_GFK", 40m, "m")]
        };
        var wenige = new Kostenfall
        {
            Haltung = "wenige",
            Merkmale = new KostenfallMerkmale
            {
                DnMm = 300, LaengeM = 40,
                Schaeden = [new SchadensMerkmal("BAF", 2, false)]
            },
            Positionen = [new MassnahmePosition("SCHLAUCHLINER_GFK", 40m, "m")]
        };

        var ziel = new KostenfallMerkmale
        {
            DnMm = 300, LaengeM = 40,
            Schaeden = [new SchadensMerkmal("BAF", 2, false)]
        };

        Assert.Equal("wenige", KostenfallAehnlichkeit.FindeNachbarn(ziel, [viele, wenige], 7)[0].Haltung);
    }
}
