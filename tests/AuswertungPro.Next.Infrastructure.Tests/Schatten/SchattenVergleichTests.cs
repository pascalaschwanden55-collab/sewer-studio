using AuswertungPro.Next.Application.Schatten;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Schatten;

public sealed class SchattenVergleichTests
{
    [Fact]
    public void OhneMenschWerte_KeinVergleich()
        => Assert.Equal(SchattenAbweichung.KeinVergleich,
            SchattenVergleich.Bewerte(null, "", null, "3", "Schlauchliner", 12000m));

    // Nullbericht 11.07.: 167 Haltungen ohne Schattenlauf wurden als "Gleich" gezaehlt.
    // Leere Schatten-Seite darf NIE als Uebereinstimmung gelten -> grau.
    [Fact]
    public void OhneSchattenWerte_KeinVergleich()
        => Assert.Equal(SchattenAbweichung.KeinVergleich,
            SchattenVergleich.Bewerte("3", "Schlauchliner", "10000", null, null, null));

    [Fact]
    public void SchattenKostenNull_ZaehltNichtAlsWert()
        => Assert.Equal(SchattenAbweichung.KeinVergleich,
            SchattenVergleich.Bewerte("3", "Schlauchliner", "10000", "", "", 0m));

    [Fact]
    public void KlasseAbweichend_IstRot()
        => Assert.Equal(SchattenAbweichung.StarkAbweichend,
            SchattenVergleich.Bewerte("2", "Schlauchliner", "10000", "4", "Schlauchliner", 10000m));

    [Fact]
    public void MassnahmeAbweichend_IstGelb()
        => Assert.Equal(SchattenAbweichung.LeichtAbweichend,
            SchattenVergleich.Bewerte("3", "Roboter-Reparatur", "10000", "3", "Schlauchliner (Nadelfilz)", 10000m));

    [Fact]
    public void KostenUeberToleranz_IstGelb()
        => Assert.Equal(SchattenAbweichung.LeichtAbweichend,
            SchattenVergleich.Bewerte("3", "Schlauchliner", "10'000.00 CHF", "3", "Schlauchliner", 14000m));

    [Fact]
    public void AllesVergleichbareGleich_IstGruen()
        => Assert.Equal(SchattenAbweichung.Gleich,
            SchattenVergleich.Bewerte("3", "Schlauchliner (Nadelfilz)", "10'000", "3", "Schlauchliner", 11000m));

    [Theory]
    [InlineData("1'200.50", 1200.50)]
    [InlineData("1200,50 CHF", 1200.50)]
    [InlineData("CHF 1 200", 1200.0)]
    [InlineData("", null)]
    [InlineData("k.A.", null)]
    public void KostenParser_IstTolerant(string text, double? erwartet)
    {
        var wert = SchattenVergleich.TryParseKosten(text);
        if (erwartet is null) Assert.Null(wert);
        else Assert.Equal((decimal)erwartet.Value, wert);
    }
}
