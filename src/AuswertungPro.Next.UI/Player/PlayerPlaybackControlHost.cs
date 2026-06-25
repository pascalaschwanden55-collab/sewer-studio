namespace AuswertungPro.Next.UI.Player;

public sealed class PlayerPlaybackControlHost
{
    private readonly Func<bool> _readIsPlaying;
    private readonly Action<bool> _setPause;
    private readonly Action _play;

    public PlayerPlaybackControlHost(
        Func<bool> readIsPlaying,
        Action<bool> setPause,
        Action play)
    {
        ArgumentNullException.ThrowIfNull(readIsPlaying);
        ArgumentNullException.ThrowIfNull(setPause);
        ArgumentNullException.ThrowIfNull(play);

        _readIsPlaying = readIsPlaying;
        _setPause = setPause;
        _play = play;
    }

    public bool IsPlaying => _readIsPlaying();

    public void SetPause(bool pause)
        => _setPause(pause);

    public void Play()
        => _play();
}
