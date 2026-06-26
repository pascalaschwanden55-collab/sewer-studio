namespace AuswertungPro.Next.UI.Ai;

public sealed record CodingUnappliedChangesCloseDialogWorkflowActions(
    Func<Func<bool>, bool> RunWithSuspendedOverlay,
    Func<CodingApplyDialogService> CreateDialogService,
    Func<bool> ApplyChanges);

public static class CodingUnappliedChangesCloseDialogWorkflow
{
    public static bool Execute(
        Func<Func<bool>, bool> runWithSuspendedOverlay,
        Func<bool> applyChanges)
        => Execute(
            new CodingUnappliedChangesCloseDialogWorkflowActions(
                RunWithSuspendedOverlay: runWithSuspendedOverlay,
                CreateDialogService: CodingApplyDialogServiceFactory.Create,
                ApplyChanges: applyChanges));

    public static bool Execute(CodingUnappliedChangesCloseDialogWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.RunWithSuspendedOverlay);
        ArgumentNullException.ThrowIfNull(actions.CreateDialogService);
        ArgumentNullException.ThrowIfNull(actions.ApplyChanges);

        return actions.RunWithSuspendedOverlay(() =>
        {
            var service = actions.CreateDialogService();
            ArgumentNullException.ThrowIfNull(service);

            return service.ConfirmUnappliedChangesOnClose(actions.ApplyChanges);
        });
    }
}
