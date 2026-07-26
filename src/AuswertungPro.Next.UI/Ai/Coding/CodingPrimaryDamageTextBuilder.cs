using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingPrimaryDamageTextBuilder
{
    public static string Build(ProtocolDocument? doc)
    {
        var entries = doc?.Current?.Entries?
            .Where(e => !e.IsDeleted && !string.IsNullOrWhiteSpace(e.Code))
            .ToList();

        return entries is { Count: > 0 }
            ? string.Join("\n", DataPageProtocolObservationMapper.BuildPrimaryDamageLines(entries))
            : string.Empty;
    }
}
