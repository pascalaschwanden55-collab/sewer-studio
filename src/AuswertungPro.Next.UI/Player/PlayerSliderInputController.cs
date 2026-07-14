namespace AuswertungPro.Next.UI.Player;

public sealed class PlayerSliderInputController
{
    private readonly PlayerControlInputController _controlInputController;
    private readonly PlayerPositionInputController _positionInputController;
    private readonly PlayerPositionSliderStateController _positionSliderStateController;
    private readonly PlayerWindowTimerController _timerController;

    public PlayerSliderInputController(PlayerWindowControllerSet controllers)
    {
        ArgumentNullException.ThrowIfNull(controllers);

        _controlInputController = controllers.ControlInputController;
        _positionInputController = controllers.PositionInputController;
        _positionSliderStateController = controllers.PositionSliderStateController;
        _timerController = controllers.TimerController;
    }

    public void SetSpeed(float rate) => _controlInputController.SetSpeed(rate);

    public void SetVolume(double volume) => _controlInputController.SetVolume(volume);

    public void SetOverlayOpacity(double opacity) => _controlInputController.SetOverlayOpacity(opacity);

    public void HandlePositionChanged()
        => PlayerPositionSliderValueChangedWorkflow.Execute(
            new PlayerPositionSliderValueChangedWorkflowRequest(_positionSliderStateController.IsDragging),
            new PlayerPositionSliderValueChangedWorkflowActions(
                () => _positionInputController.UpdateSeekPreview(
                    _positionSliderStateController.IsDragging,
                    _timerController.IsScrubTimerEnabled,
                    _timerController.StartScrubTimer)));
}
