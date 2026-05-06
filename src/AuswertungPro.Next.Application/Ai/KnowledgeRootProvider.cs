using System;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Phase 5.3: Pfad-Resolver fuer den KnowledgeRoot-Ordner (C:\KI_BRAIN).
/// Wird beim App-Start einmalig in der UI-Schicht gesetzt
/// (Func zeigt auf <c>UI.Ai.KnowledgeRoot.GetRoot</c>) und liefert Application/
/// Infrastructure-Services den Pfad ohne UI-Dependency.
/// </summary>
public static class KnowledgeRootProvider
{
    private static Func<string>? _resolver;

    /// <summary>Registriert den Pfad-Resolver. Einmal beim App-Start.</summary>
    public static void SetResolver(Func<string> resolver)
        => _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));

    /// <summary>Liefert den KnowledgeRoot-Pfad. Erfordert vorherigen <see cref="SetResolver"/>.</summary>
    public static string GetRoot()
        => _resolver?.Invoke()
           ?? throw new InvalidOperationException(
               "KnowledgeRootProvider.SetResolver must be called at app startup before GetRoot.");

    /// <summary>True wenn ein Resolver registriert ist.</summary>
    public static bool HasResolver => _resolver is not null;
}
