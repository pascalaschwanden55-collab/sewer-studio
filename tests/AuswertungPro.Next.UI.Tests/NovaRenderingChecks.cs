using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
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
    /// Nova-Fixwelle B6, seit Etappe 2b als Marke: Die Zustandsklasse der Schachtliste war eine
    /// Auswahlspalte, deren Text am Ende der implizite TextBlock-Stil des Themes einfaerbte —
    /// im Dunkeln weiss auf Gelb. Sie zeigt jetzt dieselbe Marke wie die Haltungsliste; ihre
    /// Tinte kommt aus der ZustandsklasseInkPolicy und muss auf der Klassenfarbe lesbar sein.
    /// </summary>
    internal static void SchachtZustandsklasseMarkeBleibtLesbar()
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Zustandsklasse", "2", FieldSource.Manual, false);

        var spalte = ZustandsklasseChipColumnFactory.Create("Zustandsklasse", "ZUSTANDSKLASSE");
        var wurzel = Assert.IsType<Grid>(spalte.CellTemplate.LoadContent());
        wurzel.DataContext = record;

        var zelle = new DataGridCell
        {
            DataContext = record,
            // Bewusst der normale Zellenstil: Die Zelle der Marke wird NICHT mehr
            // ganzflaechig eingefaerbt; die Farbe traegt allein der Chip.
            Style = DataGridFieldMetaTooltipStyleFactory.Create("Zustandsklasse", null),
            Content = wurzel
        };
        Arrange(zelle, 180, 40);

        var marke = wurzel.Children.OfType<Border>().Single();
        var beschriftung = Assert.IsType<TextBlock>(marke.Child);
        Assert.Equal(Visibility.Visible, marke.Visibility);
        Assert.Equal("Z2", beschriftung.Text);

        var grund = Assert.IsType<SolidColorBrush>(marke.Background).Color;
        var tinte = Assert.IsType<SolidColorBrush>(beschriftung.Foreground).Color;
        Assert.True(
            ZustandsklasseInkPolicy.Contrast(tinte, grund) >= 4.5,
            $"Marke Z2: Tinte erreicht nur {ZustandsklasseInkPolicy.Contrast(tinte, grund):0.00}");
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

    /// <summary>
    /// Nova-Etappe 2b, Task 6: Ohne gewaehlte Zeile zeigt eine Uebersicht NUR ihren
    /// Leerzustandstext. Sonst stehen dort leere Beschriftungen — und weil eine Feldbindung ohne
    /// Datensatz <see cref="DependencyProperty.UnsetValue"/> liefert, im schlimmsten Fall der
    /// Fehltext "{DependencyProperty.UnsetValue}". Deshalb wird hier zusaetzlich geprueft, dass
    /// kein sichtbarer Text mit einer geschweiften Klammer beginnt.
    /// </summary>
    internal static void OhneAuswahlNurLeerzustand(FrameworkElement panel)
    {
        var inhalt = Assert.IsAssignableFrom<UIElement>(panel.FindName("Inhalt"));
        var leerzustand = Assert.IsType<TextBlock>(panel.FindName("Leerzustand"));

        Assert.Equal(Visibility.Collapsed, inhalt.Visibility);
        Assert.Equal(Visibility.Visible, leerzustand.Visibility);

        foreach (var text in SichtbareTexte(panel))
        {
            Assert.False(
                text.TrimStart().StartsWith("{", StringComparison.Ordinal),
                $"Sichtbarer Fehltext in der Uebersicht: {text}");
        }
    }

    /// <summary>
    /// Alle Texte, die ohne Fenster tatsaechlich sichtbar waeren: Ein eingeklappter Ast wird
    /// samt Kindern uebersprungen. <c>IsVisible</c> hilft hier nicht — es setzt eine
    /// PresentationSource voraus, die es im Kindprozess ohne Fenster nicht gibt.
    /// </summary>
    private static IEnumerable<string> SichtbareTexte(DependencyObject wurzel)
    {
        if (wurzel is UIElement element && element.Visibility != Visibility.Visible)
            yield break;

        if (wurzel is TextBlock textBlock && !string.IsNullOrEmpty(textBlock.Text))
            yield return textBlock.Text;

        var anzahl = VisualTreeHelper.GetChildrenCount(wurzel);
        for (var i = 0; i < anzahl; i++)
        {
            foreach (var text in SichtbareTexte(VisualTreeHelper.GetChild(wurzel, i)))
                yield return text;
        }
    }
}
