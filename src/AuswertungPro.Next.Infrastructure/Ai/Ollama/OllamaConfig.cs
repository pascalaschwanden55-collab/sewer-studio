using System;

namespace AuswertungPro.Next.Infrastructure.Ai.Ollama;

/// <summary>
/// Konfiguration fuer den Ollama-Server und verwendete Modelle.
/// Das Laden aus AppSettings bleibt im UI-Projekt.
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
    // A/B Juni 2026: Qwen2.5-VL lieferte 0% (Parse-Fehler) — Defaults duerfen
    // nie wieder still auf die 2.5-Familie zurueckfallen (qwen3-vl ist freigegeben).
    public const string DefaultVisionModel = "qwen3-vl:2b";
    public const string DefaultTextModel = "qwen3-vl:2b";
    public const string DefaultEmbedModel = "nomic-embed-text";
    public const string DefaultKeepAlive = "24h";
    public const int DefaultNumCtx = 8192;
}
