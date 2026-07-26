namespace AuswertungPro.Next.Application.Ai.Training;

/// <summary>
/// Baut aus einem bestehenden Wissensordner einen neuen, ausschliesslich
/// persoenlich bestaetigten Goldstand und archiviert den vorherigen Stand.
/// </summary>
public interface IPersonalGoldBrainSeparationService
{
    Task<PersonalGoldBrainSeparationResult> SeparateAsync(
        PersonalGoldBrainSeparationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record PersonalGoldBrainSeparationRequest(
    string KnowledgeRoot,
    string LocalArchiveRoot,
    string ExternalMirrorRoot,
    string ExternalArchiveRoot,
    string LegacyProtocolTrainingPath,
    string ConfirmedByUser,
    DateTimeOffset StartedUtc,
    IReadOnlyList<string> RequiredMainCodes,
    bool DryRun = false);

public sealed record PersonalGoldBrainSeparationResult(
    bool Success,
    bool DryRun,
    int SourceSamples,
    int PersonalGoldSamples,
    int FullGoldSamples,
    int SourceKnowledgeSamples,
    int ActiveKnowledgeSamples,
    string? ActiveRoot,
    string? LocalArchiveRoot,
    string? ExternalArchiveRoot,
    string? ReceiptPath,
    string? Error);

/// <summary>
/// Holt persoenlich bestaetigte ManualCoding-Faelle nach, die nur noch in der
/// archivierten SQLite-KB und nicht mehr in training_samples.json stehen.
/// </summary>
public interface IPersonalGoldArchiveRecoveryService
{
    Task<PersonalGoldArchiveRecoveryResult> RecoverAsync(
        PersonalGoldArchiveRecoveryRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record PersonalGoldArchiveRecoveryRequest(
    string ActiveKnowledgeRoot,
    string LegacyKnowledgeRoot,
    string ConfirmedByUser,
    DateTimeOffset StartedUtc,
    IReadOnlyList<string> RequiredMainCodes,
    bool DryRun = false);

public sealed record PersonalGoldArchiveRecoveryResult(
    bool Success,
    bool DryRun,
    int ExistingPersonalGoldSamples,
    int DatabaseOnlyCandidates,
    int RecoveredSamples,
    int ActivePersonalGoldSamples,
    string? ReceiptPath,
    string? Error);
