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
        ApplyCompletion(viewModel, result);

        return new PipelineResultPresentation(BuildVisibleDetections(result.MappedEntries, rawDetections));
    }

    private static void ApplyCompletion(VideoAnalysisPipelineViewModel viewModel, PipelineResult result)
    {
        var warnings = (result.Warnings ?? Array.Empty<string>())
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Select(message => message.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (result.Incomplete)
            warnings.Insert(0, "Die Videoanalyse ist unvollständig. Fehlende Abschnitte und Befunde müssen geprüft werden.");

        viewModel.ResultWarningText = string.Join(Environment.NewLine, warnings);
        viewModel.PhaseLabel = result.Incomplete ? "Unvollständig"
            : warnings.Count > 0 ? "Fertig mit Hinweisen" : "Fertig";
        viewModel.StatusText = warnings.Count > 0
            ? $"{viewModel.PhaseLabel}. Bitte Hinweise und Befunde vor dem Übertragen prüfen."
            : "Fertig. Du kannst jetzt übertragen.";
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
                .Select(DetectionItem.FromMapped)
                .ToList();
        }

        return rawDetections
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
