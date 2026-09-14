using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Zeitregeln der Startanimation 5.0 (Entscheid Pascal 14.09.2026: Variante «Kugel, straffer»).
/// Alle Zeiten sind Sekunden seit Start der Animationsuhr.
/// </summary>
public sealed class StartupSplashChoreografieTests
{
    private const int Knoten = 112;

    [Fact]
    public void Knoten_erscheinen_in_Spiralreihenfolge_und_alle_bis_1_5_Sekunden()
    {
        Assert.Equal(0, StartupSplashChoreografie.KnotenSichtbarkeit(0, Knoten, 0.0));
        Assert.Equal(1, StartupSplashChoreografie.KnotenSichtbarkeit(0, Knoten, 0.5), precision: 6);
        Assert.Equal(0, StartupSplashChoreografie.KnotenSichtbarkeit(Knoten - 1, Knoten, 1.0));
        Assert.Equal(1, StartupSplashChoreografie.KnotenSichtbarkeit(Knoten - 1, Knoten, 1.5), precision: 6);
    }

    [Fact]
    public void Knoten_blenden_weich_ein_statt_zu_springen()
    {
        var mitte = StartupSplashChoreografie.KnotenSichtbarkeit(0, Knoten, 0.15 + 0.35 / 2);
        Assert.InRange(mitte, 0.4, 0.95);
    }

    [Fact]
    public void Verbindungen_folgen_ihren_Knoten_ab_0_7_Sekunden()
    {
        Assert.Equal(0, StartupSplashChoreografie.VerbindungSichtbarkeit(0, 1, Knoten, 0.7));
        Assert.Equal(1, StartupSplashChoreografie.VerbindungSichtbarkeit(0, 1, Knoten, 1.1), precision: 6);
        Assert.Equal(0, StartupSplashChoreografie.VerbindungSichtbarkeit(Knoten - 2, Knoten - 1, Knoten, 1.5));
        Assert.Equal(1, StartupSplashChoreografie.VerbindungSichtbarkeit(Knoten - 2, Knoten - 1, Knoten, 2.1), precision: 6);
    }

    [Theory]
    [InlineData(0.0, false)]
    [InlineData(1.49, false)]
    [InlineData(1.5, true)]
    [InlineData(12.0, true)]
    public void Impulse_laufen_erst_ab_1_5_Sekunden_und_dann_bis_zum_Bereitsein(double sekunden, bool erwartet)
    {
        Assert.Equal(erwartet, StartupSplashChoreografie.ImpulseErlaubt(sekunden));
    }

    [Theory]
    [InlineData(2.9, -1)]
    [InlineData(3.0, 0)]
    [InlineData(3.75, 0.5)]
    [InlineData(4.5, -1)]
    [InlineData(5.0, -1)]
    [InlineData(5.4, 0)]
    [InlineData(6.15, 0.5)]
    [InlineData(6.9, -1)]
    public void Wellen_laufen_fest_bei_3_0_und_5_4_Sekunden_je_1_5_Sekunden(double sekunden, double erwartet)
    {
        Assert.Equal(erwartet, StartupSplashChoreografie.Wellenfortschritt(sekunden), precision: 6);
    }

    [Theory]
    [InlineData(8.1, 0)]
    [InlineData(8.85, 0.5)]
    [InlineData(9.7, -1)]
    [InlineData(10.8, 0)]
    public void Wellen_wiederholen_sich_alle_2_7_Sekunden_solange_das_Programm_noch_laedt(double sekunden, double erwartet)
    {
        Assert.Equal(erwartet, StartupSplashChoreografie.Wellenfortschritt(sekunden), precision: 6);
    }

    [Fact]
    public void Ringe_blenden_ab_Start_gestaffelt_ein()
    {
        Assert.Equal(0, StartupSplashChoreografie.RingStartMillisekunden(0));
        Assert.Equal(150, StartupSplashChoreografie.RingStartMillisekunden(1));
        Assert.Equal(300, StartupSplashChoreografie.RingStartMillisekunden(2));
        Assert.Equal(1200, StartupSplashChoreografie.RingDauerMillisekunden);
    }

    [Theory]
    [InlineData(-0.1, 0)]
    [InlineData(0.0, 0)]
    [InlineData(0.28, 1)]
    [InlineData(0.56, 0)]
    [InlineData(2.0, 0)]
    public void Bereitanteil_leuchtet_genau_eine_gute_halbe_Sekunde_gruen_auf(double sekundenSeitBereit, double erwartet)
    {
        Assert.Equal(erwartet, StartupSplashChoreografie.Bereitanteil(sekundenSeitBereit), precision: 6);
    }
}
