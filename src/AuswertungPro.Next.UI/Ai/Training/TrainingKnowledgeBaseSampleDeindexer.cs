using System.Net.Http;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed record TrainingKnowledgeBaseSampleDeindexRequest(
    string SampleId,
    Func<OllamaConfig> LoadConfig,
    Func<HttpClient?> GetCachedHttpClient,
    Action<HttpClient> SetCachedHttpClient,
    Action<HttpClient, OllamaConfig, string> DeindexSample);

public static class TrainingKnowledgeBaseSampleDeindexer
{
    public static void TryDeindexWithDefaults(
        string sampleId,
        Func<HttpClient?> getCachedHttpClient,
        Action<HttpClient> setCachedHttpClient)
    {
        TryDeindex(
            new TrainingKnowledgeBaseSampleDeindexRequest(
                sampleId,
                () => new AppSettingsAiSettingsProvider().Load().ToOllamaConfig(),
                getCachedHttpClient,
                setCachedHttpClient,
                DeindexWithDefaultInfrastructure));
    }

    public static void TryDeindex(TrainingKnowledgeBaseSampleDeindexRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var ollamaConfig = request.LoadConfig();
            var httpClient = request.GetCachedHttpClient();
            if (httpClient is null)
            {
                httpClient = new HttpClient { Timeout = ollamaConfig.RequestTimeout };
                request.SetCachedHttpClient(httpClient);
            }

            request.DeindexSample(httpClient, ollamaConfig, request.SampleId);
        }
        catch
        {
            // KB evtl. nicht erreichbar - Status-Aenderung bleibt persistiert.
        }
    }

    public static void DeindexWithDefaultInfrastructure(
        HttpClient httpClient,
        OllamaConfig ollamaConfig,
        string sampleId)
    {
        using var kbCtx = new KnowledgeBaseContext();
        var embedder = new EmbeddingService(httpClient, ollamaConfig);
        var kbManager = new KnowledgeBaseManager(kbCtx, embedder);
        kbManager.DeindexSample(sampleId);
    }
}
