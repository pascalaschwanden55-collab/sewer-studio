using System.Net.Http;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;

namespace AuswertungPro.Next.UI.Ai;

/// <summary>
/// Phase 5.3 Sub-C: UI-spezifische Helper fuer AiRuntimeConfig.
/// Der Record selbst lebt in Application/Ai (pure DTO);
/// das Laden + die OllamaClient-Erzeugung sind UI-Plumbing.
/// </summary>
public static class AiRuntimeConfigExtensions
{
    /// <summary>Laedt die Runtime-Konfig via einheitlicher AiPlatformConfig.</summary>
    public static AiRuntimeConfig Load() =>
        AiPlatformConfig.Load().ToRuntimeConfig();

    /// <summary>Erstellt einen OllamaClient mit den Einstellungen dieser Config.</summary>
    public static OllamaClient CreateOllamaClient(this AiRuntimeConfig cfg, HttpClient? http = null) =>
        new(cfg.OllamaBaseUri, http,
            cfg.OllamaRequestTimeout,
            keepAlive: cfg.OllamaKeepAlive,
            numCtx: cfg.OllamaNumCtx);
}
