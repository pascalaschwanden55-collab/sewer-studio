using System;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.Ai.Training;

/// <summary>
/// Steuert das autonome Training fuer einen Trainingsfall.
/// </summary>
public interface ISelfTrainingOrchestrator
{
    Task<SelfTrainingResult> RunAsync(
        TrainingCaseInput tc,
        IProgress<SelfTrainingStep> progress,
        CancellationToken ct);

    void Pause();

    void Resume();

    bool IsPaused { get; }
}
