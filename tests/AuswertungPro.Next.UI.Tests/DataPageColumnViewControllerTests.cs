using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Nova-Etappe 2b, Task 4: Kompakt wird nur EINMAL als Standard gesetzt, wenn eine
/// bestehende Installation aktualisiert wird; danach zaehlt allein die Nutzerwahl.
/// </summary>
public sealed class DataPageColumnViewControllerTests
{
    [Fact]
    public void Gespeicherte_Ansicht_Alle_und_Flag_false_setzt_einmal_Kompakt_und_Flag_true()
    {
        var (key, flagNeu) = KompaktStartRegel.Entscheide("alle", flag: false);

        Assert.Equal("kompakt", key);
        Assert.True(flagNeu);
    }

    [Fact]
    public void Flag_false_ueberschreibt_auch_eine_andere_gespeicherte_Ansicht()
    {
        var (key, flagNeu) = KompaktStartRegel.Entscheide("bewertung", flag: false);

        Assert.Equal("kompakt", key);
        Assert.True(flagNeu);
    }

    [Fact]
    public void Flag_false_und_fehlender_Schluessel_ergibt_ebenfalls_Kompakt()
    {
        var (key, flagNeu) = KompaktStartRegel.Entscheide(null, flag: false);

        Assert.Equal("kompakt", key);
        Assert.True(flagNeu);
    }

    [Fact]
    public void Danach_bleibt_die_Nutzerwahl_unangetastet()
    {
        var (key, flagNeu) = KompaktStartRegel.Entscheide("alle", flag: true);

        Assert.Equal("alle", key);
        Assert.True(flagNeu);
    }

    [Fact]
    public void Nutzerwahl_Kompakt_bleibt_bei_gesetztem_Flag_ebenfalls_Kompakt()
    {
        var (key, flagNeu) = KompaktStartRegel.Entscheide("kompakt", flag: true);

        Assert.Equal("kompakt", key);
        Assert.True(flagNeu);
    }

    /// <summary>Fix-Runde 1: WendeAn mutiert das Layout und speichert nur bei einer echten Aenderung.</summary>
    [Fact]
    public void WendeAn_setzt_Kompakt_und_Flag_und_speichert_bei_einer_Bestandsinstallation()
    {
        var layout = new AuswertungPro.Next.UI.DataPageLayoutSettings { ActiveColumnView = "alle" };
        var gespeichert = 0;

        KompaktStartRegel.WendeAn(layout, () => gespeichert++);

        Assert.Equal("kompakt", layout.ActiveColumnView);
        Assert.True(layout.NovaKompaktEinmalGesetzt);
        Assert.Equal(1, gespeichert);
    }

    [Fact]
    public void WendeAn_speichert_nicht_wenn_das_Flag_bereits_gesetzt_ist()
    {
        var layout = new AuswertungPro.Next.UI.DataPageLayoutSettings
        {
            ActiveColumnView = "bewertung",
            NovaKompaktEinmalGesetzt = true
        };
        var gespeichert = 0;

        KompaktStartRegel.WendeAn(layout, () => gespeichert++);

        Assert.Equal("bewertung", layout.ActiveColumnView);
        Assert.True(layout.NovaKompaktEinmalGesetzt);
        Assert.Equal(0, gespeichert);
    }
}
