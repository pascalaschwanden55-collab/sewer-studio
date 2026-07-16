using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>
/// Kompatibilitätsfassade für die gemeinsame Sidecar-Token-Auflösung.
/// </summary>
public static class SidecarTokenResolver
{
    public const string HeaderName = "X-Sidecar-Token";

    private static readonly ISidecarTokenResolver Default = new SidecarTokenFileResolver();

    public static ISidecarTokenResolver Current => Default;

    [Obsolete("Globaler Austausch wurde entfernt. Den Dienst per Konstruktor uebergeben.")]
    public static void Use(ISidecarTokenResolver resolver)
        => throw new NotSupportedException(
            "Die globale Sidecar-Token-Aufloesung kann nicht mehr ausgetauscht werden. " +
            "ISidecarTokenResolver bitte per Konstruktor uebergeben.");

    public static string? Resolve(string? configuredToken = null)
        => Current.Resolve(configuredToken);

    public static string? Normalize(string? token)
        => SidecarTokenFileResolver.Normalize(token);
}
