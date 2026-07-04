using System.Net.Http;
using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingSampleCommandRequestFactoryRequest(
    TrainingSample? SelectedSample,
    Func<TrainingSample, TrainingSampleDecisionResult> Decide,
    Func<HttpClient?> GetKbHttpClient,
    Action<HttpClient> SetKbHttpClient,
    Action<string> SetStatusText,
    Func<TrainingSample?, Task> PersistSamplesAsync);

public sealed record TrainingSampleCommandRequestFactoryDefaults(
    Action<string, Func<HttpClient?>, Action<HttpClient>> DeindexSample);

public static class TrainingSampleCommandRequestFactory
{
    public static TrainingSampleCommandWorkflowRequest CreateWithDefaults(
        TrainingSampleCommandRequestFactoryRequest request)
        => Create(
            request,
            new TrainingSampleCommandRequestFactoryDefaults(
                (sampleId, getCachedHttpClient, setCachedHttpClient) =>
                    TrainingKnowledgeBaseSampleDeindexer.TryDeindexWithDefaults(
                        sampleId,
                        getCachedHttpClient,
                        setCachedHttpClient)));

    public static TrainingSampleCommandWorkflowRequest Create(
        TrainingSampleCommandRequestFactoryRequest request,
        TrainingSampleCommandRequestFactoryDefaults defaults)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Decide);
        ArgumentNullException.ThrowIfNull(request.GetKbHttpClient);
        ArgumentNullException.ThrowIfNull(request.SetKbHttpClient);
        ArgumentNullException.ThrowIfNull(request.SetStatusText);
        ArgumentNullException.ThrowIfNull(request.PersistSamplesAsync);
        ArgumentNullException.ThrowIfNull(defaults);
        ArgumentNullException.ThrowIfNull(defaults.DeindexSample);

        return new TrainingSampleCommandWorkflowRequest(
            request.SelectedSample,
            request.Decide,
            sampleId => defaults.DeindexSample(
                sampleId,
                request.GetKbHttpClient,
                request.SetKbHttpClient),
            request.SetStatusText,
            request.PersistSamplesAsync);
    }
}
