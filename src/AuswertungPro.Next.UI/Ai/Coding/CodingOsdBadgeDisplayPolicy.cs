namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingOsdBadgeDisplayPolicy
{
    public static string BuildMeterText(double meter)
        => $"{meter:F2}m (OSD)";
}
