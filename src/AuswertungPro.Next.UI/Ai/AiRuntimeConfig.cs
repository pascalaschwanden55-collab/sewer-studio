using System;
using AuswertungPro.Next.UI.Ai.Ollama;

namespace AuswertungPro.Next.UI.Ai;

public sealed record AiRuntimeConfig(
    bool Enabled,
    Uri OllamaBaseUri,
    string VisionModel,
    string TextModel,
    string? EmbedModel,
    string? FfmpegPath,
    TimeSpan OllamaRequestTimeout = default,
    string OllamaKeepAlive = "24h",
    int OllamaNumCtx = 8192
)
{
    /// <summary>Lädt via einheitliche AiPlatformConfig.</summary>
    public static AiRuntimeConfig Load() =>
        AiPlatformConfig.Load().ToRuntimeConfig();

    /// <summary>Erstellt einen OllamaClient mit den Einstellungen dieser Config.</summary>
    public OllamaClient CreateOllamaClient(System.Net.Http.HttpClient? http = null) =>
        new(OllamaBaseUri, http, OllamaRequestTimeout, keepAlive: OllamaKeepAlive, numCtx: OllamaNumCtx);
}
