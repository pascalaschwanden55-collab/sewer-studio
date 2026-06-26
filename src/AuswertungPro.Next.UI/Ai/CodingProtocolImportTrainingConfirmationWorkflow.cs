using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public sealed record CodingProtocolImportTrainingConfirmationWorkflowActions(
    Func<Action<CodingEvent>, Func<string?>, CodingProtocolImportTrainingWorkflowService> CreateService);

public static class CodingProtocolImportTrainingConfirmationWorkflow
{
    public static async Task<CodingProtocolImportTrainingResult> ConfirmAsync(
        CodingEvent importEvent,
        Action<CodingEvent> seekToImportEvent,
        Func<string?> captureSnapshot,
        CodingProtocolImportTrainingConfirmationWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(importEvent);
        ArgumentNullException.ThrowIfNull(seekToImportEvent);
        ArgumentNullException.ThrowIfNull(captureSnapshot);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.CreateService);

        var service = actions.CreateService(seekToImportEvent, captureSnapshot);
        ArgumentNullException.ThrowIfNull(service);

        return await service.ConfirmAsync(importEvent);
    }
}
