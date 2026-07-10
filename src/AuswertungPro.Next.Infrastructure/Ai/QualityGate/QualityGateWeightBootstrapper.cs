using System;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

namespace AuswertungPro.Next.Infrastructure.Ai.QualityGate;

public sealed record QualityGateWeightActivationResult(
    bool Success,
    int CategoryCount,
    string Version,
    string? Error)
{
    public static QualityGateWeightActivationResult Failed(Exception ex) =>
        new(false, 0, QualityGateWeightSnapshot.DefaultVersion, ex.Message);
}

/// <summary>
/// Loads persisted category weights once during application startup and activates them
/// for every current and future <see cref="QualityGateService"/> instance.
/// </summary>
public static class QualityGateWeightBootstrapper
{
    public static QualityGateWeightActivationResult LoadAndActivate(string? databasePath = null)
    {
        try
        {
            using var db = string.IsNullOrWhiteSpace(databasePath)
                ? new KnowledgeBaseContext()
                : new KnowledgeBaseContext(databasePath);
            var learner = new WeightLearningService(db.Connection);
            var snapshot = learner.LoadSnapshot();
            QualityGateService.ConfigureProcessWeights(snapshot.Weights, snapshot.Version);

            return new QualityGateWeightActivationResult(
                true,
                snapshot.Weights.Count,
                snapshot.Version,
                null);
        }
        catch (Exception ex)
        {
            // Deterministic fallback: clear any stale process snapshot and use defaults.
            QualityGateService.ConfigureProcessWeights(
                Array.Empty<AuswertungPro.Next.Application.Ai.QualityGate.CategoryWeights>(),
                QualityGateWeightSnapshot.DefaultVersion);
            return QualityGateWeightActivationResult.Failed(ex);
        }
    }
}
