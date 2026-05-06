using System;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Phase 5.3: Bridge fuer den Sidecar-Auth-Token.
/// UI registriert beim Start einen Resolver auf <c>PythonSidecarService.CurrentAuthToken</c>
/// + <c>PythonSidecarService.TokenFilePath</c>.
/// Application/Infrastructure-Clients (z.B. VisionPipelineClient) lesen den
/// Token ohne UI-Dependency.
/// </summary>
public static class SidecarAuthTokenAccessor
{
    private static Func<string?>? _tokenResolver;
    private static Func<string?>? _tokenFilePathResolver;

    public static void SetResolvers(Func<string?> tokenResolver, Func<string?> tokenFilePathResolver)
    {
        _tokenResolver = tokenResolver ?? throw new ArgumentNullException(nameof(tokenResolver));
        _tokenFilePathResolver = tokenFilePathResolver ?? throw new ArgumentNullException(nameof(tokenFilePathResolver));
    }

    /// <summary>Aktueller Auth-Token (oder null falls Sidecar nicht laeuft / kein Resolver).</summary>
    public static string? CurrentAuthToken => _tokenResolver?.Invoke();

    /// <summary>Pfad zur Token-Datei (oder null falls kein Resolver).</summary>
    public static string? TokenFilePath => _tokenFilePathResolver?.Invoke();
}
