using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Helpers;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Windows;

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

    private void UpdateInlineEvidencePreview(CodingEvent ev)
    {
        try
        {
            var previewPath = CodingDefectPreviewService.BuildPreviewImagePath(ev);
            if (string.IsNullOrWhiteSpace(previewPath) || !File.Exists(previewPath))
            {
                ImgInlineEvidencePreview.Source = null;
                ImgInlineEvidencePreview.Visibility = Visibility.Collapsed;
                TxtInlineEvidencePreviewStatus.Text = "Kein Bild";
                TxtInlineEvidencePreviewStatus.Visibility = Visibility.Visible;
                return;
            }

            var image = new System.Windows.Media.Imaging.BitmapImage();
            image.BeginInit();
            image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(previewPath, UriKind.Absolute);
            image.EndInit();
            image.Freeze();

            ImgInlineEvidencePreview.Source = image;
            ImgInlineEvidencePreview.Visibility = Visibility.Visible;
            TxtInlineEvidencePreviewStatus.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            ImgInlineEvidencePreview.Source = null;
            ImgInlineEvidencePreview.Visibility = Visibility.Collapsed;
            TxtInlineEvidencePreviewStatus.Text = "Bild nicht ladbar";
            TxtInlineEvidencePreviewStatus.Visibility = Visibility.Visible;
            System.Diagnostics.Debug.WriteLine($"[CodingPreview] {ex.Message}");
        }
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


    /// <summary>Zone-Dots und Konfidenz-Texte in der Event-ListBox einfaerben.</summary>
    private void ColorizeCodingEventListItems()
    {
        for (int i = 0; i < LstCodingEvents.Items.Count; i++)
        {
            if (LstCodingEvents.ItemContainerGenerator.ContainerFromIndex(i) is not ListBoxItem container) continue;
            if (LstCodingEvents.Items[i] is not CodingEvent ev) continue;

            // Zone-Dot einfaerben: Der Punkt zeigt NUR den Pruef-Status, die Konfidenz steht
            // als farbige Prozentzahl daneben (TxtConfidence). "Offen/noch nicht entschieden"
            // = grau, damit der Punkt nicht mehr mit der Konfidenz-Ampel verwechselt wird
            // (frueher faerbte er offene Befunde nach Konfidenz -> alle gleich orange).
            var zoneDot = FindCodingChild<System.Windows.Shapes.Ellipse>(container, "ZoneDot");
            if (zoneDot != null)
            {
                var status = CodingSessionViewModel.GetDefectStatus(ev);
                zoneDot.Fill = new SolidColorBrush(CodingDefectStatusDisplayPolicy.ZoneDotColor(status));
            }

            // Konfidenz-Text einfaerben
            var confText = FindCodingChild<TextBlock>(container, "TxtConfidence");
            if (confText != null && ev.AiContext != null)
            {
                confText.Text = $"{ev.AiContext.Confidence * 100:F0}%";
                confText.Foreground = CodingSessionViewModel.GetConfidenceBrush(ev.AiContext.Confidence);
            }
            else if (confText != null)
            {
                confText.Text = "";
            }

            // Status-Icon
            var statusIcon = FindCodingChild<TextBlock>(container, "TxtStatusIcon");
            if (statusIcon != null)
            {
                var status = CodingSessionViewModel.GetDefectStatus(ev);
                statusIcon.Text = CodingDefectStatusDisplayPolicy.StatusIcon(status);
                statusIcon.Foreground = CodingSessionViewModel.GetStatusBrush(status);
            }
        }

        ApplyCodingProtocolMatchListHighlights();
    }


    /// <summary>Rekursiv ein benanntes Kind-Element im VisualTree finden.</summary>
    private static T? FindCodingChild<T>(DependencyObject parent, string childName) where T : FrameworkElement
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T t && t.Name == childName)
                return t;
            var found = FindCodingChild<T>(child, childName);
            if (found != null) return found;
        }
        return null;
    }

}
