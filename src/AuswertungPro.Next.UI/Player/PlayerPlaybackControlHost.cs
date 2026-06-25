namespace AuswertungPro.Next.UI.Player;

public sealed class PlayerPlaybackControlHost
{
    private readonly Func<bool> _readIsPlaying;
    private readonly Action<bool> _setPause;
    private readonly Action _play;
    private readonly Action _stop;

    public PlayerPlaybackControlHost(
        Func<bool> readIsPlaying,
        Action<bool> setPause,
        Action play,
        Action stop)
    {
        ArgumentNullException.ThrowIfNull(readIsPlaying);
        ArgumentNullException.ThrowIfNull(setPause);
        ArgumentNullException.ThrowIfNull(play);
        ArgumentNullException.ThrowIfNull(stop);

        _readIsPlaying = readIsPlaying;
        _setPause = setPause;
        _play = play;
        _stop = stop;
    }

    public bool IsPlaying => _readIsPlaying();

    public void SetPause(bool pause)
        => _setPause(pause);

    public void Play()
        => _play();

    public void Stop()
        => _stop();
}
