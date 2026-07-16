using AuswertungPro.Next.Application.Ai.KnowledgeBase;

namespace AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

public sealed record KnowledgeBaseHealthResult(bool IsHealthy, string? Error)
{
    public static readonly KnowledgeBaseHealthResult Ok = new(true, null);
}

/// <summary>Prueft eine vorhandene SQLite-KB schreibgeschuetzt vor der normalen Nutzung.</summary>
public static class KnowledgeBaseHealthChecker
{
    private static readonly IKnowledgeBaseHealthInspector Default =
        new KnowledgeBaseHealthInspectionService();

    public static IKnowledgeBaseHealthInspector Current => Default;

    [Obsolete("Globaler Austausch wurde entfernt. Den Dienst per Konstruktor uebergeben.")]
    public static void Use(IKnowledgeBaseHealthInspector inspector)
        => throw new NotSupportedException(
            "Die globale Wissensdatenbank-Pruefung kann nicht mehr ausgetauscht werden. " +
            "IKnowledgeBaseHealthInspector bitte per Konstruktor uebergeben.");

    public static KnowledgeBaseHealthResult Check(string dbPath)
    {
        var result = Current.Inspect(dbPath);
        return result.IsHealthy
            ? KnowledgeBaseHealthResult.Ok
            : new KnowledgeBaseHealthResult(false, result.Error);
    }
}
