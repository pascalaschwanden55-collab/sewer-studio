namespace AuswertungPro.Next.UI.Player;

public sealed record PlayerPlaybackDialogWorkflowActions(
    Func<PlayerPlaybackDialogService> CreateDialogService);

public static class PlayerPlaybackDialogWorkflow
{
    public static void ShowUnsupportedRate(float rate)
        => ShowUnsupportedRate(
            rate,
            new PlayerPlaybackDialogWorkflowActions(
                CreateDialogService: PlayerPlaybackDialogServiceFactory.Create));

    public static void ShowUnsupportedRate(
        float rate,
        PlayerPlaybackDialogWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.CreateDialogService);

        var service = actions.CreateDialogService();
        ArgumentNullException.ThrowIfNull(service);

        service.ShowUnsupportedRate(rate);
    }
}
