using System;

namespace AuswertungPro.Next.UI.Ai;

internal static class OverlayCanvasPosition
{
    internal static double Clamp(double value, double available, double required)
        => Math.Clamp(value, 2, Math.Max(2, available - required - 2));
}
