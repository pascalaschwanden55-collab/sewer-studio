namespace AuswertungPro.Next.Application.Ai.KnowledgeBase;

/// <summary>
/// Ergebnis der schreibgeschuetzten Pruefung einer Wissensdatenbank.
/// </summary>
public sealed record KnowledgeBaseHealthInspection(
    bool DatabaseExists,
    bool IsHealthy,
    string? Error);

/// <summary>
/// Prueft, ob eine vorhandene Wissensdatenbank lesbar und strukturell gesund ist.
/// Eine fehlende Datenbank ist kein Fehler, weil sie beim ersten Start angelegt wird.
/// </summary>
public interface IKnowledgeBaseHealthInspector
{
    KnowledgeBaseHealthInspection Inspect(string dbPath);
}
