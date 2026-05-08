using System;

namespace AuswertungPro.Next.Domain.Ai.Training;

/// <summary>Status eines Trainings-/Export-Runs.</summary>
public enum TrainingRunStatus
{
    Running,
    Succeeded,
    Failed,
    Cancelled,
}

/// <summary>Trigger eines Runs — wofuer wurde er gestartet?</summary>
public static class TrainingRunTriggers
{
    public const string SelfTraining = "SelfTraining";
    public const string YoloRetrain = "YoloRetrain";
    public const string YoloDatasetExport = "YoloDatasetExport";
    public const string KbReindex = "KbReindex";
    public const string OperateurAnnotation = "OperateurAnnotation";
    public const string Manual = "Manual";
}

/// <summary>
/// Roadmap P1.3: Provenance fuer jeden Trainings-/Export-Run.
///
/// Voraussetzung fuer Regression-Detection — jedes <see cref="TrainingSample"/>
/// kann ueber <c>TrainingRunId</c> dem Run zugeordnet werden, der es erzeugt
/// oder bearbeitet hat. Damit laesst sich beim Modell-Regress nachvollziehen,
/// welche Run-Cohorte ggf. fehlerhaft ist.
/// </summary>
public sealed record TrainingRun(
    string RunId,
    DateTime StartedUtc,
    string Trigger,
    TrainingRunStatus Status,
    DateTime? FinishedUtc,
    int? SamplesAffected,
    string? ErrorMessage,
    string? Notes);
