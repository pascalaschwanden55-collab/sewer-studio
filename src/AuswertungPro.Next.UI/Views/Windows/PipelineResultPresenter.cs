using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Uebertraegt ein erfolgreiches Pipeline-Ergebnis in die Abschlussanzeige.
/// Fenster-Lifecycle, Fehlerbehandlung, ObservableCollection und Radarzeichnung
/// bleiben beim aufrufenden Fenster.
/// </summary>
internal static class PipelineResultPresenter
{
    private const int MaxVisibleDetections = 250;

    internal static PipelineResultPresentation ApplySuccessful(
        VideoAnalysisPipelineViewModel viewModel,
        PipelineResult result)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(result);

        if (!result.IsSuccess)
            throw new ArgumentException("Nur erfolgreiche Pipeline-Ergebnisse koennen dargestellt werden.", nameof(result));

        var rawDetections = result.Detections ?? Array.Empty<RawVideoDetection>();
        ApplyStatistics(viewModel, result.Stats, rawDetections);
        viewModel.TelemetryText = PipelineTelemetryFormatter.Format(result.Telemetry);

        return new PipelineResultPresentation(BuildVisibleDetections(result.MappedEntries, rawDetections));
    }

    private static void ApplyStatistics(
        VideoAnalysisPipelineViewModel viewModel,
        PipelineStats? stats,
        IReadOnlyList<RawVideoDetection> rawDetections)
    {
        viewModel.FramesAnalyzed = stats?.FramesAnalyzed ?? 0;
        viewModel.DetectionCount = rawDetections.Count;
        viewModel.HighConfidenceCount = stats?.EntriesWithHighConfidence ?? 0;

        viewModel.PillarDetectionCount = rawDetections.Count;
        viewModel.PillarQuantCount = rawDetections.Count(HasQuantification);
        viewModel.PillarLocalCount = rawDetections.Count(
            detection => !string.IsNullOrWhiteSpace(detection.PositionClock));

        viewModel.StatsText = stats is null
            ? string.Empty
            : $"Frames: {stats.FramesAnalyzed}, Detections: {stats.DetectionsRaw}, "
                + $"Entries: {stats.EntriesGenerated}, HighConf: {stats.EntriesWithHighConfidence}";
    }

    private static IReadOnlyList<DetectionItem> BuildVisibleDetections(
        IReadOnlyList<MappedProtocolEntry>? mappedEntries,
        IReadOnlyList<RawVideoDetection> rawDetections)
    {
        if (mappedEntries is { Count: > 0 })
        {
            return mappedEntries
                .Take(MaxVisibleDetections)
                .Select(DetectionItem.FromMapped)
                .ToList();
        }

        return rawDetections
            .Take(MaxVisibleDetections)
            .Select(DetectionItem.From)
            .ToList();
    }

    private static bool HasQuantification(RawVideoDetection detection)
        => detection.HeightMm.HasValue
            || detection.WidthMm.HasValue
            || detection.IntrusionPercent.HasValue
            || detection.CrossSectionReductionPercent.HasValue
            || detection.DiameterReductionMm.HasValue
            || detection.ExtentPercent.HasValue;
}

internal readonly record struct PipelineResultPresentation(
    IReadOnlyList<DetectionItem> VisibleDetections);
