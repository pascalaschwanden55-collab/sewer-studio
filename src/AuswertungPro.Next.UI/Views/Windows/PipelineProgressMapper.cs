using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Views.Windows;

/// <summary>
/// Uebertraegt eine Pipeline-Fortschrittsmeldung in den sichtbaren Fensterzustand.
/// WPF-Steuerelemente und Fenster-Lifecycle bleiben beim aufrufenden Fenster.
/// Eine Instanz gehoert genau zu einem Analyselauf, damit die ETA nicht zwischen
/// zwei Laeufen vermischt wird.
/// </summary>
internal sealed class PipelineProgressMapper
{
    private const int MaxVisibleFindings = 8;

    private readonly VideoAnalysisPipelineViewModel _viewModel;
    private readonly List<LiveFrameFinding> _visibleFindings;
    private readonly Func<byte[], ImageSource> _previewDecoder;
    private readonly IEtaCalculator _etaCalculator;
    private readonly Func<TimeSpan> _elapsedProvider;

    internal PipelineProgressMapper(
        VideoAnalysisPipelineViewModel viewModel,
        List<LiveFrameFinding> visibleFindings,
        Func<byte[], ImageSource> previewDecoder)
        : this(
            viewModel,
            visibleFindings,
            previewDecoder,
            new EtaCalculator(),
            StartElapsedClock())
    {
    }

    internal PipelineProgressMapper(
        VideoAnalysisPipelineViewModel viewModel,
        List<LiveFrameFinding> visibleFindings,
        Func<byte[], ImageSource> previewDecoder,
        IEtaCalculator etaCalculator,
        Func<TimeSpan> elapsedProvider)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(visibleFindings);
        ArgumentNullException.ThrowIfNull(previewDecoder);
        ArgumentNullException.ThrowIfNull(etaCalculator);
        ArgumentNullException.ThrowIfNull(elapsedProvider);

        _viewModel = viewModel;
        _visibleFindings = visibleFindings;
        _previewDecoder = previewDecoder;
        _etaCalculator = etaCalculator;
        _elapsedProvider = elapsedProvider;
    }

    internal PipelineProgressEffects Apply(PipelineProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);

        var isVideoPhase = IsVideoPhase(progress.Phase);
        ApplyCommonState(progress, isVideoPhase);
        if (isVideoPhase)
            return ApplyVideoState(progress);

        if (progress.Phase == PipelinePhase.CodeMapping)
            ApplyMappingState(progress);
        else if (progress.Phase == PipelinePhase.Done)
            ApplyDoneState();

        return default;
    }

    private void ApplyCommonState(PipelineProgress progress, bool isVideoPhase)
    {
        _viewModel.StatusText = progress.Status;
        _viewModel.PhaseLabel = progress.Phase switch
        {
            PipelinePhase.VideoAnalysis => "Videoanalyse",
            PipelinePhase.MultiModelDetection => "Multi-Model Pipeline",
            PipelinePhase.CodeMapping => "Code-Mapping",
            PipelinePhase.Done => "Fertig",
            _ => progress.Phase.ToString()
        };

        _viewModel.VideoPhaseActive = isVideoPhase;
        _viewModel.VideoPhaseDone = !isVideoPhase;
        _viewModel.MappingPhaseDone = progress.Phase == PipelinePhase.Done;
        _viewModel.IsMultiModelActive = progress.Phase == PipelinePhase.MultiModelDetection;

        if (progress.Phase != PipelinePhase.MultiModelDetection)
            return;

        var totalFrames = PipelineStatusParser.TryExtractYoloTotalFrames(progress.Status);
        if (totalFrames.HasValue)
            _viewModel.YoloSkippedFrames = totalFrames.Value;
    }

    private PipelineProgressEffects ApplyVideoState(PipelineProgress progress)
    {
        _viewModel.VideoProgressPct = Math.Clamp(progress.PercentInPhase, 0, 100);
        _viewModel.MappingProgressPct = 0;

        if (progress.FramesDone.HasValue)
            _viewModel.FramesAnalyzed = Math.Max(0, progress.FramesDone.Value);
        if (progress.FramesTotal.HasValue)
            _viewModel.TotalFrames = Math.Max(0, progress.FramesTotal.Value);

        UpdateEta();
        UpdateStatusValues(progress.Status);

        _viewModel.LiveFrameStatus = progress.Status;
        UpdateLiveFrameInfo();

        var renderOverlay = ApplyLiveFrame(progress);
        return new PipelineProgressEffects(renderOverlay, ForwardLiveFrame: true);
    }

    private void UpdateEta()
    {
        if (_viewModel.TotalFrames <= 0)
            return;

        var estimate = _etaCalculator.MeldeFortschritt(
            _viewModel.FramesAnalyzed,
            _viewModel.TotalFrames,
            _elapsedProvider());
        _viewModel.EtaText = EtaAnzeigeFormatter.Format(estimate);
    }

    private void UpdateStatusValues(string status)
    {
        var meter = PipelineStatusParser.TryExtractMeter(status);
        if (!string.IsNullOrWhiteSpace(meter))
            _viewModel.CurrentMeter = meter;

        var findingCount = PipelineStatusParser.TryExtractFindingCount(status);
        if (findingCount.HasValue)
            _viewModel.DetectionCount = Math.Max(_viewModel.DetectionCount, findingCount.Value);
    }

    private bool ApplyLiveFrame(PipelineProgress progress)
    {
        var changed = false;
        if (progress.FramePreviewPng is { Length: > 0 })
        {
            _viewModel.LiveFrameImage = _previewDecoder(progress.FramePreviewPng);
            changed = true;
        }

        if (progress.LiveFindings is null)
            return changed;

        _visibleFindings.Clear();
        _visibleFindings.AddRange(progress.LiveFindings.Take(MaxVisibleFindings));
        _viewModel.LiveFrameQuantSummary = LiveFindingSummaryBuilder.BuildQuantSummary(_visibleFindings);

        _viewModel.PillarDetectionCount = Math.Max(
            _viewModel.PillarDetectionCount,
            _visibleFindings.Count);
        _viewModel.PillarQuantCount = Math.Max(
            _viewModel.PillarQuantCount,
            _visibleFindings.Count(HasQuantification));
        _viewModel.PillarLocalCount = Math.Max(
            _viewModel.PillarLocalCount,
            _visibleFindings.Count(finding => !string.IsNullOrWhiteSpace(finding.PositionClock)));

        return true;
    }

    private void ApplyMappingState(PipelineProgress progress)
    {
        _viewModel.VideoProgressPct = 100;
        _viewModel.MappingProgressPct = Math.Clamp(progress.PercentInPhase, 0, 100);

        if (progress.ItemsDone.HasValue)
            _viewModel.DetectionCount = Math.Max(_viewModel.DetectionCount, progress.ItemsDone.Value);

        _viewModel.LiveFrameStatus = progress.Status;
        UpdateLiveFrameInfo();
    }

    private void ApplyDoneState()
    {
        _viewModel.VideoProgressPct = 100;
        _viewModel.MappingProgressPct = 100;
        _viewModel.LiveFrameStatus = "Analyse abgeschlossen";
        UpdateLiveFrameInfo();
        _viewModel.LiveFrameQuantSummary = LiveFindingSummaryBuilder.BuildQuantSummary(_visibleFindings);
    }

    private void UpdateLiveFrameInfo()
    {
        _viewModel.LiveFrameInfo = LiveFindingSummaryBuilder.BuildFrameInfo(
            _viewModel.FramesAnalyzed,
            _viewModel.TotalFrames,
            _viewModel.CurrentMeter);
    }

    private static bool HasQuantification(LiveFrameFinding finding)
        => finding.HeightMm.HasValue
            || finding.WidthMm.HasValue
            || finding.IntrusionPercent.HasValue
            || finding.CrossSectionReductionPercent.HasValue
            || finding.DiameterReductionMm.HasValue
            || finding.ExtentPercent.HasValue;

    private static bool IsVideoPhase(PipelinePhase phase)
        => phase is PipelinePhase.VideoAnalysis or PipelinePhase.MultiModelDetection;

    private static Func<TimeSpan> StartElapsedClock()
    {
        var clock = Stopwatch.StartNew();
        return () => clock.Elapsed;
    }
}

internal readonly record struct PipelineProgressEffects(
    bool RenderLiveFrameOverlay,
    bool ForwardLiveFrame);
