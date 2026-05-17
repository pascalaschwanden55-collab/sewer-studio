using System;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Phase 5.3 Sub-C: KI-Runtime-Konfiguration als reiner Datentyp.
/// Geladen ueber <c>AiPlatformConfig.Load().ToRuntimeConfig()</c> (UI-Schicht).
/// </summary>
public sealed record AiRuntimeConfig(
    bool Enabled,
    Uri OllamaBaseUri,
    string VisionModel,
    string TextModel,
    string? EmbedModel,
    string? FfmpegPath,
    string? ReferenceVisionModel = null,
    TimeSpan OllamaRequestTimeout = default,
    string OllamaKeepAlive = "24h",
    int OllamaNumCtx = 8192,
    bool EnableDiagnostics = false
);
