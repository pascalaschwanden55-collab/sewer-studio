using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Ai;

public static class CodingFindingCodeResolver
{
    public static string? Resolve(
        LiveFrameFinding finding,
        double currentMeter,
        IEnumerable<CodingEvent> importEvents)
    {
        var hinted = VsaCodeResolver.NormalizeFindingCode(finding.VsaCodeHint);
        if (hinted != null)
            return CodingImportFallbackCodeResolver.RefineGenericCode(importEvents, hinted, currentMeter) ?? hinted;

        var coarse = VsaCodeResolver.InferCodeFromLabel(finding.Label);
        if (coarse != null)
            return CodingImportFallbackCodeResolver.RefineGenericCode(importEvents, coarse, currentMeter) ?? coarse;

        return CodingImportFallbackCodeResolver.ResolveFallbackCode(importEvents, currentMeter);
    }
}
