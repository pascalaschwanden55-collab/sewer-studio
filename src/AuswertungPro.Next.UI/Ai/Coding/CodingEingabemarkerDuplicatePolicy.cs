using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingEingabemarkerDuplicatePolicy
{
    private const double MeterTolerance = 1.0;

    public static CodingEvent? FindDuplicate(
        IEnumerable<CodingEvent> existingEvents,
        string codeHint,
        double currentMeter)
    {
        var isOneTimeCode = CodingDedupPolicy.IsOneTimeCode(codeHint);
        return existingEvents.FirstOrDefault(existing =>
            CodingDedupPolicy.CodesMatch(existing.Entry.Code, codeHint)
            && (isOneTimeCode || Math.Abs(existing.MeterAtCapture - currentMeter) < MeterTolerance));
    }
}
