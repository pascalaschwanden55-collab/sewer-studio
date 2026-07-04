using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public static class TrainingReviewPendingGeometryController
{
    public static void Clear(
        Action<BoundingBox?> setPendingBox,
        Action<TrainingSegmentationMask?> setPendingSamMask)
    {
        ArgumentNullException.ThrowIfNull(setPendingBox);
        ArgumentNullException.ThrowIfNull(setPendingSamMask);

        setPendingBox(null);
        setPendingSamMask(null);
    }
}
