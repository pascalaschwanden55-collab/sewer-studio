using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.UI.Behaviors;

namespace AuswertungPro.Next.UI.Controls;

/// <summary>
/// Spalten-Auswahl fuer ein personalisierbares <see cref="DataGrid"/>: ein Kontextmenue
/// mit einer Checkbox je Spalte (ein-/ausblenden) plus "Alle einblenden". Reorder bleibt
/// nativ ueber Spalten-Ziehen. Nach jeder Aenderung wird ueber <see cref="GridPersonalization.Persist"/>
/// persistiert (Sichtbarkeits-Wechsel loesen kein LayoutChanged aus).
/// </summary>
public static class ColumnChooser
{
    /// <summary>
    /// Darf diese Spalte versteckt werden? Nur wenn danach noch mindestens eine
    /// Spalte sichtbar bleibt (kein All-Hidden). Pure, testbar.
    /// </summary>
    public static bool CanHide(DataGrid grid, DataGridColumn column)
    {
        if (column.Visibility != Visibility.Visible)
            return true; // bereits versteckt -> Einblenden immer erlaubt
        return grid.Columns.Count(c => c.Visibility == Visibility.Visible) > 1;
    }

    public static void Show(DataGrid grid)
    {
        var menu = new ContextMenu { PlacementTarget = grid };

        foreach (var column in grid.Columns.OrderBy(c => c.DisplayIndex).ToList())
        {
            var col = column;
            var item = new MenuItem
            {
                Header = col.Header?.ToString() ?? "(Spalte)",
                IsCheckable = true,
                IsChecked = col.Visibility == Visibility.Visible,
                StaysOpenOnClick = true
            };

            item.Click += (_, _) =>
            {
                var wantVisible = item.IsChecked;
                if (!wantVisible && !CanHide(grid, col))
                {
                    item.IsChecked = true; // letzte sichtbare Spalte bleibt sichtbar
                    return;
                }

                col.Visibility = wantVisible ? Visibility.Visible : Visibility.Collapsed;
                GridPersonalization.Persist(grid);
            };

            menu.Items.Add(item);
        }

        menu.Items.Add(new Separator());
        var resetItem = new MenuItem { Header = "Alle Spalten einblenden" };
        resetItem.Click += (_, _) =>
        {
            foreach (var c in grid.Columns)
                c.Visibility = Visibility.Visible;
            GridPersonalization.Persist(grid);
        };
        menu.Items.Add(resetItem);

        menu.IsOpen = true;
    }
}
