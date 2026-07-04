using AuswertungPro.Next.Infrastructure.Ai.Ollama;

namespace AuswertungPro.Next.UI.Ai.Training;

internal static class TrainingOllamaReachabilityChecker
{
    public static async Task<bool> CheckAsync(OllamaConfig config, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(config);

        try
        {
            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            using var response = await http.GetAsync(new Uri(config.BaseUri, "/api/tags"), ct).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
