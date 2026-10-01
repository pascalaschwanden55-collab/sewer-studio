using AuswertungPro.Next.Application.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Die Breite einer Haltung steht in SIA405 nicht an der Haltung, sondern als
/// Hoehen-Breiten-Verhaeltnis am Rohrprofil. Hin und zurueck muss dieselbe Zahl
/// herauskommen.
/// </summary>
public sealed class XtfRohrprofilVerhaeltnisTests
{
    [Theory]
    [InlineData("1000", "600", "1.66667")]
    [InlineData("900", "600", "1.5")]
    [InlineData("600", "900", "0.66667")]
    [InlineData("300", "300", null)]
    [InlineData("300", "", null)]
    [InlineData("", "600", null)]
    [InlineData("300", "abc", null)]
    [InlineData("0", "600", null)]
    public void Aus_Hoehe_und_Breite_wird_das_Verhaeltnis(string hoehe, string breite, string? erwartet)
        => Assert.Equal(erwartet, XtfRohrprofilVerhaeltnis.Berechne(hoehe, breite));

    [Theory]
    [InlineData("1000", "1.66667", 600)]
    [InlineData("900", "1.5", 600)]
    [InlineData("600", "0.66667", 900)]
    [InlineData("1000", "1,66667", 600)]
    [InlineData("1000", "", null)]
    [InlineData("", "1.5", null)]
    [InlineData("1000", "0", null)]
    [InlineData("1000", "Quatsch", null)]
    public void Aus_Hoehe_und_Verhaeltnis_wird_die_Breite(string hoehe, string verhaeltnis, int? erwartet)
        => Assert.Equal(erwartet, XtfRohrprofilVerhaeltnis.Breite(hoehe, verhaeltnis));

    [Fact]
    public void Hin_und_zurueck_ergibt_dieselbe_Breite()
    {
        var verhaeltnis = XtfRohrprofilVerhaeltnis.Berechne("1200", "800");

        Assert.Equal(800, XtfRohrprofilVerhaeltnis.Breite("1200", verhaeltnis));
    }

    [Theory]
    [InlineData("1.66667", "1.66667", true)]
    [InlineData("1.666670", "1.66667", true)]
    [InlineData("1.5", "1.66667", false)]
    [InlineData("", "", true)]
    [InlineData("", "1.5", false)]
    public void Gleichheit_vergleicht_Zahlen_nicht_Texte(string a, string b, bool erwartet)
        => Assert.Equal(erwartet, XtfRohrprofilVerhaeltnis.Gleich(a, b));
}
