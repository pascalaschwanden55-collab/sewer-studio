using System.Globalization;

namespace AuswertungPro.Next.Application.Ai;

public static class PipelineReachLengthParser
{
    public static double? TryParse(string? raw)
    {
        var normalized = raw?.Replace(',', '.');
        if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var reachLength)
            || reachLength <= 0)
        {
            return null;
        }

        return reachLength;
    }
}
