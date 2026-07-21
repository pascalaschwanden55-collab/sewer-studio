using System.Text.Json.Serialization;

namespace AuswertungPro.Next.Application.Ai.Training.Inventory;

public sealed class TeacherInventoryRecord
{
    [JsonRequired] public required string RecordKey { get; init; }
    [JsonRequired] public required string VsaCode { get; init; }
    [JsonRequired] public TrainingInventoryPathReference FullFrame { get; init; } = new();
    [JsonRequired] public TrainingInventoryPathReference CroppedRegion { get; init; } = new();
    [JsonRequired] public TrainingInventoryPathReference YoloAnnotation { get; init; } = new();
    [JsonRequired] public TrainingInventoryPathReference Video { get; init; } = new();
    [JsonRequired] public TrainingInventoryBoxState BoxState { get; init; }
    [JsonIgnore] public bool HasPositiveArea => BoxState != TrainingInventoryBoxState.MissingOrNonPositiveArea;
    [JsonRequired] public TrainingInventoryHoldingState HoldingState { get; init; }
    public string? SuggestedHolding { get; init; }
    [JsonRequired] public IReadOnlyList<string> HoldingCandidates { get; init; } = Array.Empty<string>();
    [JsonRequired] public TrainingInventoryDisposition Disposition { get; init; }
    [JsonRequired] public TrainingInventoryEvalState EvalState { get; init; }
    [JsonRequired] public IReadOnlyList<string> ReasonCodes { get; init; } = Array.Empty<string>();
}

public sealed class TrainingInventoryPathReference
{
    public string? StoredPath { get; init; }
    [JsonRequired] public TrainingInventoryPathState State { get; init; }
    [JsonIgnore] public bool Exists => State == TrainingInventoryPathState.Existing;
    [JsonRequired] public bool IsProtected { get; init; }
    [JsonRequired] public TrainingInventoryHashState HashState { get; init; } = TrainingInventoryHashState.NotApplicable;
    public string? ExistingPath { get; init; }
    public string? SuggestedPath { get; init; }
    [JsonRequired] public IReadOnlyList<string> Candidates { get; init; } = Array.Empty<string>();
    public string? Sha256 { get; init; }
    public string? Error { get; init; }
}
