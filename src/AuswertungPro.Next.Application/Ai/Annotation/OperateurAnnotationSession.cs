using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.Application.Ai.Annotation;

/// <summary>
/// Zustandsmaschine fuer einen aktiven Annotations-Zustand pro CodeTask.
/// PreviewReady = Box + Maske vorhanden, aber noch nicht committed.
/// </summary>
public enum CodeTaskState
{
    Pending,
    Active,
    PreviewReady,
    Committed,
    Skipped,
    Rejected,
    Error
}

/// <summary>
/// Eine Annotations-Aufgabe pro Protokoll-Code. Lebt nur in der Session,
/// nicht persistiert. Dauerhafte Wahrheit ist TrainingSamplesStore.
/// </summary>
public sealed class CodeTask
{
    public string Code { get; init; } = "";
    public double Meterstand { get; init; }
    public CodeTaskState State { get; set; } = CodeTaskState.Pending;

    // PreviewReady-Daten
    public BoundingBoxNormalized? Box { get; set; }
    public MaskPreview? Preview { get; set; }
    public double? FrameDeltaSeconds { get; set; }

    // Committed-Daten
    public string? CommittedSampleId { get; set; }
    public DateTime? CommittedUtc { get; set; }

    // Skipped/Rejected-Daten
    public string? UserReason { get; set; }
}

/// <summary>
/// Aufgaben-Status pro TrainingMode-Sitzung. UI-thread-bound, lebt im
/// Code-Behind des PlayerWindow waehrend der Sitzung. Keine Persistenz.
/// </summary>
public sealed class OperateurAnnotationSession
{
    public string CaseId { get; init; } = "";
    public string VideoPath { get; init; } = "";
    public string PdfPath { get; init; } = "";
    public DateTime StartedUtc { get; init; } = DateTime.UtcNow;

    public IList<CodeTask> Tasks { get; } = new List<CodeTask>();
    public CodeTask? Active { get; set; }
}
