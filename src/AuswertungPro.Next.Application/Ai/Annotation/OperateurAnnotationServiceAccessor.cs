namespace AuswertungPro.Next.Application.Ai.Annotation;

/// <summary>
/// Slice 1 (Operateur-Annotation): einfacher Singleton-Accessor, damit das
/// PlayerWindow den Service nicht ueber Konstruktor-Injection bekommen muss
/// (PlayerWindow wird an ~5 Stellen instanziert; eine Factory aufzubauen
/// waere fuer Slice 1 zuviel Scope).
///
/// Pattern identisch zu <see cref="SidecarAuthTokenAccessor"/>: das
/// App-Bootstrapping fuellt <see cref="Current"/> nach dem DI-BuildServiceProvider
/// einmal mit der voll konstruierten Service-Instanz; Konsumenten ziehen
/// lazy ueber <see cref="Current"/>.
/// </summary>
public static class OperateurAnnotationServiceAccessor
{
    /// <summary>
    /// Aktuell registrierter Service. Null, solange die App noch nicht
    /// gebootstrapped ist oder die KI-Pipeline ausgeschaltet wurde.
    /// </summary>
    public static IOperateurAnnotationService? Current { get; set; }
}
