using System;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Training.Models;

namespace AuswertungPro.Next.Application.Ai.Training;

/// <summary>
/// Audit 2026-04-23 ARCH-H2: Interface fuer den Batch-Selbsttraining-Orchestrator.
/// Erlaubt Mock-basierte Tests fuer ViewModels die den Batch-Lauf steuern, und
/// dokumentiert die gemeinsame Lifecycle-API (Run + Pause/Resume) der Trainings-
/// Pipelines.
///
/// Verwandte Interfaces in der Trainings-Familie (ARCH-H2):
/// - <see cref="ISelfTrainingOrchestrator"/> — Self-Training pro Fall
/// - <c>IBatchSelfTrainingOrchestrator</c> (dieses) — Batch ueber Ordner-Tree
/// - <c>IInitialTrainingOrchestrator</c> — Erstaufbau der KB
/// </summary>
public interface IBatchSelfTrainingOrchestrator
{
    /// <summary>
    /// Startet den Batch-Durchlauf (scannt Ordner-Tree, verarbeitet nacheinander).
    /// </summary>
    Task<BatchSelfTrainingResult> RunAsync(
        BatchSelfTrainingRequest request,
        IProgress<BatchSelfTrainingProgress>? progress = null,
        CancellationToken ct = default);

    /// <summary>Pausiert den laufenden Batch-Lauf nach der aktuellen Haltung.</summary>
    void Pause();

    /// <summary>Setzt nach Pause fort.</summary>
    void Resume();

    /// <summary>True wenn gerade pausiert.</summary>
    bool IsPaused { get; }
}
