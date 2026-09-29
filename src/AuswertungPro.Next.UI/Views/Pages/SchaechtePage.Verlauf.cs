using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Views.Pages.Schachtansicht;

namespace AuswertungPro.Next.UI.Views.Pages;

/// <summary>
/// Optik Aufgabe 16: Die Eingabewege der Schachtseite laufen durch den Rueckgaengig-Verlauf —
/// Gegenstueck zur Haltungsseite. Die bisherigen Handler bleiben unveraendert.
/// </summary>
public partial class SchaechtePage
{
    private readonly DatenVerlaufZellErfassung _verlaufZelle = new();

    private IDisposable? ErfasseVerlauf(SchachtRecord? record, string? feld)
        => record is not null && _vm is { } vm ? vm.Verlauf.Erfasse(record, feld) : null;

    private void Grid_BeginningEditMitVerlauf(object sender, DataGridBeginningEditEventArgs e)
    {
        Grid_BeginningEdit(sender, e);
        if (!e.Cancel)
            _verlaufZelle.Beginne(ErfasseVerlauf(e.Row?.Item as SchachtRecord, e.Column?.GetValue(FrameworkElement.TagProperty) as string));
    }

    private void Grid_CellEditEndingMitVerlauf(object sender, DataGridCellEditEndingEventArgs e)
    {
        Grid_CellEditEnding(sender, e);
        _verlaufZelle.BeendeNachCommit(Dispatcher);
    }

    private IDisposable? ErfasseVerlauf(ComboBox? combo)
        => combo is null ? null : ErfasseVerlauf(ResolveRecordFromComboBox(combo), (combo.Tag as ComboBindingTag)?.RecordField);

    private void ComboBox_SelectionChangedMitVerlauf(object sender, SelectionChangedEventArgs e)
    {
        using var _ = ErfasseVerlauf(sender as ComboBox);
        ComboBox_SelectionChanged(sender, e);
    }

    private void ComboBox_LostKeyboardFocusMitVerlauf(object sender, KeyboardFocusChangedEventArgs e)
    {
        using var _ = ErfasseVerlauf(sender as ComboBox);
        ComboBox_LostKeyboardFocus(sender, e);
    }

    private void CommitSchachtDetailMitVerlauf(SchachtRecord record, KonsolidiertesSchachtFeld feld, string? value)
    {
        using var _ = ErfasseVerlauf(record, feld.PrimaerKey);
        CommitSchachtDetailKonsolidiert(record, feld, value);
    }

    private void ClearColumnMitVerlauf(string fieldName, string displayName)
    {
        using var _ = _vm is { } vm ? vm.Verlauf.ErfasseMehrere(vm.Records.ToList(), $"Spalte leeren: {string.Join(' ', fieldName.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))}") : null;
        ClearColumn(fieldName, displayName);
    }
}
