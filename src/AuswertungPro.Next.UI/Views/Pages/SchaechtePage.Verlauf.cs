using System.Windows.Controls;
using System.Windows.Input;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.Views.Pages.Schachtansicht;

namespace AuswertungPro.Next.UI.Views.Pages;

/// <summary>Optik Aufgabe 16: Huellen der Schachtseite um die alten Handler; die Logik steht in <see cref="DatenVerlaufSeitenAnbindung"/>.</summary>
public partial class SchaechtePage
{
    private DatenVerlaufSeitenAnbindung VerlaufAnbindung => field ??= new(() => _vm?.Verlauf);
    private IDisposable? ErfasseCombo(object sender)
        => sender is ComboBox c ? VerlaufAnbindung.Erfasse(ResolveRecordFromComboBox(c), (c.Tag as ComboBindingTag)?.RecordField, nurZelle: true) : null;
    private void Grid_BeginningEditMitVerlauf(object sender, DataGridBeginningEditEventArgs e) => VerlaufAnbindung.OeffneZelleNach(
        () => Grid_BeginningEdit(sender, e), () => !e.Cancel,
        () => VerlaufAnbindung.Erfasse(e.Row?.Item as SchachtRecord, DatenVerlaufSeitenAnbindung.ZellFeld(e.Column), nurZelle: true));
    private void Grid_CellEditEndingMitVerlauf(object sender, DataGridCellEditEndingEventArgs e)
        => VerlaufAnbindung.CommitZelle(() => Grid_CellEditEnding(sender, e), Dispatcher);
    private void ComboBox_SelectionChangedMitVerlauf(object sender, SelectionChangedEventArgs e)
        => DatenVerlaufSeitenAnbindung.Umschliesse(ErfasseCombo(sender), () => ComboBox_SelectionChanged(sender, e));
    private void ComboBox_LostKeyboardFocusMitVerlauf(object sender, KeyboardFocusChangedEventArgs e)
        => DatenVerlaufSeitenAnbindung.Umschliesse(ErfasseCombo(sender), () => ComboBox_LostKeyboardFocus(sender, e));
    private void CommitSchachtDetailMitVerlauf(SchachtRecord record, KonsolidiertesSchachtFeld feld, string? value)
        => DatenVerlaufSeitenAnbindung.Umschliesse(VerlaufAnbindung.Erfasse(record, feld.PrimaerKey), () => CommitSchachtDetailKonsolidiert(record, feld, value));
    private void ClearColumnMitVerlauf(string fieldName, string displayName) => DatenVerlaufSeitenAnbindung.Umschliesse(
        _vm is { } vm ? VerlaufAnbindung.ErfasseSpalte(vm.Records, $"Spalte leeren: {string.Join(' ', fieldName.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))}") : null,
        () => ClearColumn(fieldName, displayName));
}
