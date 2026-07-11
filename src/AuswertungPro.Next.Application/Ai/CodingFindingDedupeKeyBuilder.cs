namespace AuswertungPro.Next.Application.Ai;

public static class CodingFindingDedupeKeyBuilder
{
    public static string Build(string code, LiveFrameFinding finding)
    {
        if (finding.BboxX1.HasValue && finding.BboxY1.HasValue
            && finding.BboxX2.HasValue && finding.BboxY2.HasValue)
        {
            var centerX = Math.Round((finding.BboxX1.Value + finding.BboxX2.Value) / 2, 1);
            var centerY = Math.Round((finding.BboxY1.Value + finding.BboxY2.Value) / 2, 1);
            return $"{code}@{centerX:F1},{centerY:F1}";
        }

        return $"{code}@{ClockPositionNormalizer.Normalize(finding.PositionClock) ?? "?"}";
    }
}
