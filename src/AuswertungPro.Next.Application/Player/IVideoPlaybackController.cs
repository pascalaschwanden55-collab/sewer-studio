using System;

namespace AuswertungPro.Next.Application.Player;

public readonly record struct PlaybackPositionSnapshot(
    long TimeMs,
    long LengthMs,
    float Rate);

public readonly record struct PlaybackSeekSnapshot(
    long? CurrentTimeMs,
    long? DurationMs,
    double Position);

public readonly record struct PlaybackRateChange(
    float RequestedRate,
    float AppliedRate,
    bool IsSupported);

public interface IVideoPlaybackController
{
    event EventHandler? PositionUpdateRequested;

    event EventHandler? ScrubRequested;

    // Slice 8a CodingMode VLC-Migration (Slice-1): Lifecycle-Events vom Backend.
    event EventHandler<long>? LengthChanged;

    event EventHandler<string>? EncounteredError;

    event EventHandler? FirstPlayingOnce;

    object NativePlayer { get; }

    bool IsDragging { get; }

    bool IsPlaying { get; }

    long LengthMs { get; }

    long TimeMs { get; set; }

    float CurrentRate { get; }

    void Play();

    void Play(string path);

    void EnsurePlaying();

    void Pause();

    void Resume();

    void Stop();

    bool TogglePlayPause();

    PlaybackRateChange SetSpeed(float rate);

    PlaybackRateChange ChangeSpeed(float delta);

    void BeginDrag(double sliderValue, double sliderMaximum);

    PlaybackSeekSnapshot PreviewSeek(double sliderValue, double sliderMaximum);

    PlaybackSeekSnapshot SeekToSlider(double sliderValue, double sliderMaximum);

    PlaybackSeekSnapshot ScrubSeekToSlider(double sliderValue, double sliderMaximum);

    void CompleteDrag(double sliderValue, double sliderMaximum);

    void CancelDrag(double sliderValue, double sliderMaximum);

    bool JumpSeconds(int seconds);

    PlaybackPositionSnapshot GetPositionSnapshot();

    bool TryGetCurrentTime(out TimeSpan time);

    bool TrySeekTo(TimeSpan time);

    void Cleanup();
}
