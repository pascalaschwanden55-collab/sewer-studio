using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Views.Pages;

namespace AuswertungPro.Next.UI.Tests;

internal static class NovaRenderingChecks
{
    internal static void ColorColumnsUseTheirCellForeground()
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Zustandsklasse", "2", FieldSource.Manual, false);
        var textColumn = DataGridWrappingTextColumnFactory.Create("Zustandsklasse", "Text");
        var comboColumn = DataGridComboColumnFactory.Create(
            "Zustandsklasse", "Auswahl", "Choices", "Zustandsklasse", (_, _) => { }, (_, _) => { },
            allowFreeText: false, bindIsProjectReady: false);
        var text = new TextBlock { Style = textColumn.ElementStyle };
        text.SetBinding(TextBlock.TextProperty, textColumn.Binding);
        var standardColumn = DataGridStandardTextColumnFactory.Create("Zustandsklasse", "Standard");
        // Der Seitenaufbau wendet die gespeicherte Spaltenausrichtung an.
        new DataGridColumnLayoutController().SetAlignment(standardColumn,
            HorizontalAlignment.Left, VerticalAlignment.Center);
        var standardText = new TextBlock { Style = standardColumn.ElementStyle };
        standardText.SetBinding(TextBlock.TextProperty, standardColumn.Binding);
        comboColumn.CellTemplate.Seal();
        var comboText = Assert.IsType<TextBlock>(comboColumn.CellTemplate.LoadContent());
        foreach (var label in new[] { text, comboText, standardText })
        {
            var cell = new DataGridCell
            {
                DataContext = record,
                Style = DataGridColorCellStyleFactory.CreateHaltungenStyle("Zustandsklasse"),
                Content = label
            };
            Arrange(cell, 180, 40);
            label.GetBindingExpression(TextBlock.ForegroundProperty)?.UpdateTarget();
            Assert.Equal("2", label.Text);
            Assert.Equal(Colors.Black, Assert.IsType<SolidColorBrush>(label.Foreground).Color);
        }
    }

    /// <summary>
    /// Nova-Fixwelle B6: Die Zustandsklasse der Schachtliste ist eine Auswahlspalte. Ihr
    /// Anzeigetext muss die Tinte der Zelle tragen (schwarz auf der Klassenfarbe), sonst faerbt
    /// ihn der implizite TextBlock-Stil des Themes — im Dunkeln weiss auf Gelb.
    /// </summary>
    internal static void SchachtZustandsklasseBleibtSchwarz()
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Zustandsklasse", "2", FieldSource.Manual, false);

        var spalte = SchaechteZustandsklasseColumnFactory.Create("Zustandsklasse", "Zustandsklasse");
        spalte.CellTemplate.Seal();
        var anzeige = Assert.IsType<TextBlock>(spalte.CellTemplate.LoadContent());

        var zelle = new DataGridCell
        {
            DataContext = record,
            Style = DataGridColorCellStyleFactory.CreateSchaechteStyle("Zustandsklasse"),
            Content = anzeige
        };
        Arrange(zelle, 180, 40);
        anzeige.GetBindingExpression(TextBlock.ForegroundProperty)?.UpdateTarget();

        Assert.Equal("2", anzeige.Text);
        Assert.Equal(Colors.Black, Assert.IsType<SolidColorBrush>(anzeige.Foreground).Color);
    }

    internal static void LongMenuCanScrollToItsLastAction()
    {
        var menu = new ContextMenu { MaxHeight = 240 };
        for (var i = 0; i < 30; i++)
            menu.Items.Add(new MenuItem { Header = $"Aktion {i + 1}" });
        Arrange(menu, 360, 240);
        var scroll = Assert.IsType<ScrollViewer>(menu.Template.FindName("MenuScrollViewer", menu));
        Assert.True(scroll.ScrollableHeight > 0, "Ein langes Menue braucht erreichbare untere Eintraege.");
        scroll.ScrollToEnd();
        menu.UpdateLayout();
        Assert.True(scroll.VerticalOffset > 0);
        var last = (MenuItem)menu.Items[^1];
        var bounds = last.TransformToAncestor(scroll).TransformBounds(new Rect(last.RenderSize));
        Assert.True(bounds.Top >= 0 && bounds.Bottom <= scroll.ActualHeight + 1,
            $"Letzte Aktion liegt ausserhalb des sichtbaren Menues: {bounds}; Hoehe {scroll.ActualHeight}");
    }

    private static void Arrange(FrameworkElement element, double width, double height)
    {
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        element.UpdateLayout();
    }

}
