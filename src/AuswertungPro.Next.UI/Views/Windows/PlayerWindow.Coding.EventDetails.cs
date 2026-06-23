using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;

using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    // Defekt-Detail-Panel, Aktionsbuttons und Listenfaerbung.

    private void CodingEvents_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LstCodingEvents.SelectedItem is CodingEvent ev)
        {
            if (_codingVm != null) _codingVm.SelectedDefect = ev;
            UpdateInlineDefectDetail(ev);
        }
        else
        {
            if (_codingVm != null) _codingVm.SelectedDefect = null;
            HideInlineDefectDetail();
        }
    }

    /// <summary>Mittlere Spalte: kompakte Defekt-Details inline anzeigen.</summary>
    private void UpdateInlineDefectDetail(CodingEvent ev)
    {
        var state = CodingDefectStatusDisplayPolicy.BuildInlineDetail(ev);
        TxtInlineDetailCode.Text = state.CodeText;
        TxtInlineDetailDesc.Text = state.DescriptionText;
        TxtInlineDetailDistance.Text = state.DistanceText;
        TxtInlineDetailConfidence.Text = state.ConfidenceText;

        if (state.Confidence.HasValue)
        {
            TxtInlineDetailConfidence.Foreground =
                ViewModels.Windows.CodingSessionViewModel.GetConfidenceBrush(state.Confidence.Value);
        }
        else
        {
            TxtInlineDetailConfidence.Foreground =
                new System.Windows.Media.SolidColorBrush(
                    PlayerStatusColors.Muted);
        }

        BtnInlineAccept.Visibility = state.CanAct ? Visibility.Visible : Visibility.Collapsed;
        BtnInlineReject.Visibility = state.CanAct ? Visibility.Visible : Visibility.Collapsed;
        TxtInlineDetailStatus.Text = state.StatusText;
        UpdateInlineEvidencePreview(ev);

        // Mittlere Spalte einblenden
        CodingDefectDetailInline.Visibility = Visibility.Visible;
        ColDefectDetail.Width = new GridLength(300);
    }

    private double GetCodingSidePanelWidth()
        => CodingSidePanelWidthPolicy.Resolve(ActualWidth, Width);

    private void HideInlineDefectDetail()
    {
        ImgInlineEvidencePreview.Source = null;
        ImgInlineEvidencePreview.Visibility = Visibility.Collapsed;
        TxtInlineEvidencePreviewStatus.Visibility = Visibility.Visible;
        CodingDefectDetailInline.Visibility = Visibility.Collapsed;
        ColDefectDetail.Width = new GridLength(0);
    }

    private void CodingEvents_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var dep = e.OriginalSource as DependencyObject;
        while (dep != null && dep is not ListBoxItem)
        {
            // Run/Inline-Elemente sind kein Visual — LogicalTreeHelper als Fallback
            dep = dep is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(dep)
                : LogicalTreeHelper.GetParent(dep);
        }

        if (dep is ListBoxItem item)
        {
            item.IsSelected = true;
            item.Focus();
        }
    }

}
