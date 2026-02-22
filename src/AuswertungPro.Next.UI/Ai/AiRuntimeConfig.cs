using System;

namespace AuswertungPro.Next.UI.Ai;

public sealed record AiRuntimeConfig(
    bool Enabled,
    Uri OllamaBaseUri,
    string VisionModel,
    string TextModel,
    string? EmbedModel,
    string? FfmpegPath
)
{
    public static AiRuntimeConfig Load()
    {
        // Enable über ENV, damit du nicht sofort AppSettings umbauen musst.
        // setx AUSWERTUNGPRO_AI_ENABLED 1
        var enabled = (Environment.GetEnvironmentVariable("AUSWERTUNGPRO_AI_ENABLED") ?? "0")
            .Trim() is "1" or "true" or "TRUE" or "True";

        var url = Environment.GetEnvironmentVariable("AUSWERTUNGPRO_OLLAMA_URL")
            ?? "http://localhost:11434";

        var vision = Environment.GetEnvironmentVariable("AUSWERTUNGPRO_AI_VISION_MODEL")
            ?? "qwen2.5vl:7b";

        var text = Environment.GetEnvironmentVariable("AUSWERTUNGPRO_AI_TEXT_MODEL")
            ?? "qwen2.5:7b";

        var embed = Environment.GetEnvironmentVariable("AUSWERTUNGPRO_AI_EMBED_MODEL")
            ?? "nomic-embed-text";

        var ffmpeg = Environment.GetEnvironmentVariable("AUSWERTUNGPRO_FFMPEG")
            ?? "ffmpeg";

        return new AiRuntimeConfig(
            Enabled: enabled,
            OllamaBaseUri: new Uri(url),
            VisionModel: vision,
            TextModel: text,
            EmbedModel: embed,
            FfmpegPath: ffmpeg
        );
    }
}
