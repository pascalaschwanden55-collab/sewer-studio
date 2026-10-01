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
    /// Nova-Fixwelle B6, seit Etappe 2b als gemeinsame Marke: Die Zustandsklasse der
    /// Schachtliste war eine <c>DataGridComboBoxColumn</c>. Deren Anzeigeelement ist ein
    /// internes ComboBox-Abkoemmling; die Ziffer bekam ihre Farbe am Ende vom impliziten
    /// TextBlock-Stil des Themes und stand im Dunkeln weiss auf Gelb. Haltungs- und Schachtliste
    /// verwenden jetzt dieselbe Fabrik: Marke zum Anzeigen, Auswahl 0 bis 4 zum Bearbeiten. Die
    /// tatsaechlich gezeichnete Tinte prueft
    /// <c>NovaRenderingChecks.SchachtZustandsklasseMarkeBleibtLesbar</c> im STA-Kindprozess.
    /// </summary>
    [Fact]
    public void Zustandsklasse_der_Schachtliste_hat_getrennte_Vorlagen_fuer_Anzeige_und_Auswahl()
    {
        var spalte = AuswertungPro.Next.UI.Views.Pages.ZustandsklasseChipColumnFactory.Create("Zustandsklasse", "Zustandsklasse");

        Assert.NotNull(spalte.CellTemplate);
        Assert.NotNull(spalte.CellEditingTemplate);
        Assert.Equal(typeof(System.Windows.Controls.Grid), spalte.CellTemplate.VisualTree.Type);
        Assert.Equal(typeof(System.Windows.Controls.ComboBox), spalte.CellEditingTemplate.VisualTree.Type);
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
