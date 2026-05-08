using System;
using System.Collections.Generic;
using System.Linq;

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

    /// <summary>
    /// Aktiviert eine Aufgabe. Vorheriges Active geht zurueck auf
    /// Pending (sofern es nicht ohnehin schon Committed/Skipped/Rejected ist),
    /// neues Active geht auf <see cref="CodeTaskState.Active"/>.
    /// </summary>
    public void MoveToCode(CodeTask task)
    {
        if (task is null) throw new ArgumentNullException(nameof(task));
        if (!Tasks.Contains(task))
            throw new ArgumentException("CodeTask gehoert nicht zu dieser Session.", nameof(task));

        if (Active is { } prev && prev != task && IsInProgress(prev.State))
        {
            // Active oder PreviewReady fallen zurueck auf Pending, wenn der
            // Operateur ohne Commit weiterspringt (kein Datenverlust, keine
            // Halb-Annotationen einfrieren).
            prev.State = CodeTaskState.Pending;
            prev.Box = null;
            prev.Preview = null;
            prev.FrameDeltaSeconds = null;
        }

        Active = task;
        if (IsInProgress(task.State) || task.State == CodeTaskState.Pending)
            task.State = CodeTaskState.Active;
    }

    /// <summary>Markiert das aktive Sample als committed; Active bleibt vorerst.</summary>
    public void MarkActiveCommitted(string sampleId, DateTime utcNow)
    {
        if (Active is null)
            throw new InvalidOperationException("Kein aktives CodeTask — MoveToCode zuerst.");
        EnsureNotTerminal(Active.State);
        if (string.IsNullOrWhiteSpace(sampleId))
            throw new ArgumentException("sampleId Pflicht.", nameof(sampleId));

        Active.CommittedSampleId = sampleId;
        Active.CommittedUtc = utcNow;
        Active.State = CodeTaskState.Committed;
    }

    /// <summary>Operateur uebergeht den Code (z.B. Frame unscharf). Active bleibt vorerst.</summary>
    public void MarkActiveSkipped(string reason)
    {
        if (Active is null)
            throw new InvalidOperationException("Kein aktives CodeTask — MoveToCode zuerst.");
        EnsureNotTerminal(Active.State);
        Active.UserReason = reason ?? "";
        Active.State = CodeTaskState.Skipped;
    }

    /// <summary>Operateur erkennt Protokollfehler / Code nicht zutreffend.</summary>
    public void MarkActiveRejected(string reason)
    {
        if (Active is null)
            throw new InvalidOperationException("Kein aktives CodeTask — MoveToCode zuerst.");
        EnsureNotTerminal(Active.State);
        Active.UserReason = reason ?? "";
        Active.State = CodeTaskState.Rejected;
    }

    /// <summary>True, wenn der State terminal ist (Committed/Skipped/Rejected).</summary>
    public static bool IsTerminal(CodeTaskState state)
        => state == CodeTaskState.Committed
        || state == CodeTaskState.Skipped
        || state == CodeTaskState.Rejected;

    private static void EnsureNotTerminal(CodeTaskState state)
    {
        if (IsTerminal(state))
            throw new InvalidOperationException(
                $"CodeTask ist bereits {state} — terminaler State darf nicht ueberschrieben werden.");
    }

    /// <summary>Naechstes <see cref="CodeTaskState.Pending"/>-Task nach Active, oder null.</summary>
    public CodeTask? FindNextPending()
    {
        var startIdx = Active is null ? -1 : Tasks.IndexOf(Active);
        for (int i = startIdx + 1; i < Tasks.Count; i++)
            if (Tasks[i].State == CodeTaskState.Pending)
                return Tasks[i];
        return null;
    }

    /// <summary>Erstes <see cref="CodeTaskState.Pending"/>-Task in der Liste, oder null.</summary>
    public CodeTask? FindFirstPending()
        => Tasks.FirstOrDefault(t => t.State == CodeTaskState.Pending);

    private static bool IsInProgress(CodeTaskState s)
        => s == CodeTaskState.Active || s == CodeTaskState.PreviewReady;
}
