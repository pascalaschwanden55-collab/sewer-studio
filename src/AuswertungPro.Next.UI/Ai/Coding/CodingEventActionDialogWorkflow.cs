namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed record CodingEventActionDialogWorkflowActions(
    Func<CodingEventActionDialogService> CreateDialogService,
    Func<Func<bool>, bool> RunWithSuspendedOverlay);

public static class CodingEventActionDialogWorkflow
{
    public static void ShowStretchCloseRequiresLaterMeter()
        => ShowStretchCloseRequiresLaterMeter(
            new CodingEventActionDialogWorkflowActions(
                CreateDialogService: CodingEventActionDialogServiceFactory.Create,
                RunWithSuspendedOverlay: callback => callback()));

    public static void ShowStretchCloseRequiresLaterMeter(
        CodingEventActionDialogWorkflowActions actions)
    {
        var service = Create(actions);

        service.ShowStretchCloseRequiresLaterMeter();
    }

    public static bool ConfirmDelete(
        string? code,
        Func<Func<bool>, bool> runWithSuspendedOverlay)
        => ConfirmDelete(
            code,
            new CodingEventActionDialogWorkflowActions(
                CreateDialogService: CodingEventActionDialogServiceFactory.Create,
                RunWithSuspendedOverlay: runWithSuspendedOverlay));

    public static bool ConfirmDelete(
        string? code,
        CodingEventActionDialogWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.RunWithSuspendedOverlay);

        return actions.RunWithSuspendedOverlay(() =>
        {
            var service = Create(actions);

            return service.ConfirmDelete(code);
        });
    }

    private static CodingEventActionDialogService Create(
        CodingEventActionDialogWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.CreateDialogService);

        var service = actions.CreateDialogService();
        ArgumentNullException.ThrowIfNull(service);

        return service;
    }
}
