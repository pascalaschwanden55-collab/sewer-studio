using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed record CodingOpenStretchDamageDialogWorkflowActions(
    Func<Func<CodingOpenStretchDamageDialogDecision>, CodingOpenStretchDamageDialogDecision> RunWithSuspendedOverlay,
    Func<CodingOpenStretchDamageDialogService> CreateDialogService);

public static class CodingOpenStretchDamageDialogWorkflow
{
    public static CodingOpenStretchDamageDialogDecision ConfirmClose(
        IReadOnlyList<CodingEvent> openEvents,
        double closeMeter,
        Func<Func<CodingOpenStretchDamageDialogDecision>, CodingOpenStretchDamageDialogDecision> runWithSuspendedOverlay)
        => ConfirmClose(
            openEvents,
            closeMeter,
            new CodingOpenStretchDamageDialogWorkflowActions(
                RunWithSuspendedOverlay: runWithSuspendedOverlay,
                CreateDialogService: CodingOpenStretchDamageDialogServiceFactory.Create));

    public static CodingOpenStretchDamageDialogDecision ConfirmClose(
        IReadOnlyList<CodingEvent> openEvents,
        double closeMeter,
        CodingOpenStretchDamageDialogWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(openEvents);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.RunWithSuspendedOverlay);
        ArgumentNullException.ThrowIfNull(actions.CreateDialogService);

        return actions.RunWithSuspendedOverlay(() =>
        {
            var service = actions.CreateDialogService();
            ArgumentNullException.ThrowIfNull(service);

            return service.ConfirmClose(openEvents, closeMeter);
        });
    }
}
