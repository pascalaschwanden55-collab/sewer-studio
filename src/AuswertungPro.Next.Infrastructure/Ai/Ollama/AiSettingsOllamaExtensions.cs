using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.Infrastructure.Ai.Ollama;

public static class AiSettingsOllamaExtensions
{
    public static OllamaConfig ToOllamaConfig(this AiPlatformSettings settings) => new(
        BaseUri: settings.OllamaBaseUri,
        VisionModel: settings.VisionModel,
        TextModel: settings.TextModel,
        EmbedModel: settings.EmbedModel,
        RequestTimeout: settings.OllamaRequestTimeout,
        KeepAlive: settings.OllamaKeepAlive,
        NumCtx: settings.OllamaNumCtx);
}
