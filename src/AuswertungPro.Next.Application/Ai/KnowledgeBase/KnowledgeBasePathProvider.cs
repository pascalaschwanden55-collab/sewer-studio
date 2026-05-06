using System;

namespace AuswertungPro.Next.Application.Ai.KnowledgeBase;

/// <summary>
/// Phase 5.3 Sub-D: Pfad-Resolver fuer die KnowledgeBase-DB.
/// Wird beim App-Start einmalig gesetzt (in der UI-Schicht) und liefert
/// den Default-Pfad fuer KnowledgeBaseContext, damit der Infrastructure-Layer
/// keine UI-Abhaengigkeit (auf KnowledgeRoot) braucht.
/// </summary>
public static class KnowledgeBasePathProvider
{
    private static Func<string>? _resolver;

    /// <summary>
    /// Registriert den Pfad-Resolver. Muss einmal beim App-Start aufgerufen werden,
    /// bevor <see cref="GetDbPath"/> verwendet wird.
    /// </summary>
    public static void SetResolver(Func<string> resolver)
        => _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));

    /// <summary>
    /// Liefert den Pfad zur KnowledgeBase-Datei. Erfordert vorherigen Aufruf von <see cref="SetResolver"/>.
    /// </summary>
    public static string GetDbPath()
        => _resolver?.Invoke()
           ?? throw new InvalidOperationException(
               "KnowledgeBasePathProvider.SetResolver must be called at app startup before GetDbPath.");

    /// <summary>True wenn ein Resolver registriert ist (zum Testen).</summary>
    public static bool HasResolver => _resolver is not null;
}
