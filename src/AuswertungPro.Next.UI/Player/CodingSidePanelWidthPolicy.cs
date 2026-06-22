namespace AuswertungPro.Next.UI.Player;

public static class CodingSidePanelWidthPolicy
{
    public const double DefaultWidth = 760;
    public const double MinimumWidth = 760;
    public const double MaximumWidth = 840;
    public const double WidthRatio = 0.46;

    public static double Resolve(double actualWidth, double fallbackWidth)
    {
        var availableWidth = actualWidth > 0 ? actualWidth : fallbackWidth;
        if (double.IsNaN(availableWidth) || availableWidth <= 0)
            return DefaultWidth;

        return Math.Clamp(availableWidth * WidthRatio, MinimumWidth, MaximumWidth);
    }
}
