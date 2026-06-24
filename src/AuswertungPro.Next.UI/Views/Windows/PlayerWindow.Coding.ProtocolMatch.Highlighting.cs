using System.Windows.Controls;
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
                CodingProtocolMatchHighlightControls.Clear(container, emptyBadge);
                continue;
            }

            var badge = FindCodingChild<Border>(container, "CodingMatchBadge");
            var badgeText = FindCodingChild<TextBlock>(container, "TxtCodingMatchBadge");
            CodingProtocolMatchHighlightControls.Apply(container, badge, badgeText, bucket);
        }
    }
}
