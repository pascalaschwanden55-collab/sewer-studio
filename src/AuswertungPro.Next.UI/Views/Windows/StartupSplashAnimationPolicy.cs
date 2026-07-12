using System.Windows.Media;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>Reine Mathematik fuer Projektion und Farbübergänge der Startanimation.</summary>
internal static class StartupSplashAnimationPolicy
{
    public static StartupSplashProjection Project(
        double x,
        double y,
        double z,
        double cosY,
        double sinY,
        double cosX,
        double sinX,
        double cosZ,
        double sinZ,
        double cameraDistance,
        double projectionScale,
        double centerX,
        double centerY)
    {
        var x1 = x * cosY + z * sinY;
        var z1 = -x * sinY + z * cosY;
        var y1 = y * cosX - z1 * sinX;
        var z2 = y * sinX + z1 * cosX;

        var perspective = cameraDistance / (cameraDistance - z2);
        var screenX = x1 * projectionScale * perspective;
        var screenY = y1 * projectionScale * perspective;

        return new StartupSplashProjection(
            centerX + screenX * cosZ - screenY * sinZ,
            centerY + screenX * sinZ + screenY * cosZ,
            z2,
            perspective);
    }

    public static Color Blend(Color from, Color to, double amount)
    {
        amount = Clamp01(amount);
        return Color.FromRgb(
            (byte)(from.R + (to.R - from.R) * amount),
            (byte)(from.G + (to.G - from.G) * amount),
            (byte)(from.B + (to.B - from.B) * amount));
    }

    public static double Clamp01(double value)
    {
        if (value < 0)
            return 0;
        if (value > 1)
            return 1;
        return value;
    }
}

internal readonly record struct StartupSplashProjection(
    double X,
    double Y,
    double Depth,
    double Perspective);
