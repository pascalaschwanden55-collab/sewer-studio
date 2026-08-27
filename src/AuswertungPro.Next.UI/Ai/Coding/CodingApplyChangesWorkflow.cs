using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingApplyChangesWorkflowOutcome
{
    NoCodingContext,
    NoEvents,
    EmptyProtocolCancelled,
    PipeEndCancelled,
    Applied
}

/// <summary>Entscheidung des Benutzers zum fehlenden Rohrende.</summary>
public enum CodingApplyPipeEndDecision
{
    /// <summary>Rohrende am vorgeschlagenen Meter setzen.</summary>
    Insert,

    /// <summary>Ohne Rohrende uebernehmen.</summary>
    Skip,

    /// <summary>Uebernehmen abbrechen, nichts schreiben.</summary>
    Cancel
}

/// <summary>
/// Vorschlag fuer das fehlende Rohrende. Ohne brauchbaren Meter ist
/// <see cref="ProposalMeter"/> null - dann sagt <see cref="RejectedLengthM"/>,
/// ob ueberhaupt keine Laenge bekannt ist (null) oder ob eine bekannte Laenge
/// verworfen wurde, weil sie nicht hinter der letzten Beobachtung liegt.
/// </summary>
public sealed record CodingApplyPipeEndPrompt(
    double? ProposalMeter,
    double? RejectedLengthM = null,
    double LastObservationM = 0);

public sealed record CodingApplyChangesWorkflowRequest(
    bool HasCodingViewModel,
    HaltungRecord? HaltungRecord,
    IReadOnlyList<CodingEvent>? Events,
    bool ShowOverlay,
    double? HaltungslaengeM = null);

public sealed record CodingApplyChangesWorkflowActions(
    Func<CodingApplyEmptyProtocolGuardResult, bool> ConfirmEmptyProtocol,
    Action<ProtocolEntry> AddAutomaticBoundaryEvent,
    Action<ProtocolDocument> AssignProtocol,
    Action MarkProjectDirty,
    Action<ProtocolDocument> SyncCodingToPrimaryDamages,
    Action<IReadOnlyList<CodingEvent>> PersistCodingEventsAsTrainingSamples,
    Action<string> SetBaselineSignature,
    Action SaveProjectAfterCoding,
    Action<string, TimeSpan> ShowOverlay,
    Func<CodingApplyPipeEndPrompt, CodingApplyPipeEndDecision>? ConfirmMissingPipeEnd = null);

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

        var boundaries = EnsureBoundaries(request, update, actions);
        if (boundaries.Cancelled)
            return Result(CodingApplyChangesWorkflowOutcome.PipeEndCancelled);

        // Erst nach der bestaetigten Grenzentscheidung die Sitzung aendern.
        // Dadurch bleibt Abbrechen seiteneffektfrei und der naechste Abgleich
        // erkennt dieselben Grenzen als echte Codier-Ereignisse wieder.
        foreach (var entry in boundaries.AddedEntries)
            actions.AddAutomaticBoundaryEvent(entry);

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
                : $"{update.EventEntryCount} Ereignisse in Prim\u00e4re Sch\u00e4den \u00fcbernommen{boundaries.Suffix}";
            actions.ShowOverlay(message, TimeSpan.FromSeconds(4));
        }

        return Result(CodingApplyChangesWorkflowOutcome.Applied);
    }

    /// <summary>
    /// Rohranfang still ergaenzen, Rohrende nur nach Rueckfrage. Eine bewusst leere
    /// Codierung bleibt leer - dort waere ein erfundener Rohranfang nur stoerend.
    /// </summary>
    private static BoundaryOutcome EnsureBoundaries(
        CodingApplyChangesWorkflowRequest request,
        CodingApplyProtocolUpdate update,
        CodingApplyChangesWorkflowActions actions)
    {
        if (update.EventEntryCount == 0)
            return BoundaryOutcome.None;

        var entries = update.CurrentRevision.Entries;
        var plan = ProtocolBoundaryService.PlanBoundaries(entries, request.HaltungslaengeM);
        var addedEntries = new List<ProtocolEntry>(capacity: 2);

        var startAdded = false;
        if (plan.PipeStartMissing)
        {
            addedEntries.Add(ProtocolBoundaryService.InsertPipeStart(entries));
            startAdded = true;
        }

        var endAdded = false;
        if (plan.PipeEndMissing && actions.ConfirmMissingPipeEnd is { } confirm)
        {
            var decision = confirm(new CodingApplyPipeEndPrompt(
                plan.PipeEndProposalMeter,
                plan.RejectedLengthM,
                plan.LastObservationM));
            if (decision == CodingApplyPipeEndDecision.Cancel)
                return BoundaryOutcome.Stopped;

            if (decision == CodingApplyPipeEndDecision.Insert
                && plan.PipeEndProposalMeter is { } endMeter)
            {
                addedEntries.Add(ProtocolBoundaryService.AppendPipeEnd(entries, endMeter));
                endAdded = true;
            }
        }

        return new BoundaryOutcome(
            Cancelled: false,
            Suffix: BuildBoundarySuffix(startAdded, endAdded),
            AddedEntries: addedEntries);
    }

    private static string BuildBoundarySuffix(bool startAdded, bool endAdded)
        => (startAdded, endAdded) switch
        {
            (true, true) => " \u00b7 Rohranfang und Rohrende erg\u00e4nzt",
            (true, false) => " \u00b7 Rohranfang erg\u00e4nzt",
            (false, true) => " \u00b7 Rohrende erg\u00e4nzt",
            _ => string.Empty
        };

    private sealed record BoundaryOutcome(
        bool Cancelled,
        string Suffix,
        IReadOnlyList<ProtocolEntry> AddedEntries)
    {
        public static BoundaryOutcome None { get; } = new(false, string.Empty, []);
        public static BoundaryOutcome Stopped { get; } = new(true, string.Empty, []);
    }

    private static CodingApplyChangesWorkflowResult Result(
        CodingApplyChangesWorkflowOutcome outcome)
        => new(outcome);
}
