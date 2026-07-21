using System.Text.Json.Serialization;

namespace AuswertungPro.Next.Application.Ai.Training.Inventory;

public sealed class TrainingInventorySourceDocument
{
    [JsonRequired] public required string Path { get; init; }
    [JsonRequired] public TrainingInventoryDataKind DataKind { get; init; }
    [JsonRequired] public TrainingInventorySourceRole Role { get; init; }
    public long? Bytes { get; init; }
    public DateTimeOffset? LastWriteUtc { get; init; }
    public string? Sha256 { get; init; }
    [JsonRequired] public TrainingInventoryParseState ParseState { get; init; }
    [JsonRequired] public TrainingInventoryValidationLevel ValidationLevel { get; init; }
    public int? RecordCount { get; init; }
    public string? Error { get; init; }
}
