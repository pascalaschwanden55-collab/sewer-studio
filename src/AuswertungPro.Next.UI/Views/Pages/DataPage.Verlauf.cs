using System.Windows.Controls;
using System.Windows.Input;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Views.Pages;

/// <summary>Optik Aufgabe 16: Huellen der Haltungsseite um die alten Handler; die Logik steht in <see cref="DatenVerlaufSeitenAnbindung"/>.</summary>
public partial class DataPage
{
    private DatenVerlaufSeitenAnbindung VerlaufAnbindung => field ??= new(() => (DataContext as DataPageViewModel)?.Verlauf);
    private IDisposable? ErfasseCombo(object sender)
        => sender is ComboBox c ? VerlaufAnbindung.Erfasse(ResolveRecordFromComboBox(c), c.Tag as string, nurZelle: true) : null;
    private void Grid_PreparingCellForEditMitVerlauf(object sender, DataGridPreparingCellForEditEventArgs e)
        => VerlaufAnbindung.OeffneZelle(VerlaufAnbindung.Erfasse(e.Row?.Item as HaltungRecord, DatenVerlaufSeitenAnbindung.ZellFeld(e.Column), nurZelle: true), () => Grid_PreparingCellForEdit(sender, e));
    private void Grid_CellEditEndingMitVerlauf(object sender, DataGridCellEditEndingEventArgs e)
        => VerlaufAnbindung.CommitZelle(() => Grid_CellEditEnding(sender, e), Dispatcher);
    private void ComboBox_SelectionChangedMitVerlauf(object sender, SelectionChangedEventArgs e)
        => DatenVerlaufSeitenAnbindung.Umschliesse(ErfasseCombo(sender), () => ComboBox_SelectionChanged(sender, e));
    private void ComboBox_LostKeyboardFocusMitVerlauf(object sender, KeyboardFocusChangedEventArgs e)
        => DatenVerlaufSeitenAnbindung.Umschliesse(ErfasseCombo(sender), () => ComboBox_LostKeyboardFocus(sender, e));
    private void CommitHaltungDetailFieldMitVerlauf(HaltungRecord record, string fieldName, string? value)
        => DatenVerlaufSeitenAnbindung.Umschliesse(VerlaufAnbindung.Erfasse(record, fieldName), () => CommitHaltungDetailField(record, fieldName, value));
    private void ClearColumnMitVerlauf(string fieldName, string displayName) => DatenVerlaufSeitenAnbindung.Umschliesse(
        DataContext is DataPageViewModel vm ? VerlaufAnbindung.ErfasseSpalte(vm.Records, $"Spalte leeren: {FieldCatalog.Get(fieldName).Label}") : null,
        () => ClearColumn(fieldName, displayName));
}
