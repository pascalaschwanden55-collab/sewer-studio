using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingFindingFilterPolicy
{
    public static IReadOnlyList<LiveFrameFinding> FilterValid(
        IReadOnlyList<LiveFrameFinding> raw,
        double currentMeter,
        Func<LiveFrameFinding, double, string?> codeResolver,
        IEnumerable<CodingEvent>? sessionEvents,
        IEnumerable<CodingEvent>? viewEvents,
        Action<string>? trace = null)
    {
        var filtered = new List<LiveFrameFinding>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var finding in raw)
        {
            var code = codeResolver(finding, currentMeter);

            if (code != null
                && CodingDedupPolicy.IsOneTimeCode(code)
                && (ExistsCode(sessionEvents, code) || ExistsCode(viewEvents, code)))
            {
                trace?.Invoke($"[KI-Filter] {code} uebersprungen (bereits vorhanden, live-check)");
                continue;
            }

            trace?.Invoke(
                $"[KI-Filter] Label='{finding.Label}' VsaCodeHint='{finding.VsaCodeHint}' -> Code='{code ?? "(null)"}'");

            if (code == null)
            {
                trace?.Invoke($"[KI-Filter] Verworfen: Label='{finding.Label}' (kein VSA-Code ableitbar)");
                continue;
            }

            var normalizedFinding = string.Equals(code, finding.VsaCodeHint, StringComparison.OrdinalIgnoreCase)
                ? finding
                : finding with { VsaCodeHint = code };

            var dedupeKey = CodingFindingDedupeKeyBuilder.Build(code, normalizedFinding);
            if (!seen.Add(dedupeKey))
                continue;

            filtered.Add(normalizedFinding);
        }

        return filtered;
    }

    private static bool ExistsCode(IEnumerable<CodingEvent>? events, string code)
        => events?.Any(e => CodingDedupPolicy.CodesMatch(e.Entry.Code, code)) == true;
}
