using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using AuswertungPro.Next.UI.Behaviors;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Haekchen und Zaehler der Spaltenansicht-Chips. Reine Anzeige — welche Ansicht gilt,
/// entscheidet der <see cref="DataPageColumnViewController"/>.
///
/// Herausgezogen, damit die <c>DataPage</c>-Teildateien mit der Aufklapp-Liste unter ihrer
/// gemeinsamen Groessengrenze bleiben. Die Schachtseite hat weiterhin ihre eigene Fassung
/// (zusaetzliche Faltung und Spaltenbereinigung) und bleibt hier unangetastet.
/// </summary>
public static class ColumnViewChipSync
{
    public static void Wende(ItemsControl chips, string? aktiverSchluessel, IReadOnlyCollection<string> felder)
    {
        var namen = felder.ToList();
        foreach (var chip in VisualTreeSafe.FindDescendants<ToggleButton>(chips))
        {
            chip.IsChecked = string.Equals(chip.Tag as string, aktiverSchluessel, StringComparison.OrdinalIgnoreCase);
            if (chip.DataContext is not DataPageColumnView view)
                continue;

            // Der Zaehler steht in der Chip-Vorlage; fehlt er (fremde Vorlage), wird nichts
            // gesetzt statt eine Ausnahme zu werfen.
            var zaehler = VisualTreeSafe.FindDescendants<TextBlock>(chip).FirstOrDefault(t => t.Name == "ChipZaehler");
            if (zaehler is not null)
                zaehler.Text = view.Anzahl(namen).ToString();
        }
    }
}
