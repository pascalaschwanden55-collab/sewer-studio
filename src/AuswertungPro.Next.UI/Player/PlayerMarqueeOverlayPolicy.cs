namespace AuswertungPro.Next.UI.Player;

public sealed record PlayerMarqueeOverlayState(
    int Enable,
    int X,
    int Y,
    int Size,
    int Color,
    int Opacity,
    string Text);

public static class PlayerMarqueeOverlayPolicy
{
    public static int DisabledEnable => 0;

    public static PlayerMarqueeOverlayState BuildShow(string text)
        => new(
            Enable: 1,
            X: 16,
            Y: 16,
            Size: 24,
            Color: 0xFFFFFF,
            Opacity: 200,
            Text: text);
}
