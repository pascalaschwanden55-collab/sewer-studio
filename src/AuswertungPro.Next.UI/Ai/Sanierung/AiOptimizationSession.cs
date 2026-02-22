namespace AuswertungPro.Next.UI.Ai.Sanierung;

public sealed class AiOptimizationSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string HaltungId { get; set; } = "";
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string InputSnapshot { get; set; } = "";
    public string RuleSnapshot { get; set; } = "";
    public string AiResultSnapshot { get; set; } = "";
    public UserDecision Decision { get; set; } = UserDecision.Pending;
    public string? FinalAppliedMeasure { get; set; }
}

public enum UserDecision { Pending, Accepted, Rejected, Edited }
