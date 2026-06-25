using LibVLCSharp.Shared;
using LibVLCSharp.WPF;

namespace AuswertungPro.Next.UI.Player;

public static class PlayerVideoViewMediaAttachment
{
    public static void Attach(VideoView videoView, MediaPlayer mediaPlayer)
    {
        ArgumentNullException.ThrowIfNull(videoView);
        ArgumentNullException.ThrowIfNull(mediaPlayer);

        videoView.MediaPlayer = mediaPlayer;
    }

    public static void Detach(VideoView? videoView)
    {
        if (videoView is not null)
            videoView.MediaPlayer = null;
    }
}
