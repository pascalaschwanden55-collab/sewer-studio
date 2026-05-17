using System.Net.Http;
using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.Infrastructure.Ai.Ollama;

/// <summary>
/// Phase 5.3: Infrastructure-Helper fuer AiRuntimeConfig — erzeugt OllamaClient.
/// Lebt hier (nicht in UI), damit Infrastructure-Services (z.B. AiSanierungOptimizationService)
/// ohne UI-Dependency arbeiten koennen.
/// </summary>
public static class AiRuntimeConfigExtensions
{
    /// <summary>Erstellt einen OllamaClient mit den Einstellungen dieser Config.</summary>
    public static OllamaClient CreateOllamaClient(this AiRuntimeConfig cfg, HttpClient? http = null) =>
        new(cfg.OllamaBaseUri, http,
            cfg.OllamaRequestTimeout,
            keepAlive: cfg.OllamaKeepAlive,
            numCtx: cfg.OllamaNumCtx,
            diagnosticsEnabled: cfg.EnableDiagnostics);
}
