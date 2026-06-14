namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Fachregeln fuer Live-Codier-Deduplication, bewusst frei von UI-Abhaengigkeiten.
/// </summary>
public static class CodingDedupPolicy
{
    private const double TerminalMeterTolerance = 0.05;

    public static bool IsOneTimeCode(string? code)
    {
        var main = MainCode(code);
        return main is "BCD" or "BCE" or "BDC";
    }

    public static bool CodesMatch(string? existingCode, string? newCode)
    {
        if (string.IsNullOrWhiteSpace(existingCode) || string.IsNullOrWhiteSpace(newCode))
            return false;

        if (string.Equals(existingCode, newCode, StringComparison.OrdinalIgnoreCase))
            return true;

        var existingMain = MainCode(existingCode);
        var newMain = MainCode(newCode);
        return existingMain is not null
            && newMain is not null
            && string.Equals(existingMain, newMain, StringComparison.OrdinalIgnoreCase);
    }

    public static bool ShouldStopAnalysisAfterTerminalCode(
        IEnumerable<(string? Code, double? Meter, TimeSpan? VideoTime)> terminalCandidates,
        double? currentMeter,
        TimeSpan? currentVideoTime)
    {
        foreach (var candidate in terminalCandidates)
        {
            var main = MainCode(candidate.Code);
            if (main is not ("BCE" or "BDC"))
                continue;

            if (candidate.Meter.HasValue && currentMeter.HasValue)
            {
                if (currentMeter.Value >= candidate.Meter.Value - TerminalMeterTolerance)
                    return true;

                continue;
            }

            if (candidate.VideoTime.HasValue && currentVideoTime.HasValue
                && currentVideoTime.Value >= candidate.VideoTime.Value)
                return true;

            if (!candidate.Meter.HasValue && !candidate.VideoTime.HasValue)
                return true;
        }

        return false;
    }

    public static bool ShouldDeferSpatialCodeUntilCloser(
        string? code,
        MetrierungProximityResult proximity)
    {
        if (MainCode(code) is not "BCC")
            return false;

        return !proximity.IsCodierbar;
    }

    private static string? MainCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        var trimmed = code.Trim();
        return trimmed.Length >= 3 ? trimmed[..3].ToUpperInvariant() : trimmed.ToUpperInvariant();
    }
}
