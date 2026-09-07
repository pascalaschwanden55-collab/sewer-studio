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
}
