using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Player;

public sealed class CodingSidePanelControllerSet
{
    public CodingEventsListControls EventsList { get; private set; } = null!;

    public CodingStatisticsControls Statistics { get; private set; } = null!;

    public CodingInlineDefectDetailControls InlineDefectDetail { get; private set; } = null!;

    public CodingEventCreationPostActions EventCreationPostActions { get; private set; } = null!;

    public bool IsInitialized =>
        EventsList is not null &&
        Statistics is not null &&
        InlineDefectDetail is not null &&
        EventCreationPostActions is not null;

    public void Initialize(
        CodingEventsListControls eventsList,
        CodingStatisticsControls statistics,
        CodingInlineDefectDetailControls inlineDefectDetail,
        CodingEventCreationPostActions eventCreationPostActions)
    {
        ArgumentNullException.ThrowIfNull(eventsList);
        ArgumentNullException.ThrowIfNull(statistics);
        ArgumentNullException.ThrowIfNull(inlineDefectDetail);
        ArgumentNullException.ThrowIfNull(eventCreationPostActions);

        EventsList = eventsList;
        Statistics = statistics;
        InlineDefectDetail = inlineDefectDetail;
        EventCreationPostActions = eventCreationPostActions;
    }
}
