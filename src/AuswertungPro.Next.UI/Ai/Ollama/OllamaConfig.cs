// AuswertungPro – KI Videoanalyse Modul
using System;

namespace AuswertungPro.Next.UI.Ai.Ollama;

/// <summary>
/// Konfiguration für den Ollama-Server und verwendete Modelle.
/// Werte werden aus Umgebungsvariablen geladen (Fallback: Defaults).
/// </summary>
public sealed record OllamaConfig(
    Uri BaseUri,
    string VisionModel,
    string TextModel,
    string EmbedModel,
    TimeSpan RequestTimeout)
{
    // ── Modell-Konstanten ────────────────────────────────────────────────
    public const string DefaultVisionModel = "qwen2.5vl:7b";
    public const string DefaultTextModel   = "qwen2.5:14b";
    public const string DefaultEmbedModel  = "mxbai-embed-large";

    // ── Env-Variablen ────────────────────────────────────────────────────
    private const string EnvUrl     = "AUSWERTUNGPRO_OLLAMA_URL";
    private const string EnvVision  = "AUSWERTUNGPRO_AI_VISION_MODEL";
    private const string EnvText    = "AUSWERTUNGPRO_AI_TEXT_MODEL";
    private const string EnvEmbed   = "AUSWERTUNGPRO_AI_EMBED_MODEL";
    private const string EnvTimeout = "AUSWERTUNGPRO_AI_TIMEOUT_MIN";

    /// <summary>Lädt Konfiguration aus Umgebungsvariablen.</summary>
    public static OllamaConfig Load()
    {
        var url = Environment.GetEnvironmentVariable(EnvUrl)?.Trim()
                  ?? "http://localhost:11434";

        var vision = Environment.GetEnvironmentVariable(EnvVision)?.Trim()
                     ?? DefaultVisionModel;

        var text = Environment.GetEnvironmentVariable(EnvText)?.Trim()
                   ?? DefaultTextModel;

        var embed = Environment.GetEnvironmentVariable(EnvEmbed)?.Trim()
                    ?? DefaultEmbedModel;

        var timeoutMin = int.TryParse(
            Environment.GetEnvironmentVariable(EnvTimeout), out var t) ? t : 30;

        return new OllamaConfig(
            BaseUri:        new Uri(url),
            VisionModel:    vision,
            TextModel:      text,
            EmbedModel:     embed,
            RequestTimeout: TimeSpan.FromMinutes(timeoutMin));
    }
}
