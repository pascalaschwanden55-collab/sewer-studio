using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Zeitregeln der Startanimation 5.0 (Entscheid Pascal 14.09.2026, Variante D «Kugel,
/// deutlich neu»: Einflug, Bogenringe, Ringwellen, gruene Bereit-Welle).
/// Alle Zeiten sind Sekunden seit Start der Animationsuhr.
/// </summary>
public sealed class StartupSplashChoreografieTests
{
    private const int Knoten = 112;

    [Fact]
    public void Knoten_fliegen_in_Spiralreihenfolge_ein_und_alle_sind_bis_1_85_Sekunden_da()
    {
        Assert.Equal(0, StartupSplashChoreografie.EinflugFortschritt(0, Knoten, 0.05));
        Assert.Equal(0.5, StartupSplashChoreografie.EinflugFortschritt(0, Knoten, 0.45), precision: 6);
        Assert.Equal(1, StartupSplashChoreografie.EinflugFortschritt(0, Knoten, 0.85), precision: 6);
        Assert.Equal(0, StartupSplashChoreografie.EinflugFortschritt(Knoten - 1, Knoten, 1.0));
        Assert.Equal(1, StartupSplashChoreografie.EinflugFortschritt(Knoten - 1, Knoten, 1.85), precision: 6);
    }

    [Fact]
    public void Einflug_beginnt_und_endet_weich()
    {
        // Ease-in-out: am Anfang und am Ende langsam, in der Mitte am schnellsten.
        var fruehe = StartupSplashChoreografie.EinflugFortschritt(0, Knoten, 0.25);
        var mitte = StartupSplashChoreografie.EinflugFortschritt(0, Knoten, 0.45);
        var spaete = StartupSplashChoreografie.EinflugFortschritt(0, Knoten, 0.65);
        Assert.InRange(fruehe, 0.05, 0.2);
        Assert.Equal(0.5, mitte, precision: 6);
        Assert.InRange(spaete, 0.8, 0.95);
    }

    [Fact]
    public void Startpunkte_liegen_weit_ausserhalb_der_Kugel_und_in_alle_Richtungen()
    {
        var richtungen = new HashSet<int>();
        for (var i = 0; i < Knoten; i++)
        {
            var winkel = StartupSplashChoreografie.EinflugRichtung(i);
            Assert.InRange(winkel, 0, Math.PI * 2);
            richtungen.Add((int)(winkel / (Math.PI / 4)));
            Assert.InRange(StartupSplashChoreografie.EinflugAbstand(i), 2.0, 3.3);
        }
        // Alle acht Himmelsrichtungen sind belegt: Die Knoten kommen nicht aus einer Ecke.
        Assert.Equal(8, richtungen.Count);
    }

    [Theory]
    [InlineData(0.0, 0.72)]
    [InlineData(2.0, 1.0)]
    [InlineData(8.0, 1.0)]
    public void Kugel_waechst_in_zwei_Sekunden_von_72_auf_100_Prozent(double sekunden, double erwartet)
    {
        Assert.Equal(erwartet, StartupSplashChoreografie.KugelMassstab(sekunden), precision: 6);
    }

    [Fact]
    public void Verbindungen_erscheinen_erst_wenn_beide_Enden_angekommen_sind()
    {
        var ankunft = StartupSplashChoreografie.VerbindungAnkunft(0, 1, Knoten);
        Assert.Equal(0.05 + 1.0 / (Knoten - 1) + 0.8, ankunft, precision: 6);
        Assert.Equal(0, StartupSplashChoreografie.VerbindungSichtbarkeit(0, 1, Knoten, ankunft - 0.01));
        Assert.Equal(1, StartupSplashChoreografie.VerbindungSichtbarkeit(0, 1, Knoten, ankunft + 0.25), precision: 6);
        Assert.Equal(1, StartupSplashChoreografie.VerbindungSichtbarkeit(0, 1, Knoten, 8.0), precision: 6);
    }

    [Fact]
    public void Verbindungen_blitzen_bei_Ankunft_auf_und_klingen_in_einer_halben_Sekunde_ab()
    {
        var ankunft = StartupSplashChoreografie.VerbindungAnkunft(0, 1, Knoten);
        Assert.Equal(0, StartupSplashChoreografie.VerbindungBlitz(0, 1, Knoten, ankunft - 0.01));
        Assert.Equal(1, StartupSplashChoreografie.VerbindungBlitz(0, 1, Knoten, ankunft), precision: 6);
        Assert.InRange(StartupSplashChoreografie.VerbindungBlitz(0, 1, Knoten, ankunft + 0.5), 0.0, 0.1);
    }

    [Fact]
    public void Ringe_zeichnen_sich_gestaffelt_als_wachsende_Boegen()
    {
        Assert.Equal(0, StartupSplashChoreografie.RingBogen(0, 0.3));
        Assert.InRange(StartupSplashChoreografie.RingBogen(0, 1.0), 0.3, 0.99);
        Assert.Equal(1, StartupSplashChoreografie.RingBogen(0, 1.7), precision: 6);
        Assert.Equal(0, StartupSplashChoreografie.RingBogen(2, 0.7));
        Assert.Equal(1, StartupSplashChoreografie.RingBogen(2, 2.1), precision: 6);
    }

    [Fact]
    public void Ringe_und_Kern_blenden_mit_dem_Bogenanfang_ein()
    {
        Assert.Equal(0, StartupSplashChoreografie.RingSichtbarkeit(0, 0.3));
        Assert.Equal(1, StartupSplashChoreografie.RingSichtbarkeit(0, 0.7), precision: 6);
        Assert.Equal(0, StartupSplashChoreografie.RingSichtbarkeit(2, 0.7));
        Assert.Equal(1, StartupSplashChoreografie.RingSichtbarkeit(2, 1.1), precision: 6);
        Assert.Equal(0, StartupSplashChoreografie.KernGluehen(0.4));
        Assert.Equal(1, StartupSplashChoreografie.KernGluehen(1.6), precision: 6);
    }

    [Theory]
    [InlineData(0.0, false)]
    [InlineData(1.89, false)]
    [InlineData(1.9, true)]
    [InlineData(12.0, true)]
    public void Impulse_laufen_erst_ab_1_9_Sekunden_und_dann_bis_zum_Bereitsein(double sekunden, bool erwartet)
    {
        Assert.Equal(erwartet, StartupSplashChoreografie.ImpulseErlaubt(sekunden));
    }

    [Theory]
    [InlineData(2.9, -1)]
    [InlineData(3.0, 0)]
    [InlineData(3.75, 0.5)]
    [InlineData(4.5, -1)]
    [InlineData(5.4, -1)]
    [InlineData(8.4, 0)]
    [InlineData(9.15, 0.5)]
    [InlineData(13.8, 0)]
    public void Scanwelle_laeuft_bei_3_0_Sekunden_und_danach_alle_5_4_Sekunden(double sekunden, double erwartet)
    {
        Assert.Equal(erwartet, StartupSplashChoreografie.Wellenfortschritt(sekunden), precision: 6);
    }

    [Theory]
    [InlineData(4.9, -1)]
    [InlineData(5.0, 0)]
    [InlineData(5.6, 0.5)]
    [InlineData(6.2, -1)]
    [InlineData(10.4, 0)]
    [InlineData(11.0, 0.5)]
    public void Ringwelle_laeuft_bei_5_0_Sekunden_und_danach_alle_5_4_Sekunden(double sekunden, double erwartet)
    {
        Assert.Equal(erwartet, StartupSplashChoreografie.Ringwellenfortschritt(sekunden), precision: 6);
    }

    [Theory]
    [InlineData(-0.1, 0)]
    [InlineData(0.0, 0)]
    [InlineData(0.45, 0.5)]
    [InlineData(0.9, 1)]
    [InlineData(2.0, 1)]
    public void Bereitwelle_laeuft_in_0_9_Sekunden_nach_aussen_und_bleibt_dann_voll(double sekundenSeitBereit, double erwartet)
    {
        Assert.Equal(erwartet, StartupSplashChoreografie.Bereitwelle(sekundenSeitBereit), precision: 6);
    }
}
