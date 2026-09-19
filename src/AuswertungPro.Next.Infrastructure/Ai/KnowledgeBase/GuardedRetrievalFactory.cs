using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

/// <summary>
/// Die EINE Stelle, an der ein <see cref="RetrievalService"/> entsteht.
///
/// Anlass (Auditbefund 11, 18.09.2026): Drei Stellen erzeugten eine eigene Suche, aber nur
/// eine reichte die Sperrliste der reservierten Pruefhaltungen weiter. Vollprotokoll-
/// Erstellung und Selbsttraining konnten dadurch reservierte Testfaelle als
/// Vergleichswissen verwenden — die Trennung von Lernwissen und Pruefdaten galt nicht
/// ueberall.
///
/// Drei Umgehungen bei drei Stellen heissen: Der Schutz darf nicht daran haengen, dass
/// jeder Aufrufer daran denkt. Die Anwendung setzt den Pruefdaten-Root deshalb EINMAL
/// beim Start; wer danach eine Suche baut, bekommt die Sperrliste automatisch.
/// Ein Waechter haelt fest, dass `new RetrievalService(` sonst nirgends im Produktivcode
/// steht.
/// </summary>
public static class GuardedRetrievalFactory
{
    private static string? _defaultEvalSetRoot;

    /// <summary>
    /// Setzt den Pruefdaten-Root fuer alle spaeter erzeugten Suchen. Die Anwendung ruft
    /// das einmal beim Aufbau ihrer Dienste.
    /// </summary>
    public static void ConfigureDefaultEvalSetRoot(string? evalSetRoot)
        => _defaultEvalSetRoot = evalSetRoot;

    /// <summary>
    /// Erzeugt die Suche mit der Sperrliste. Ohne eigenen Root gilt der zentral
    /// gesetzte Standard. Ein fehlender Root liefert eine leere Liste — dasselbe
    /// Verhalten wie bisher, denn dann sind auch keine Pruefdaten reserviert.
    /// </summary>
    public static RetrievalService Create(
        KnowledgeBaseContext db,
        EmbeddingService embedder,
        string? evalSetRoot = null)
        => new(db, embedder,
            EvalContaminationGuard.LoadEvalHaltungKeys(evalSetRoot ?? _defaultEvalSetRoot));

    /// <summary>Fuer Aufrufer, welche die Sperrliste bereits geladen haben.</summary>
    public static RetrievalService Create(
        KnowledgeBaseContext db,
        EmbeddingService embedder,
        IReadOnlySet<string>? evalHaltungKeys)
        => new(db, embedder, evalHaltungKeys);
}
