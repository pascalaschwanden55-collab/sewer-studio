using AuswertungPro.Next.Infrastructure.Ai.SelfImproving;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Erzeugt schwere Training-Center-Dienste erst bei der ersten Nutzung. Review-Queue und
/// SAM-Dienst bleiben danach für die Lebensdauer des Fensters erhalten.
/// </summary>
internal sealed class TrainingCenterLazyServices
{
    private readonly Func<ReviewQueueService> _createReviewQueue;
    private readonly Func<TrainingReviewSamSegmentationService> _createReviewSam;
    private readonly Func<FewShotExampleStore> _createFewShotStore;
    private ReviewQueueService? _reviewQueue;
    private TrainingReviewSamSegmentationService? _reviewSam;

    public TrainingCenterLazyServices(
        Func<ReviewQueueService> createReviewQueue,
        Func<TrainingReviewSamSegmentationService> createReviewSam,
        Func<FewShotExampleStore> createFewShotStore)
    {
        _createReviewQueue = createReviewQueue ?? throw new ArgumentNullException(nameof(createReviewQueue));
        _createReviewSam = createReviewSam ?? throw new ArgumentNullException(nameof(createReviewSam));
        _createFewShotStore = createFewShotStore ?? throw new ArgumentNullException(nameof(createFewShotStore));
    }

    public ReviewQueueService GetReviewQueue()
        => _reviewQueue ??= _createReviewQueue();

    public TrainingReviewSamSegmentationService GetReviewSam()
        => _reviewSam ??= _createReviewSam();

    public FewShotExampleStore CreateFewShotStore()
        => _createFewShotStore();
}
