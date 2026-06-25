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

    public MediaPlayer MediaPlayer => _mediaPlayer;

    public PlayerMediaHosts Hosts { get; }

    public void AttachVideoView(VideoView videoView)
        => PlayerVideoViewMediaAttachment.Attach(videoView, _mediaPlayer);

    public void DetachVideoView(VideoView? videoView)
        => PlayerPlaybackResourceCleaner.DetachVideoView(
            () => PlayerVideoViewMediaAttachment.Detach(videoView));

    public void DisposeMediaPlayer(Action<string> trace)
        => PlayerPlaybackResourceCleaner.DisposeMediaPlayer(_mediaPlayer, trace);

    public void DisposeLibVlc(Action<string> trace)
        => PlayerPlaybackResourceCleaner.DisposeLibVlc(_libVlc, trace);
}
