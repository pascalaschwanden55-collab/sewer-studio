namespace AuswertungPro.Next.UI.Player;

public sealed class PlayerPlaybackControlHost
{
    private readonly Func<bool> _readIsPlaying;
    private readonly Action<bool> _setPause;
    private readonly Action _play;
    private readonly Action _stop;
    private readonly Func<float> _readRate;
    private readonly Func<float, int> _setRate;
    private readonly Func<int> _readVolume;
    private readonly Action<int> _setVolume;
    private readonly Func<bool> _readMute;
    private readonly Action<bool> _setMute;
    private readonly Func<bool> _shouldStartPlayback;
    private readonly Action<string> _playPath;

    public PlayerPlaybackControlHost(
        Func<bool> readIsPlaying,
        Action<bool> setPause,
        Action play,
        Action stop,
        Func<float> readRate,
        Func<float, int> setRate,
        Func<int> readVolume,
        Action<int> setVolume,
        Func<bool> readMute,
        Action<bool> setMute,
        Func<bool> shouldStartPlayback,
        Action<string> playPath)
    {
        ArgumentNullException.ThrowIfNull(readIsPlaying);
        ArgumentNullException.ThrowIfNull(setPause);
        ArgumentNullException.ThrowIfNull(play);
        ArgumentNullException.ThrowIfNull(stop);
        ArgumentNullException.ThrowIfNull(readRate);
        ArgumentNullException.ThrowIfNull(setRate);
        ArgumentNullException.ThrowIfNull(readVolume);
        ArgumentNullException.ThrowIfNull(setVolume);
        ArgumentNullException.ThrowIfNull(readMute);
        ArgumentNullException.ThrowIfNull(setMute);
        ArgumentNullException.ThrowIfNull(shouldStartPlayback);
        ArgumentNullException.ThrowIfNull(playPath);

        _readIsPlaying = readIsPlaying;
        _setPause = setPause;
        _play = play;
        _stop = stop;
        _readRate = readRate;
        _setRate = setRate;
        _readVolume = readVolume;
        _setVolume = setVolume;
        _readMute = readMute;
        _setMute = setMute;
        _shouldStartPlayback = shouldStartPlayback;
        _playPath = playPath;
    }

    public bool IsPlaying => _readIsPlaying();

    public float Rate => _readRate();

    public int Volume => _readVolume();

    public bool IsMuted => _readMute();

    public bool ShouldStartPlayback => _shouldStartPlayback();

    public void SetPause(bool pause)
        => _setPause(pause);

    public void Play()
        => _play();

    public void Stop()
        => _stop();

    public int SetRate(float rate)
        => _setRate(rate);

    public void SetVolume(int volume)
        => _setVolume(Math.Clamp(volume, 0, 100));

    public void SetMute(bool mute)
        => _setMute(mute);

    public void PlayPath(string path)
        => _playPath(path);
}
