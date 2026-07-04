using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingProtocolStartdataSuggestionRequestFactoryRequest(
    InfraSelfImproving.ReviewQueueService? QueueService,
    ICodeCatalogProvider? InjectedCatalog,
    Action ReloadReviewQueue,
    Action<Action> OnUi,
    Action<string> SetReviewStatusText,
    Action<string> Log);

public static class TrainingProtocolStartdataSuggestionRequestFactory
{
    public static TrainingProtocolStartdataSuggestionWorkflowRequest CreateWithDefaults(
        TrainingProtocolStartdataSuggestionRequestFactoryRequest request)
        => Create(
            request,
            () => VsaCodeResolver.CurrentCatalog,
            TrainingSamplesStore.LoadAsync);

    public static TrainingProtocolStartdataSuggestionWorkflowRequest Create(
        TrainingProtocolStartdataSuggestionRequestFactoryRequest request,
        Func<ICodeCatalogProvider?> FallbackCatalog,
        Func<Task<List<TrainingSample>>> LoadSamplesAsync)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(FallbackCatalog);
        ArgumentNullException.ThrowIfNull(LoadSamplesAsync);

        return new TrainingProtocolStartdataSuggestionWorkflowRequest(
            request.QueueService,
            request.InjectedCatalog,
            FallbackCatalog,
            LoadSamplesAsync,
            request.ReloadReviewQueue,
            request.OnUi,
            request.SetReviewStatusText,
            request.Log);
    }
}
