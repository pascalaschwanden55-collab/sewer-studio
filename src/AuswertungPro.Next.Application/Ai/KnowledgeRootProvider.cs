using System;
using System.IO;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Phase 5.3: Pfad-Resolver fuer den KnowledgeRoot-Ordner (C:\KI_BRAIN).
/// Wird beim App-Start einmalig in der UI-Schicht gesetzt
/// (Func zeigt auf <c>UI.Ai.KnowledgeRoot.GetRoot</c>) und liefert Application/
/// Infrastructure-Services den Pfad ohne UI-Dependency.
///
/// Fallback fuer Tests/Headless: SEWERSTUDIO_KNOWLEDGE_ROOT Env-Var oder
/// %TEMP%\SewerStudioTests, falls kein Resolver registriert.
/// </summary>
public static class KnowledgeRootProvider
{
    private static Func<string>? _resolver;

    /// <summary>Registriert den Pfad-Resolver. Einmal beim App-Start.</summary>
    public static void SetResolver(Func<string> resolver)
        => _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));

    /// <summary>Liefert den KnowledgeRoot-Pfad.
    /// Reihenfolge: Resolver → SEWERSTUDIO_KNOWLEDGE_ROOT → Temp-Fallback.</summary>
    public static string GetRoot()
    {
        if (_resolver is not null)
            return _resolver();

        var envRoot = Environment.GetEnvironmentVariable("SEWERSTUDIO_KNOWLEDGE_ROOT");
        if (!string.IsNullOrWhiteSpace(envRoot))
            return envRoot;

        var fallback = Path.Combine(Path.GetTempPath(), "SewerStudioTests");
        Directory.CreateDirectory(fallback);
        return fallback;
    }

    /// <summary>True wenn ein Resolver registriert ist.</summary>
    public static bool HasResolver => _resolver is not null;
}
