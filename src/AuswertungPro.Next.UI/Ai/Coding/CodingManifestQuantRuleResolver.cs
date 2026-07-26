using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingManifestQuantRuleResolver
{
    public static QuantificationGate.ManifestQuantRule Resolve(IVsaCodeSelectionCatalog? catalog, string code)
    {
        if (catalog == null)
            return new QuantificationGate.ManifestQuantRule(true, true, true);

        var (q1, q2) = catalog.GetQuantRule(code, null);
        var clock = catalog.GetClockRule(code);
        var allowClock = !string.Equals(clock?.Mode, "none", StringComparison.OrdinalIgnoreCase);

        return new QuantificationGate.ManifestQuantRule(
            HasQ1: q1 != null,
            HasQ2: q2 != null,
            AllowClock: allowClock);
    }
}
