using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.UI.Ai;

/// <summary>
/// Phase 5.3 Sub-C: UI-spezifische Helper fuer AiRuntimeConfig.
/// Der Record selbst lebt in Application/Ai (pure DTO);
/// das Laden ist UI-Plumbing (haengt an AiPlatformConfig).
/// CreateOllamaClient ist nach Infrastructure/Ai/Ollama/AiRuntimeConfigExtensions umgezogen.
/// </summary>
public static class AiRuntimeConfigLoader
{
    /// <summary>Laedt die Runtime-Konfig via einheitlicher AiPlatformConfig.</summary>
    public static AiRuntimeConfig Load() =>
        AiPlatformConfig.Load().ToRuntimeConfig();
}
