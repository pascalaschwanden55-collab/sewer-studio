namespace AuswertungPro.Next.Application.Ai.Training;

/// <summary>
/// Uebernimmt bereits persoenlich bestaetigte Trainingsbilder in den kanonischen
/// Goldordner und haelt JSON sowie Wissensdatenbank auf demselben Pfadstand.
/// </summary>
public interface IPersonalGoldFrameMigrationService
{
    Task<PersonalGoldFrameMigrationResult> MigrateAsync(
        PersonalGoldFrameMigrationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record PersonalGoldFrameMigrationRequest(
    string KnowledgeRoot,
    string ConfirmedByUser,
    IReadOnlyList<string> RequiredMainCodes,
    DateTimeOffset StartedUtc,
    int TargetMinimumPerMainCode = 30,
    int TargetMaximumPerMainCode = 50,
    bool DryRun = false);

public sealed record PersonalGoldFrameMigrationResult(
    bool Success,
    bool DryRun,
    int SelectedSamples,
    int MigratedSamples,
    int UniqueGoldFrames,
    int FullGoldSamples,
    string? InventoryPath,
    string? AuditDirectory,
    string? Error,
    IReadOnlyList<PersonalGoldMainCodeStatus> MainCodes);

public sealed record PersonalGoldMainCodeStatus(
    string MainCode,
    int PersonalSamples,
    int BboxSamples,
    int FullGoldSamples,
    int UniqueGoldFrames,
    int TargetMinimum,
    int TargetMaximum,
    int NeededForMinimum,
    string Status);
