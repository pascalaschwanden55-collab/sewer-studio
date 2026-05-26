using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Configuration;

namespace AuswertungPro.Next.UI.Services;

public sealed class AppSettingsAiSettingsProvider : IAiSettingsProvider
{
    public AiPlatformSettings Load()
    {
        AppSettings? settings = null;
        try
        {
            settings = AppSettings.Load();
        }
        catch
        {
            settings = null;
        }

        return AiSettingsFactory.Load(ToSource(settings));
    }

    public static AiSettingsSource ToSource(AppSettings? settings)
    {
        if (settings is null)
            return new AiSettingsSource();

        return new AiSettingsSource(
            Enabled: settings.AiEnabled,
            OllamaUrl: settings.AiOllamaUrl,
            VisionModel: settings.AiVisionModel,
            TextModel: settings.AiTextModel,
            EmbedModel: settings.AiEmbedModel,
            OllamaTimeoutMin: settings.AiOllamaTimeoutMin,
            OllamaKeepAlive: settings.AiOllamaKeepAlive,
            OllamaNumCtx: settings.AiOllamaNumCtx,
            MultiModelEnabled: settings.PipelineMultiModelEnabled,
            SidecarUrl: settings.PipelineSidecarUrl,
            SidecarToken: settings.PipelineSidecarToken,
            PipelineMode: settings.PipelineMode,
            YoloConfidence: settings.PipelineYoloConfidence,
            DinoBoxThreshold: settings.PipelineDinoBoxThreshold,
            DinoTextThreshold: settings.PipelineDinoTextThreshold,
            PipeDiameterMm: settings.PipelinePipeDiameterMm,
            FfmpegPath: settings.AiFfmpegPath);
    }
}
