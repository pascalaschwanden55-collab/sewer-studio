using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai;

public enum CodingApplyChangesWorkflowOutcome
{
    NoCodingContext,
    NoEvents,
    EmptyProtocolCancelled,
    Applied
}

public sealed record CodingApplyChangesWorkflowRequest(
    bool HasCodingViewModel,
    HaltungRecord? HaltungRecord,
    IReadOnlyList<CodingEvent>? Events,
    bool ShowOverlay);

public sealed record CodingApplyChangesWorkflowActions(
    Func<CodingApplyEmptyProtocolGuardResult, bool> ConfirmEmptyProtocol,
    Action<ProtocolDocument> AssignProtocol,
    Action MarkProjectDirty,
    Action<ProtocolDocument> SyncCodingToPrimaryDamages,
    Action<IReadOnlyList<CodingEvent>> PersistCodingEventsAsTrainingSamples,
    Action<string> SetBaselineSignature,
    Action SaveProjectAfterCoding,
    Action<string, TimeSpan> ShowOverlay);

public sealed record CodingApplyChangesWorkflowResult(
    CodingApplyChangesWorkflowOutcome Outcome)
{
    public bool Applied => Outcome == CodingApplyChangesWorkflowOutcome.Applied;
}

public static class CodingApplyChangesWorkflow
{
    public static CodingApplyChangesWorkflowResult Execute(
        CodingApplyChangesWorkflowRequest request,
        CodingApplyChangesWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (!request.HasCodingViewModel || request.HaltungRecord is null)
            return Result(CodingApplyChangesWorkflowOutcome.NoCodingContext);

        if (request.Events is null)
            return Result(CodingApplyChangesWorkflowOutcome.NoEvents);

        var update = CodingApplyProtocolUpdateBuilder.Create(request.HaltungRecord, request.Events);
        var emptyGuard = CodingApplyEmptyProtocolGuard.Build(update.EventEntryCount, update.CurrentRevision.Entries);
        if (!actions.ConfirmEmptyProtocol(emptyGuard))
            return Result(CodingApplyChangesWorkflowOutcome.EmptyProtocolCancelled);

        CodingProtocolRevisionUpdater.ApplyCodingEvents(update.CurrentRevision, update.Events);

        actions.AssignProtocol(update.Document);
        actions.MarkProjectDirty();

        actions.SyncCodingToPrimaryDamages(update.Document);
        actions.MarkProjectDirty();

        actions.PersistCodingEventsAsTrainingSamples(update.Events);
        actions.SetBaselineSignature(CodingEventsSignatureBuilder.Build(request.Events));
        actions.SaveProjectAfterCoding();

        if (request.ShowOverlay)
        {
            var message = update.EventEntryCount == 0
                ? "Prim\u00e4re Sch\u00e4den geleert"
                : $"{update.EventEntryCount} Ereignisse in Prim\u00e4re Sch\u00e4den \u00fcbernommen";
            actions.ShowOverlay(message, TimeSpan.FromSeconds(4));
        }

        return Result(CodingApplyChangesWorkflowOutcome.Applied);
    }

    private static CodingApplyChangesWorkflowResult Result(
        CodingApplyChangesWorkflowOutcome outcome)
        => new(outcome);
}
