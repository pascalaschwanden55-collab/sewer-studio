using System.Collections.ObjectModel;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingImportReferenceDropOutcome
{
    MissingCodingEvents,
    MissingSession,
    CopiedToCoding,
    MovedToImport
}

public sealed record CodingImportReferenceDropRequest(
    CodingEvent Event,
    bool TargetIsCoding,
    ObservableCollection<CodingEvent>? CodingEvents,
    ObservableCollection<CodingEvent> ImportEvents);

public sealed record CodingImportReferenceDropActions(
    Action<ProtocolEntry, OverlayGeometry?>? AddSessionEvent,
    Action<Guid>? RemoveSessionEvent);

public sealed record CodingImportReferenceDropResult(CodingImportReferenceDropOutcome Outcome)
{
    public bool Applied => Outcome is CodingImportReferenceDropOutcome.CopiedToCoding
        or CodingImportReferenceDropOutcome.MovedToImport;
}

/// <summary>Steuert Kopieren und Verschieben im Import-/KI-Abgleich.</summary>
public sealed class CodingImportReferenceDropController
{
    public CodingImportReferenceDropResult Execute(
        CodingImportReferenceDropRequest request,
        CodingImportReferenceDropActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Event);
        ArgumentNullException.ThrowIfNull(request.ImportEvents);
        ArgumentNullException.ThrowIfNull(actions);

        if (request.CodingEvents is null)
            return new CodingImportReferenceDropResult(CodingImportReferenceDropOutcome.MissingCodingEvents);

        if (request.TargetIsCoding)
        {
            if (actions.AddSessionEvent is null)
                return new CodingImportReferenceDropResult(CodingImportReferenceDropOutcome.MissingSession);

            var clone = CodingEventColumnTransfer.CloneWithNewIds(request.Event);
            actions.AddSessionEvent(clone.Entry, clone.Overlay);
            return new CodingImportReferenceDropResult(CodingImportReferenceDropOutcome.CopiedToCoding);
        }

        CodingEventColumnTransfer.Move(
            request.Event,
            request.CodingEvents,
            request.ImportEvents);
        actions.RemoveSessionEvent?.Invoke(request.Event.EventId);
        return new CodingImportReferenceDropResult(CodingImportReferenceDropOutcome.MovedToImport);
    }
}
