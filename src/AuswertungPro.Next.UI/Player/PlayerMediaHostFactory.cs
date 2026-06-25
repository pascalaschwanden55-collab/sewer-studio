using LibVLCSharp.Shared;

namespace AuswertungPro.Next.UI.Player;

public sealed record PlayerMediaHosts(
    PlayerTimelineHost TimelineHost,
    PlayerPlaybackControlHost PlaybackControlHost,
    PlayerMarqueeOverlayHost MarqueeOverlayHost,
    PlayerSnapshotCaptureHost SnapshotCaptureHost);

public static class PlayerMediaHostFactory
{
    public static PlayerMediaHosts Create(LibVLC libVlc, MediaPlayer player)
    {
        ArgumentNullException.ThrowIfNull(libVlc);
        ArgumentNullException.ThrowIfNull(player);

        var timelineHost = new PlayerTimelineHost(
            readTimeMilliseconds: () => player.Time,
            readLengthMilliseconds: () => player.Length,
            seekMilliseconds: milliseconds => player.Time = milliseconds,
            setPositionRatio: position => player.Position = position);

        var playbackControlHost = new PlayerPlaybackControlHost(
            readIsPlaying: () => player.IsPlaying,
            setPause: pause => player.SetPause(pause),
            play: () => player.Play(),
            stop: () => player.Stop(),
            readRate: () => player.Rate,
            setRate: player.SetRate,
            shouldStartPlayback: () =>
            {
                var state = player.State;
                return state == VLCState.Stopped || state == VLCState.Ended;
            },
            playPath: path =>
            {
                using var media = new Media(libVlc, path, FromType.FromPath);
                player.Play(media);
            });

        var marqueeOverlayHost = new PlayerMarqueeOverlayHost(
            setMarqueeInt: (option, value) => player.SetMarqueeInt(option, value),
            setMarqueeString: (option, value) => player.SetMarqueeString(option, value));

        var snapshotCaptureHost = new PlayerSnapshotCaptureHost(
            takeSnapshot: (path, width, height) => player.TakeSnapshot(0, path, width, height));

        return new PlayerMediaHosts(
            timelineHost,
            playbackControlHost,
            marqueeOverlayHost,
            snapshotCaptureHost);
    }
}
