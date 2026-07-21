using System.Text.Json.Serialization;

namespace AuswertungPro.Next.Application.Ai.Training.Inventory;

public sealed class TrainingInventoryIssue
{
    [JsonRequired] public TrainingInventoryIssueSeverity Severity { get; init; }
    [JsonRequired] public required string Code { get; init; }
    [JsonRequired] public required string Message { get; init; }
    public string? Path { get; init; }
    public string? RecordKey { get; init; }
}
