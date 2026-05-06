using AuswertungPro.Next.Application.Ai.Ollama;

namespace AuswertungPro.Next.UI.Ai.Ollama;

/// <summary>
/// Phase 5.3 Sub-F1: UI-Helper fuer OllamaConfig.
/// Der Record selbst lebt in Application/Ai/Ollama (pure DTO);
/// das Laden via AiPlatformConfig ist UI-Plumbing.
/// </summary>
public static class OllamaConfigExtensions
{
    /// <summary>Laedt die Ollama-Konfig via einheitlicher AiPlatformConfig.</summary>
    public static OllamaConfig Load() =>
        AiPlatformConfig.Load().ToOllamaConfig();
}
