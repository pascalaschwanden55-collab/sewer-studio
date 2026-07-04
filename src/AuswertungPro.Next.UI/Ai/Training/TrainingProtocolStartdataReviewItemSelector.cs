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

    public static List<InfraSelfImproving.ReviewQueueItem> SelectOnUi(
        IEnumerable<InfraSelfImproving.ReviewQueueItem> items,
        Action<Action> onUi)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(onUi);

        List<InfraSelfImproving.ReviewQueueItem>? selected = null;
        onUi(() => selected = Select(items));
        return selected ?? new List<InfraSelfImproving.ReviewQueueItem>();
    }

    private static bool IsProtocolStartdata(InfraSelfImproving.ReviewQueueItem item)
        => string.Equals(
            item.SelfTrainingMatchLevel,
            "ProtocolStartdata",
            StringComparison.OrdinalIgnoreCase);
}
