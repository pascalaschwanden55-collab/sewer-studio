using System;

namespace AuswertungPro.Next.Application.Ai.Ollama;

/// <summary>
/// Phase 5.3: Resolver fuer die OllamaConfig — UI registriert beim Start
/// einen Loader (typischerweise <c>AiPlatformConfig.Load().ToOllamaConfig()</c>),
/// damit Application/Infrastructure-Services die Konfig ohne UI-Dependency lesen.
/// </summary>
public static class OllamaConfigProvider
{
    private static Func<OllamaConfig>? _loader;

    /// <summary>Registriert den Loader. Einmal beim App-Start.</summary>
    public static void SetLoader(Func<OllamaConfig> loader)
        => _loader = loader ?? throw new ArgumentNullException(nameof(loader));

    /// <summary>Liefert die OllamaConfig. Loader oder InvalidOperationException wenn nicht registriert.</summary>
    public static OllamaConfig Load()
        => _loader?.Invoke()
           ?? throw new InvalidOperationException(
               "OllamaConfigProvider.SetLoader must be called at app startup before Load.");

    /// <summary>True wenn ein Loader registriert ist.</summary>
    public static bool HasLoader => _loader is not null;
}
