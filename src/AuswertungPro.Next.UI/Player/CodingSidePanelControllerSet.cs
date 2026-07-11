using System.Windows.Controls;
using System.Windows.Documents;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Player;

public sealed record CodingSidePanelControllerControls(
    ListBox CodingEvents,
    Run CodingDefectCount,
    Run CodingOpenCount,
    TextBlock CodingStatAiCriteriaMet,
    TextBlock CodingStatHumanAccepted,
    TextBlock CodingStatHumanCorrected,
    TextBlock CodingStatRejected,
    TextBlock CodingStatOpen,
    TextBlock CodingStatAvgAiConfidence,
    TextBlock InlineDetailCode,
    TextBlock InlineDetailDescription,
    TextBlock InlineDetailDistance,
    TextBlock InlineDetailConfidence,
    TextBlock InlineDetailStatus,
    Image InlineEvidencePreview,
    TextBlock InlineEvidencePreviewStatus,
    Button InlineAccept,
    Button InlineReject,
    Border DefectDetailInline,
    ColumnDefinition DefectDetailColumn);

public sealed record CodingSidePanelControllerActions(
    Action RefreshEvents,
    Action<CodingEvent> SelectCreatedEvent,
    Action CancelSchema,
    Action ClearCurrentOverlay,
    Action ClearSelectedCode,
    Action RedrawCanvas,
    Action ClearSelectedCodeText,
    Action DisableCreateEvent,
    Action ClearOverlayInfo);

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

    public void Initialize(CodingSidePanelControllerControls controls, CodingSidePanelControllerActions actions)
    {
        ArgumentNullException.ThrowIfNull(controls);
        ArgumentNullException.ThrowIfNull(actions);

        Initialize(
            new CodingEventsListControls(controls.CodingEvents),
            new CodingStatisticsControls(
                controls.CodingDefectCount,
                controls.CodingOpenCount,
                controls.CodingStatAiCriteriaMet,
                controls.CodingStatHumanAccepted,
                controls.CodingStatHumanCorrected,
                controls.CodingStatRejected,
                controls.CodingStatOpen,
                controls.CodingStatAvgAiConfidence),
            new CodingInlineDefectDetailControls(
                controls.InlineDetailCode,
                controls.InlineDetailDescription,
                controls.InlineDetailDistance,
                controls.InlineDetailConfidence,
                controls.InlineDetailStatus,
                controls.InlineEvidencePreview,
                controls.InlineEvidencePreviewStatus,
                controls.InlineAccept,
                controls.InlineReject,
                controls.DefectDetailInline,
                controls.DefectDetailColumn),
            new CodingEventCreationPostActions(
                actions.RefreshEvents,
                actions.SelectCreatedEvent,
                actions.CancelSchema,
                actions.ClearCurrentOverlay,
                actions.ClearSelectedCode,
                actions.RedrawCanvas,
                actions.ClearSelectedCodeText,
                actions.DisableCreateEvent,
                actions.ClearOverlayInfo));
    }

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
