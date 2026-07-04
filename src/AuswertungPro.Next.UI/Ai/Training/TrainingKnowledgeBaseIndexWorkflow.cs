using System.Net.Http;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingKnowledgeBaseIndexWorkflowRequest(
    IReadOnlyList<TrainingSample> Samples,
    CancellationToken CancellationToken,
    Func<OllamaConfig> LoadConfig,
    Func<HttpClient?> GetCachedHttpClient,
    Action<HttpClient> SetCachedHttpClient,
    Func<OllamaConfig, HttpClient, Func<IReadOnlyList<TrainingSample>, CancellationToken, Task<KbIndexOutcome>>> CreateIndexRunAsync);

public static class TrainingKnowledgeBaseIndexWorkflow
{
    public static Task<KbIndexOutcome> RunWithDefaultsAsync(
        IReadOnlyList<TrainingSample> samples,
        CancellationToken cancellationToken,
        Func<HttpClient?> getCachedHttpClient,
        Action<HttpClient> setCachedHttpClient,
        AppSettings? settings,
        Action<string> log)
        => RunAsync(
            new TrainingKnowledgeBaseIndexWorkflowRequest(
                samples,
                cancellationToken,
                () => new AppSettingsAiSettingsProvider().Load().ToOllamaConfig(),
                getCachedHttpClient,
                setCachedHttpClient,
                (config, httpClient) =>
                {
                    var runner = TrainingKbIndexRunner.CreateDefault(
                        config,
                        httpClient,
                        settings,
                        log);
                    return runner.RunAsync;
                }));

    public static async Task<KbIndexOutcome> RunAsync(TrainingKnowledgeBaseIndexWorkflowRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var config = request.LoadConfig();
        var httpClient = request.GetCachedHttpClient();
        if (httpClient is null)
        {
            httpClient = new HttpClient { Timeout = config.RequestTimeout };
            request.SetCachedHttpClient(httpClient);
        }

        var runAsync = request.CreateIndexRunAsync(config, httpClient);
        return await runAsync(request.Samples, request.CancellationToken).ConfigureAwait(false);
    }
}
