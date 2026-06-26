namespace AuswertungPro.Next.UI.Player;

public sealed class CodingLiveAiTimerControllerOwner
{
    public CodingLiveAiTimerController? Controller { get; private set; }

    public bool HasController => Controller is not null;

    public CodingLiveAiTimerController Ensure(Func<CodingLiveAiTimerController> createController)
    {
        ArgumentNullException.ThrowIfNull(createController);

        return Controller ??= createController();
    }

    public void Stop(bool resetButton)
    {
        Controller?.Stop(resetButton);
    }
}
