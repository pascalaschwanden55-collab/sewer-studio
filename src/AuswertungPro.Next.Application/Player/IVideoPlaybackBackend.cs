using System;

namespace AuswertungPro.Next.Application.Player;

public enum VideoPlaybackState
{
    NothingSpecial,
    Opening,
    Buffering,
    Playing,
    Paused,
    Stopped,
    Ended,
    Error
}

public interface IVideoPlaybackBackend : IDisposable
{
    object NativePlayer { get; }

    bool IsPlaying { get; }

    long LengthMs { get; }

    long TimeMs { get; set; }

    float Position { get; set; }

    float Rate { get; }

    VideoPlaybackState State { get; }

    void Play(string path);

    void Play();

    void SetPause(bool pause);

    void Stop();

    int SetRate(float rate);
}
