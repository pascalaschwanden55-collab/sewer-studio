using System;
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

    /// <summary>
    /// Tiefennebel: daempft die ferne Hemisphaere (Depth -1 = hinten) auf
    /// <paramref name="fogFloor"/>, die nahe (Depth +1 = vorn) bleibt unveraendert.
    /// </summary>
    public static double DepthFog(double depth, double fogFloor = 0.35)
    {
        fogFloor = Clamp01(fogFloor);
        var depth01 = Clamp01((depth + 1.0) / 2.0);
        return fogFloor + (1.0 - fogFloor) * depth01;
    }

    /// <summary>Kubischer Ease-in auf 0..1 (der Eingang wird geclamppt).</summary>
    public static double EaseInCubic(double t)
    {
        t = Clamp01(t);
        return t * t * t;
    }

    /// <summary>
    /// Massstab der Kugel aus der verfuegbaren Canvas-Flaeche: Die kuerzere Kante
    /// wird auf das Entwurfsmass bezogen, damit die Kugel auf jedem Bildschirm
    /// vollstaendig sichtbar bleibt. Nach oben ist der Faktor begrenzt, nach unten
    /// nie kleiner als ein Viertel, damit ein winziges Fenster nichts Ungueltiges liefert.
    /// </summary>
    public static double SphereScale(double canvasWidth, double canvasHeight, double designEdge, double maxScale)
    {
        if (designEdge <= 0 || double.IsNaN(designEdge))
            return 1.0;
        var edge = Math.Min(canvasWidth, canvasHeight);
        if (edge <= 0 || double.IsNaN(edge) || double.IsInfinity(edge))
            return 1.0;
        var scale = edge / designEdge;
        var upper = maxScale > 0 ? maxScale : 1.0;
        return Math.Clamp(scale, 0.25, upper);
    }
}

internal readonly record struct StartupSplashProjection(
    double X,
    double Y,
    double Depth,
    double Perspective);
