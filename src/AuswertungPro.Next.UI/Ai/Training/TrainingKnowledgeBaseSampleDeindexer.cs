using AuswertungPro.Next.Application.Common;
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

/// <summary>
/// Ergebnis der KB-Entfernung. <c>Removed=false</c> heisst: Der abgeleitete
/// Wissensdatenbank-Eintrag besteht moeglicherweise weiter und kann als
/// Vergleichswissen dienen (Auditbefund 16).
/// </summary>
public sealed record TrainingKnowledgeBaseDeindexResult(bool Removed, string? Error)
{
    public static TrainingKnowledgeBaseDeindexResult Ok() => new(true, null);
    public static TrainingKnowledgeBaseDeindexResult Failed(string error) => new(false, error);
}

public static class TrainingKnowledgeBaseSampleDeindexer
{
    public static TrainingKnowledgeBaseDeindexResult TryDeindexWithDefaults(
        string sampleId,
        Func<HttpClient?> getCachedHttpClient,
        Action<HttpClient> setCachedHttpClient)
        => TryDeindex(
            new TrainingKnowledgeBaseSampleDeindexRequest(
                sampleId,
                () => new AppSettingsAiSettingsProvider().Load().ToOllamaConfig(),
                getCachedHttpClient,
                setCachedHttpClient,
                DeindexWithDefaultInfrastructure));

    public static TrainingKnowledgeBaseDeindexResult TryDeindex(
        TrainingKnowledgeBaseSampleDeindexRequest request)
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
            return TrainingKnowledgeBaseDeindexResult.Ok();
        }
        catch (Exception ex)
        {
            // Die persoenliche Entscheidung bleibt gespeichert — sie soll nicht an einer
            // gesperrten oder nicht erreichbaren KB scheitern. Der Fehler darf aber nicht
            // mehr spurlos verschwinden: Der freigegebene Eintrag kann sonst weiter als
            // Vergleichswissen dienen, waehrend die Oberflaeche Vollzug meldet.
            return TrainingKnowledgeBaseDeindexResult.Failed(UserError.DescribeAndReport(ex, "KB-Eintrag entfernen"));
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
