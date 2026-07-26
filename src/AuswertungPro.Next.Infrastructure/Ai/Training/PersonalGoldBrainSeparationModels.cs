namespace AuswertungPro.Next.Infrastructure.Ai.Training;

internal sealed record PersonalGoldBrainResolvedPaths(
    string TransactionId,
    string KnowledgeRoot,
    string LocalArchiveRoot,
    string ExternalMirrorRoot,
    string ExternalArchiveRoot,
    string StagingRoot,
    string LegacyProtocolTrainingPath,
    string DisconnectedLegacyProtocolTrainingPath,
    string CommitJournalPath,
    string LocalSafetyRoot,
    string ExternalSafetyRoot,
    string LegacySafetyRoot,
    string SamplesPath,
    string DatabasePath);

internal sealed record PersonalGoldBrainCommitJournal(
    int SchemaVersion,
    string TransactionId,
    DateTimeOffset StartedUtc,
    string KnowledgeRoot,
    string LocalArchiveRoot,
    string ExternalMirrorRoot,
    string ExternalArchiveRoot,
    string StagingRoot,
    string LegacyProtocolTrainingPath,
    string DisconnectedLegacyProtocolTrainingPath,
    bool LegacyProtocolTrainingWasPresent,
    string? LegacyProtocolTrainingSha256,
    bool LocalExternalContextDirectoryWasPresent,
    bool ExternalContextDirectoryWasPresent);

internal sealed record PersonalGoldBrainSourceDatabaseInspection(int SourceSamples);

internal enum PersonalGoldBrainCommitRecoveryState
{
    Unknown,
    Prepared,
    ExternalMirrorMoved,
    LocalKnowledgeMoved,
    ActiveKnowledgePublished,
    Restored
}

internal enum PersonalGoldBrainCommitStep
{
    ExternalMirrorMoved,
    LocalKnowledgeMoved,
    ActiveKnowledgePublished
}
