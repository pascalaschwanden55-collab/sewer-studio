using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public sealed record CodingProtocolImportTrainingConfirmationWorkflowActions(
    Func<Action<CodingEvent>, Func<string?>, Func<string, CodingEvent, Task<CodingProtocolVerificationResult?>>?, CodingProtocolImportTrainingWorkflowService> CreateService);

public static class CodingProtocolImportTrainingConfirmationWorkflow
{
    public static Task<CodingProtocolImportTrainingResult> ConfirmAsync(
        CodingEvent importEvent,
        Action<CodingEvent> seekToImportEvent,
        Func<string?> captureSnapshot)
        => ConfirmAsync(
            importEvent,
            seekToImportEvent,
            captureSnapshot,
            verifyProtocolAsync: null);

    public static Task<CodingProtocolImportTrainingResult> ConfirmAsync(
        CodingEvent importEvent,
        Action<CodingEvent> seekToImportEvent,
        Func<string?> captureSnapshot,
        Func<string, CodingEvent, Task<CodingProtocolVerificationResult?>>? verifyProtocolAsync)
        => ConfirmAsync(
            importEvent,
            seekToImportEvent,
            captureSnapshot,
            verifyProtocolAsync,
            new CodingProtocolImportTrainingConfirmationWorkflowActions(
                CreateService: CodingProtocolImportTrainingWorkflowServiceFactory.Create));

    public static async Task<CodingProtocolImportTrainingResult> ConfirmAsync(
        CodingEvent importEvent,
        Action<CodingEvent> seekToImportEvent,
        Func<string?> captureSnapshot,
        CodingProtocolImportTrainingConfirmationWorkflowActions actions)
        => await ConfirmAsync(
            importEvent,
            seekToImportEvent,
            captureSnapshot,
            verifyProtocolAsync: null,
            actions);

    public static async Task<CodingProtocolImportTrainingResult> ConfirmAsync(
        CodingEvent importEvent,
        Action<CodingEvent> seekToImportEvent,
        Func<string?> captureSnapshot,
        Func<string, CodingEvent, Task<CodingProtocolVerificationResult?>>? verifyProtocolAsync,
        CodingProtocolImportTrainingConfirmationWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(importEvent);
        ArgumentNullException.ThrowIfNull(seekToImportEvent);
        ArgumentNullException.ThrowIfNull(captureSnapshot);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.CreateService);

        var service = actions.CreateService(seekToImportEvent, captureSnapshot, verifyProtocolAsync);
        ArgumentNullException.ThrowIfNull(service);

        return await service.ConfirmAsync(importEvent);
    }
}
