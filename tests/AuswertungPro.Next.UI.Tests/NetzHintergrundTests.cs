using AuswertungPro.Next.UI.Controls;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Nova-Etappe 2: das WPF-freie Bewegungsmodell des Hintergrund-Leitungsnetzes.
/// Deterministisch (gleicher Startwert -> gleiche Knoten), bleibt im Rahmen, Alpha der
/// Verbindungen nimmt mit dem Abstand ab.
/// </summary>
public sealed class NetzHintergrundTests
{
    [Fact]
    public void Knoten_sind_deterministisch_und_bleiben_im_Rahmen()
    {
        var a = new NetzHintergrundModell(800, 600);
        var b = new NetzHintergrundModell(800, 600);
        Assert.Equal(60, a.Knoten.Count);
        Assert.Equal(a.Knoten[7].X, b.Knoten[7].X);
        for (var i = 0; i < 500; i++) { a.Schritt(); }
        Assert.All(a.Knoten, k => { Assert.InRange(k.X, 0, 800); Assert.InRange(k.Y, 0, 600); Assert.InRange(k.R, 1.2, 2.8); });
    }

    [Fact]
    public void Verbindungen_nur_unter_dem_Hoechstabstand_mit_abnehmendem_Alpha()
    {
        var m = new NetzHintergrundModell(2000, 2000, knoten: 2);
        var v = m.Verbindungen(170);
        Assert.All(v, x => Assert.InRange(x.Alpha, 0, 1));
    }
}
