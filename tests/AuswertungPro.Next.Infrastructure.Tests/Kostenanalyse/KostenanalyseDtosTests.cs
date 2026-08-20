using AuswertungPro.Next.Application.Kostenanalyse;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Kostenanalyse;

public sealed class KostenanalyseDtosTests
{
    [Fact]
    public void Merkmale_kennen_ihre_Schadensarten_als_Menge()
    {
        var merkmale = new KostenfallMerkmale
        {
            DnMm = 300,
            LaengeM = 42.5,
            BogenAnzahl = 1,
            AnschlussAnzahl = 3,
            Schaeden =
            [
                new SchadensMerkmal("BAF", 2, HatStrecke: true),
                new SchadensMerkmal("BAJ", 1, HatStrecke: false)
            ]
        };

        Assert.Equal(new[] { "BAF", "BAJ" }, merkmale.Schadensarten);
        Assert.True(merkmale.HatBogen);
    }

    [Fact]
    public void Ein_Vorschlag_ohne_Positionen_ist_eine_Enthaltung()
    {
        var enthaltung = KostenVorschlag.Enthaltung(EnthaltungsGrund.ZuWenigeFaelle, "nur 1 ähnlicher Fall");

        Assert.True(enthaltung.IstEnthaltung);
        Assert.Empty(enthaltung.Positionen);
        Assert.Equal("nur 1 ähnlicher Fall", enthaltung.GrundText);
    }
}
