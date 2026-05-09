using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels.Windows;

namespace AuswertungPro.Next.UI.Views.Windows;

// CodingModeWindow Event-Listen-Pflege (Slice 8a.2.8 / Slice 8a.2.9):
// UI-Listen-Helfer fuer Status-Text-Mapping, Zone-Dot/Konfidenz/Status-
// Icon-Einfaerbung, VisualTree-FindChild, Statistik-Panel-Update und
// Listbox-Sortierungs-Refresh. Reine UI-Update-Logik. Sortierung selbst
// delegiert seit 8a.2.9 ans ViewModel (_vm.SortByMeter).
public partial class CodingModeWindow
{
    private static string StatusToDisplayText(DefectStatus status) => status switch
    {
        DefectStatus.AutoAccepted     => "Auto-Akzeptiert (Green Zone)",
        DefectStatus.Pending          => "Review empfohlen (Yellow Zone)",
        DefectStatus.ReviewRequired   => "Manuell erforderlich (Red Zone)",
        DefectStatus.Accepted         => "Akzeptiert",
        DefectStatus.AcceptedWithEdit => "Bearbeitet",
        DefectStatus.Rejected         => "Abgelehnt",
        _ => ""
    };

    /// <summary>Ereignisliste nach Meter aufsteigend sortieren + Listbox-Refresh.
    /// Sortierung delegiert ans ViewModel; nur die ListBox-Anzeige bleibt hier.</summary>
    private void ResortEventsByMeter()
    {
        if (_vm == null) return;

        var selected = LstEvents.SelectedItem;
        _vm.SortByMeter();

        // ItemsSource nullen + neu setzen erzwingt ein vollstaendiges
        // ItemContainer-Rebuild (sonst behaelt WPF teilweise alte Container-
        // Bindings nach Clear+Add). Reine UI-Sorge, bleibt im Window.
        LstEvents.ItemsSource = null;
        LstEvents.ItemsSource = _vm.Events;
        if (selected != null)
            LstEvents.SelectedItem = selected;
    }

    /// <summary>Zone-Dots und Konfidenz-Texte in der Event-ListBox einfaerben.</summary>
    private void ColorizeEventListItems()
    {
        for (int i = 0; i < LstEvents.Items.Count; i++)
        {
            if (LstEvents.ItemContainerGenerator.ContainerFromIndex(i) is not ListBoxItem container) continue;
            if (LstEvents.Items[i] is not CodingEvent ev) continue;

            // Zone-Dot finden und einfaerben
            var zoneDot = FindChild<Ellipse>(container, "ZoneDot");
            if (zoneDot != null)
            {
                if (ev.AiContext != null)
                    zoneDot.Fill = CodingSessionViewModel.GetConfidenceBrush(ev.AiContext.Confidence);
                else
                    zoneDot.Fill = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)); // Manuell = blau
            }

            // Konfidenz-Text finden und einfaerben
            var confText = FindChild<TextBlock>(container, "TxtConfidence");
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
            var statusIcon = FindChild<TextBlock>(container, "TxtStatusIcon");
            if (statusIcon != null)
            {
                var status = CodingSessionViewModel.GetDefectStatus(ev);
                statusIcon.Text = status switch
                {
                    DefectStatus.AutoAccepted     => "✓",
                    DefectStatus.Accepted         => "✓",
                    DefectStatus.AcceptedWithEdit  => "✎",
                    DefectStatus.Pending           => "⏳",
                    DefectStatus.ReviewRequired    => "⚠",
                    DefectStatus.Rejected          => "✗",
                    _ => ""
                };
                statusIcon.Foreground = CodingSessionViewModel.GetStatusBrush(status);
            }
        }
    }

    /// <summary>Rekursiv ein benanntes Kind-Element im VisualTree finden.</summary>
    private static T? FindChild<T>(DependencyObject parent, string childName) where T : FrameworkElement
    {
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T t && t.Name == childName)
                return t;
            var found = FindChild<T>(child, childName);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>Statistiken + Zaehlungen im Seitenpanel aktualisieren.</summary>
    private void UpdateStatistics()
    {
        RunDefectCount.Text = _vm.EventCount.ToString();

        int openCount = _vm.Events.Count(e =>
            e.AiContext != null &&
            CodingSessionViewModel.GetDefectStatus(e) is DefectStatus.Pending or DefectStatus.ReviewRequired);
        RunOpenCount.Text = openCount.ToString();

        TxtStatAutoAccepted.Text = _vm.StatAutoAccepted.ToString();
        TxtStatPending.Text = _vm.StatPending.ToString();
        TxtStatReviewRequired.Text = _vm.StatReviewRequired.ToString();
        TxtStatAvgConfidence.Text = _vm.StatAverageConfidence > 0
            ? $"{_vm.StatAverageConfidence * 100:F0}%"
            : "–";
    }
}
