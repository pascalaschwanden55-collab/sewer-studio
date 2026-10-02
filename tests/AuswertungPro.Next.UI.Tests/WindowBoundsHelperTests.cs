using System.Windows;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>Gemeinsame Fensterregel der Dialoge (B6, Deepscan 02.10.2026).</summary>
public sealed class WindowBoundsHelperTests
{
    private static readonly Rect Area = new(0, 0, 1920, 1040);

    [Fact]
    public void Fenster_im_Arbeitsbereich_bleibt_unveraendert()
    {
        var r = new WindowBoundsHelper.Rahmen(100, 50, 800, 600);

        Assert.Equal(r, WindowBoundsHelper.KlemmeAufArbeitsbereich(r, Area));
    }

    [Fact]
    public void Zu_grosses_Fenster_wird_auf_Arbeitsbereich_minus_20_verkleinert()
    {
        var neu = WindowBoundsHelper.KlemmeAufArbeitsbereich(new WindowBoundsHelper.Rahmen(0, 0, 3000, 2000), Area);

        Assert.Equal(1900, neu.Width);
        Assert.Equal(1020, neu.Height);
        Assert.Equal(0, neu.Left);
        Assert.Equal(0, neu.Top);
    }

    [Fact]
    public void Fenster_ueber_dem_Rand_wird_zurueckgeschoben()
    {
        var links = WindowBoundsHelper.KlemmeAufArbeitsbereich(new WindowBoundsHelper.Rahmen(-50, -30, 800, 600), Area);
        Assert.Equal(0, links.Left);
        Assert.Equal(0, links.Top);

        var rechts = WindowBoundsHelper.KlemmeAufArbeitsbereich(new WindowBoundsHelper.Rahmen(1800, 900, 800, 600), Area);
        Assert.Equal(1920 - 800, rechts.Left);
        Assert.Equal(1040 - 600, rechts.Top);
    }

    [Fact]
    public void Nie_gezeigtes_Fenster_mit_NaN_Position_bleibt_unveraendert()
    {
        var neu = WindowBoundsHelper.KlemmeAufArbeitsbereich(
            new WindowBoundsHelper.Rahmen(double.NaN, double.NaN, 800, 600), Area);

        Assert.True(double.IsNaN(neu.Left));
        Assert.True(double.IsNaN(neu.Top));
        Assert.Equal(800, neu.Width);
    }
}
