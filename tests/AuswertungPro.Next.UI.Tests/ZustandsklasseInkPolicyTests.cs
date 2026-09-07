using System.Globalization;
using System.Windows;
using System.Windows.Media;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Nova-Etappe 1: Die Zustandsklassen-Marken tragen eine Textfarbe je Klasse, die auf dem
/// Marken-Hintergrund mindestens 4,5:1 erreicht (Nachpruefung R05).
/// </summary>
public sealed class ZustandsklasseInkPolicyTests
{
    [Fact]
    public void Jede_Zustandsklasse_bekommt_eine_Textfarbe_mit_mindestens_4_5_zu_1()
    {
        foreach (var klasse in ZustandsklasseColorPalette.SelectionOptions)
        {
            var brush = (SolidColorBrush)ZustandsklasseColorPalette.HaltungenPalette[klasse];
            var ink = ZustandsklasseInkPolicy.InkFor(brush.Color);
            Assert.True(
                ZustandsklasseInkPolicy.Contrast(ink, brush.Color) >= 4.5,
                $"Z{klasse}: Kontrast {ZustandsklasseInkPolicy.Contrast(ink, brush.Color):0.00}");
        }
    }

    [Fact]
    public void Dunkle_Tinte_wird_bevorzugt_wenn_beide_reichen()
    {
        // Gelb: dunkel ergibt deutlich mehr als 4,5, Weiss deutlich weniger.
        var ink = ZustandsklasseInkPolicy.InkFor(Color.FromRgb(0xFF, 0xFF, 0x00));
        Assert.Equal(ZustandsklasseInkPolicy.DarkInk, ink);
    }

    [Fact]
    public void Konverter_liefert_gefrorene_Tinte_fuer_bekannte_Klasse()
    {
        var conv = new ZustandsklasseInkConverter();
        var result = conv.Convert("2", typeof(Brush), null!, CultureInfo.InvariantCulture);
        var brush = Assert.IsType<SolidColorBrush>(result);
        Assert.True(brush.IsFrozen);
    }

    /// <summary>
    /// Nova-Fixwelle B6: Der Zellstil setzt schwarze Tinte an der DataGridCell, doch der implizite
    /// TextBlock-Stil des dunklen Themes setzt Foreground selbst und schlaegt die Vererbung —
    /// die Ziffern standen weiss auf Gelb. Der Zellstil bringt seine eigene, engere Fassung mit.
    /// </summary>
    [Fact]
    public void Zellstil_bindet_die_Textfarbe_an_die_Zelle()
    {
        var stil = AuswertungPro.Next.UI.Views.Pages.ZustandsklasseCellStyleFactory.CreateHaltungenStyle("Zustandsklasse");

        var textStil = Assert.IsType<Style>(stil.Resources[typeof(System.Windows.Controls.TextBlock)]);
        var setter = Assert.IsType<Setter>(Assert.Single(textStil.Setters));
        Assert.Equal(System.Windows.Controls.TextBlock.ForegroundProperty, setter.Property);
        var bindung = Assert.IsType<System.Windows.Data.Binding>(setter.Value);
        Assert.Equal(nameof(System.Windows.Controls.Control.Foreground), bindung.Path.Path);
        Assert.Equal(typeof(System.Windows.Controls.DataGridCell), bindung.RelativeSource?.AncestorType);
    }

    /// <summary>Auf jeder Klassenflaeche erreicht die schwarze Zellentinte mindestens 4,5:1.</summary>
    [Fact]
    public void Schwarze_Zellentinte_reicht_auf_jeder_Klassenflaeche()
    {
        foreach (var klasse in ZustandsklasseColorPalette.SelectionOptions)
        {
            var brush = (SolidColorBrush)ZustandsklasseColorPalette.HaltungenPalette[klasse];
            Assert.True(
                ZustandsklasseInkPolicy.Contrast(ZustandsklasseInkPolicy.DarkInk, brush.Color) >= 4.5,
                $"Z{klasse}: schwarze Tinte erreicht nur {ZustandsklasseInkPolicy.Contrast(ZustandsklasseInkPolicy.DarkInk, brush.Color):0.00}");
        }
    }

    [Fact]
    public void Unbekannte_Klasse_liefert_keine_Farbe()
    {
        var conv = new ZustandsklasseInkConverter();
        Assert.Equal(DependencyProperty.UnsetValue, conv.Convert("9", typeof(Brush), null!, CultureInfo.InvariantCulture));
    }
}
