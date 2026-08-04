using LibVLCSharp.Shared;
using LibVLCSharp.WPF;

namespace AuswertungPro.Next.UI.Player;

public sealed class PlayerMediaRuntime
{
    private readonly LibVLC _libVlc;
    private readonly MediaPlayer _mediaPlayer;

    public PlayerMediaRuntime(
        LibVLC libVlc,
        MediaPlayer mediaPlayer,
        PlayerMediaHosts hosts)
    {
        _libVlc = libVlc ?? throw new ArgumentNullException(nameof(libVlc));
        _mediaPlayer = mediaPlayer ?? throw new ArgumentNullException(nameof(mediaPlayer));
        Hosts = hosts ?? throw new ArgumentNullException(nameof(hosts));
    }

    public PlayerMediaHosts Hosts { get; }

    public bool TryGetVideoAspect(out double aspect)
        => PlayerVideoAspectResolver.TryResolve(
            _mediaPlayer.Size,
            TryReadVideoAspectMetadata(),
            out aspect);

    public void AttachVideoView(VideoView videoView)
        => PlayerVideoViewMediaAttachment.Attach(videoView, _mediaPlayer);

    public void DetachVideoView(VideoView? videoView)
        => PlayerPlaybackResourceCleaner.DetachVideoView(
            () => PlayerVideoViewMediaAttachment.Detach(videoView));

    public void DisposeMediaPlayer(Action<string> trace)
        => PlayerPlaybackResourceCleaner.DisposeMediaPlayer(_mediaPlayer, trace);

    public void DisposeLibVlc(Action<string> trace)
        => PlayerPlaybackResourceCleaner.DisposeLibVlc(_libVlc, trace);

    private PlayerVideoAspectMetadata? TryReadVideoAspectMetadata()
    {
        try
        {
            using var media = _mediaPlayer.Media;
            if (media is null)
                return null;

            foreach (var track in media.Tracks)
            {
                if (track.TrackType != TrackType.Video)
                    continue;

                var video = track.Data.Video;
                var swapAxes = video.Orientation is
                    VideoOrientation.LeftTop or
                    VideoOrientation.LeftBottom or
                    VideoOrientation.RightTop or
                    VideoOrientation.RightBottom;

                return new PlayerVideoAspectMetadata(
                    video.SarNum,
                    video.SarDen,
                    swapAxes);
            }
        }
        catch
        {
            // Noch nicht geparste oder gerade freigegebene Medien liefern keine
            // Zusatzmetadaten. Die sichere Pixelgroesse bleibt der Rueckfall.
        }

        return null;
    }
}
