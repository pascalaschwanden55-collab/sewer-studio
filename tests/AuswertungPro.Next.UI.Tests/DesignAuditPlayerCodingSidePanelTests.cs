using System.IO;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DesignAuditPlayerCodingSidePanelTests
{
    [Fact]
    public void Player_coding_side_panel_has_only_the_active_inline_detail_panel()
    {
        var sidePanel = ReadUiFile("Views", "Windows", "PlayerCodingSidePanel.xaml");
        var accessors = ReadUiFile("Views", "Windows", "PlayerWindow.CodingSidePanelAccessors.cs");
        var coding = ReadCodingPartials();

        Assert.Contains("x:Name=\"CodingDefectDetailInline\"", sidePanel);
        Assert.DoesNotContain("x:Name=\"CodingDefectDetailPanel\"", sidePanel);
        Assert.DoesNotContain("CodingDefectDetailPanel", accessors);
        Assert.DoesNotContain("CodingDefectDetailPanel", coding);
        Assert.DoesNotContain("UpdateCodingDefectDetailPanel", coding);
    }

    [Fact]
    public void Player_coding_side_panel_uses_readable_font_sizes_and_section_labels()
    {
        var sidePanel = ReadUiFile("Views", "Windows", "PlayerCodingSidePanel.xaml");

        Assert.DoesNotContain("FontSize=\"8\"", sidePanel);
        Assert.DoesNotContain("FontSize=\"9\"", sidePanel);
        Assert.Contains("Style=\"{DynamicResource SectionLabel}\"", sidePanel);
    }

    [Fact]
    public void Player_hides_coding_overlay_when_external_window_gets_focus()
    {
        var coding = ReadCodingPartials();
        var window = ReadUiFile("Views", "Windows", "PlayerWindow.Wiring.cs");
        var controls = ReadUiFile("Ai", "CodingOverlayInputControls.cs");
        var suspendBody = ExtractMethodBody(coding, "private void SuspendCodingOverlayInput()");
        var hideBody = ExtractMethodBody(coding, "private void HideCodingOverlayForExternalWindow()");

        Assert.Contains("HideCodingOverlayForExternalWindow", window);
        Assert.Contains("CodingOverlayInputControls.ClosePopup(CodingOverlayPopup)", hideBody);
        Assert.DoesNotContain("CodingOverlayPopup.IsOpen = false", coding);
        Assert.Contains("RestoreCodingOverlayAfterExternalWindow", window);
        Assert.DoesNotContain("CodingOverlayPopup.IsOpen = false", suspendBody);
        Assert.Contains("CodingOverlayInputControls.SuspendCanvas(CodingOverlayCanvas)", suspendBody);
        Assert.Contains("overlayCanvas.IsHitTestVisible = false", controls);
    }

    [Fact]
    public void Player_uses_same_overlay_policy_for_rendering_and_events()
    {
        var coding = ReadCodingPartials();
        var summary = ReadUiFile("Ai", "CodingMultiModelFindingSummary.cs");
        var resultWorkflow = ReadUiFile("Ai", "CodingMultiModelAnalysisResultWorkflow.cs");

        Assert.Contains("CodingMultiModelAnalysisResultWorkflow.Execute", coding);
        Assert.Contains("CodingMultiModelFindingSummary.Build(segmented, result)", resultWorkflow);
        Assert.Contains("CodingSegmentedFindingVisibility.BuildVisibleCodingFindings", summary);
        Assert.Contains("CodingSamMaskOverlayController.RenderCandidates", coding);
        Assert.Contains("findingSummary.VisibleCodierbar", resultWorkflow);
        Assert.DoesNotContain("AddMultiModelFindingsAsEvents(\r\n                    segmented.Where(s => s.Proximity.IsCodierbar).ToList()", coding);
    }

    [Fact]
    public void Player_renders_ahead_segment_masks_without_coding_them()
    {
        var coding = ReadCodingPartials();
        var summary = ReadUiFile("Ai", "CodingMultiModelFindingSummary.cs");
        var resultWorkflow = ReadUiFile("Ai", "CodingMultiModelAnalysisResultWorkflow.cs");
        var renderWorkflow = ReadUiFile("Ai", "CodingMultiModelResultsRenderWorkflow.cs");
        var showBody = ExtractMethodBody(coding, "private void ShowMultiModelResults");

        Assert.Contains("CodingSegmentedFindingVisibility.BuildVisibleMaskRenderCandidates", coding);
        Assert.Contains("CodingMultiModelResultsRenderWorkflow.Execute", showBody);
        Assert.Contains("actions.BuildVisibleMaskRenderCandidates(request.Segmented)", renderWorkflow);
        Assert.Contains("CodingSegmentedFindingVisibility.BuildVisibleCodingFindings(segmented)", summary);
        Assert.Contains("AddMultiModelFindingsAsEvents(", coding);
        Assert.Contains("findingSummary.VisibleCodierbar", resultWorkflow);
    }

    [Fact]
    public void Player_status_mentions_background_masks_suppressed()
    {
        var summary = ReadUiFile("Ai", "CodingMultiModelFindingSummary.cs");
        var resultWorkflow = ReadUiFile("Ai", "CodingMultiModelAnalysisResultWorkflow.cs");

        Assert.Contains("CodingSegmentedFindingVisibility.BuildOverlaySuppressionText", summary);
        Assert.Contains("findingSummary.TimingText", resultWorkflow);
    }

    [Fact]
    public void Player_coding_detail_shows_large_ai_evidence_preview()
    {
        var sidePanel = ReadUiFile("Views", "Windows", "PlayerCodingSidePanel.xaml");
        var accessors = ReadUiFile("Views", "Windows", "PlayerWindow.CodingSidePanelAccessors.cs");
        var coding = ReadCodingPartials();
        var previewService = ReadUiFile("Ai", "CodingInlineEvidencePreviewService.cs");
        var previewWorkflow = ReadUiFile("Ai", "CodingInlineEvidencePreviewWorkflow.cs");

        Assert.Contains("x:Name=\"ImgInlineEvidencePreview\"", sidePanel);
        Assert.Contains("x:Name=\"TxtInlineEvidencePreviewStatus\"", sidePanel);
        Assert.Contains("ImgInlineEvidencePreview", accessors);
        Assert.Contains("CodingInlineEvidencePreviewWorkflow.Execute", coding);
        Assert.Contains("CodingInlineEvidencePreviewService.Build", previewWorkflow);
        Assert.Contains("CodingDefectPreviewService.BuildPreviewImagePath", previewService);
    }

    [Fact]
    public void Player_coding_detail_uses_open_decision_policy_for_confirm_buttons()
    {
        var coding = ReadCodingPartials();
        var detailBody = ExtractMethodBody(coding, "private void UpdateInlineDefectDetail");
        var policy = ReadUiFile("Ai", "CodingDefectStatusDisplayPolicy.cs");
        var controls = ReadUiFile("Ai", "CodingInlineDefectDetailControls.cs");

        Assert.Contains("CodingDefectStatusDisplayPolicy.BuildInlineDetail(ev)", detailBody);
        Assert.Contains("_codingSidePanelControllers.InlineDefectDetail.Apply(state)", detailBody);
        // CanAct-Logik ist seit W2-2 in DefectStatusPolicy (Application.Ai), kein VM-Aufruf mehr.
        Assert.Contains("DefectStatusPolicy.CanAct(ev)", policy);
        Assert.Contains("BtnInlineAccept.Visibility = state.CanAct ? Visibility.Visible : Visibility.Collapsed", controls);
        Assert.Contains("BtnInlineReject.Visibility = state.CanAct ? Visibility.Visible : Visibility.Collapsed", controls);
    }

    [Fact]
    public void Player_photo_window_shows_segmented_evidence_preview_before_raw_photos()
    {
        var coding = ReadCodingPartials();
        var photoBody = ExtractMethodBody(coding, "private void CodingEventShowPhotos_Click");
        var policy = ReadUiFile("Ai", "CodingPhotoDisplayPathPolicy.cs");
        var loader = ReadUiFile("Ai", "CodingPhotoViewerImageSourceLoader.cs");
        var displayWorkflow = ReadUiFile("Ai", "CodingPhotoViewerDisplayWorkflow.cs");
        var viewerWorkflowFactory = ReadUiFile("Ai", "CodingPhotoViewerWorkflowServiceFactory.cs");
        var viewerService = ReadUiFile("Ai", "CodingPhotoViewerWindowService.cs");

        Assert.DoesNotContain("CodingPhotoViewerWorkflowServiceFactory.Create", photoBody);
        Assert.Contains("CodingPhotoViewerWorkflowServiceFactory.Create", displayWorkflow);
        Assert.Contains("CodingPhotoViewerWindowServiceFactory.Create", viewerWorkflowFactory);
        Assert.Contains("CodingPhotoViewerImageSourceLoader.Load", viewerService);
        Assert.Contains("CodingDefectPreviewService.BuildPreviewImagePath", loader);
        Assert.Contains("CodingPhotoDisplayPathPolicy.BuildDisplayPhotoPaths", loader);
        Assert.True(
            policy.IndexOf("displayPaths.Add(evidencePreviewPath)", StringComparison.Ordinal)
            < policy.IndexOf("foreach (var photoPath in photoPaths)", StringComparison.Ordinal),
            "Segmentierte Beweisvorschau muss vor den Rohfotos eingefuegt werden.");
    }

    [Fact]
    public void Player_stops_ai_analysis_before_snapshot_after_rohrende()
    {
        var coding = ReadCodingPartials();
        var preflightWorkflow = ReadUiFile("Ai", "CodingAnalysisPreflightWorkflow.cs");
        var singleModelWorkflow = ReadUiFile("Ai", "CodingSingleModelAnalysisWorkflow.cs");
        var runBody = ExtractMethodBody(coding, "private async Task RunCodingAnalysisAsync");

        var preflightIndex = runBody.IndexOf("CodingAnalysisPreflightWorkflow.Execute", StringComparison.Ordinal);
        var singleModelIndex = runBody.IndexOf("CodingSingleModelAnalysisWorkflow.ExecuteAsync", StringComparison.Ordinal);
        var stopIndex = preflightWorkflow.IndexOf("actions.IsAfterTerminalBoundary(framePosition)", StringComparison.Ordinal);
        var captureIndex = singleModelWorkflow.IndexOf("actions.CaptureSnapshotAsync", StringComparison.Ordinal);

        Assert.True(preflightIndex >= 0, "RunCodingAnalysisAsync muss zuerst den Preflight ausfuehren.");
        Assert.True(singleModelIndex >= 0, "RunCodingAnalysisAsync muss weiterhin Single-Model-Frames capturen koennen.");
        Assert.True(preflightIndex < singleModelIndex, "Stop-Pruefung muss vor Snapshot/SAM laufen.");
        Assert.True(stopIndex >= 0, "Preflight muss nach BCE/BDC stoppen.");
        Assert.True(captureIndex >= 0, "Single-Model-Workflow muss Frames mit Analyse-Cancellation capturen koennen.");
        Assert.Contains("CodingDedupPolicy.ShouldStopAnalysisAfterTerminalCode", coding);
    }

    [Fact]
    public void Player_defers_spatial_bogen_before_creating_protocol_event()
    {
        var coding = ReadCodingPartials();
        var policy = ReadUiFile("Ai", "CodingMultiModelFindingAddDecisionPolicy.cs");
        var workflow = ReadUiFile("Ai", "CodingMultiModelFindingEventWorkflow.cs");
        var addBody = ExtractMethodBody(coding, "private void AddMultiModelFindingsAsEvents");

        Assert.Contains("CodingDedupPolicy.ShouldDeferSpatialCodeUntilCloser", policy);
        Assert.Contains("CodingMultiModelFindingEventWorkflow.Execute", addBody);
        Assert.DoesNotContain("CodingMultiModelFindingAddDecisionPolicy.Decide", addBody);
        Assert.Contains("CodingMultiModelFindingAddDecisionPolicy.Decide", workflow);
        Assert.True(
            workflow.IndexOf("CodingMultiModelFindingAddDecisionPolicy.Decide", StringComparison.Ordinal)
            < FirstIndexOf(
                workflow,
                "codingSessionService.AddEvent(entry)",
                "codingSessionService.AddEvent(draft.Entry)",
                "CodingMultiModelEventAppender.Apply"),
            "Bogen-Vorschau muss vor AddEvent ausgesiebt werden.");
    }

    [Fact]
    public void Player_rohranfang_photos_stay_on_event_frame()
    {
        var coding = ReadCodingPartials();
        var captureBody = ExtractMethodBody(coding, "private string? CodingCaptureSnapshot");
        var persistBody = ExtractMethodBody(coding, "private async System.Threading.Tasks.Task PersistSingleEventAsTrainingSample");

        Assert.DoesNotContain("PlayerBoundaryPhotoPolicy.GetRequiredSnapshotTime(entry.Code", captureBody);
        Assert.DoesNotContain("SeekToRequiredPhotoTime", captureBody);
        Assert.Contains("_detectionConfirmationBuffer.FrameBytes", persistBody);
        Assert.DoesNotContain("CaptureFrameBytesAtRequiredPhotoTimeAsync", persistBody);
        Assert.DoesNotContain("Rohranfang-Foto nach Datenblendung nicht verfuegbar", persistBody);
    }

    [Fact]
    public void Player_coding_analysis_keeps_analyzed_frame_for_gold_snapshot()
    {
        var coding = ReadCodingPartials();
        var startWorkflow = ReadUiFile("Ai", "CodingMultiModelAnalysisStartWorkflow.cs");
        var multiModelBody = ExtractMethodBody(coding, "private async Task RunCodingMultiModelAnalysisAsync");

        Assert.Contains("CodingMultiModelAnalysisStartWorkflow.ExecuteAsync", multiModelBody);
        Assert.Contains("actions.StoreAnalyzedFrame(pngBytes, request.CaptureTimestampSeconds)", startWorkflow);
        Assert.True(
            multiModelBody.IndexOf("CodingMultiModelAnalysisStartWorkflow.ExecuteAsync", StringComparison.Ordinal)
            < multiModelBody.IndexOf("TryHandleBoundaryClassifierResult", StringComparison.Ordinal),
            "Der Gold-Snapshot muss im Startworkflow gesetzt werden, bevor ein BCD/BCE-Event entstehen kann.");
        Assert.True(
            startWorkflow.IndexOf("actions.StoreAnalyzedFrame(pngBytes, request.CaptureTimestampSeconds)", StringComparison.Ordinal)
            < startWorkflow.IndexOf("CodingMultiModelAnalysisStartWorkflowOutcome.Ready", StringComparison.Ordinal),
            "Der Startworkflow darf erst nach dem Speichern des analysierten Frames als Ready zurueckkehren.");
    }

    [Fact]
    public void Player_ai_findings_attach_analyzed_frame_photo_before_add_event()
    {
        var coding = ReadCodingPartials();
        var qwenBody = ExtractMethodBody(coding, "private void AddAiFindingsAsEvents");
        var qwenWorkflow = ReadUiFile("Ai", "CodingLiveFindingEventWorkflow.cs");
        var qwenAppender = ReadUiFile("Ai", "CodingLiveFindingSessionAppender.cs");
        var qwenAppenderBody = ExtractMethodBody(qwenAppender, "Func<ProtocolEntry, CodingEvent> addEvent)");
        var multiModelBody = ExtractMethodBody(coding, "private void AddMultiModelFindingsAsEvents");
        var multiModelWorkflow = ReadUiFile("Ai", "CodingMultiModelFindingEventWorkflow.cs");

        Assert.Contains("CodingLiveFindingEventWorkflow.Execute", qwenBody);
        Assert.Contains("CodingLiveFindingSessionAppender.Append", qwenWorkflow);
        AssertAnalyzedFrameAttachedBeforeAddEvent(qwenAppenderBody);
        Assert.Contains("CodingMultiModelFindingEventWorkflow.Execute", multiModelBody);
        AssertAnalyzedFrameAttachedBeforeAddEvent(multiModelWorkflow);
        Assert.DoesNotContain("codingSessionService.AddEvent(draft.Entry)", qwenBody);
        Assert.Contains("CodingMultiModelEventAppender.Apply", multiModelWorkflow);
        Assert.DoesNotContain("CodingMultiModelEventAppender.Apply", multiModelBody);
        Assert.DoesNotContain("codingSessionService.AddEvent(draft.Entry)", multiModelBody);
    }

    [Fact]
    public void Player_boundary_classifier_passes_current_analyzed_frame_to_boundary_events()
    {
        var coding = ReadCodingPartials();
        var boundaryBody = ExtractMethodBody(coding, "private bool TryHandleBoundaryClassifierResult");
        var boundaryCommandWorkflow = ReadUiFile("Ai", "CodingBoundaryClassifierCommandWorkflow.cs");

        Assert.Contains("_detectionConfirmationBuffer.FrameBytes", boundaryBody);
        Assert.Contains("request.AnalyzedFrameBytes", boundaryCommandWorkflow);
        Assert.Contains("EnsureRohranfangExists(startMeter, startTime, frameBytes, ref anyAdded)", boundaryBody);
        Assert.Contains("EnsureRohrendeExists(meterEnd, endTime, frameBytes)", boundaryBody);
    }

    [Fact]
    public void Player_handles_structural_classifier_before_no_detection_return()
    {
        var coding = ReadCodingPartials();
        var multiModelBody = ExtractMethodBody(coding, "private async Task RunCodingMultiModelAnalysisAsync");
        var structuralBody = ExtractMethodBody(coding, "private bool TryHandleStructuralClassifierResult");
        var resultWorkflow = ReadUiFile("Ai", "CodingMultiModelAnalysisResultWorkflow.cs");
        var structuralCommandWorkflow = ReadUiFile("Ai", "CodingStructuralClassifierCommandWorkflow.cs");
        var structuralWorkflow = ReadUiFile("Ai", "CodingStructuralClassifierResultWorkflow.cs");

        var boundaryIndex = multiModelBody.IndexOf("TryHandleBoundaryClassifierResult", StringComparison.Ordinal);
        var structuralIndex = multiModelBody.IndexOf("TryHandleStructuralClassifierResult", StringComparison.Ordinal);
        var resultWorkflowIndex = multiModelBody.IndexOf("CodingMultiModelAnalysisResultWorkflow.Execute", StringComparison.Ordinal);

        Assert.True(boundaryIndex >= 0, "Boundary-Classifier muss zuerst behandelt werden.");
        Assert.True(structuralIndex > boundaryIndex, "BCA/BCC darf BCD/BCE nicht ueberholen.");
        Assert.True(resultWorkflowIndex > structuralIndex, "BCA/BCC muss vor dem YOLO/DINO-No-Detection-Abbruch behandelt werden.");
        Assert.Contains("!result.IsRelevant || !result.HasDetections", resultWorkflow);
        Assert.Contains("CodingStructuralClassifierCommandWorkflow.Execute", structuralBody);
        Assert.Contains("actions.ExecuteResultWorkflow", structuralCommandWorkflow);
        Assert.Contains("CodingClassifierDisplayPolicy.IsStructuralClassifierCode(code)", structuralWorkflow);
        Assert.Contains("CodingStructuralClassifierEventFactory.Create", structuralWorkflow);
        Assert.Contains("CodingStructuralClassifierEventAppender.Apply", structuralWorkflow);
        Assert.DoesNotContain("codingSessionService.AddEvent(draft.Entry)", structuralBody);

        var clearIndex = structuralWorkflow.IndexOf("actions.ClearDetectionOverlays()", StringComparison.Ordinal);
        var listIndex = structuralWorkflow.IndexOf("actions.ShowResolvedFinding", StringComparison.Ordinal);
        Assert.True(clearIndex >= 0 && listIndex > clearIndex,
            "Die Befundliste muss nach dem Overlay-Clear gesetzt werden, sonst verschwindet der Bogen-Hinweis.");
    }

    [Fact]
    public void Player_exit_coding_mode_passes_current_analyzed_frame_to_auto_rohrende()
    {
        var coding = ReadCodingPartials();
        var workflow = ReadUiFile("Ai", "CodingModeExitFinalizationWorkflow.cs");
        var commandWorkflow = ReadUiFile("Ai", "CodingModeExitCommandWorkflow.cs");
        var exitBody = ExtractMethodBody(coding, "private void ExitCodingMode");

        Assert.Contains("CodingModeExitCommandWorkflow.Execute", exitBody);
        Assert.Contains("_detectionConfirmationBuffer.FrameBytes", coding);
        Assert.Contains("actions.FinalizeExit()", commandWorkflow);
        Assert.Contains("request.AnalyzedFrameBytes", workflow);
        Assert.Contains("actions.EnsureRohrendeExists", workflow);
    }

    [Fact]
    public void Player_coding_analysis_prefers_video_position_over_stale_viewmodel_meter_for_classifier()
    {
        var coding = ReadCodingPartials();
        var osdController = ReadUiFile("Player", "CodingOsdMeterController.cs");
        var inferenceWorkflow = ReadUiFile("Ai", "CodingMultiModelInferenceWorkflow.cs");
        var multiModelBody = ExtractMethodBody(coding, "private async Task RunCodingMultiModelAnalysisAsync");
        var resolveBody = ExtractMethodBody(coding, "private double ResolveCodingMeterForFrame");

        var meterStart = inferenceWorkflow.IndexOf("var currentMeterForClassifier", StringComparison.Ordinal);
        var resolveIndex = inferenceWorkflow.IndexOf("actions.ResolveCurrentMeter", StringComparison.Ordinal);
        var inputIndex = inferenceWorkflow.IndexOf("CodingMultiModelClassifierInputPolicy.Build", StringComparison.Ordinal);
        var controllerResolverIndex = resolveBody.IndexOf("_codingOsdMeterController.ResolveMeter", StringComparison.Ordinal);
        var viewModelMeterIndex = resolveBody.IndexOf("_codingSessionHost.CurrentMeter", StringComparison.Ordinal);

        Assert.True(meterStart >= 0, "Analyse muss einen Meter fuer den Klassifikator bestimmen.");
        Assert.True(resolveIndex >= 0, "Der Klassifikator muss den gemeinsamen Frame-Meter-Resolver verwenden.");
        Assert.True(inputIndex > resolveIndex, "Der Klassifikator-Input muss nach der Frame-Meter-Aufloesung gebaut werden.");
        Assert.Contains("CodingMultiModelInferenceWorkflow.ExecuteAsync", multiModelBody);
        Assert.Contains("ResolveCurrentMeter: ResolveCodingMeterForFrame", multiModelBody);
        Assert.Contains("CodingMultiModelClassifierInputPolicy.Build", inferenceWorkflow);
        Assert.True(controllerResolverIndex >= 0, "Video-Positions-Fallback muss ueber den OSD-Meter-Controller laufen.");
        Assert.Contains("CodingMeterResolver.Resolve", osdController);
        Assert.True(viewModelMeterIndex >= 0, "ViewModel-Meter darf nur als spaeter Fallback genutzt werden.");
        Assert.True(
            controllerResolverIndex < viewModelMeterIndex,
            "Staler CurrentMeter=0 darf die echte Videoposition nicht ueberstimmen, sonst blockiert BCD die Pipeline.");
    }

    [Fact]
    public void Player_ai_events_use_analyzed_frame_meter_not_stale_selected_meter()
    {
        var coding = ReadCodingPartials();
        var runBody = ExtractMethodBody(coding, "private async Task RunCodingAnalysisAsync");
        var multiModelBody = ExtractMethodBody(coding, "private void AddMultiModelFindingsAsEvents");
        var multiModelCommandWorkflow = ReadUiFile("Ai", "CodingMultiModelFindingEventCommandWorkflow.cs");
        var qwenBody = ExtractMethodBody(coding, "private void AddAiFindingsAsEvents");
        var qwenCommandWorkflow = ReadUiFile("Ai", "CodingLiveFindingEventCommandWorkflow.cs");
        var boundaryBody = ExtractMethodBody(coding, "private bool TryHandleBoundaryClassifierResult");
        var boundaryCommandWorkflow = ReadUiFile("Ai", "CodingBoundaryClassifierCommandWorkflow.cs");

        Assert.Contains("CodingAnalysisPreflightWorkflow.Execute", runBody);
        Assert.Contains("ResolveCodingMeterForFrame(timestamp)", runBody);
        Assert.Contains("ResolveMeterForFrame: (timestamp, osdMeter)", multiModelBody);
        Assert.Contains("request.CaptureTimestampSeconds", multiModelCommandWorkflow);
        Assert.Contains("request.FrameOsdMeter", multiModelCommandWorkflow);
        Assert.Contains("ResolveMeterForFrame: (timestamp, osdMeter)", qwenBody);
        Assert.Contains("request.Result.TimestampSeconds", qwenCommandWorkflow);
        Assert.Contains("request.Result.MeterReading", qwenCommandWorkflow);
        Assert.Contains("ResolveMeterForFrame: (timestamp, osdMeter)", boundaryBody);
        Assert.Contains("request.CaptureTimestampSeconds", boundaryCommandWorkflow);
        Assert.Contains("request.FrameOsdMeter", boundaryCommandWorkflow);

        Assert.DoesNotContain("double meter = _codingLastOsdMeter ?? codingVm.CurrentMeter", multiModelBody);
        Assert.DoesNotContain("double meter = _codingLastOsdMeter ?? codingVm.CurrentMeter", qwenBody);
        Assert.DoesNotContain("var meter = _codingLastOsdMeter ?? _codingVm.CurrentMeter", boundaryBody);
    }

    [Fact]
    public void Player_reads_osd_meter_from_analyzed_frame_before_multimodel_detection()
    {
        var coding = ReadCodingPartials();
        var osdService = ReadUiFile("Ai", "CodingOsdMeterService.cs");
        var singleModelWorkflow = ReadUiFile("Ai", "CodingSingleModelAnalysisWorkflow.cs");
        var startWorkflow = ReadUiFile("Ai", "CodingMultiModelAnalysisStartWorkflow.cs");
        var inferenceWorkflow = ReadUiFile("Ai", "CodingMultiModelInferenceWorkflow.cs");
        var multiModelBody = ExtractMethodBody(coding, "private async Task RunCodingMultiModelAnalysisAsync");
        var readerBody = ExtractMethodBody(coding, "private async Task<double?> TryReadAnalyzedFrameOsdMeterAsync");
        var helperBody = ExtractMethodBody(coding, "private async Task<double?> TryReadOsdMeterFromFrameBytesAsync");

        var startIndex = multiModelBody.IndexOf("CodingMultiModelAnalysisStartWorkflow.ExecuteAsync", StringComparison.Ordinal);
        var inferenceIndex = multiModelBody.IndexOf("CodingMultiModelInferenceWorkflow.ExecuteAsync", StringComparison.Ordinal);
        var addIndex = multiModelBody.IndexOf("AddMultiModelFindingsAsEvents(", StringComparison.Ordinal);

        Assert.True(startIndex >= 0, "Multi-Model muss den OSD-Meter aus exakt dem analysierten Frame lesen.");
        Assert.True(inferenceIndex >= 0, "Test erwartet den Klassifikator-Meter im Multi-Model-Pfad.");
        Assert.True(startIndex < inferenceIndex, "OSD-Meter muss vor Klassifikator/Boundary-Logik vorliegen.");
        Assert.Contains("actions.TryReadAnalyzedFrameOsdMeterAsync", startWorkflow);
        Assert.Contains("request.FrameOsdMeter", inferenceWorkflow);
        Assert.Contains("start.FrameOsdMeter", multiModelBody);
        Assert.Contains("start.FrameOsdMeter", multiModelBody[addIndex..]);
        Assert.Contains("actions.TryReadAnalyzedFrameOsdMeterAsync", singleModelWorkflow);
        Assert.Contains("result with { MeterReading = frameOsdMeter }", singleModelWorkflow);
        Assert.Contains("TryReadOsdMeterFromFrameBytesAsync", readerBody);
        Assert.Contains("CodingOsdMeterService", helperBody);
        Assert.Contains("CodingOsdMeterReader.BuildOsdSearchImage", osdService);
        Assert.Contains("CodingOsdMeterReader.AcceptMeterCandidate", osdService);
    }

    [Fact]
    public void Player_auto_boundary_events_attach_passed_frame_before_add_event()
    {
        var coding = ReadCodingPartials();
        var workflow = ReadUiFile("Ai", "CodingBoundaryEventWorkflow.cs");
        var bcdBody = ExtractMethodBody(coding, "private void EnsureRohranfangExists");
        var bceBody = ExtractMethodBody(coding, "private void EnsureRohrendeExists");
        var workflowStartBody = ExtractMethodBody(workflow, "public static CodingBoundaryEventWorkflowResult EnsureStart");
        var workflowEndBody = ExtractMethodBody(workflow, "public static CodingBoundaryEventWorkflowResult EnsureEnd");

        Assert.Contains("CodingBoundaryEventWorkflow.EnsureStart", bcdBody);
        Assert.Contains("CodingBoundaryEventWorkflow.EnsureEnd", bceBody);
        AssertBoundaryFrameAttachedBeforeAddEvent(workflowStartBody);
        AssertBoundaryFrameAttachedBeforeAddEvent(workflowEndBody);
    }

    [Fact]
    public void Player_event_seek_allows_zero_timestamp()
    {
        var coding = ReadCodingPartials();
        var workflow = ReadUiFile("Ai", "CodingEventSeekCommandWorkflow.cs");
        var seekBody = ExtractMethodBody(coding, "private void CodingEventSeek_Click");

        Assert.Contains("CodingEventSeekCommandWorkflow.Execute", seekBody);
        Assert.Contains("CodingEventSeekPolicy.TryGetSeekMilliseconds(selectedEvent", workflow);
        Assert.DoesNotContain("selectedEvent.VideoTimestamp.TotalMilliseconds > 0", workflow);
    }

    [Fact]
    public void Player_import_seek_allows_zero_timestamp()
    {
        var coding = ReadCodingPartials();
        var workflow = ReadUiFile("Ai", "CodingImportEventSeekCommandWorkflow.cs");
        var seekBody = ExtractMethodBody(coding, "private void SeekToImportEvent(object? selectedItem)");

        Assert.Contains("CodingImportEventSeekCommandWorkflow.Execute", seekBody);
        Assert.Contains("CodingEventSeekPolicy.TryGetSeekMilliseconds(importEvent", workflow);
        Assert.DoesNotContain("importEvent.VideoTimestamp.TotalMilliseconds > 0", workflow);
    }

    [Fact]
    public void Player_manual_photo_aligns_event_time_to_current_frame_before_snapshot()
    {
        var coding = ReadCodingPartials();
        var workflow = ReadUiFile("Ai", "CodingTakePhotoCommandWorkflow.cs");
        var photoBody = ExtractMethodBody(coding, "private void CodingTakePhotoForSelectedEvent");

        var timeIndex = workflow.IndexOf("actions.GetCurrentPlayerTimestamp()", StringComparison.Ordinal);
        var scopeIndex = workflow.IndexOf("actions.ApplyPhotoTimestamp", StringComparison.Ordinal);
        var snapshotIndex = workflow.IndexOf("actions.CaptureSnapshot(entry)", StringComparison.Ordinal);

        Assert.True(timeIndex >= 0, "Manuelles Foto muss den aktuellen Player-Zeitpunkt lesen.");
        Assert.True(scopeIndex >= 0, "Befund- und Event-Zeit muessen vor dem Snapshot per Scope auf den Foto-Frame gesetzt werden.");
        Assert.True(snapshotIndex >= 0, "Manuelles Foto muss weiter den aktuellen Frame capturen.");
        Assert.True(scopeIndex < snapshotIndex, "Dateiname und Befund muessen den Foto-Zeitpunkt verwenden.");
        Assert.Contains("CodingTakePhotoCommandWorkflow.Execute", coding);
        Assert.Contains("GetCurrentPlayerTimestamp: GetCurrentPlayerTimestamp", coding);
        Assert.Contains("CodingEventPhotoTimestampScope.Apply", coding);
        Assert.Contains("CaptureSnapshot: CodingCaptureSnapshot", coding);
        Assert.Contains("restoreOriginalTime()", workflow);
        Assert.DoesNotContain("entry.Zeit = photoTime.Value", coding);
        Assert.DoesNotContain("codingEvent.VideoTimestamp = photoTime.Value", coding);
        Assert.Contains("CodingEventPhotoApplier.Apply", coding);
    }

    [Fact]
    public void Player_coding_side_panel_exposes_protocol_match_controls()
    {
        var sidePanel = ReadUiFile("Views", "Windows", "PlayerCodingSidePanel.xaml");
        var sidePanelCode = ReadUiFile("Views", "Windows", "PlayerCodingSidePanel.xaml.cs");
        var accessors = ReadUiFile("Views", "Windows", "PlayerWindow.CodingSidePanelAccessors.cs");
        var eventBinder = ReadUiFile("Views", "Windows", "PlayerCodingSidePanelEventBinder.cs");

        Assert.Contains("x:Name=\"BtnRunCodingProtocolMatch\"", sidePanel);
        Assert.Contains("x:Name=\"TxtCodingProtocolMatchSummary\"", sidePanel);
        Assert.Contains("x:Name=\"BtnAcceptGreenCodingMatches\"", sidePanel);
        Assert.Contains("x:Name=\"CodingMatchBadge\"", sidePanel);
        Assert.Contains("x:Name=\"TxtCodingMatchBadge\"", sidePanel);
        Assert.Contains("CodingProtocolMatchRequested", sidePanelCode);
        Assert.Contains("CodingAcceptGreenMatchesRequested", sidePanelCode);
        Assert.Contains("BtnRunCodingProtocolMatch", accessors);
        Assert.Contains("TxtCodingProtocolMatchSummary", accessors);
        Assert.Contains("BtnAcceptGreenCodingMatches", accessors);
        Assert.Contains("PlayerCodingSidePanelEventBinder.Bind", accessors);
        Assert.Contains("sidePanel.CodingProtocolMatchRequested += handlers.CodingProtocolMatch", eventBinder);
        Assert.Contains("sidePanel.CodingAcceptGreenMatchesRequested += handlers.CodingAcceptGreenMatches", eventBinder);
    }

    [Fact]
    public void Player_runs_coding_protocol_match_from_import_and_ki_events()
    {
        var coding = ReadCodingPartials();
        var workflow = ReadUiFile("Ai", "CodingProtocolMatchCommandWorkflow.cs");
        var runBody = ExtractMethodBody(coding, "private void RunCodingProtocolMatch()");

        Assert.Contains("using AuswertungPro.Next.Application.Ai.Evaluation;", coding);
        Assert.Contains("private CodingProtocolMatchStateController _codingProtocolMatchState => _codingProtocolStates.ProtocolMatchState", coding);
        Assert.Contains("_codingProtocolMatchState.Buckets", runBody);
        Assert.Contains("StoreMatch: _codingProtocolMatchState.Store", runBody);
        Assert.Contains("CodingProtocolMatchCommandWorkflow.Execute", runBody);
        Assert.Contains("CodingProtocolMatchRunner.Run", runBody);
        Assert.DoesNotContain("CodingProtocolMatchService.Match", runBody);
        Assert.DoesNotContain("_codingImportEvents.Select(ev => ev.Entry).ToList()", runBody);
        Assert.DoesNotContain("_codingVm.Events.Select(ev => ev.Entry).ToList()", runBody);
        Assert.DoesNotContain("CodingProtocolMatchBucketBuilder.Rebuild", runBody);
        Assert.DoesNotContain("if (!_codingSessionHost.HasViewModel) return", runBody);
        Assert.Contains("if (!request.HasCodingViewModel)", workflow);
        Assert.Contains("actions.RunMatch()", workflow);
        Assert.Contains("actions.StoreMatch(routing)", workflow);
        Assert.Contains("actions.UpdateSummary(routing)", workflow);
        Assert.Contains("actions.RefreshEvents()", workflow);
        Assert.Contains("actions.ScheduleHighlights()", workflow);
        Assert.Contains("CodingProtocolMatchHighlightControls.Apply", coding);
        Assert.Contains(
            "CodingProtocolMatchDisplayPolicy.BadgeText",
            ReadUiFile("Ai", "CodingProtocolMatchHighlightControls.cs"));
    }

    [Fact]
    public void Player_green_match_training_button_reuses_import_confirm_core()
    {
        var coding = ReadCodingPartials();
        var importConfirmBody = ExtractMethodBody(coding, "private async Task HandleImportConfirmAsync");
        var greenBody = ExtractMethodBody(coding, "private async Task HandleCodingAcceptGreenMatchesAsync");
        var coreBody = ExtractMethodBody(coding, "private async Task<bool> ConfirmImportAsTrainingAsync");
        var acceptGreenCommandWorkflow = ReadUiFile("Ai", "CodingAcceptGreenMatchesCommandWorkflow.cs");
        var importConfirmCommandWorkflow = ReadUiFile("Ai", "CodingImportConfirmCommandWorkflow.cs");
        var importTrainingResultWorkflow = ReadUiFile("Ai", "CodingImportTrainingResultWorkflow.cs");
        var confirmationWorkflow = ReadUiFile("Ai", "CodingProtocolImportTrainingConfirmationWorkflow.cs");
        var workflow = ReadUiFile("Ai", "CodingProtocolImportTrainingWorkflowService.cs");
        var workflowFactory = ReadUiFile("Ai", "CodingProtocolImportTrainingWorkflowServiceFactory.cs");

        Assert.Contains("CodingImportConfirmCommandWorkflow.ExecuteAsync", importConfirmBody);
        Assert.DoesNotContain("LstImportEvents.SelectedItem is not CodingEvent", importConfirmBody);
        Assert.Contains("request.SelectedItem is not CodingEvent", importConfirmCommandWorkflow);
        Assert.Contains("actions.ConfirmImportAsTrainingAsync(importEvent)", importConfirmCommandWorkflow);
        Assert.Contains("CodingAcceptGreenMatchesCommandWorkflow.ExecuteAsync", greenBody);
        Assert.Contains("CodingProtocolGreenMatchTrainingRunner.AcceptGreenMatchesAsync", greenBody);
        Assert.DoesNotContain("if (!_codingSessionHost.HasViewModel) return", greenBody);
        Assert.DoesNotContain("if (_lastCodingMatch == null)", greenBody);
        Assert.Contains("if (!request.HasCodingViewModel)", acceptGreenCommandWorkflow);
        Assert.Contains("actions.RunProtocolMatch()", acceptGreenCommandWorkflow);
        Assert.Contains("routing = actions.GetCurrentRouting()", acceptGreenCommandWorkflow);
        Assert.Contains("actions.AcceptGreenMatchesAsync(routing)", acceptGreenCommandWorkflow);
        Assert.Contains("actions.ShowOverlay(overlay.Value)", acceptGreenCommandWorkflow);
        Assert.DoesNotContain("_lastCodingMatch.Trainingskandidaten", greenBody);
        Assert.DoesNotContain("CodingProtocolTrainingCandidateResolver.ResolveImportEvents", greenBody);
        Assert.DoesNotContain("_codingImportEvents.FirstOrDefault", greenBody);
        Assert.DoesNotContain("foreach (var importEvent", greenBody);
        Assert.DoesNotContain("CodingProtocolImportTrainingWorkflowServiceFactory.Create", coreBody);
        Assert.DoesNotContain("new CodingProtocolImportTrainingConfirmationWorkflowActions", coreBody);
        Assert.Contains("CodingProtocolImportTrainingWorkflowServiceFactory.Create", confirmationWorkflow);
        Assert.Contains("new CodingProtocolImportTrainingConfirmationWorkflowActions", confirmationWorkflow);
        Assert.Contains("CodingImportTrainingResultWorkflow.Execute", coreBody);
        Assert.DoesNotContain("if (!result.Accepted)", coreBody);
        Assert.DoesNotContain("var badge = result.Badge", coreBody);
        Assert.Contains("if (!importResult.Accepted)", importTrainingResultWorkflow);
        Assert.Contains("actions.ShowBadge(badge.Text)", importTrainingResultWorkflow);
        Assert.Contains("actions.ScheduleHideBadge(badge.AutoHideDelay)", importTrainingResultWorkflow);
        Assert.DoesNotContain("TeacherAnnotationStore.AppendAsync(annotation)", coreBody);
        Assert.Contains("await _seekAndWait(importEvent)", workflow);
        Assert.Contains("await _appendAnnotation(annotation)", workflow);
        Assert.Contains("TeacherAnnotationStore.AppendAsync", workflowFactory);
    }

    [Fact]
    public void Player_protocol_match_badges_are_not_limited_to_visible_list_rows()
    {
        var sidePanel = ReadUiFile("Views", "Windows", "PlayerCodingSidePanel.xaml");

        Assert.Equal(2, CountOccurrences(sidePanel, "VirtualizingPanel.IsVirtualizing=\"False\""));
        Assert.Equal(2, CountOccurrences(sidePanel, "ScrollViewer.CanContentScroll=\"False\""));
    }

    private static string ReadUiFile(params string[] relativeParts)
    {
        var root = FindRepoRoot();
        var path = Path.Combine(new[] { root, "src", "AuswertungPro.Next.UI" }.Concat(relativeParts).ToArray());
        return File.ReadAllText(path);
    }

    private static string ReadCodingPartials()
    {
        var root = FindRepoRoot();
        var dir = Path.Combine(root, "src", "AuswertungPro.Next.UI", "Views", "Windows");
        var files = Directory.GetFiles(dir, "PlayerWindow.Coding*.cs")
            .Where(path => !Path.GetFileName(path).Contains("SidePanelAccessors", StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal);

        return string.Join(Environment.NewLine, files.Select(File.ReadAllText));
    }

    private static void AssertAnalyzedFrameAttachedBeforeAddEvent(string methodBody)
    {
        var attachIndex = FirstIndexOf(
            methodBody,
            "AttachAnalyzedFramePhoto(entry)",
            "AttachAnalyzedFramePhoto(draft.Entry)",
            "attachAnalyzedFramePhoto(draft.Entry)",
            "actions.AttachAnalyzedFramePhoto(draft.Entry)");
        var addIndex = FirstIndexOf(
            methodBody,
            "codingSessionService.AddEvent(entry)",
            "codingSessionService.AddEvent(draft.Entry)",
            "addEvent(draft.Entry)",
            "CodingMultiModelEventAppender.Apply");

        Assert.True(attachIndex >= 0, "KI-Befunde muessen den analysierten Frame in FotoPaths speichern.");
        Assert.True(addIndex >= 0, "Test erwartet AddEvent im KI-Befundpfad.");
        Assert.True(attachIndex < addIndex, "Der Frame muss vor AddEvent am ProtocolEntry haengen.");
    }

    private static int FirstIndexOf(string source, params string[] patterns)
    {
        return patterns
            .Select(pattern => source.IndexOf(pattern, StringComparison.Ordinal))
            .Where(index => index >= 0)
            .DefaultIfEmpty(-1)
            .Min();
    }

    private static void AssertBoundaryFrameAttachedBeforeAddEvent(string methodBody)
    {
        var attachIndex = FirstIndexOf(
            methodBody,
            "AttachBoundaryAnalyzedFramePhoto(entry, analyzedFrameBytes)",
            "AttachBoundaryAnalyzedFramePhoto(draft.Entry, analyzedFrameBytes)",
            "actions.AttachBoundaryAnalyzedFramePhoto(draft.Entry, frameBytes)",
            "actions.AttachBoundaryAnalyzedFramePhoto(draft.Entry, request.AnalyzedFrameBytes)");
        var addIndex = FirstIndexOf(
            methodBody,
            "_codingSessionService.AddEvent(entry)",
            "_codingSessionService.AddEvent(draft.Entry)",
            "CodingBoundaryEventAppender.Apply");

        Assert.True(attachIndex >= 0, "Auto-BCD/BCE muessen ihren eigenen analysierten Frame bekommen.");
        Assert.True(addIndex >= 0, "Test erwartet AddEvent im Boundary-Pfad.");
        Assert.True(attachIndex < addIndex, "Boundary-Frame muss vor AddEvent am ProtocolEntry haengen.");
    }

    private static string ExtractMethodBody(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Method signature not found: {signature}");

        var braceStart = source.IndexOf('{', start);
        Assert.True(braceStart >= 0, $"Method body not found: {signature}");

        var depth = 0;
        for (var i = braceStart; i < source.Length; i++)
        {
            if (source[i] == '{')
                depth++;
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0)
                    return source[braceStart..(i + 1)];
            }
        }

        throw new InvalidDataException($"Method body not closed: {signature}");
    }

    private static int CountOccurrences(string source, string needle)
    {
        var count = 0;
        var start = 0;
        while (true)
        {
            var index = source.IndexOf(needle, start, StringComparison.Ordinal);
            if (index < 0)
                return count;

            count++;
            start = index + needle.Length;
        }
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AuswertungPro.sln")))
                return current.FullName;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repo root with AuswertungPro.sln was not found.");
    }
}

