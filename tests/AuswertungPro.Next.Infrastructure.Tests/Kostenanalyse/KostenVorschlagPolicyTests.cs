using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Kostenanalyse;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Kostenanalyse;

public sealed class KostenVorschlagPolicyTests
{
    private static KostenfallMerkmale Ziel(int dn = 300, int boegen = 0) => new()
    {
        DnMm = dn,
        LaengeM = 40,
        BogenAnzahl = boegen,
        Schaeden = [new SchadensMerkmal("BAF", 1, false)]
    };

    private static Kostenfall Fall(string name, int dn = 300, int boegen = 0) => new()
    {
        Haltung = name,
        Merkmale = new KostenfallMerkmale
        {
            DnMm = dn,
            LaengeM = 40,
            BogenAnzahl = boegen,
            Schaeden = [new SchadensMerkmal("BAF", 1, false)]
        },
        Positionen = [new MassnahmePosition("SCHLAUCHLINER_GFK", 40m, "m")]
    };

    private static IReadOnlyList<Kostenfall> Faelle(int anzahl, int dn = 300, int boegen = 0)
        => Enumerable.Range(0, anzahl).Select(i => Fall($"H{i:D2}", dn, boegen)).ToList();

    [Fact]
    public void Genug_aehnliche_Faelle_ergeben_einen_Vorschlag()
    {
        var vorschlag = KostenVorschlagPolicy.Schlage(Ziel(), Faelle(5));

        Assert.False(vorschlag.IstEnthaltung);
        Assert.Equal(5, vorschlag.HerangezogeneFaelle);
        Assert.Equal("SCHLAUCHLINER_GFK", Assert.Single(vorschlag.Positionen).ItemKey);
    }

    [Fact]
    public void Weniger_als_drei_Faelle_ergeben_eine_Enthaltung()
    {
        var vorschlag = KostenVorschlagPolicy.Schlage(Ziel(), Faelle(2));

        Assert.True(vorschlag.IstEnthaltung);
        Assert.Equal(EnthaltungsGrund.ZuWenigeFaelle, vorschlag.Grund);
        Assert.Contains("2", vorschlag.GrundText);
    }

    [Fact]
    public void Ein_unbekannter_Durchmesser_ergibt_eine_Enthaltung()
    {
        var vorschlag = KostenVorschlagPolicy.Schlage(Ziel(dn: 333), Faelle(5));

        Assert.Equal(EnthaltungsGrund.DurchmesserUnbekannt, vorschlag.Grund);
    }

    [Fact]
    public void Ein_Bogen_ohne_gelernte_Bogenfaelle_ergibt_eine_Enthaltung()
    {
        var vorschlag = KostenVorschlagPolicy.Schlage(Ziel(boegen: 1), Faelle(5));

        Assert.Equal(EnthaltungsGrund.BogenNichtGelernt, vorschlag.Grund);
        Assert.Contains("Bogen", vorschlag.GrundText);
    }

    [Fact]
    public void Mit_genug_Bogenfaellen_wird_wieder_vorgeschlagen()
    {
        var vorschlag = KostenVorschlagPolicy.Schlage(Ziel(boegen: 1), Faelle(12, boegen: 1));

        Assert.False(vorschlag.IstEnthaltung);
    }

    [Fact]
    public void Uneinige_Nachbarn_ergeben_eine_Enthaltung()
    {
        // Drei Nachbarn, drei verschiedene Pakete -> keine Position erreicht die Mehrheit.
        var faelle = new List<Kostenfall>
        {
            Fall("A") with { Positionen = [new MassnahmePosition("A_POS", 1m, "Stk")] },
            Fall("B") with { Positionen = [new MassnahmePosition("B_POS", 1m, "Stk")] },
            Fall("C") with { Positionen = [new MassnahmePosition("C_POS", 1m, "Stk")] }
        };

        var vorschlag = KostenVorschlagPolicy.Schlage(Ziel(), faelle);

        Assert.Equal(EnthaltungsGrund.NachbarnUneinig, vorschlag.Grund);
    }

    [Fact]
    public void Ohne_gelernte_Faelle_wird_geschwiegen()
    {
        Assert.True(KostenVorschlagPolicy.Schlage(Ziel(), []).IstEnthaltung);
    }

    [Fact]
    public void Der_Grund_ist_immer_im_Klartext_lesbar()
    {
        var vorschlag = KostenVorschlagPolicy.Schlage(Ziel(), Faelle(1));

        Assert.NotEmpty(vorschlag.GrundText);
        Assert.DoesNotContain("Exception", vorschlag.GrundText);
    }
}
