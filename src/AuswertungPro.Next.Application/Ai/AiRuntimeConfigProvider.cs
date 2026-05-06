using System;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Phase 5.3: Resolver fuer die AiRuntimeConfig — UI registriert beim Start
/// einen Loader (typischerweise <c>AiPlatformConfig.Load().ToRuntimeConfig()</c>),
/// damit Application/Infrastructure-Services die Konfig ohne UI-Dependency lesen.
/// </summary>
public static class AiRuntimeConfigProvider
{
    private static Func<AiRuntimeConfig>? _loader;

    /// <summary>Registriert den Loader. Einmal beim App-Start.</summary>
    public static void SetLoader(Func<AiRuntimeConfig> loader)
        => _loader = loader ?? throw new ArgumentNullException(nameof(loader));

    /// <summary>Liefert die AiRuntimeConfig. Loader oder InvalidOperationException wenn nicht registriert.</summary>
    public static AiRuntimeConfig Load()
        => _loader?.Invoke()
           ?? throw new InvalidOperationException(
               "AiRuntimeConfigProvider.SetLoader must be called at app startup before Load.");

    /// <summary>True wenn ein Loader registriert ist.</summary>
    public static bool HasLoader => _loader is not null;
}
