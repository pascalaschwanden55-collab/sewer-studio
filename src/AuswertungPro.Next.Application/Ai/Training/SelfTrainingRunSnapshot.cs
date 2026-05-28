namespace AuswertungPro.Next.Application.Ai.Training;

/// <summary>Snapshot eines einzelnen Self-Training-Laufs.</summary>
public sealed record SelfTrainingRunSnapshot(
    DateTime TimestampUtc,
    string CaseId,
    int TotalEntries,
    double ExactPercent,
    double PartialPercent,
    double MismatchPercent,
    double NoFindingsPercent);
