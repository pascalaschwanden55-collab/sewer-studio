namespace AuswertungPro.Next.UI.Ai;

public static class CodingOsdBadgeDisplayPolicy
{
    public static string BuildMeterText(double meter)
        => $"{meter:F2}m (OSD)";
}
