using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    // Defekt-Detail-Panel, Aktionsbuttons und Listenfaerbung.

    private void CodingEvents_SelectionChanged(object sender, SelectionChangedEventArgs e)
        => CodingInlineDefectSelectionWorkflow.Execute(
            LstCodingEvents.SelectedItem,
            new CodingInlineDefectSelectionActions(
                SetSelectedDefect: _codingSessionHost.SelectDefect,
                UpdateInlineDefectDetail: UpdateInlineDefectDetail,
                HideInlineDefectDetail: HideInlineDefectDetail));

    /// <summary>Mittlere Spalte: kompakte Defekt-Details inline anzeigen.</summary>
    private void UpdateInlineDefectDetail(CodingEvent ev)
    {
        var state = CodingDefectStatusDisplayPolicy.BuildInlineDetail(ev);
        _codingInlineDefectDetailControls.Apply(state);
        UpdateInlineEvidencePreview(ev);
    }

    private double GetCodingSidePanelWidth()
        => CodingSidePanelWidthPolicy.Resolve(ActualWidth, Width);

    private void HideInlineDefectDetail()
    {
        _codingInlineDefectDetailControls.Hide();
    }

    private void CodingEvents_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        CodingEventListItemSelectionHelper.SelectContainingListBoxItem(e.OriginalSource as DependencyObject);
    }
}
