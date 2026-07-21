using System.Text.Json.Serialization;

namespace AuswertungPro.Next.Application.Ai.Training.Inventory;

public static class TrainingDataInventoryReportSchema
{
    public const string CurrentSchemaVersion = "2.2";
    public const string CurrentScannerVersion = "ap0.1-v5";
}

public sealed class TrainingDataInventoryReport
{
    [JsonRequired] public string SchemaVersion { get; init; } = TrainingDataInventoryReportSchema.CurrentSchemaVersion;
    [JsonRequired] public string ScannerVersion { get; init; } = TrainingDataInventoryReportSchema.CurrentScannerVersion;
    [JsonRequired] public string RunId { get; init; } = Guid.NewGuid().ToString("N");
    [JsonRequired] public DateTimeOffset GeneratedUtc { get; init; }
    [JsonRequired] public bool ReadOnly { get; init; } = true;
    [JsonRequired] public TrainingInventoryEvalProtectionStatus EvalProtection { get; init; } = new();
    [JsonRequired] public required string KnowledgeRoot { get; init; }
    public string? EvalSetRoot { get; init; }
    [JsonRequired] public IReadOnlyList<string> SearchRoots { get; init; } = Array.Empty<string>();
    [JsonRequired] public IReadOnlyList<string> ProtectedRoots { get; init; } = Array.Empty<string>();
    [JsonRequired] public TrainingDataInventorySummary Summary { get; init; } = new();
    [JsonRequired] public IReadOnlyList<TrainingInventorySourceDocument> Sources { get; init; } = Array.Empty<TrainingInventorySourceDocument>();
    [JsonRequired] public IReadOnlyList<TeacherInventoryRecord> TeacherRecords { get; init; } = Array.Empty<TeacherInventoryRecord>();
    [JsonRequired] public IReadOnlyList<TrainingInventoryIssue> Issues { get; init; } = Array.Empty<TrainingInventoryIssue>();
    [JsonRequired] public IReadOnlyList<string> SkippedDirectories { get; init; } = Array.Empty<string>();
}

public sealed class TrainingInventoryEvalProtectionStatus
{
    [JsonRequired] public bool ImageHashCheckEnabled { get; init; }
    [JsonRequired] public IReadOnlyList<TrainingInventoryEvalSetStatus> Sets { get; init; } = Array.Empty<TrainingInventoryEvalSetStatus>();
    [JsonRequired] public IReadOnlyList<string> DiscoveryErrors { get; init; } = Array.Empty<string>();
    [JsonIgnore] public bool SetsFound => Sets.Count > 0;
    [JsonIgnore] public bool DiscoveryComplete => DiscoveryErrors.Count == 0;
    [JsonIgnore] public bool ImageHashesAvailable => ImageHashCheckEnabled
                                        && SetsFound
                                        && DiscoveryComplete
                                        && Sets.All(set => set.ImageHashesComplete);
    [JsonIgnore] public bool HoldingKeysAvailable => SetsFound
                                        && DiscoveryComplete
                                        && Sets.All(set => set.HoldingKeysComplete);
    [JsonIgnore] public bool Complete => ImageHashesAvailable && HoldingKeysAvailable;
}

public sealed class TrainingInventoryEvalSetStatus
{
    [JsonRequired] public required string RootPath { get; init; }
    [JsonRequired] public int ImageFiles { get; init; }
    [JsonRequired] public int ManifestImageHashes { get; init; }
    [JsonRequired] public int VerifiedImageHashes { get; init; }
    [JsonRequired] public int HoldingKeys { get; init; }
    [JsonRequired] public bool ImageHashesComplete { get; init; }
    [JsonRequired] public bool HoldingKeysComplete { get; init; }
    [JsonIgnore] public bool Complete => ImageHashesComplete && HoldingKeysComplete && Errors.Count == 0;
    [JsonRequired] public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
}

public sealed class TrainingDataInventorySummary
{
    [JsonRequired] public TrainingInventoryDataFactsSummary Data { get; init; } = new();
    [JsonRequired] public TrainingInventoryHoldingSummary Holdings { get; init; } = new();
    [JsonRequired] public TrainingInventoryTriageSummary Triage { get; init; } = new();
    [JsonRequired] public TrainingInventoryPathSummary Paths { get; init; } = new();
    [JsonRequired] public TrainingInventoryEvalSummary Evaluation { get; init; } = new();
    [JsonRequired] public TrainingInventorySourceSummary Sources { get; init; } = new();
}

public sealed class TrainingInventoryDataFactsSummary
{
    [JsonRequired] public int TeacherRecords { get; init; }
    [JsonRequired] public int ExistingFullFrames { get; init; }
    [JsonRequired] public int PositiveAreaBoxes { get; init; }
    [JsonRequired] public int StrictlyValidBoxes { get; init; }
    [JsonRequired] public int ExistingFrameAndPositiveArea { get; init; }
    [JsonRequired] public int ExistingFrameAndStrictlyValidBox { get; init; }
}

public sealed class TrainingInventoryHoldingSummary
{
    [JsonRequired] public int Explicit { get; init; }
    [JsonRequired] public int NonExplicit { get; init; }
    [JsonRequired] public int ExistingFramePositiveAreaExplicit { get; init; }
}

public sealed class TrainingInventoryTriageSummary
{
    [JsonRequired] public int TrainValCandidates { get; init; }
    [JsonRequired] public int QuarantineOrigin { get; init; }
    [JsonRequired] public int QuarantineGeometry { get; init; }
    [JsonRequired] public int Archive { get; init; }
    [JsonRequired] public int EvaluationLocked { get; init; }
    [JsonRequired] public int EvaluationNotChecked { get; init; }
    [JsonIgnore] public int Total => TrainValCandidates
                        + QuarantineOrigin
                        + QuarantineGeometry
                        + Archive
                        + EvaluationLocked
                        + EvaluationNotChecked;
}

public sealed class TrainingInventoryPathSummary
{
    [JsonRequired] public int FullFrameSuggestions { get; init; }
    [JsonRequired] public int AmbiguousFullFrameReferences { get; init; }
    [JsonRequired] public int ReadErrors { get; init; }
}

public sealed class TrainingInventoryEvalSummary
{
    [JsonRequired] public int ReservedRecords { get; init; }
    [JsonRequired] public int UncheckedRecords { get; init; }
}

public sealed class TrainingInventorySourceSummary
{
    [JsonRequired] public int Documents { get; init; }
    [JsonRequired] public int InvalidDocuments { get; init; }
}
