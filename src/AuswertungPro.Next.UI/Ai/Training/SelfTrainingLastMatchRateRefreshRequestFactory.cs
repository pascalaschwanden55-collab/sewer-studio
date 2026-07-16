using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record SelfTrainingLastMatchRateRefreshRequestFactoryRequest(
    Func<Task<List<SelfTrainingRunSnapshot>>> LoadRunsAsync,
    SelfTrainingMatchRatePresentationUi Ui);

public sealed record SelfTrainingLastMatchRateRefreshDefaultRequestFactoryRequest(
    SelfTrainingMatchRatePresentationUi Ui);

public static class SelfTrainingLastMatchRateRefreshRequestFactory
{
    public static SelfTrainingLastMatchRateRefreshWorkflowRequest CreateWithDefaults(
        SelfTrainingLastMatchRateRefreshDefaultRequestFactoryRequest request,
        ISelfTrainingHistoryStore? historyStore = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Ui);

        return Create(new SelfTrainingLastMatchRateRefreshRequestFactoryRequest(
            (historyStore ?? SelfTrainingHistoryStore.Current).LoadAsync,
            request.Ui));
    }

    public static SelfTrainingLastMatchRateRefreshWorkflowRequest Create(
        SelfTrainingLastMatchRateRefreshRequestFactoryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.LoadRunsAsync);
        ArgumentNullException.ThrowIfNull(request.Ui);

        return new SelfTrainingLastMatchRateRefreshWorkflowRequest(
            request.LoadRunsAsync,
            request.Ui);
    }
}
