using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.Application.Maintenance;

/// <summary>Zusammenfassung eines MaintenanceScheduler-Laufs.</summary>
public sealed record MaintenanceRunResult(
    bool RanFrameCleanup,
    bool RanVersionsPrune,
    int FramesDeleted,
    long FramesDeletedBytes,
    int VersionsPruned,
    DateTime StartedUtc,
    DateTime FinishedUtc,
    IReadOnlyList<string> Errors)
{
    public TimeSpan Duration => FinishedUtc - StartedUtc;
    public bool HasErrors => Errors.Count > 0;
}
