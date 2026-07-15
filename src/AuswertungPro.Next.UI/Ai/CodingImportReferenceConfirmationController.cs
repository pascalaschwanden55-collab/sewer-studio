using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public enum CodingImportReferenceConfirmationOutcome
{
    MissingSelection,
    MissingCode,
    Confirmed
}

public sealed record CodingImportReferenceConfirmationActions(
    Action ShowMissingCode,
    Func<CodingEvent, Task> PersistTrainingSampleAsync,
    Action ShowSuccess,
    Action RefreshProtocolMatch);

/// <summary>Bestaetigt einen Importbefund fuer Training und Wissensdatenbank.</summary>
public sealed class CodingImportReferenceConfirmationController
{
    public async Task<CodingImportReferenceConfirmationOutcome> ExecuteAsync(
        CodingEvent? selectedEvent,
        CodingImportReferenceConfirmationActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        if (selectedEvent is null)
            return CodingImportReferenceConfirmationOutcome.MissingSelection;

        if (string.IsNullOrWhiteSpace(selectedEvent.Entry.Code))
        {
            actions.ShowMissingCode();
            return CodingImportReferenceConfirmationOutcome.MissingCode;
        }

        CodingEventDecisionPolicy.ApplyManualReviewDecision(
            selectedEvent,
            CodingUserDecision.Accepted,
            "Import bestaetigt (ins Brain)");
        await actions.PersistTrainingSampleAsync(selectedEvent);
        actions.ShowSuccess();
        actions.RefreshProtocolMatch();
        return CodingImportReferenceConfirmationOutcome.Confirmed;
    }
}
