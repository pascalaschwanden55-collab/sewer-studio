using AuswertungPro.Next.Infrastructure.Ai.SelfImproving;
using AuswertungPro.Next.UI.Ai.Training;
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
    private readonly Func<int?> _resolveReviewPipeDiameterMm;
    private ReviewQueueService? _reviewQueue;
    private TrainingReviewSamSegmentationService? _reviewSam;
    private TrainingReviewSamWorkflow? _reviewSamWorkflow;

    public TrainingCenterLazyServices(
        Func<ReviewQueueService> createReviewQueue,
        Func<TrainingReviewSamSegmentationService> createReviewSam,
        Func<int?> resolveReviewPipeDiameterMm)
    {
        _createReviewQueue = createReviewQueue ?? throw new ArgumentNullException(nameof(createReviewQueue));
        _createReviewSam = createReviewSam ?? throw new ArgumentNullException(nameof(createReviewSam));
        _resolveReviewPipeDiameterMm = resolveReviewPipeDiameterMm
            ?? throw new ArgumentNullException(nameof(resolveReviewPipeDiameterMm));
    }

    public ReviewQueueService GetReviewQueue()
        => _reviewQueue ??= _createReviewQueue();

    public TrainingReviewSamSegmentationService GetReviewSam()
        => _reviewSam ??= _createReviewSam();

    public TrainingReviewSamWorkflow GetReviewSamWorkflow()
        => _reviewSamWorkflow ??= new TrainingReviewSamWorkflow(
            GetReviewSam,
            _resolveReviewPipeDiameterMm);
}
