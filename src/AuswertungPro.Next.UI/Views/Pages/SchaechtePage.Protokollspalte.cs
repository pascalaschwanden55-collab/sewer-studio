using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Controls;
using AuswertungPro.Next.UI.DataPage;
using CommunityToolkit.Mvvm.Input;

namespace AuswertungPro.Next.UI.Views.Pages;

/// <summary>
/// Nova-Etappe 2b (Inventar 4.4): Die nur lesende Protokollspalte der Schachtliste.
///
/// In "Kompakt" und "Dokumente und Medien" steht statt des rohen Dateipfads ein Knopf "PDF".
/// Er ist eine virtuelle Spalte (<see cref="NovaStatusSpalten.Protokoll"/>): kein Feld, kein
/// Export, keine Layout-Persistenz. Bearbeitet wird der Pfad weiterhin in "Alle Spalten" und in
/// den Eingabefeldern.
/// </summary>
public partial class SchaechtePage
{
    private ICommand? _protokollOeffnenCommand;

    /// <summary>
    /// Oeffnet das Schachtprotokoll der angeklickten Zeile. Der Befehl nimmt genau denselben
    /// Weg wie das Kontextmenue und die alte Schachtansicht
    /// (<c>RouteSchachtansichtAction("openpdf", ...)</c>) — kein zweiter Dateizugriff, keine
    /// zweite Fehlermeldung.
    /// </summary>
    public ICommand ProtokollOeffnenCommand
        => _protokollOeffnenCommand ??= new RelayCommand<SchachtRecord>(OeffneProtokoll);

    private void OeffneProtokoll(SchachtRecord? record)
    {
        if (record is null)
            return;

        RouteSchachtansichtAction("openpdf", record);
    }

    /// <summary>
    /// Haengt die virtuelle Protokollspalte hinter die Feldspalten. Sie taucht in
    /// <c>_columnFields</c> unter ihrem virtuellen Schluessel auf, damit die Spaltenansichten
    /// sie ein- und ausblenden koennen; die Layout-Speicherung ueberspringt genau solche
    /// <c>Nova_*</c>-Schluessel (siehe <see cref="DataGridColumnLayoutController"/>).
    /// </summary>
    private void ErgaenzeProtokollspalte()
    {
        var kopf = GrossbuchstabenConverter.Anwenden("Protokoll") ?? "Protokoll";
        var spalte = SchaechteProtokollColumnFactory.Create(kopf);
        spalte.SetValue(FrameworkElement.TagProperty, NovaStatusSpalten.Protokoll);

        Grid.Columns.Add(spalte);
        _columnFields[spalte] = NovaStatusSpalten.Protokoll;
        _columnAlignmentToolbar.SetAlignment(spalte, HorizontalAlignment.Left, VerticalAlignment.Center);
    }
}
