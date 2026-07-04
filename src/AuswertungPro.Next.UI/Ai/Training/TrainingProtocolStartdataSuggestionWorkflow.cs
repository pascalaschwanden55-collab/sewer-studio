using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Protocol;
using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingProtocolStartdataSuggestionWorkflowRequest(
    InfraSelfImproving.ReviewQueueService? QueueService,
    ICodeCatalogProvider? InjectedCatalog,
    Func<ICodeCatalogProvider?> FallbackCatalog,
    Func<Task<List<TrainingSample>>> LoadSamplesAsync,
    Action ReloadReviewQueue,
    Action<Action> OnUi,
    Action<string> SetReviewStatusText,
    Action<string> Log);

public static class TrainingProtocolStartdataSuggestionWorkflow
{
    public static async Task RunAsync(TrainingProtocolStartdataSuggestionWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.FallbackCatalog);
        ArgumentNullException.ThrowIfNull(request.LoadSamplesAsync);
        ArgumentNullException.ThrowIfNull(request.ReloadReviewQueue);
        ArgumentNullException.ThrowIfNull(request.OnUi);
        ArgumentNullException.ThrowIfNull(request.SetReviewStatusText);
        ArgumentNullException.ThrowIfNull(request.Log);

        if (request.QueueService is null)
            return;

        var catalog = TrainingProtocolStartdataCatalogController.Resolve(
            request.InjectedCatalog,
            request.FallbackCatalog);
        if (!TrainingProtocolStartdataCatalogController.EnsureAvailable(
                catalog,
                request.OnUi,
                request.SetReviewStatusText))
            return;

        var samples = await request.LoadSamplesAsync().ConfigureAwait(false);
        var result = TrainingProtocolStartdataQueueController.Run(
            samples,
            catalog,
            request.QueueService);

        TrainingProtocolStartdataQueueCompletionController.Apply(
            result,
            request.ReloadReviewQueue,
            request.OnUi,
            request.SetReviewStatusText,
            request.Log);
    }
}
