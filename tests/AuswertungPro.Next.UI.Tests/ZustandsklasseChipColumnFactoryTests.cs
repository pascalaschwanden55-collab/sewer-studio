using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Views.Pages;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Nova-Etappe 2b, Task 3: Die Zustandsklasse als Marke ("Chip"). Gemeinsame Fabrik fuer
/// Haltungs- und Schachtliste. Wichtig: Der Chip erfindet nie eine Zahl, und der getrennte
/// Zustand "nicht berechnet" bleibt ein Gedankenstrich — niemals Z4.
/// </summary>
public sealed class ZustandsklasseChipColumnFactoryTests
{
    [Fact]
    public void Die_Spalte_zeigt_eine_Marke_und_bearbeitet_weiterhin_als_Auswahl_0_bis_4()
    {
        StaTestRunner.Run(() =>
        {
            var spalte = ZustandsklasseChipColumnFactory.Create(FieldKeys.ConditionClass, "ZUSTANDSKLASSE");
            Assert.Equal("ZUSTANDSKLASSE", spalte.Header);
            Assert.NotNull(spalte.CellTemplate);

            var auswahl = Assert.IsType<ComboBox>(spalte.CellEditingTemplate.LoadContent());
            Assert.Equal(ZustandsklasseColorPalette.SelectionOptions, auswahl.ItemsSource);
            var bindung = Assert.IsType<Binding>(BindingOperations.GetBinding(auswahl, Selector.SelectedItemProperty));
            Assert.Equal($"Fields[{FieldKeys.ConditionClass}]", bindung.Path.Path);
            Assert.Equal(BindingMode.TwoWay, bindung.Mode);
        });
    }

    [Theory]
    [InlineData("0", "Z0")]
    [InlineData("2", "Z2")]
    [InlineData("4", "Z4")]
    public void Eine_gueltige_Klasse_erscheint_als_gefuellte_Marke(string wert, string beschriftung)
    {
        StaTestRunner.Run(() =>
        {
            var (gefuellt, leer) = Marken(wert);

            Assert.Equal(Visibility.Visible, gefuellt.Visibility);
            Assert.Equal(Visibility.Collapsed, leer.Visibility);
            Assert.Equal(beschriftung, Assert.IsType<TextBlock>(gefuellt.Child).Text);
            Assert.IsType<SolidColorBrush>(gefuellt.Background);
        });
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("nicht berechnet")]
    public void Ohne_gueltige_Klasse_bleibt_es_beim_gestrichelten_Gedankenstrich(string wert)
    {
        StaTestRunner.Run(() =>
        {
            var (gefuellt, leer) = Marken(wert);

            Assert.Equal(Visibility.Collapsed, gefuellt.Visibility);
            Assert.Equal(Visibility.Visible, leer.Visibility);
            Assert.Equal("nicht berechnet", leer.ToolTip);

            var rand = leer.Children.OfType<Rectangle>().Single();
            Assert.NotNull(rand.StrokeDashArray);
            Assert.NotEmpty(rand.StrokeDashArray);
            Assert.Equal("–", leer.Children.OfType<TextBlock>().Single().Text);
        });
    }

    /// <summary>Der Chip liest den Wert wie die Farbe: "2,4" ist Klasse 2, nie eine erfundene Zahl.</summary>
    [Fact]
    public void Ein_Dezimalwert_folgt_derselben_Normalisierung_wie_die_Farbe()
    {
        StaTestRunner.Run(() =>
        {
            var (gefuellt, _) = Marken("2,4");
            Assert.Equal(Visibility.Visible, gefuellt.Visibility);
            Assert.Equal("Z2", Assert.IsType<TextBlock>(gefuellt.Child).Text);
        });
    }

    private static (Border Gefuellt, Grid Leer) Marken(string wert)
    {
        var spalte = ZustandsklasseChipColumnFactory.Create(FieldKeys.ConditionClass, "ZUSTANDSKLASSE");
        var wurzel = Assert.IsType<Grid>(spalte.CellTemplate.LoadContent());

        var record = new HaltungRecord();
        record.SetFieldValue(FieldKeys.ConditionClass, wert, FieldSource.Manual, userEdited: true);
        wurzel.DataContext = record;
        WpfBindungsPumpe.Leeren();

        return (wurzel.Children.OfType<Border>().Single(), wurzel.Children.OfType<Grid>().Single());
    }
}
