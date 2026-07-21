using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Media;

public static class PhotoProtocolEntryMatcher
{
    public static ProtocolEntry? FindNearestActiveEntry(
        IEnumerable<ProtocolEntry> entries,
        double meter,
        double maxDistance = 1.0)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var nearest = entries
            .Where(entry => !entry.IsDeleted && entry.MeterStart is not null)
            .OrderBy(entry => Math.Abs(entry.MeterStart!.Value - meter))
            .FirstOrDefault();

        return nearest is not null
            && Math.Abs(nearest.MeterStart!.Value - meter) <= maxDistance
                ? nearest
                : null;
    }
}
