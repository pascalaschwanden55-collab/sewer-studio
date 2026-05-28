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
    public const string DefaultVisionModel = "qwen2.5vl:3b";
    public const string DefaultTextModel = "qwen2.5:3b";
    public const string DefaultEmbedModel = "nomic-embed-text";
    public const string DefaultKeepAlive = "24h";
    public const int DefaultNumCtx = 8192;
}
