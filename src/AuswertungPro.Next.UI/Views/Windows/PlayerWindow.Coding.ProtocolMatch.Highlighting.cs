using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void ApplyCodingProtocolMatchListHighlights()
    {
        ApplyCodingProtocolMatchListHighlights(LstCodingEvents);
        ApplyCodingProtocolMatchListHighlights(LstImportEvents);
    }

    private void ApplyCodingProtocolMatchListHighlights(ListBox listBox)
    {
        for (var i = 0; i < listBox.Items.Count; i++)
        {
            if (listBox.ItemContainerGenerator.ContainerFromIndex(i) is not ListBoxItem container)
                continue;

            if (listBox.Items[i] is not CodingEvent ev
                || !_codingProtocolMatchBuckets.TryGetValue(ev.Entry.EntryId, out var bucket))
            {
                var emptyBadge = FindCodingChild<Border>(container, "CodingMatchBadge");
                if (emptyBadge != null)
                    emptyBadge.Visibility = Visibility.Collapsed;
                container.ClearValue(Control.BackgroundProperty);
                container.ClearValue(FrameworkElement.ToolTipProperty);
                continue;
            }

            container.Background = new SolidColorBrush(CodingProtocolMatchDisplayPolicy.BackgroundColor(bucket));
            container.ToolTip = CodingProtocolMatchDisplayPolicy.Tooltip(bucket);

            var badge = FindCodingChild<Border>(container, "CodingMatchBadge");
            var badgeText = FindCodingChild<TextBlock>(container, "TxtCodingMatchBadge");
            if (badge != null)
            {
                badge.Background = new SolidColorBrush(CodingProtocolMatchDisplayPolicy.BadgeColor(bucket));
                badge.Visibility = Visibility.Visible;
            }
            if (badgeText != null)
                badgeText.Text = CodingProtocolMatchDisplayPolicy.BadgeText(bucket);
        }
    }
}
