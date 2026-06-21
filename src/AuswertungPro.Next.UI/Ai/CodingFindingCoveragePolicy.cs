using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingFindingCoveragePolicy
{
    public static CodingEvent? FindCoveringEvent(
        IEnumerable<CodingEvent> existingEvents,
        string code,
        double meter,
        LiveFrameFinding finding)
        => existingEvents.FirstOrDefault(existing =>
            CodingDedupPolicy.CodesMatch(existing.Entry.Code, code)
            && IsCovered(existing, meter, finding));

    public static void MarkCoveredAgain(CodingEvent existing, double meter)
    {
        if (existing.Entry.IsStreckenschaden)
            existing.MeterAtCapture = Math.Max(existing.MeterAtCapture, meter);
    }

    public static bool IsCovered(CodingEvent existing, double newMeter, LiveFrameFinding newFinding)
    {
        if (CodingDedupPolicy.IsOneTimeCode(existing.Entry.Code))
            return true;

        if (existing.Entry.IsStreckenschaden)
        {
            var start = existing.Entry.MeterStart ?? existing.MeterAtCapture;
            var end = existing.Entry.MeterEnd ?? double.MaxValue;
            return newMeter >= start - 0.1 && newMeter <= end + 0.1;
        }

        if (existing.AiContext?.Decision is CodingUserDecision.Accepted
            or CodingUserDecision.AcceptedWithEdit)
        {
            return Math.Abs(existing.MeterAtCapture - newMeter) < 1.0;
        }

        if (Math.Abs(existing.MeterAtCapture - newMeter) >= 1.0)
            return false;

        var baseCode = newFinding.VsaCodeHint?.Length >= 3
            ? newFinding.VsaCodeHint[..3].ToUpperInvariant()
            : "";
        return baseCode == "BCA"
            ? IsSamePosition(existing, newFinding)
            : true;
    }

    public static bool IsSamePosition(CodingEvent existing, LiveFrameFinding newFinding)
    {
        var newHasBbox = newFinding.BboxX1.HasValue && newFinding.BboxY1.HasValue
                       && newFinding.BboxX2.HasValue && newFinding.BboxY2.HasValue;
        var existingHasBbox = existing.Overlay?.Points?.Count >= 4;

        if (newHasBbox && existingHasBbox)
        {
            var newCenterX = (newFinding.BboxX1!.Value + newFinding.BboxX2!.Value) / 2;
            var newCenterY = (newFinding.BboxY1!.Value + newFinding.BboxY2!.Value) / 2;
            var points = existing.Overlay!.Points;
            var existingCenterX = (points[0].X + points[2].X) / 2;
            var existingCenterY = (points[0].Y + points[2].Y) / 2;
            var distance = Math.Sqrt(
                Math.Pow(newCenterX - existingCenterX, 2)
                + Math.Pow(newCenterY - existingCenterY, 2));
            return distance < 0.15;
        }

        var existingClock = existing.Entry.CodeMeta?.Parameters
            ?.GetValueOrDefault("vsa.uhr.von");
        var newClock = newFinding.PositionClock;

        if (!string.IsNullOrEmpty(existingClock) && !string.IsNullOrEmpty(newClock))
            return string.Equals(existingClock, newClock, StringComparison.OrdinalIgnoreCase);

        return true;
    }
}
