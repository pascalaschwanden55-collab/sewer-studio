namespace AuswertungPro.Next.Domain.Models;

public enum ConsistencyWarningSeverity
{
    Info,
    Warning,
    Error
}

public sealed record ConsistencyWarning
{
    public required string RuleId { get; init; }
    public required ConsistencyWarningSeverity Severity { get; init; }
    public required string Message { get; init; }
    public string? MeasureId { get; init; }
    public string? ItemKey { get; init; }
    public string? SuggestedFix { get; init; }
}
