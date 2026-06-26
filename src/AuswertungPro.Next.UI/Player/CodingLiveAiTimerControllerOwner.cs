using System.Windows.Controls.Primitives;

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

    public CodingLiveAiTimerController Ensure(
        ToggleButton button,
        EventHandler analysisTick,
        Func<bool> canBlink)
    {
        ArgumentNullException.ThrowIfNull(button);
        ArgumentNullException.ThrowIfNull(analysisTick);
        ArgumentNullException.ThrowIfNull(canBlink);

        return Ensure(() => new CodingLiveAiTimerController(button, analysisTick, canBlink));
    }

    public void Stop(bool resetButton)
    {
        Controller?.Stop(resetButton);
    }
}
