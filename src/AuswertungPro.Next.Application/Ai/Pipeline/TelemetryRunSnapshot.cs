using System;

namespace AuswertungPro.Next.Application.Ai.Pipeline;

/// <summary>
/// Sprint 2 (2026-05-07): Persistierter Pipeline-Telemetry-Lauf
/// (gelesen aus pipeline_telemetry.db). Pure Record, kein I/O —
/// im Application-Layer angesiedelt damit auch UI-/Reporting-Code
/// auf SQLite-Snapshots zugreifen kann ohne Infrastructure-Referenz.
/// </summary>
public sealed record TelemetryRunSnapshot(
    long Id,
    DateTime TimestampUtc,
    string Label,
    int TotalFrames,
    int SkippedFrames,
    long WallClockMs,
    double ExtractionMeanMs,
    double ExtractionP95Ms,
    double YoloMeanMs,
    double YoloP95Ms,
    double DinoMeanMs,
    double DinoP95Ms,
    double SamMeanMs,
    double SamP95Ms,
    double QwenMeanMs,
    double QwenP95Ms,
    double TotalMeanMs,
    double TotalP95Ms);
