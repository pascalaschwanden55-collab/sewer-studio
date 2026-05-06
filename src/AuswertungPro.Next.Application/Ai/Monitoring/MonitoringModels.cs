using System;

namespace AuswertungPro.Next.Application.Ai.Monitoring;

/// <summary>
/// Phase 5.3 vorbereitend: Pure DTOs der Monitoring-Schicht
/// (Accuracy, Drift, Histogram, ModelVersion).
/// Vorher in <c>UI.Ai.Monitoring/*.cs</c> verstreut.
/// </summary>
public sealed record CodeAccuracyMetric(
    string VsaCode,
    int TruePositives,
    int FalsePositives,
    int FalseNegatives,
    double Precision,
    double Recall,
    double F1Score,
    int TotalSamples
);

public sealed record OverallAccuracy(int Total, int Correct, double Accuracy);

public sealed record DriftCheckResult(
    double KlDivergence,
    bool IsDrifting,
    int ThisWeekCount,
    int LastWeekCount,
    double ThisWeekMean,
    double LastWeekMean
);

public sealed record HistogramBin(
    double BinLower,
    double BinUpper,
    int Count,
    double Fraction
);

public sealed record ModelVersionInfo(
    string VersionId,
    string ModelVersion,
    double EceBefore,
    double EceAfter,
    string Notes,
    DateTime CreatedUtc
);
