using System;

namespace AuswertungPro.Next.UI.Ai.Ollama;

/// <summary>
/// Konfiguration für den Ollama-Server und verwendete Modelle.
/// Werte werden via AiPlatformConfig geladen.
/// </summary>
public sealed record OllamaConfig(
    Uri BaseUri,
    string VisionModel,
    string TextModel,
    string EmbedModel,
    TimeSpan RequestTimeout,
    string KeepAlive = OllamaConfig.DefaultKeepAlive,
    int NumCtx = OllamaConfig.DefaultNumCtx)
{
    // ── Modell-Konstanten (Single Source of Truth) ──────────────────────
    public const string DefaultVisionModel = "qwen2.5vl:32b";
    public const string DefaultTextModel   = "qwen2.5vl:32b";
    public const string DefaultEmbedModel  = "nomic-embed-text";
    public const string DefaultKeepAlive   = "24h";
    public const int    DefaultNumCtx      = 8192;

    /// <summary>Lädt via einheitliche AiPlatformConfig.</summary>
    public static OllamaConfig Load() =>
        AiPlatformConfig.Load().ToOllamaConfig();
}
