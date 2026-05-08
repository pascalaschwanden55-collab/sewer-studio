using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

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
///
/// Implementiert <see cref="INotifyPropertyChanged"/>, damit die Operator-
/// Code-Liste in der UI direkt auf State-Wechsel reagiert (DataTrigger
/// auf <see cref="State"/>, ohne dass das Code-Behind <c>Items.Refresh()</c>
/// rufen muss). CodeTask lebt in Application — kein Domain-Layer-Bruch
/// (ADR-0004 betrifft nur Domain-Modelle wie HaltungRecord/SchachtRecord).
/// </summary>
public sealed class CodeTask : INotifyPropertyChanged
{
    public string Code { get; init; } = "";
    public double Meterstand { get; init; }

    private CodeTaskState _state = CodeTaskState.Pending;
    public CodeTaskState State
    {
        get => _state;
        set => SetProperty(ref _state, value);
    }

    // PreviewReady-Daten
    private BoundingBoxNormalized? _box;
    public BoundingBoxNormalized? Box
    {
        get => _box;
        set => SetProperty(ref _box, value);
    }

    private MaskPreview? _preview;
    public MaskPreview? Preview
    {
        get => _preview;
        set => SetProperty(ref _preview, value);
    }

    private double? _frameDeltaSeconds;
    public double? FrameDeltaSeconds
    {
        get => _frameDeltaSeconds;
        set => SetProperty(ref _frameDeltaSeconds, value);
    }

    // Committed-Daten
    private string? _committedSampleId;
    public string? CommittedSampleId
    {
        get => _committedSampleId;
        set => SetProperty(ref _committedSampleId, value);
    }

    private DateTime? _committedUtc;
    public DateTime? CommittedUtc
    {
        get => _committedUtc;
        set => SetProperty(ref _committedUtc, value);
    }

    // Skipped/Rejected-Daten
    private string? _userReason;
    public string? UserReason
    {
        get => _userReason;
        set => SetProperty(ref _userReason, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
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
