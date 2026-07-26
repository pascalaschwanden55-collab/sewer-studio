using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingImportReferenceConfirmationOutcome
{
    MissingSelection,
    MissingCode,
    Confirmed,
    PersistenceFailed
}

public sealed record CodingImportReferenceConfirmationActions(
    Action ShowMissingCode,
    Func<CodingEvent, Task> PersistTrainingSampleAsync,
    Action ShowSuccess,
    Action RefreshProtocolMatch,
    Func<CodingEvent, Task<CodingTrainingSamplePersistenceResult>>? PersistTrainingSampleWithResultAsync = null,
    Action<string>? ShowPersistenceError = null);

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
        CodingTrainingSamplePersistenceResult persistence;
        try
        {
            if (actions.PersistTrainingSampleWithResultAsync is not null)
            {
                persistence = await actions.PersistTrainingSampleWithResultAsync(selectedEvent);
            }
            else
            {
                await actions.PersistTrainingSampleAsync(selectedEvent);
                persistence = CodingTrainingSamplePersistenceResult.Ok;
            }
        }
        catch (Exception ex)
        {
            persistence = CodingTrainingSamplePersistenceResult.Failed(ex.Message);
        }

        if (!persistence.Success)
        {
            actions.ShowPersistenceError?.Invoke(
                persistence.Error ?? "Training konnte nicht gespeichert werden.");
            return CodingImportReferenceConfirmationOutcome.PersistenceFailed;
        }

        actions.ShowSuccess();
        actions.RefreshProtocolMatch();
        return CodingImportReferenceConfirmationOutcome.Confirmed;
    }
}
