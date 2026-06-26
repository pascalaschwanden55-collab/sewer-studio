namespace AuswertungPro.Next.UI.Ai;

public sealed record CodingApplyEmptyProtocolDialogWorkflowActions(
    Func<CodingApplyDialogService> CreateDialogService);

public static class CodingApplyEmptyProtocolDialogWorkflow
{
    public static bool Execute(CodingApplyEmptyProtocolGuardResult guard)
        => Execute(
            guard,
            new CodingApplyEmptyProtocolDialogWorkflowActions(
                CreateDialogService: CodingApplyDialogServiceFactory.Create));

    public static bool Execute(
        CodingApplyEmptyProtocolGuardResult guard,
        CodingApplyEmptyProtocolDialogWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(guard);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.CreateDialogService);

        var service = actions.CreateDialogService();
        ArgumentNullException.ThrowIfNull(service);

        return service.ConfirmEmptyProtocol(guard);
    }
}
