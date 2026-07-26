namespace AuswertungPro.Next.Application.Ai.Training;

/// <summary>
/// Verwaltet den vorbereitenden Gold-Eingang. Bilder darin sind noch keine
/// Trainingsdaten und werden nur gelesen.
/// </summary>
public interface IPersonalGoldInboxService
{
    string EnsureFolders();

    Task<PersonalGoldInboxSnapshot> LoadAsync(
        CancellationToken cancellationToken = default);
}

public sealed record PersonalGoldInboxImage(
    string FramePath,
    string QueueId,
    string? SuggestedMainCode);

public sealed record PersonalGoldInboxSnapshot(
    string RootPath,
    IReadOnlyList<PersonalGoldInboxImage> Images,
    IReadOnlyList<string> Issues);
