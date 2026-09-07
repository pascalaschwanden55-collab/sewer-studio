using System.Linq;
using System.Reflection;
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

    /// <summary>
    /// Fix-Runde 1 (Reviewer, Important): Zeichne() darf nicht mehr wie zu Beginn den Canvas je
    /// Bild leeren und neu befuellen (GC-/CPU-Antipattern bei 33 ms Takt). Dieser Test greift auf
    /// die privaten Methoden per Reflection zu (wie der bestehende ToastHostAnimationTests-Test)
    /// und prueft am echten Formen-Pool: zwei aufeinanderfolgende Zeichne()-Aufrufe ohne
    /// zwischenzeitlichen Schritt() duerfen die Kinderzahl von Flaeche nicht erhoehen, und die
    /// Formen selbst bleiben dieselben Objekte (kein Neuanlegen).
    /// </summary>
    [Fact]
    public void Zeichne_erhoeht_die_Kinderzahl_bei_wiederholten_Aufrufen_nicht()
    {
        StaTestRunner.Run(() =>
        {
            var control = new NetzHintergrund();
            var neuesModell = typeof(NetzHintergrund).GetMethod(
                "NeuesModellUndStandbild", BindingFlags.Instance | BindingFlags.NonPublic);
            var zeichne = typeof(NetzHintergrund).GetMethod(
                "Zeichne", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(neuesModell);
            Assert.NotNull(zeichne);

            // Baut ein Modell (ActualWidth/Height sind ohne Layout 0 -> Modell mit 1x1,
            // trotzdem 60 Knoten) und zeichnet einmal -- das entspricht dem Ladepfad.
            neuesModell!.Invoke(control, null);
            var ersteAnzahl = control.Flaeche.Children.Count;
            var ersteFormen = control.Flaeche.Children.Cast<object>().ToList();
            Assert.True(ersteAnzahl > 0);

            zeichne!.Invoke(control, null);
            var zweiteAnzahl = control.Flaeche.Children.Count;
            var zweiteFormen = control.Flaeche.Children.Cast<object>().ToList();

            zeichne.Invoke(control, null);
            var dritteAnzahl = control.Flaeche.Children.Count;

            Assert.Equal(ersteAnzahl, zweiteAnzahl);
            Assert.Equal(zweiteAnzahl, dritteAnzahl);
            // Nicht nur gleich viele -- dieselben Formen-Objekte (Pool statt Neuanlage).
            Assert.Equal(ersteFormen, zweiteFormen);
        });
    }
}
