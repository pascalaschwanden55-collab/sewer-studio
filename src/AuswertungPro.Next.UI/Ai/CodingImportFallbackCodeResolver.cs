using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingImportFallbackCodeResolver
{
    public static string? RefineGenericCode(
        IEnumerable<CodingEvent> importEvents,
        string? genericCode,
        double currentMeter)
    {
        if (string.IsNullOrWhiteSpace(genericCode))
            return null;

        var family = genericCode.Trim().ToUpperInvariant();
        return BestCandidate(
            importEvents,
            currentMeter,
            code => code.StartsWith(family, StringComparison.OrdinalIgnoreCase));
    }

    public static string? ResolveFallbackCode(IEnumerable<CodingEvent> importEvents, double currentMeter)
        => BestCandidate(importEvents, currentMeter, _ => true);

    private static string? BestCandidate(
        IEnumerable<CodingEvent> importEvents,
        double currentMeter,
        Func<string, bool> codePredicate)
    {
        return importEvents
            .Where(ev => !string.IsNullOrWhiteSpace(ev.Entry?.Code))
            .Select(ev => new
            {
                Code = ev.Entry!.Code.Trim().ToUpperInvariant(),
                Distance = Math.Abs(ev.MeterAtCapture - currentMeter)
            })
            .Where(x =>
                codePredicate(x.Code) &&
                PlayerImportFallbackCodePolicy.IsWithinMeterWindow(x.Code, x.Distance))
            .OrderBy(x => x.Distance)
            .ThenByDescending(x => x.Code.Length)
            .FirstOrDefault()
            ?.Code;
    }
}
