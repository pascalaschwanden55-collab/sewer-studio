using System;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Phase 5.3 Sub-A: Resolver fuer die PipelineConfig (Sidecar-URL,
/// YOLO-Schwellen, DINO-Schwellen, MultiModel-Flag). UI registriert beim
/// App-Start einen Loader (typischerweise <c>AiPlatformConfig.Load().ToPipelineConfig()</c>),
/// damit Application/Infrastructure-Services die Konfig ohne UI-Dependency lesen.
///
/// Analog zu <see cref="AiRuntimeConfigProvider"/> und <see cref="Ollama.OllamaConfigProvider"/>.
/// </summary>
public static class PipelineConfigProvider
{
    private static Func<PipelineConfig>? _loader;

    /// <summary>Registriert den Loader. Einmal beim App-Start.</summary>
    public static void SetLoader(Func<PipelineConfig> loader)
        => _loader = loader ?? throw new ArgumentNullException(nameof(loader));

    /// <summary>Liefert die PipelineConfig. Loader oder InvalidOperationException wenn nicht registriert.</summary>
    public static PipelineConfig Load()
        => _loader?.Invoke()
           ?? throw new InvalidOperationException(
               "PipelineConfigProvider.SetLoader must be called at app startup before Load.");

    /// <summary>True wenn ein Loader registriert ist.</summary>
    public static bool HasLoader => _loader is not null;
}
