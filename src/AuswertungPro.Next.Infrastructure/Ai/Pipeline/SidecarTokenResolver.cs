using System.Threading;
using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>
/// Kompatibilitätsfassade für die gemeinsame Sidecar-Token-Auflösung.
/// </summary>
public static class SidecarTokenResolver
{
    public const string HeaderName = "X-Sidecar-Token";

    private static ISidecarTokenResolver _current = new SidecarTokenFileResolver();

    public static ISidecarTokenResolver Current => Volatile.Read(ref _current);

    public static void Use(ISidecarTokenResolver resolver)
        => Volatile.Write(
            ref _current,
            resolver ?? throw new ArgumentNullException(nameof(resolver)));

    public static string? Resolve(string? configuredToken = null)
        => Current.Resolve(configuredToken);

    public static string? Normalize(string? token)
        => SidecarTokenFileResolver.Normalize(token);
}
