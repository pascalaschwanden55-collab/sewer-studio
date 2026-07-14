namespace AuswertungPro.Next.UI.Player;

public sealed record PlayerPlaybackControllerActions(
    Action StartUpdateTimer,
    Action UpdateRateLabel,
    Action ClearDetectionOverlays,
    Action<long, long> ApplyPlaybackState,
    Action UpdateCodingCurrentCode);

public sealed class PlayerPlaybackController
{
    private readonly string _videoPath;
    private readonly PlayerPlaybackControlHost _playbackHost;
    private readonly PlayerTimelineHost _timelineHost;
    private readonly Func<bool> _isDragging;
    private readonly Func<bool> _isCodingMode;
    private readonly PlayerPlaybackControllerActions _actions;

    public PlayerPlaybackController(
        string videoPath,
        PlayerPlaybackControlHost playbackHost,
        PlayerTimelineHost timelineHost,
        Func<bool> isDragging,
        Func<bool> isCodingMode,
        PlayerPlaybackControllerActions actions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(videoPath);
        ArgumentNullException.ThrowIfNull(playbackHost);
        ArgumentNullException.ThrowIfNull(timelineHost);
        ArgumentNullException.ThrowIfNull(isDragging);
        ArgumentNullException.ThrowIfNull(isCodingMode);
        ArgumentNullException.ThrowIfNull(actions);

        _videoPath = videoPath;
        _playbackHost = playbackHost;
        _timelineHost = timelineHost;
        _isDragging = isDragging;
        _isCodingMode = isCodingMode;
        _actions = actions;
    }

    public bool TryGetCurrentTime(out TimeSpan time)
        => PlayerPlaybackGateway.TryGetCurrentTime(
            () => _timelineHost.TimeMilliseconds ?? 0,
            out time);

    public bool TrySeekTo(TimeSpan time)
        => PlayerPlaybackGateway.TrySeekTo(
            time,
            () => _timelineHost.LengthMilliseconds ?? 0,
            _timelineHost.SeekMilliseconds,
            EnsurePlaying,
            UpdateUi);

    public void TogglePlayPause()
        => PlayerPlaybackCommandRunner.TogglePlayPause(
            EnsurePlaying,
            () => _playbackHost.IsPlaying,
            _playbackHost.SetPause);

    public void Resume()
        => PlayerPlaybackCommandRunner.Play(
            EnsurePlaying,
            _playbackHost.SetPause,
            _actions.UpdateRateLabel,
            _actions.ClearDetectionOverlays);

    public void Pause()
        => PlayerPlaybackCommandRunner.Pause(
            _playbackHost.SetPause,
            _actions.UpdateRateLabel);

    public void Stop()
        => PlayerPlaybackCommandRunner.Stop(
            _playbackHost.Stop,
            _actions.UpdateRateLabel);

    public void EnsurePlaying()
        => PlayerPlaybackStartWorkflow.EnsurePlaying(
            new PlayerPlaybackEnsurePlayingRequest(
                _playbackHost.ShouldStartPlayback,
                _videoPath),
            new PlayerPlaybackEnsurePlayingActions(Play));

    public bool JumpSeconds(int seconds)
        => PlayerPlaybackCommandRunner.JumpSeconds(
            _timelineHost.TimeMilliseconds ?? 0,
            _timelineHost.LengthMilliseconds ?? 0,
            seconds,
            _timelineHost.SeekMilliseconds,
            _actions.ClearDetectionOverlays,
            UpdateUi);

    public void Play(string path)
        => PlayerPlaybackStartWorkflow.Play(
            new PlayerPlaybackStartRequest(path),
            new PlayerPlaybackStartActions(
                _playbackHost.PlayPath,
                _actions.StartUpdateTimer,
                _actions.UpdateRateLabel));

    public void UpdateUi()
        => PlayerUiUpdateWorkflow.Execute(
            new PlayerUiUpdateWorkflowRequest(
                _isDragging(),
                _isCodingMode(),
                _timelineHost.TimeMilliseconds ?? 0,
                _timelineHost.LengthMilliseconds ?? 0),
            new PlayerUiUpdateWorkflowActions(
                _actions.ApplyPlaybackState,
                _actions.UpdateRateLabel,
                _actions.UpdateCodingCurrentCode));
}
