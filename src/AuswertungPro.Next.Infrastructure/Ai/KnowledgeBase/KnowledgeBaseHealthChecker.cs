using System.Threading;
using AuswertungPro.Next.Application.Ai.KnowledgeBase;

namespace AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

public sealed record KnowledgeBaseHealthResult(bool IsHealthy, string? Error)
{
    public static readonly KnowledgeBaseHealthResult Ok = new(true, null);
}

/// <summary>Prueft eine vorhandene SQLite-KB schreibgeschuetzt vor der normalen Nutzung.</summary>
public static class KnowledgeBaseHealthChecker
{
    private static IKnowledgeBaseHealthInspector _current = new KnowledgeBaseHealthInspectionService();

    public static IKnowledgeBaseHealthInspector Current => Volatile.Read(ref _current);

    public static void Use(IKnowledgeBaseHealthInspector inspector)
        => Volatile.Write(
            ref _current,
            inspector ?? throw new ArgumentNullException(nameof(inspector)));

    public static KnowledgeBaseHealthResult Check(string dbPath)
    {
        var result = Current.Inspect(dbPath);
        return result.IsHealthy
            ? KnowledgeBaseHealthResult.Ok
            : new KnowledgeBaseHealthResult(false, result.Error);
    }
}
