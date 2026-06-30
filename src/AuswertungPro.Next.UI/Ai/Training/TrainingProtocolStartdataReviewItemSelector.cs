using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

namespace AuswertungPro.Next.UI.Ai.Training;

public static class TrainingProtocolStartdataReviewItemSelector
{
    public static int Count(IEnumerable<InfraSelfImproving.ReviewQueueItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return items.Count(IsProtocolStartdata);
    }

    public static List<InfraSelfImproving.ReviewQueueItem> Select(
        IEnumerable<InfraSelfImproving.ReviewQueueItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return items.Where(IsProtocolStartdata).ToList();
    }

    private static bool IsProtocolStartdata(InfraSelfImproving.ReviewQueueItem item)
        => string.Equals(
            item.SelfTrainingMatchLevel,
            "ProtocolStartdata",
            StringComparison.OrdinalIgnoreCase);
}
