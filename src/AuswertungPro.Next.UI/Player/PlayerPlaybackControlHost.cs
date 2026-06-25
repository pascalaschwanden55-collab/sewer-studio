namespace AuswertungPro.Next.UI.Player;

public sealed class PlayerPlaybackControlHost
{
    private readonly Func<bool> _readIsPlaying;
    private readonly Action<bool> _setPause;
    private readonly Action _play;
    private readonly Action _stop;
    private readonly Func<float> _readRate;
    private readonly Func<float, int> _setRate;

    public PlayerPlaybackControlHost(
        Func<bool> readIsPlaying,
        Action<bool> setPause,
        Action play,
        Action stop,
        Func<float> readRate,
        Func<float, int> setRate)
    {
        ArgumentNullException.ThrowIfNull(readIsPlaying);
        ArgumentNullException.ThrowIfNull(setPause);
        ArgumentNullException.ThrowIfNull(play);
        ArgumentNullException.ThrowIfNull(stop);
        ArgumentNullException.ThrowIfNull(readRate);
        ArgumentNullException.ThrowIfNull(setRate);

        _readIsPlaying = readIsPlaying;
        _setPause = setPause;
        _play = play;
        _stop = stop;
        _readRate = readRate;
        _setRate = setRate;
    }

    public bool IsPlaying => _readIsPlaying();

    public float Rate => _readRate();

    public void SetPause(bool pause)
        => _setPause(pause);

    public void Play()
        => _play();

    public void Stop()
        => _stop();

    public int SetRate(float rate)
        => _setRate(rate);
}
