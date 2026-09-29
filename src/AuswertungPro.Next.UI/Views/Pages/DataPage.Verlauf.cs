using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AuswertungPro.Next.Application.UseCases.Datenaenderungen;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Views.Pages;

/// <summary>
/// Optik Aufgabe 16: Die Eingabewege der Haltungsseite laufen durch den Rueckgaengig-Verlauf.
/// Die bisherigen Handler bleiben unveraendert; diese Huellen legen nur den Erfassungsbereich darum.
/// Tabellenzelle und Auswahlspalte erfassen nur das Feld selbst samt dem, was es ableitet
/// (<see cref="DatenaenderungsVerlauf.ZellSchrittFelder"/>); Formular und «Spalte leeren» den Datensatz.
/// </summary>
public partial class DataPage
{
    private readonly DatenVerlaufZellErfassung _verlaufZelle = new();

    private IDisposable? ErfasseVerlauf(HaltungRecord? record, string? feld, bool nurZelle = false)
        => record is not null && DataContext is DataPageViewModel vm
            ? vm.Verlauf.Erfasse(record, feld, nurZelle && feld is not null ? DatenaenderungsVerlauf.ZellSchrittFelder(feld) : null)
            : null;

    private void Grid_PreparingCellForEditMitVerlauf(object sender, DataGridPreparingCellForEditEventArgs e)
    {
        _verlaufZelle.Beginne(ErfasseVerlauf(e.Row?.Item as HaltungRecord, e.Column.GetValue(FrameworkElement.TagProperty) as string, nurZelle: true));
        Grid_PreparingCellForEdit(sender, e);
    }

    private void Grid_CellEditEndingMitVerlauf(object sender, DataGridCellEditEndingEventArgs e)
    {
        Grid_CellEditEnding(sender, e);
        _verlaufZelle.BeendeNachCommit(Dispatcher);
    }

    private void ComboBox_SelectionChangedMitVerlauf(object sender, SelectionChangedEventArgs e)
    {
        using var _ = ErfasseVerlauf(sender is ComboBox c ? ResolveRecordFromComboBox(c) : null, (sender as ComboBox)?.Tag as string, nurZelle: true);
        ComboBox_SelectionChanged(sender, e);
    }

    private void ComboBox_LostKeyboardFocusMitVerlauf(object sender, KeyboardFocusChangedEventArgs e)
    {
        using var _ = ErfasseVerlauf(sender is ComboBox c ? ResolveRecordFromComboBox(c) : null, (sender as ComboBox)?.Tag as string, nurZelle: true);
        ComboBox_LostKeyboardFocus(sender, e);
    }

    private void CommitHaltungDetailFieldMitVerlauf(HaltungRecord record, string fieldName, string? value)
    {
        using var _ = ErfasseVerlauf(record, fieldName);
        CommitHaltungDetailField(record, fieldName, value);
    }

    private void ClearColumnMitVerlauf(string fieldName, string displayName)
    {
        using var _ = DataContext is DataPageViewModel vm ? vm.Verlauf.ErfasseMehrere(vm.Records, $"Spalte leeren: {FieldCatalog.Get(fieldName).Label}") : null;
        ClearColumn(fieldName, displayName);
    }
}
