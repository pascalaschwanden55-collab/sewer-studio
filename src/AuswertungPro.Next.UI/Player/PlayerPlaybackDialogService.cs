using System;

namespace AuswertungPro.Next.UI.Player;

public sealed class PlayerPlaybackDialogService
{
    private readonly Action<string, string> _showInfo;

    public PlayerPlaybackDialogService(Action<string, string> showInfo)
    {
        _showInfo = showInfo;
    }

    public void ShowUnsupportedRate(float rate)
        => _showInfo(
            $"SetRate({rate:0.##}) nicht unterst\u00fctzt f\u00fcr dieses Video.",
            "Video");
}
