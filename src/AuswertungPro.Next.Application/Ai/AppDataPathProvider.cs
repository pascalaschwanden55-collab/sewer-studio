using System;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Phase 5.3: Pfad-Resolver fuer das LocalAppData-Verzeichnis der App
/// (z.B. <c>%LOCALAPPDATA%\SewerStudio</c>).
/// UI registriert beim Start einen Resolver auf <c>AppSettings.AppDataDir</c>.
/// </summary>
public static class AppDataPathProvider
{
    private static Func<string>? _resolver;

    /// <summary>Registriert den Pfad-Resolver. Einmal beim App-Start.</summary>
    public static void SetResolver(Func<string> resolver)
        => _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));

    /// <summary>Liefert den AppData-Pfad. Erfordert vorherigen <see cref="SetResolver"/>.</summary>
    public static string GetAppDataDir()
        => _resolver?.Invoke()
           ?? throw new InvalidOperationException(
               "AppDataPathProvider.SetResolver must be called at app startup before GetAppDataDir.");

    /// <summary>True wenn ein Resolver registriert ist.</summary>
    public static bool HasResolver => _resolver is not null;
}
