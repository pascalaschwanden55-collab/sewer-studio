namespace AuswertungPro.Next.Application.Ai.Training;

/// <summary>
/// Persistiert die Ergebnisverlaeufe der Selbsttraining-Laeufe.
/// </summary>
public interface ISelfTrainingHistoryStore
{
    string StoragePath { get; }

    Task<List<SelfTrainingRunSnapshot>> LoadAsync();

    Task AppendRunAsync(SelfTrainingRunSnapshot run);
}
