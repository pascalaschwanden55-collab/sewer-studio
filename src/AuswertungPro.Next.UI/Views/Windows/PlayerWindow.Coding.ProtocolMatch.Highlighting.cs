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
        CodingProtocolMatchListHighlightWorkflow.Execute(
            new CodingProtocolMatchListHighlightWorkflowRequest(listBox.Items.Count),
            new CodingProtocolMatchListHighlightWorkflowActions(
                HighlightItem: index =>
                {
                    if (listBox.ItemContainerGenerator.ContainerFromIndex(index) is not ListBoxItem container)
                        return CodingProtocolMatchListHighlightItemOutcome.Skipped;

                    if (listBox.Items[index] is not CodingEvent ev
                        || !_codingProtocolMatchState.TryGetBucket(ev.Entry.EntryId, out var bucket))
                    {
                        var emptyBadge = FindCodingChild<Border>(container, "CodingMatchBadge");
                        CodingProtocolMatchHighlightControls.Clear(container, emptyBadge);
                        return CodingProtocolMatchListHighlightItemOutcome.Cleared;
                    }

                    var badge = FindCodingChild<Border>(container, "CodingMatchBadge");
                    var badgeText = FindCodingChild<TextBlock>(container, "TxtCodingMatchBadge");
                    CodingProtocolMatchHighlightControls.Apply(container, badge, badgeText, bucket);
                    return CodingProtocolMatchListHighlightItemOutcome.Highlighted;
                }));
    }
}
