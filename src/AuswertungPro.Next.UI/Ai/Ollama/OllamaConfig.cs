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
    /// <summary>"auto" = GpuModelSelector waehlt anhand VRAM (32b oder 7b).</summary>
    public const string DefaultVisionModel = "auto";
    public const string DefaultTextModel   = "auto";
    public const string DefaultEmbedModel  = "nomic-embed-text";
    public const string DefaultKeepAlive   = "24h";
    public const int    DefaultNumCtx      = 8192;

    /// <summary>Lädt via einheitliche AiPlatformConfig.</summary>
    public static OllamaConfig Load() =>
        AiPlatformConfig.Load().ToOllamaConfig();
}
