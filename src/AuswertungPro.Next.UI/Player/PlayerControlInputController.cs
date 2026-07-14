namespace AuswertungPro.Next.UI.Player;

public sealed class PlayerControlInputController
{
    private readonly PlayerControlSettingsController _settingsController;
    private readonly PlayerControlSettingsView _settingsView;
    private readonly PlayerPlaybackControlHost _playbackHost;
    private readonly PlayerSpeedControls _speedControls;
    private readonly Action<float> _showUnsupportedRate;

    public PlayerControlInputController(
        PlayerControlSettingsController settingsController,
        PlayerControlSettingsView settingsView,
        PlayerPlaybackControlHost playbackHost,
        PlayerSpeedControls speedControls,
        Action<float> showUnsupportedRate)
    {
        _settingsController = settingsController ?? throw new ArgumentNullException(nameof(settingsController));
        _settingsView = settingsView ?? throw new ArgumentNullException(nameof(settingsView));
        _playbackHost = playbackHost ?? throw new ArgumentNullException(nameof(playbackHost));
        _speedControls = speedControls ?? throw new ArgumentNullException(nameof(speedControls));
        _showUnsupportedRate = showUnsupportedRate ?? throw new ArgumentNullException(nameof(showUnsupportedRate));
    }

    public bool IsEnabled { get; private set; }

    public void Initialize()
    {
        _settingsView.ApplyInitial(_settingsController.LoadInitial());
        UpdateRateLabel();
        IsEnabled = true;
    }

    public void SetSpeed(float rate)
    {
        if (!IsEnabled)
            return;

        PlayerPlaybackCommandRunner.SetSpeed(
            rate,
            _playbackHost.SetRate,
            _showUnsupportedRate,
            UpdateRateLabel);
    }

    public void ChangeSpeed(float delta)
        => SetSpeed(PlayerPlaybackState.ApplyRateDelta(_playbackHost.Rate, delta));

    public void SetVolume(double volume)
    {
        if (!IsEnabled)
            return;

        _settingsView.ApplyVolume(_settingsController.SetVolume(volume));
    }

    public void SetMuted(bool muted)
    {
        if (!IsEnabled)
            return;

        _settingsView.ApplyMuted(_settingsController.SetMuted(muted));
    }

    public void SetOverlayOpacity(double opacity)
    {
        if (!IsEnabled)
            return;

        _settingsView.ApplyOverlayOpacity(_settingsController.SetOverlayOpacity(opacity));
    }

    public void UpdateRateLabel() => _speedControls.Update(_playbackHost.Rate);
}
