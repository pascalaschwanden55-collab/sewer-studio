using System.Text.Json;
using System.Text.Json.Serialization;
using AuswertungPro.Next.Application.Ai.Training.ExportPlans;

namespace AuswertungPro.Next.Infrastructure.Ai.Training.ExportPlans;

internal sealed class TrainingExportRegistryFileDocument
{
    [JsonRequired]
    public required string SchemaVersion { get; init; }

    [JsonRequired]
    public TrainingExportRegistryApprovalStatus ApprovalStatus { get; init; }

    public string? ApprovedBy { get; init; }

    public DateTimeOffset? ApprovedUtc { get; init; }

    public IReadOnlyList<string> ApprovedSampleIds { get; init; }
        = Array.Empty<string>();

    [JsonRequired]
    public IReadOnlyDictionary<string, TrainingExportHoldingRole> HoldingRoles { get; init; }
        = new Dictionary<string, TrainingExportHoldingRole>();

    [JsonRequired]
    public IReadOnlyList<TrainingExportProtectedSetFileDocument> ProtectedSets { get; init; }
        = Array.Empty<TrainingExportProtectedSetFileDocument>();

    /// <summary>Optionale, menschlich kuratierte Negativbilder (additiv; fehlt bei Alt-Registrys).</summary>
    public IReadOnlyList<TrainingExportNegativeImageFileDocument>? NegativeImages { get; init; }
}

internal sealed class TrainingExportNegativeImageFileDocument
{
    [JsonRequired]
    public required string Path { get; init; }

    [JsonRequired]
    public required string Sha256 { get; init; }

    /// <summary>Optionaler Split-Hinweis (train/validation); null = Planer entscheidet deterministisch.</summary>
    public TrainingExportTarget? Split { get; init; }

    public string? SourceType { get; init; }

    /// <summary>Echte Haltung; vorhanden nur bei streng gebundenen neuen Negativ-Sets.</summary>
    public string? HoldingKey { get; init; }

    public string? PhysicalHoldingKey { get; init; }

    public string? SetId { get; init; }

    public string? SetManifestSha256 { get; init; }

    public string? QueueId { get; init; }

    public string? ReviewSha256 { get; init; }

    public string? QueueManifestSha256 { get; init; }

    public string? CandidatesSha256 { get; init; }

    public int? ClassMapVersion { get; init; }

    public string? ClassMapSha256 { get; init; }

    public string? VsaManifestHash { get; init; }

    public string? ReviewItemId { get; init; }

    public string? ReviewDecision { get; init; }
}

internal sealed record NegativeBinding(
    string? SourceType,
    string? HoldingKey,
    string? PhysicalHoldingKey,
    string? NegativeSetId,
    string? NegativeSetManifestSha256,
    string? QueueId,
    string? ReviewSha256,
    string? QueueManifestSha256,
    string? CandidatesSha256,
    int? ClassMapVersion,
    string? ClassMapSha256,
    string? VsaManifestHash,
    string? ReviewItemId,
    string? ReviewDecision)
{
    public static NegativeBinding Legacy { get; } = new(
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null);

    public bool IsLegacy => HoldingKey is null;
}

internal sealed record ValidatedNegativeSetManifest(
    string Sha256,
    NegativeSetManifestFileDocument Document,
    IReadOnlyDictionary<string, StableNegativeSetFile> Files,
    ValidatedNegativeSetReceipts Receipts);

internal sealed record StableNegativeSetFile(
    string Path,
    byte[] Bytes,
    string Sha256);

internal sealed record ValidatedNegativeSetReceipts(
    string QueueId,
    string ClassMapSha256,
    string VsaManifestHash,
    int ClassMapVersion,
    IReadOnlyDictionary<string, int> Classes);

internal sealed class NegativeSetManifestFileDocument
{
    [JsonRequired]
    public required string SchemaVersion { get; init; }

    [JsonRequired]
    public required string Purpose { get; init; }

    [JsonRequired]
    public required string SetId { get; init; }

    [JsonRequired]
    public required string Pilot { get; init; }

    [JsonRequired]
    public required string Role { get; init; }

    [JsonRequired]
    public required string CreatedUtc { get; init; }

    [JsonRequired]
    public bool Frozen { get; init; }

    [JsonRequired]
    public required string DatasetStatus { get; init; }

    [JsonRequired]
    public required string HashAlgorithm { get; init; }

    [JsonRequired]
    public int ImagesCount { get; init; }

    [JsonRequired]
    public int HoldingsCount { get; init; }

    [JsonRequired]
    public int HashesCount { get; init; }

    [JsonRequired]
    public required IReadOnlyDictionary<string, NegativeSetHashFileDocument> Hashes { get; init; }

    [JsonRequired]
    public required NegativeSetSemanticFileDocument Semantic { get; init; }
}

internal sealed class NegativeSetHashFileDocument
{
    [JsonRequired]
    public required string Sha256 { get; init; }

    [JsonRequired]
    public long SizeBytes { get; init; }
}

internal sealed class NegativeSetSemanticFileDocument
{
    [JsonRequired]
    public required string SchemaVersion { get; init; }

    [JsonRequired]
    public required string Purpose { get; init; }

    [JsonRequired]
    public required string Pilot { get; init; }

    [JsonRequired]
    public required string Role { get; init; }

    [JsonRequired]
    public required NegativeSetQueueFileDocument Queue { get; init; }

    [JsonRequired]
    public required NegativeSetReviewFileDocument Review { get; init; }

    [JsonRequired]
    public int ClassMapVersion { get; init; }

    [JsonRequired]
    public required string ClassMapSha256 { get; init; }

    [JsonRequired]
    public required string ClassMapReceiptPath { get; init; }

    [JsonRequired]
    public required string VsaManifestHash { get; init; }

    [JsonRequired]
    public required IReadOnlyList<string> ClassNames { get; init; }

    [JsonRequired]
    public required IReadOnlyList<NegativeSetProtectedSetFileDocument> ProtectedSets { get; init; }

    [JsonRequired]
    public required NegativeSetProtectionSnapshotFileDocument ProtectionSnapshot { get; init; }

    [JsonRequired]
    public required NegativeSetSplitRuleFileDocument SplitRule { get; init; }

    [JsonRequired]
    public required IReadOnlyList<NegativeSetImageFileDocument> Images { get; init; }
}

internal sealed class NegativeSetQueueFileDocument
{
    [JsonRequired]
    public required string QueueId { get; init; }

    [JsonRequired]
    public required string QueueManifestSha256 { get; init; }

    [JsonRequired]
    public required string QueueManifestReceiptPath { get; init; }

    [JsonRequired]
    public required string CandidatesSha256 { get; init; }

    [JsonRequired]
    public required string CandidatesReceiptPath { get; init; }
}

internal sealed class NegativeSetReviewFileDocument
{
    [JsonRequired]
    public required string Purpose { get; init; }

    [JsonRequired]
    public required string ReviewSha256 { get; init; }

    [JsonRequired]
    public required string ReceiptPath { get; init; }

    [JsonRequired]
    public int ReviewedImages { get; init; }

    [JsonRequired]
    public required NegativeSetDecisionCountsFileDocument DecisionCounts { get; init; }
}

internal sealed class NegativeSetDecisionCountsFileDocument
{
    [JsonRequired]
    public int AllClassesClear { get; init; }

    [JsonRequired]
    public int MappedObjectVisible { get; init; }

    [JsonRequired]
    public int ExcludeUncertain { get; init; }
}

internal sealed class NegativeSetSplitRuleFileDocument
{
    [JsonRequired]
    public required string Name { get; init; }

    [JsonRequired]
    public required string Salt { get; init; }

    [JsonRequired]
    public bool OneImagePerPhysicalHolding { get; init; }

    [JsonRequired]
    public int ValidationCount { get; init; }

    [JsonRequired]
    public int TrainCount { get; init; }
}

internal sealed class NegativeSetImageFileDocument
{
    [JsonRequired]
    public required string Id { get; init; }

    [JsonRequired]
    public required string FileName { get; init; }

    [JsonRequired]
    public required string ImageSha256 { get; init; }

    [JsonRequired]
    public long SizeBytes { get; init; }

    [JsonRequired]
    public required string ImageFormat { get; init; }

    [JsonRequired]
    public required string HoldingKey { get; init; }

    [JsonRequired]
    public required string PhysicalHoldingKey { get; init; }

    [JsonRequired]
    public TrainingExportTarget Split { get; init; }

    [JsonRequired]
    public required string ReviewItemId { get; init; }

    [JsonRequired]
    public required string ReviewDecision { get; init; }

    [JsonRequired]
    public required string SourceRef { get; init; }

    [JsonRequired]
    public required string InspectionDate { get; init; }
}

internal sealed class NegativeSetProtectedSetFileDocument
{
    [JsonRequired]
    public required string SetId { get; init; }

    [JsonRequired]
    public required string ManifestStatus { get; init; }

    [JsonRequired]
    public string? ManifestSha256 { get; init; }

    [JsonRequired]
    public required string CandidatesSha256 { get; init; }
}

internal sealed class NegativeSetProtectionSnapshotFileDocument
{
    [JsonRequired]
    public required string TrainingSamplesSha256 { get; init; }

    [JsonRequired]
    public required string ExportRegistrySha256 { get; init; }

    [JsonRequired]
    public int KnownImageHashes { get; init; }

    [JsonRequired]
    public required string KnownImageHashesSha256 { get; init; }

    [JsonRequired]
    public int KnownHoldingAliases { get; init; }

    [JsonRequired]
    public required string KnownHoldingAliasesSha256 { get; init; }

    [JsonRequired]
    public required string CandidateScopeSha256 { get; init; }

    [JsonRequired]
    public required string BaseModelSha256 { get; init; }
}

internal sealed class QueueManifestReceiptFileDocument
{
    [JsonRequired]
    public required string SchemaVersion { get; init; }

    [JsonRequired]
    public required string Purpose { get; init; }

    [JsonRequired]
    public required string QueueId { get; init; }

    [JsonRequired]
    public required string Pilot { get; init; }

    [JsonRequired]
    public required string Role { get; init; }

    [JsonRequired]
    public required string CreatedUtc { get; init; }

    [JsonRequired]
    public bool Frozen { get; init; }

    [JsonRequired]
    public required string DatasetStatus { get; init; }

    [JsonRequired]
    public required string Warning { get; init; }

    [JsonRequired]
    public required string ReviewTarget { get; init; }

    [JsonRequired]
    public int ClassMapVersion { get; init; }

    [JsonRequired]
    public required string ClassMapSha256 { get; init; }

    [JsonRequired]
    public required string VsaManifestHash { get; init; }

    [JsonRequired]
    public required IReadOnlyList<string> ClassNames { get; init; }

    [JsonRequired]
    public required IReadOnlyList<NegativeSetProtectedSetFileDocument> ProtectedSets { get; init; }

    [JsonRequired]
    public required NegativeSetProtectionSnapshotFileDocument ProtectionSnapshot { get; init; }

    [JsonRequired]
    public required JsonElement SelectionRule { get; init; }

    [JsonRequired]
    public required JsonElement Sources { get; init; }

    [JsonRequired]
    public int CandidatesCount { get; init; }

    [JsonRequired]
    public int ImagesCount { get; init; }

    [JsonRequired]
    public int HoldingsCount { get; init; }

    [JsonRequired]
    public required string HashAlgorithm { get; init; }

    [JsonRequired]
    public int HashesCount { get; init; }

    [JsonRequired]
    public required IReadOnlyDictionary<string, NegativeSetHashFileDocument> Hashes { get; init; }

    [JsonRequired]
    public required QueueSemanticReceiptFileDocument Semantic { get; init; }

    [JsonRequired]
    public required QueueSelectionReceiptFileDocument SelectionReceipt { get; init; }
}

internal sealed class QueueSemanticReceiptFileDocument
{
    [JsonRequired]
    public required string SchemaVersion { get; init; }

    [JsonRequired]
    public required string Purpose { get; init; }

    [JsonRequired]
    public required string Pilot { get; init; }

    [JsonRequired]
    public required string Role { get; init; }

    [JsonRequired]
    public int ClassMapVersion { get; init; }

    [JsonRequired]
    public required string ClassMapSha256 { get; init; }

    [JsonRequired]
    public required string VsaManifestHash { get; init; }

    [JsonRequired]
    public required IReadOnlyList<string> ClassNames { get; init; }

    [JsonRequired]
    public required IReadOnlyList<NegativeSetProtectedSetFileDocument> ProtectedSets { get; init; }

    [JsonRequired]
    public required NegativeSetProtectionSnapshotFileDocument ProtectionSnapshot { get; init; }

    [JsonRequired]
    public required IReadOnlyList<QueueModelReceiptFileDocument> ModelScope { get; init; }

    [JsonRequired]
    public required JsonElement SelectionRule { get; init; }

    [JsonRequired]
    public required JsonElement Sources { get; init; }

    [JsonRequired]
    public required IReadOnlyList<QueueItemReceiptFileDocument> Items { get; init; }
}

internal sealed class QueueSelectionReceiptFileDocument
{
    [JsonRequired]
    public required IReadOnlyList<QueueModelReceiptFileDocument> Models { get; init; }

    [JsonRequired]
    public required IReadOnlyList<QueueItemReceiptFileDocument> Items { get; init; }
}

internal sealed class QueueItemReceiptFileDocument
{
    [JsonRequired]
    public required string Id { get; init; }

    [JsonRequired]
    public required string ImageSha256 { get; init; }

    [JsonRequired]
    public required string HoldingKey { get; init; }

    [JsonRequired]
    public required string PhysicalHoldingKey { get; init; }

    [JsonRequired]
    public required string SourceRef { get; init; }

    [JsonRequired]
    public required string InspectionDate { get; init; }

    [JsonRequired]
    public long SizeBytes { get; init; }

    [JsonRequired]
    public required string ImageFormat { get; init; }

    [JsonRequired]
    public required IReadOnlyList<QueuePredictionReceiptFileDocument> Predictions { get; init; }
}

internal sealed class QueueModelReceiptFileDocument
{
    [JsonRequired]
    public required string CandidateId { get; init; }

    [JsonRequired]
    public required string CandidateManifestSha256 { get; init; }

    [JsonRequired]
    public required string WeightsSha256 { get; init; }

    [JsonRequired]
    public required string DatasetPlanId { get; init; }

    [JsonRequired]
    public required string DatasetManifestSha256 { get; init; }
}

internal sealed class QueuePredictionReceiptFileDocument
{
    [JsonRequired]
    public required string ModelId { get; init; }

    [JsonRequired]
    public bool PredictedBcc { get; init; }

    [JsonRequired]
    public int BccDetectionCount { get; init; }

    [JsonRequired]
    public double? MaxBccConfidence { get; init; }
}

internal sealed class QueueCandidateReceiptFileDocument
{
    [JsonRequired]
    public required string Id { get; init; }

    [JsonRequired]
    public required string FramePath { get; init; }

    [JsonRequired]
    public required string Category { get; init; }

    [JsonRequired]
    public required string Status { get; init; }

    [JsonRequired]
    public required string SourceSha256 { get; init; }
}

internal sealed class ReviewReceiptFileDocument
{
    [JsonRequired]
    public required string SchemaVersion { get; init; }

    [JsonRequired]
    public required string Purpose { get; init; }

    [JsonRequired]
    public required string QueueId { get; init; }

    [JsonRequired]
    public required string QueueManifestSha256 { get; init; }

    [JsonRequired]
    public required string CandidatesSha256 { get; init; }

    [JsonRequired]
    public required string ClassMapSha256 { get; init; }

    [JsonRequired]
    public required string Reviewer { get; init; }

    [JsonRequired]
    public required string UpdatedAtUtc { get; init; }

    [JsonRequired]
    public required IReadOnlyDictionary<string, ReviewDecisionReceiptFileDocument> Decisions { get; init; }
}

internal sealed class ReviewDecisionReceiptFileDocument
{
    [JsonRequired]
    public required string Decision { get; init; }

    [JsonRequired]
    public required string Comment { get; init; }

    [JsonRequired]
    public required string ReviewedAtUtc { get; init; }
}

internal sealed class ClassMapReceiptFileDocument
{
    [JsonRequired]
    public int Version { get; init; }

    [JsonRequired]
    public required string VsaManifestHash { get; init; }

    [JsonRequired]
    public required IReadOnlyDictionary<string, int> Classes { get; init; }
}

internal sealed class TrainingExportProtectedSetFileDocument
{
    [JsonRequired]
    public required string SetId { get; init; }

    [JsonRequired]
    public TrainingExportProtectedSetRole Role { get; init; }

    [JsonRequired]
    public required string RootPath { get; init; }

    [JsonRequired]
    public required string ManifestSha256 { get; init; }
}
