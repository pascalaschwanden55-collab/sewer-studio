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
    int NumCtx = OllamaConfig.DefaultNumCtx,
    string? ReferenceVisionModel = null)
{
    // ── Modell-Konstanten (Single Source of Truth) ──────────────────────
    // V4.1: 8B×6 Slots (8192 ctx) + 32B Swap-Eskalation bei Yellow/Red
    public const string DefaultVisionModel = "qwen3-vl:8b-q8";
    public const string DefaultTextModel   = "qwen3-vl:8b-q8";
    /// <summary>Eskalationsmodell: qwen3-vl:32b (RAM, num_gpu=0, ~28s pro Request).</summary>
    public const string DefaultReferenceVisionModel = "qwen3-vl:32b";
    public const string DefaultEmbedModel  = "nomic-embed-text";
    public const string DefaultKeepAlive   = "24h";
    public const int    DefaultNumCtx      = 8192;

    /// <summary>Lädt via einheitliche AiPlatformConfig.</summary>
    public static OllamaConfig Load() =>
        AiPlatformConfig.Load().ToOllamaConfig();
}
