using System;
using LibVLCSharp.Shared;

namespace AuswertungPro.Next.UI.Player;

public static class PlayerMarqueeOverlayDisabler
{
    public static void Disable(Action<VideoMarqueeOption, int> setMarqueeInt)
        => AuswertungPro.Next.Application.Common.BestEffort.Try(
            () => setMarqueeInt(VideoMarqueeOption.Enable, PlayerMarqueeOverlayPolicy.DisabledEnable),
            "VLC: Marquee deaktivieren");
}
