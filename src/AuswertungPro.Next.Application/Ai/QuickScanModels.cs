using System.Collections.Generic;

namespace AuswertungPro.Next.Application.Ai;

// Reine Ergebnis-/Fortschritts-DTOs des KI-Schnellscans. Liegen bewusst in Application
// (Vertrags-/DTO-Schicht), damit der IQuickScanService-Vertrag sie fuehren kann; die
// Implementierung QuickScanService in Infrastructure fuellt sie.

public sealed record QuickScanSegment(
    double TimestampSeconds,
    bool HasDamage,
    int Severity,
    string? Label,
    string? Clock);

public sealed record QuickScanProgress(
    int FramesDone,
    int FramesTotal,
    string Status,
    QuickScanSegment? LatestSegment);

public sealed record QuickScanResult(
    IReadOnlyList<QuickScanSegment> Segments,
    double VideoDurationSeconds,
    int FramesAnalyzed,
    string? Error);
