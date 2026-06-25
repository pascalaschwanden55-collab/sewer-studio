using LibVLCSharp.Shared;

namespace AuswertungPro.Next.UI.Player;

public sealed class PlayerMarqueeOverlayHost
{
    private readonly Action<VideoMarqueeOption, int> _setMarqueeInt;
    private readonly Action<VideoMarqueeOption, string> _setMarqueeString;

    public PlayerMarqueeOverlayHost(
        Action<VideoMarqueeOption, int> setMarqueeInt,
        Action<VideoMarqueeOption, string> setMarqueeString)
    {
        ArgumentNullException.ThrowIfNull(setMarqueeInt);
        ArgumentNullException.ThrowIfNull(setMarqueeString);

        _setMarqueeInt = setMarqueeInt;
        _setMarqueeString = setMarqueeString;
    }

    public void Show(PlayerMarqueeOverlayState marquee)
    {
        _setMarqueeInt(VideoMarqueeOption.Enable, marquee.Enable);
        _setMarqueeInt(VideoMarqueeOption.X, marquee.X);
        _setMarqueeInt(VideoMarqueeOption.Y, marquee.Y);
        _setMarqueeInt(VideoMarqueeOption.Size, marquee.Size);
        _setMarqueeInt(VideoMarqueeOption.Color, marquee.Color);
        _setMarqueeInt(VideoMarqueeOption.Opacity, marquee.Opacity);
        _setMarqueeString(VideoMarqueeOption.Text, marquee.Text);
    }

    public void Disable()
        => PlayerMarqueeOverlayDisabler.Disable(_setMarqueeInt);
}
