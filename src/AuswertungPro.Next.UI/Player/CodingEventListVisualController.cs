using System.Windows.Controls;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Behaviors;

namespace AuswertungPro.Next.UI.Player;

/// <summary>
/// Aktualisiert die sichtbaren Zustands- und Abgleichmarkierungen der Coding-Listen.
/// </summary>
public sealed class CodingEventListVisualController
{
    private readonly ListBox _codingEvents;
    private readonly ListBox _importEvents;
    private readonly CodingProtocolMatchStateController _protocolMatchState;

    public CodingEventListVisualController(
        ListBox codingEvents,
        ListBox importEvents,
        CodingProtocolMatchStateController protocolMatchState)
    {
        _codingEvents = codingEvents ?? throw new ArgumentNullException(nameof(codingEvents));
        _importEvents = importEvents ?? throw new ArgumentNullException(nameof(importEvents));
        _protocolMatchState = protocolMatchState
            ?? throw new ArgumentNullException(nameof(protocolMatchState));
    }

    public void ColorizeCodingEvents()
    {
        var codingEvents = _codingEvents.Items.OfType<CodingEvent>().ToList();
        CodingEventListItemColorizeWorkflow.Execute(
            new CodingEventListItemColorizeWorkflowRequest(_codingEvents.Items.Count),
            new CodingEventListItemColorizeWorkflowActions(
                TryApplyItem: index => TryColorizeCodingEvent(index, codingEvents),
                RefreshHighlights: ApplyProtocolMatchHighlights));
    }

    public void ApplyProtocolMatchHighlights()
    {
        ApplyProtocolMatchHighlights(_codingEvents);
        ApplyProtocolMatchHighlights(_importEvents);
    }

    private bool TryColorizeCodingEvent(int index, IReadOnlyList<CodingEvent> codingEvents)
    {
        if (_codingEvents.ItemContainerGenerator.ContainerFromIndex(index) is not ListBoxItem container)
            return false;
        if (_codingEvents.Items[index] is not CodingEvent codingEvent)
            return false;

        var zoneDot = VisualTreeSafe.FindNamedDescendant<System.Windows.Shapes.Ellipse>(
            container,
            "ZoneDot");
        var confidenceText = VisualTreeSafe.FindNamedDescendant<TextBlock>(
            container,
            "TxtConfidence");
        var statusIcon = VisualTreeSafe.FindNamedDescendant<TextBlock>(
            container,
            "TxtStatusIcon");
        var meterText = VisualTreeSafe.FindNamedDescendant<TextBlock>(
            container,
            "TxtEventMeter");
        var stretchBadge = VisualTreeSafe.FindNamedDescendant<Border>(
            container,
            "StretchOpenBadge");

        CodingEventListItemControls.Apply(
            zoneDot,
            confidenceText,
            statusIcon,
            meterText,
            stretchBadge,
            codingEvent,
            codingEvents);
        return true;
    }

    private void ApplyProtocolMatchHighlights(ListBox listBox)
    {
        CodingProtocolMatchListHighlightWorkflow.Execute(
            new CodingProtocolMatchListHighlightWorkflowRequest(listBox.Items.Count),
            new CodingProtocolMatchListHighlightWorkflowActions(
                HighlightItem: index => ApplyProtocolMatchHighlight(listBox, index)));
    }

    private CodingProtocolMatchListHighlightItemOutcome ApplyProtocolMatchHighlight(
        ListBox listBox,
        int index)
    {
        if (listBox.ItemContainerGenerator.ContainerFromIndex(index) is not ListBoxItem container)
            return CodingProtocolMatchListHighlightItemOutcome.Skipped;

        if (listBox.Items[index] is not CodingEvent codingEvent
            || !_protocolMatchState.TryGetBucket(codingEvent.Entry.EntryId, out var bucket))
        {
            var emptyBadge = VisualTreeSafe.FindNamedDescendant<Border>(
                container,
                "CodingMatchBadge");
            CodingProtocolMatchHighlightControls.Clear(container, emptyBadge);
            return CodingProtocolMatchListHighlightItemOutcome.Cleared;
        }

        var badge = VisualTreeSafe.FindNamedDescendant<Border>(container, "CodingMatchBadge");
        var badgeText = VisualTreeSafe.FindNamedDescendant<TextBlock>(
            container,
            "TxtCodingMatchBadge");
        CodingProtocolMatchHighlightControls.Apply(container, badge, badgeText, bucket);
        return CodingProtocolMatchListHighlightItemOutcome.Highlighted;
    }
}
