namespace AuswertungPro.Next.Application.Ai.Sanierung;

/// <summary>
/// Persistiert die nachvollziehbaren KI-Sanierungsentscheidungen pro Haltung.
/// </summary>
public interface IAiOptimizationSessionStore
{
    string StoragePath { get; }

    Task SaveAsync(AiOptimizationSession session);

    Task<IReadOnlyList<AiOptimizationSession>> LoadAllAsync();

    Task<IReadOnlyList<AiOptimizationSession>> LoadForHaltungAsync(string haltungId);
}
