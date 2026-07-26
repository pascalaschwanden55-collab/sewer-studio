using System.Globalization;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingStretchDamageClosePolicy
{
    public const double CloseToleranceMeters = 0.01;

    public static bool CanClose(double startMeter, double currentMeter)
        => currentMeter > startMeter + CloseToleranceMeters;

    public static string BuildClosedStatusText(string code, double startMeter, double endMeter)
        => string.Format(
            CultureInfo.InvariantCulture,
            "Streckenschaden geschlossen: {0} {1:F2}m - {2:F2}m",
            code,
            startMeter,
            endMeter);
}
