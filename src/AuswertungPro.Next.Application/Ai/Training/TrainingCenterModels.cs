using System;
using AuswertungPro.Next.Domain.Ai.Training;

namespace AuswertungPro.Next.Application.Ai.Training;

public enum TrainingCaseStatus
{
    New = 0,
    Approved = 1,
    Rejected = 2,
    /// <summary>Fall wurde durch Selbsttraining verarbeitet.</summary>
    SelfTrained = 3,
    /// <summary>Fall wurde durch Batch-Import + KB verarbeitet.</summary>
    BatchImported = 4
}

/// <summary>Ein Ergebnis-Eintrag im Selbsttraining-Verlauf.</summary>
public sealed class SelfTrainingEntryResult
{
    public int Index { get; init; }
    public string VsaCode { get; init; } = "";
    public double Meter { get; init; }
    public MatchLevel Level { get; init; }
    public string Summary { get; init; } = "";
}
