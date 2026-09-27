using System.Diagnostics;
using System.Globalization;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Application.UseCases.CodingReplay;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.Infrastructure.Ai.QualityGate;
using AuswertungPro.Next.UI.Ai.Coding;

namespace CodingReplay;

/// <summary>
/// Isolierter Host fuer bestehende Player-Workflows. Einzelbilder beginnen ohne
/// Verlauf. Ein explizit gestarteter Videonachlauf behaelt genau eine Sitzung.
/// </summary>
public sealed class ReplayCodingAnalyzer(
    ReplayVisionClient vision, SingleFrameMultiModelService multiModel,
    CodingOsdMeterService osd, IVsaCodeSelectionCatalog catalog) : ICodingReplayAnalyzer
{
    public ReplayVideoAnalysisSession StartVideoSession(ReplayVideoAnalysisContext context)
        => new(this, context);

    public ReplayVideoAnalysisSession StartCandidateVideoSession(
        ReplayVideoAnalysisContext context,
        ReplayCandidateDetectionSet candidateDetections)
    {
        ArgumentNullException.ThrowIfNull(candidateDetections);
        return new(this, context, candidateDetections);
    }

    public async Task<CodingReplayObservation> AnalyzeAsync(
        CodingReplayFrame frame,
        byte[] image,
        CancellationToken ct)
    {
        var state = ReplayCodingState.CreateSingle(frame);
        var videoFrame = new ReplayVideoFrame(frame.Id, "", frame.ImageSha256, frame.TimestampSeconds);
        var result = await AnalyzeFrameCoreAsync(state, videoFrame, image, persistentVideo: false, ct);
        return new CodingReplayObservation(
            result.Outcome,
            result.Events.Select(e => new CodingReplayEvent(e.Code, e.MeterStart, e.QualityGate)).ToArray(),
            result.Trace,
            result.TechnicalError);
    }

    internal Task<ReplayVideoFrameObservation> AnalyzeVideoFrameAsync(
        ReplayCodingState state,
        ReplayVideoFrame frame,
        byte[] image,
        CancellationToken ct)
        => AnalyzeFrameCoreAsync(state, frame, image, persistentVideo: true, ct);

    private async Task<ReplayVideoFrameObservation> AnalyzeFrameCoreAsync(
        ReplayCodingState state,
        ReplayVideoFrame frame,
        byte[] image,
        bool persistentVideo,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(image);
        ct.ThrowIfCancellationRequested();

        vision.Reset();
        var trace = vision.Trace;
        var before = state.SnapshotEvents().ToDictionary(e => e.EventId);
        var time = TimeSpan.FromSeconds(frame.TimestampSeconds);
        state.CurrentVideoTime = time;
        trace["session"] = persistentVideo
            ? $"persistent_video_session:{state.Session.ActiveSession?.SessionId}"
            : "fresh_per_frame_without_imported_findings";

        if (persistentVideo && CodingDedupPolicy.ShouldStopAnalysisAfterTerminalCode(
                CodingTerminalBoundaryCandidateBuilder.Enumerate(
                    state.Session.Events,
                    state.Session.Events,
                    []),
                state.CurrentMeter,
                time))
        {
            trace["terminal_stop"] = "true";
            return Result(
                frame,
                "StoppedAtTerminalBoundary",
                state.CurrentMeter,
                "session_meter_at_terminal",
                state,
                before,
                trace,
                null);
        }

        var previousOsdMeter = state.LastAcceptedOsdMeter;
        var previousOsdTime = state.LastAcceptedOsdTimestampSeconds;
        var osdWatch = Stopwatch.StartNew();
        var meterRead = await osd.ReadMeterAsync(
            image,
            frame.TimestampSeconds,
            previousOsdMeter,
            previousOsdTime,
            ct);
        ct.ThrowIfCancellationRequested();
        trace["osd_elapsed_ms"] = osdWatch.Elapsed.TotalMilliseconds.ToString("F1", CultureInfo.InvariantCulture);
        trace["osd_raw_reply"] = meterRead.RawReply ?? "";
        trace["osd_candidate"] = meterRead.Candidate?.ToString(CultureInfo.InvariantCulture) ?? "";
        if (!string.IsNullOrWhiteSpace(meterRead.Error))
            trace["osd_error"] = meterRead.Error;

        double meter;
        bool meterFromOsd;
        string meterSource;
        if (!persistentVideo)
        {
            if (meterRead.Meter is null)
            {
                trace["osd"] = meterRead.Error
                    ?? (meterRead.RawReply is null
                        ? "keine Antwort im Lesebudget"
                        : "Antwort ohne eindeutigen Meterwert");
                return Result(
                    frame,
                    "MeterUnavailable",
                    0,
                    "unavailable",
                    state,
                    before,
                    trace,
                    "Kein gelesener Meterstand; im Einzelbild fehlt eine unabhaengige Timeline-Schaetzung.");
            }
            meter = meterRead.Meter.Value;
            meterFromOsd = true;
            meterSource = "osd_same_frame";
        }
        else
        {
            var video = state.Video!;
            var resolution = CodingMeterResolver.Resolve(
                frame.TimestampSeconds,
                meterRead.Meter,
                previousOsdMeter,
                previousOsdTime,
                frame.TimestampSeconds,
                video.DurationSeconds,
                video.ReachLengthM,
                state.CurrentMeter);
            meter = resolution.Meter;
            meterFromOsd = resolution.IsOsd;
            meterSource = meterRead.Meter.HasValue
                ? "osd_same_frame"
                : resolution.IsOsd
                    ? "osd_recent"
                    : CodingMeterResolver.EstimateFromVideo(
                        frame.TimestampSeconds,
                        video.DurationSeconds,
                        video.ReachLengthM).HasValue
                        ? "video_timeline_estimate"
                        : "session_meter_fallback";
        }

        if (meterRead.Meter.HasValue)
        {
            state.LastAcceptedOsdMeter = meterRead.Meter;
            state.LastAcceptedOsdTimestampSeconds = frame.TimestampSeconds;
        }
        state.CurrentMeter = meter;
        state.Session.MoveToMeter(meter);
        trace["meter"] = meter.ToString("F2", CultureInfo.InvariantCulture);
        trace["meter_source"] = meterSource;
        trace["meter_from_osd"] = meterFromOsd.ToString();

        var context = CodingAnalysisContext.CreateDefault(
            () => state.Session.Events,
            () => state.Session.Events,
            () => [],
            () => null,
            () => 1,
            _ => throw new InvalidOperationException("Kein Player-Snapshot im Messhost."));

        if (persistentVideo && context.IsAfterTerminalBoundary(meter, time))
        {
            trace["terminal_stop"] = "true";
            return Result(frame, "StoppedAtTerminalBoundary", meter, meterSource, state, before, trace, null);
        }

        if (state.UseFrameReadiness)
        {
            state.Readiness.Update(frame.TimestampSeconds, meterRead.Meter.HasValue, frame.TimestampSeconds);
            trace["frame_readiness"] = state.Readiness.State.ToString();
            trace["frame_readiness_skipped"] = state.Readiness.SkippedFrames.ToString(CultureInfo.InvariantCulture);
            trace["first_clean_frame_seconds"] = state.Readiness.FirstCleanFrameSeconds?.ToString(CultureInfo.InvariantCulture) ?? "";
            if (!state.Readiness.IsReady)
                return Result(frame, "FrameNotReady", meter, meterSource, state, before, trace, null);
        }

        var candidateFrame = state.CandidateDetections?.ForFrame(frame.Id);
        if (state.CandidateDetections is { } candidateSet)
        {
            trace["candidate_status"] = "development_candidate_unqualified";
            trace["candidate_id"] = candidateSet.Document.CandidateId;
            trace["candidate_role"] = candidateSet.Document.Model.Role;
            trace["candidate_expected_weights_sha256"] = candidateSet.Document.Model.ExpectedWeightsSha256;
            trace["candidate_actual_weights_sha256"] = candidateSet.Document.Model.ActualWeightsSha256;
            trace["candidate_detection_file_sha256"] = candidateSet.FileSha256;
            trace["candidate_detection_count"] = candidateFrame!.Detections.Count.ToString(CultureInfo.InvariantCulture);
            trace["candidate_inference_ms"] = candidateFrame.InferenceTimeMs.ToString("F3", CultureInfo.InvariantCulture);
            if (!string.IsNullOrWhiteSpace(candidateFrame.TechnicalError))
            {
                trace["candidate_technical_error"] = candidateFrame.TechnicalError;
                return Result(frame, "CandidateTechnicalError", meter, meterSource, state, before, trace,
                    "Kandidatendetektion technisch fehlgeschlagen: " + candidateFrame.TechnicalError);
            }
        }

        var resolutions = new List<string>();
        var attachedFramePhotos = new List<string>();
        string? Resolve(LiveFrameFinding finding, double atMeter)
        {
            var code = CodingFindingCodeResolver.Resolve(finding, atMeter, []);
            resolutions.Add($"{finding.Label} -> {code ?? "kein Code"}");
            trace["code_resolution"] = string.Join("\n", resolutions);
            return code;
        }
        string? Label(string code) => VsaCodeResolver.LookupLabel(code);
        var rules = new List<string>();
        void Log(string text)
        {
            rules.Add(text);
            trace["rule_messages"] = string.Join("\n", rules);
        }
        bool ApplyStretch(IReadOnlyList<StreckenschadenTracker.SegmentAction> actions, TimeSpan atTime) =>
            CodingStreckenschadenActionApplier.Apply(
                actions,
                state.Session.Events,
                state.Session,
                atTime,
                Label,
                _ => { });
        void CloseStretch(double atMeter) => ApplyStretch(state.Tracker.CloseAll(atMeter), time);
        var boundary = CreateBoundary(state, meter, time, Label, Log, frame, image, attachedFramePhotos);

        SingleFrameResult? raw = null;
        var outcome = "NotRun";
        var inference = await CodingMultiModelInferenceWorkflow.ExecuteAsync(
            new(state.Video?.Holding ?? frame.Id, image, frame.TimestampSeconds, meter,
                state.Session.ActiveSession?.Calibration?.NominalDiameterMm,
                state.Session.EndMeter,
                ct),
            new(
                ResolveCurrentMeter: (_, _) => meter,
                AnalyzeFrameAsync: async (bytes, input, token) => raw = candidateFrame is null
                    ? await multiModel.AnalyzeFrameAsync(
                        bytes,
                        input.NominalDiameterMm,
                        null,
                        token,
                        input.CurrentMeter,
                        input.ReachLength)
                    : await multiModel.AnalyzeCandidateFrameAsync(
                        bytes,
                        input.NominalDiameterMm,
                        ToCandidate(candidateFrame, state.CandidateDetections!.Document.Model),
                        null,
                        token,
                        input.CurrentMeter,
                        input.ReachLength),
                SetCodingAiState: (text, _, _, _) => trace["player_status"] = text,
                TryHandleBoundaryClassifierResultAsync: async (result, _, _) =>
                {
                    var handled = await CodingBoundaryClassifierResultWorkflow.ExecuteAsync(
                        new(result, meter, state.Session.EndMeter, time, state.Session.Events.Count, image),
                        new(Label, Log, () => { }, () => { }, (_, _) => { }, (_, _) => { },
                            (m, _, bytes) => boundary.EnsureStartAsync(m, bytes),
                            CloseStretch,
                            (m, _, bytes) => boundary.EnsureEnd(m, bytes),
                            () => state.Session.Events.Count,
                            (text, _, _) => trace["player_status"] = text));
                    if (handled.Handled)
                        outcome = handled.Outcome.ToString();
                    return handled.Handled;
                },
                TryHandleStructuralClassifierResult: (result, _, _) =>
                {
                    var handled = CodingStructuralClassifierResultWorkflow.Execute(
                        new(result, meter, time, state.Session.Events, state.Session, meterFromOsd),
                        new(Label, Resolve, () => { }, () => { }, (_, _) => { }, _ => { }, () => { },
                            (text, _, _) => trace["player_status"] = text));
                    if (handled.Handled)
                        outcome = "Structural_" + handled.Outcome;
                    return handled.Handled;
                },
                HandleAnalysisResult: result =>
                {
                    var displayed = CodingMultiModelAnalysisResultWorkflow.Execute(
                        new(result, state.Video?.Holding ?? frame.Id),
                        new(
                            (text, _, _, _) => trace["player_status"] = text,
                            () => { },
                            r =>
                            {
                                var findings = context.BuildSegmentedFindings(r);
                                trace["segmented"] = findings.Count.ToString(CultureInfo.InvariantCulture);
                                trace["codierbar"] = findings.Count(f => f.Proximity.IsCodierbar).ToString(CultureInfo.InvariantCulture);
                                trace["segmented_origins"] = string.Join("; ", findings.Select(DescribeOrigin));
                                return findings;
                            },
                            (_, _) => { },
                            (findings, width, height, yoloConfidence) =>
                                CodingMultiModelFindingEventCommandWorkflow.Execute(
                                    new(true, findings, width, height, yoloConfidence,
                                        frame.TimestampSeconds, meter, state.Session,
                                        state.Session.Events, new QualityGateService(), meterFromOsd,
                                        null, catalog, time, time)
                                    {
                                        SameFrameMeterEvidence = meterSource == "osd_same_frame"
                                    },
                                    new(
                                        (_, _) => meter,
                                        (items, m, t) => CodingStreckenschadenTrackingCommandWorkflow.ApplyTracking(
                                            new(items, m, t, true, true),
                                            new(
                                                (segments, atMeter) => CodingStreckenschadenObservationBuilder.Build(segments, atMeter, Resolve),
                                                state.Tracker.Update,
                                                ApplyStretch,
                                                () => { })).ConsumedSegments,
                                        request => CodingMultiModelFindingEventWorkflow.Execute(
                                            request,
                                            new(Resolve, Label,
                                                entry => RememberAttachedPhoto(
                                                    state.AttachAnalyzedFramePhoto(entry, frame, image),
                                                    attachedFramePhotos),
                                                Log, () => { }, () => { }))))));
                    outcome = displayed.Outcome.ToString();
                }));

        if (inference.Outcome == CodingMultiModelInferenceWorkflowOutcome.Error)
            outcome = "Error";
        trace["inference_outcome"] = inference.Outcome.ToString();
        trace["detector_qualified"] = raw?.DetectorQualified?.ToString() ?? "unknown";
        trace["detector_qualification_reason"] = raw?.DetectorQualificationReason ?? "";
        trace["degraded_reason"] = raw?.DegradedReason ?? "";
        trace["classifier_code"] = raw?.ClassifierCode ?? "";
        trace["localized_detections"] = raw?.LocalizedDetections is { Count: > 0 } localized
            ? string.Join("; ", localized.Select(DescribeLocalized))
            : "";
        trace["open_stretches"] = state.Tracker.OpenCount.ToString(CultureInfo.InvariantCulture);
        state.LastAnalyzedFrame = image.ToArray();
        state.LastAnalyzedVideoFrame = frame;
        state.RememberNewOrigins(before, meterSource, frame.Id, ReplayFiles.Hash(image));
        AddPhotoEvidenceTrace(trace, frame, image, attachedFramePhotos, state.SnapshotEvents());
        return Result(
            frame,
            outcome,
            meter,
            meterSource,
            state,
            before,
            trace,
            raw?.Error ?? vision.TechnicalError);
    }

    internal ReplayVideoFinalization FinalizeVideo(ReplayCodingState state)
    {
        var before = state.SnapshotEvents().ToDictionary(e => e.EventId);
        var closeMeter = state.LastAcceptedOsdMeter ?? state.Session.EndMeter;
        var time = state.CurrentVideoTime;
        string? Label(string code) => VsaCodeResolver.LookupLabel(code);
        bool ApplyStretch(IReadOnlyList<StreckenschadenTracker.SegmentAction> actions, TimeSpan atTime) =>
            CodingStreckenschadenActionApplier.Apply(
                actions,
                state.Session.Events,
                state.Session,
                atTime,
                Label,
                _ => { });
        var attachedFramePhotos = new List<string>();
        var boundary = CreateBoundary(
            state,
            closeMeter,
            time,
            Label,
            _ => { },
            state.LastAnalyzedVideoFrame,
            state.LastAnalyzedFrame,
            attachedFramePhotos);

        var result = CodingModeExitFinalizationWorkflow.Execute(
            new(state.Session.Events, state.LastAcceptedOsdMeter, state.Session.EndMeter,
                time, state.LastAnalyzedFrame),
            new(
                endMeter => CodingStreckenschadenTrackingCommandWorkflow.CloseTracked(
                    new(endMeter, time),
                    new(state.Tracker.CloseAll, ApplyStretch, () => { })),
                _ => !state.Session.Events.Any(e => e.Entry.IsStreckenschaden && e.Entry.MeterEnd is null),
                (endMeter, _, bytes) => boundary.EnsureEnd(endMeter, bytes)));

        var meterSource = state.LastAcceptedOsdMeter.HasValue
            ? "osd_last_accepted"
            : "holding_end_fallback";
        state.RememberNewOrigins(
            before,
            meterSource,
            state.LastAnalyzedVideoFrame?.Id,
            state.LastAnalyzedFrame is null ? null : ReplayFiles.Hash(state.LastAnalyzedFrame));
        var events = state.SnapshotEvents();
        return new ReplayVideoFinalization(
            result.CanExit,
            state.Diff(before),
            events,
            state.Session.Events.Count(e => e.Entry.IsStreckenschaden && e.Entry.MeterEnd is null),
            CodingTerminalBoundaryPresencePolicy.HasEndOrAbortCode(state.Session.Events));
    }

    private static CodingBoundaryContext CreateBoundary(
        ReplayCodingState state,
        double meter,
        TimeSpan time,
        Func<string, string?> label,
        Action<string> log,
        ReplayVideoFrame? frame,
        byte[]? image,
        ICollection<string> attachedFramePhotos)
        => new(
            new(
                () => true,
                () => state.Session.Events,
                () => state.Session.Events,
                () => [],
                () => state.Session,
                () => state.Readiness.FirstCleanFrameSeconds,
                () => state.LastAcceptedOsdMeter ?? meter,
                () => state.Session.EndMeter,
                () => time),
            new(label, log, _ => Task.FromResult<byte[]?>(null),
                (entry, bytes) => RememberAttachedPhoto(
                    state.AttachAnalyzedFramePhoto(entry, frame, bytes ?? image),
                    attachedFramePhotos),
                () => { }, () => { }));

    private static void RememberAttachedPhoto(string? photoPath, ICollection<string> attachedFramePhotos)
    {
        if (!string.IsNullOrWhiteSpace(photoPath) && !attachedFramePhotos.Contains(photoPath))
            attachedFramePhotos.Add(photoPath);
    }

    private static void AddPhotoEvidenceTrace(
        IDictionary<string, string> trace,
        ReplayVideoFrame frame,
        byte[] image,
        IReadOnlyCollection<string> attachedFramePhotos,
        IReadOnlyList<ReplayVideoEventSnapshot> events)
    {
        trace["evidence_frame_id"] = frame.Id;
        trace["evidence_image_sha256"] = ReplayFiles.Hash(image);
        trace["evidence_photo_paths"] = string.Join("; ", attachedFramePhotos);
        trace["event_photo_paths"] = string.Join("; ", events.Select(e =>
            $"{e.EventId:N}=[{string.Join(",", e.PhotoPaths)}]"));
        trace["event_original_photo_paths"] = string.Join("; ", events.Select(e =>
            $"{e.EventId:N}=[{string.Join(",", e.OriginalPhotoPaths)}]"));
        trace["event_previous_evidence"] = string.Join("; ", events.Select(e =>
            $"{e.EventId:N}:revisions={e.PreviousEvidence.Count}:" +
            string.Join("|", e.PreviousEvidence.Select(previous =>
                $"r{previous.Revision}:frame={previous.FrameId}:sha={previous.ImageSha256}" +
                $"[{string.Join(",", previous.PhotoPaths)}]"))));
        trace["event_human_touched"] = string.Join("; ", events.Select(e =>
            $"{e.EventId:N}={e.HumanTouchedAtUtc?.ToString("O", CultureInfo.InvariantCulture) ?? ""}"));
    }

    private static ReplayVideoFrameObservation Result(
        ReplayVideoFrame frame,
        string outcome,
        double meter,
        string meterSource,
        ReplayCodingState state,
        IReadOnlyDictionary<Guid, ReplayVideoEventSnapshot> before,
        IReadOnlyDictionary<string, string> trace,
        string? technicalError)
        => new(
            frame.Id,
            frame.TimestampSeconds,
            outcome,
            meter,
            meterSource,
            state.Diff(before),
            state.SnapshotEvents(),
            new Dictionary<string, string>(trace),
            technicalError);

    private static string DescribeOrigin(SegmentedFinding finding)
        => finding.Origin is null
            ? "unknown"
            : $"{finding.Origin.SourceName}:{finding.Origin.Label}:{finding.Origin.Confidence:F3}";

    private static string DescribeLocalized(CodingLocalizedDetection detection)
        => string.Create(
            CultureInfo.InvariantCulture,
            $"{detection.SourceName}:{detection.Label}:{detection.Confidence:F3}:" +
            $"[{detection.X1:F1},{detection.Y1:F1},{detection.X2:F1},{detection.Y2:F1}]:" +
            $"yolo_sha={detection.YoloArtifactSha256 ?? ""}:review={detection.RequiresReview}");

    private static CodingDetectorCandidateFrame ToCandidate(
        ReplayCandidateFrameDetections frame,
        ReplayCandidateModel model)
        => new(
            model.Id,
            model.ExpectedWeightsSha256,
            model.ActualWeightsSha256,
            frame.ImageSha256,
            frame.Detections.Select(detection => new YoloDetectionDto(
                detection.X1,
                detection.Y1,
                detection.X2,
                detection.Y2,
                detection.ClassName,
                detection.Confidence)).ToArray(),
            frame.InferenceTimeMs);
}
