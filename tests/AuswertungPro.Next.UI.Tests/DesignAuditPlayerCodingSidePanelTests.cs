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
        var suspendBody = ExtractMethodBody(coding, "private void SuspendCodingOverlayInput()");

        Assert.Contains("HideCodingOverlayForExternalWindow", window);
        Assert.Contains("CodingOverlayPopup.IsOpen = false", coding);
        Assert.Contains("RestoreCodingOverlayAfterExternalWindow", window);
        Assert.DoesNotContain("CodingOverlayPopup.IsOpen = false", suspendBody);
        Assert.Contains("CodingOverlayCanvas.IsHitTestVisible = false", suspendBody);
    }

    [Fact]
    public void Player_uses_same_overlay_policy_for_rendering_and_events()
    {
        var coding = ReadCodingPartials();
        var summary = ReadUiFile("Ai", "CodingMultiModelFindingSummary.cs");

        Assert.Contains("CodingMultiModelFindingSummary.Build(segmented, mmResult)", coding);
        Assert.Contains("CodingSegmentedFindingVisibility.BuildVisibleCodingFindings", summary);
        Assert.Contains("SamMaskRenderer.RenderCandidates", coding);
        Assert.Contains("findingSummary.VisibleCodierbar", coding);
        Assert.DoesNotContain("AddMultiModelFindingsAsEvents(\r\n                    segmented.Where(s => s.Proximity.IsCodierbar).ToList()", coding);
    }

    [Fact]
    public void Player_renders_ahead_segment_masks_without_coding_them()
    {
        var coding = ReadCodingPartials();
        var summary = ReadUiFile("Ai", "CodingMultiModelFindingSummary.cs");
        var showBody = ExtractMethodBody(coding, "private void ShowMultiModelResults");

        Assert.Contains("CodingSegmentedFindingVisibility.BuildVisibleMaskRenderCandidates", coding);
        Assert.Contains("CodingSegmentedFindingVisibility.BuildVisibleMaskRenderCandidates(segmented)", showBody);
        Assert.Contains("CodingSegmentedFindingVisibility.BuildVisibleCodingFindings(segmented)", summary);
        Assert.Contains("AddMultiModelFindingsAsEvents(", coding);
        Assert.Contains("findingSummary.VisibleCodierbar", coding);
    }

    [Fact]
    public void Player_status_mentions_background_masks_suppressed()
    {
        var coding = ReadCodingPartials();
        var summary = ReadUiFile("Ai", "CodingMultiModelFindingSummary.cs");

        Assert.Contains("CodingSegmentedFindingVisibility.BuildOverlaySuppressionText", summary);
        Assert.Contains("findingSummary.TimingText", coding);
    }

    [Fact]
    public void Player_coding_detail_shows_large_ai_evidence_preview()
    {
        var sidePanel = ReadUiFile("Views", "Windows", "PlayerCodingSidePanel.xaml");
        var accessors = ReadUiFile("Views", "Windows", "PlayerWindow.CodingSidePanelAccessors.cs");
        var coding = ReadCodingPartials();
        var previewService = ReadUiFile("Ai", "CodingInlineEvidencePreviewService.cs");

        Assert.Contains("x:Name=\"ImgInlineEvidencePreview\"", sidePanel);
        Assert.Contains("x:Name=\"TxtInlineEvidencePreviewStatus\"", sidePanel);
        Assert.Contains("ImgInlineEvidencePreview", accessors);
        Assert.Contains("CodingInlineEvidencePreviewService.Build", coding);
        Assert.Contains("CodingDefectPreviewService.BuildPreviewImagePath", previewService);
    }

    [Fact]
    public void Player_coding_detail_uses_open_decision_policy_for_confirm_buttons()
    {
        var coding = ReadCodingPartials();
        var detailBody = ExtractMethodBody(coding, "private void UpdateInlineDefectDetail");
        var policy = ReadUiFile("Ai", "CodingDefectStatusDisplayPolicy.cs");

        Assert.Contains("CodingDefectStatusDisplayPolicy.BuildInlineDetail(ev)", detailBody);
        Assert.Contains("CodingSessionViewModel.CanActOnDefect(ev)", policy);
        Assert.Contains("BtnInlineAccept.Visibility = state.CanAct ? Visibility.Visible : Visibility.Collapsed", detailBody);
        Assert.Contains("BtnInlineReject.Visibility = state.CanAct ? Visibility.Visible : Visibility.Collapsed", detailBody);
    }

    [Fact]
    public void Player_photo_window_shows_segmented_evidence_preview_before_raw_photos()
    {
        var coding = ReadCodingPartials();
        var photoBody = ExtractMethodBody(coding, "private void CodingEventShowPhotos_Click");
        var policy = ReadUiFile("Ai", "CodingPhotoDisplayPathPolicy.cs");
        var loader = ReadUiFile("Ai", "CodingPhotoViewerImageSourceLoader.cs");
        var viewerWorkflowFactory = ReadUiFile("Ai", "CodingPhotoViewerWorkflowServiceFactory.cs");
        var viewerService = ReadUiFile("Ai", "CodingPhotoViewerWindowService.cs");

        Assert.Contains("CodingPhotoViewerWorkflowServiceFactory.Create", photoBody);
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
        var runBody = ExtractMethodBody(coding, "private async Task RunCodingAnalysisAsync");

        var stopIndex = runBody.IndexOf("IsCodingAfterTerminalBoundary", StringComparison.Ordinal);
        var captureIndex = runBody.IndexOf("await CaptureSnapshotAsync(_codingAnalysisCts.Token)", StringComparison.Ordinal);

        Assert.True(stopIndex >= 0, "RunCodingAnalysisAsync muss nach BCE/BDC stoppen.");
        Assert.True(captureIndex >= 0, "RunCodingAnalysisAsync muss weiterhin Frames mit Analyse-Cancellation capturen koennen.");
        Assert.True(stopIndex < captureIndex, "Stop-Pruefung muss vor Snapshot/SAM laufen.");
        Assert.Contains("CodingDedupPolicy.ShouldStopAnalysisAfterTerminalCode", coding);
    }

    [Fact]
    public void Player_defers_spatial_bogen_before_creating_protocol_event()
    {
        var coding = ReadCodingPartials();
        var policy = ReadUiFile("Ai", "CodingMultiModelFindingAddDecisionPolicy.cs");
        var addBody = ExtractMethodBody(coding, "private void AddMultiModelFindingsAsEvents");

        Assert.Contains("CodingDedupPolicy.ShouldDeferSpatialCodeUntilCloser", policy);
        Assert.Contains("CodingMultiModelFindingAddDecisionPolicy.Decide", addBody);
        Assert.True(
            addBody.IndexOf("CodingMultiModelFindingAddDecisionPolicy.Decide", StringComparison.Ordinal)
            < FirstIndexOf(
                addBody,
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
        Assert.Contains("_detectionPendingFrameBytes", persistBody);
        Assert.DoesNotContain("CaptureFrameBytesAtRequiredPhotoTimeAsync", persistBody);
        Assert.DoesNotContain("Rohranfang-Foto nach Datenblendung nicht verfuegbar", persistBody);
    }

    [Fact]
    public void Player_coding_analysis_keeps_analyzed_frame_for_gold_snapshot()
    {
        var coding = ReadCodingPartials();
        var multiModelBody = ExtractMethodBody(coding, "private async Task RunCodingMultiModelAnalysisAsync");

        Assert.Contains("_detectionPendingFrameBytes = pngBytes", multiModelBody);
        Assert.Contains("_detectionPendingTimestampSec = captureTimestampSec", multiModelBody);
        Assert.True(
            multiModelBody.IndexOf("_detectionPendingFrameBytes = pngBytes", StringComparison.Ordinal)
            < multiModelBody.IndexOf("TryHandleBoundaryClassifierResult", StringComparison.Ordinal),
            "Der Gold-Snapshot muss den analysierten Frame bekommen, bevor ein BCD/BCE-Event entstehen kann.");
    }

    [Fact]
    public void Player_ai_findings_attach_analyzed_frame_photo_before_add_event()
    {
        var coding = ReadCodingPartials();
        var qwenBody = ExtractMethodBody(coding, "private void AddAiFindingsAsEvents");
        var multiModelBody = ExtractMethodBody(coding, "private void AddMultiModelFindingsAsEvents");

        AssertAnalyzedFrameAttachedBeforeAddEvent(qwenBody);
        AssertAnalyzedFrameAttachedBeforeAddEvent(multiModelBody);
        Assert.Contains("CodingMultiModelEventAppender.Apply", multiModelBody);
        Assert.DoesNotContain("codingSessionService.AddEvent(draft.Entry)", multiModelBody);
    }

    [Fact]
    public void Player_boundary_classifier_passes_current_analyzed_frame_to_boundary_events()
    {
        var coding = ReadCodingPartials();
        var boundaryBody = ExtractMethodBody(coding, "private bool TryHandleBoundaryClassifierResult");

        Assert.Contains("EnsureRohranfangExists(meter, videoTime, _detectionPendingFrameBytes, ref anyAdded)", boundaryBody);
        Assert.Contains("EnsureRohrendeExists(_codingVm.EndMeter, videoTime, _detectionPendingFrameBytes)", boundaryBody);
    }

    [Fact]
    public void Player_handles_structural_classifier_before_no_detection_return()
    {
        var coding = ReadCodingPartials();
        var multiModelBody = ExtractMethodBody(coding, "private async Task RunCodingMultiModelAnalysisAsync");
        var structuralBody = ExtractMethodBody(coding, "private bool TryHandleStructuralClassifierResult");

        var boundaryIndex = multiModelBody.IndexOf("TryHandleBoundaryClassifierResult", StringComparison.Ordinal);
        var structuralIndex = multiModelBody.IndexOf("TryHandleStructuralClassifierResult", StringComparison.Ordinal);
        var noDetectionIndex = multiModelBody.IndexOf("!mmResult.IsRelevant || !mmResult.HasDetections", StringComparison.Ordinal);

        Assert.True(boundaryIndex >= 0, "Boundary-Classifier muss zuerst behandelt werden.");
        Assert.True(structuralIndex > boundaryIndex, "BCA/BCC darf BCD/BCE nicht ueberholen.");
        Assert.True(noDetectionIndex > structuralIndex, "BCA/BCC muss vor dem YOLO/DINO-No-Detection-Abbruch behandelt werden.");
        Assert.Contains("CodingClassifierDisplayPolicy.IsStructuralClassifierCode(code)", structuralBody);
        Assert.Contains("CodingStructuralClassifierEventFactory.Create", structuralBody);
        Assert.Contains("CodingStructuralClassifierEventAppender.Apply", structuralBody);
        Assert.DoesNotContain("codingSessionService.AddEvent(draft.Entry)", structuralBody);

        var clearIndex = structuralBody.IndexOf("ClearDetectionOverlays()", StringComparison.Ordinal);
        var listIndex = structuralBody.IndexOf("CodingFindingsList.ItemsSource", StringComparison.Ordinal);
        Assert.True(clearIndex >= 0 && listIndex > clearIndex,
            "Die Befundliste muss nach dem Overlay-Clear gesetzt werden, sonst verschwindet der Bogen-Hinweis.");
    }

    [Fact]
    public void Player_exit_coding_mode_passes_current_analyzed_frame_to_auto_rohrende()
    {
        var coding = ReadCodingPartials();
        var exitBody = ExtractMethodBody(coding, "private void ExitCodingMode");

        Assert.Contains("EnsureRohrendeExists(_codingVm.EndMeter, endTime, _detectionPendingFrameBytes)", exitBody);
    }

    [Fact]
    public void Player_coding_analysis_prefers_video_position_over_stale_viewmodel_meter_for_classifier()
    {
        var coding = ReadCodingPartials();
        var multiModelBody = ExtractMethodBody(coding, "private async Task RunCodingMultiModelAnalysisAsync");
        var resolveBody = ExtractMethodBody(coding, "private double ResolveCodingMeterForFrame");

        var meterStart = multiModelBody.IndexOf("var currentMeterForClassifier", StringComparison.Ordinal);
        var resolveIndex = multiModelBody.IndexOf("ResolveCodingMeterForFrame(captureTimestampSec, frameOsdMeter)", meterStart, StringComparison.Ordinal);
        var resolverIndex = resolveBody.IndexOf("CodingMeterResolver.Resolve", StringComparison.Ordinal);
        var viewModelMeterIndex = resolveBody.IndexOf("_codingVm?.CurrentMeter", StringComparison.Ordinal);

        Assert.True(meterStart >= 0, "Analyse muss einen Meter fuer den Klassifikator bestimmen.");
        Assert.True(resolveIndex >= 0, "Der Klassifikator muss den gemeinsamen Frame-Meter-Resolver verwenden.");
        Assert.Contains("CodingMultiModelClassifierInputPolicy.Build", multiModelBody);
        Assert.True(resolverIndex >= 0, "Video-Positions-Fallback muss im ausgelagerten Meter-Resolver liegen.");
        Assert.True(viewModelMeterIndex >= 0, "ViewModel-Meter darf nur als spaeter Fallback genutzt werden.");
        Assert.True(
            resolverIndex < viewModelMeterIndex,
            "Staler CurrentMeter=0 darf die echte Videoposition nicht ueberstimmen, sonst blockiert BCD die Pipeline.");
    }

    [Fact]
    public void Player_ai_events_use_analyzed_frame_meter_not_stale_selected_meter()
    {
        var coding = ReadCodingPartials();
        var runBody = ExtractMethodBody(coding, "private async Task RunCodingAnalysisAsync");
        var multiModelBody = ExtractMethodBody(coding, "private void AddMultiModelFindingsAsEvents");
        var qwenBody = ExtractMethodBody(coding, "private void AddAiFindingsAsEvents");
        var boundaryBody = ExtractMethodBody(coding, "private bool TryHandleBoundaryClassifierResult");

        Assert.Contains("ResolveCodingMeterForFrame(captureTimestampSec", runBody);
        Assert.Contains("ResolveCodingMeterForFrame(captureTimestampSec", multiModelBody);
        Assert.Contains("ResolveCodingMeterForFrame(result.TimestampSeconds, result.MeterReading", qwenBody);
        Assert.Contains("ResolveCodingMeterForFrame(captureTimestampSec", boundaryBody);

        Assert.DoesNotContain("double meter = _codingLastOsdMeter ?? codingVm.CurrentMeter", multiModelBody);
        Assert.DoesNotContain("double meter = _codingLastOsdMeter ?? codingVm.CurrentMeter", qwenBody);
        Assert.DoesNotContain("var meter = _codingLastOsdMeter ?? _codingVm.CurrentMeter", boundaryBody);
    }

    [Fact]
    public void Player_reads_osd_meter_from_analyzed_frame_before_multimodel_detection()
    {
        var coding = ReadCodingPartials();
        var osdService = ReadUiFile("Ai", "CodingOsdMeterService.cs");
        var runBody = ExtractMethodBody(coding, "private async Task RunCodingAnalysisAsync");
        var multiModelBody = ExtractMethodBody(coding, "private async Task RunCodingMultiModelAnalysisAsync");
        var readerBody = ExtractMethodBody(coding, "private async Task<double?> TryReadAnalyzedFrameOsdMeterAsync");
        var helperBody = ExtractMethodBody(coding, "private async Task<double?> TryReadOsdMeterFromFrameBytesAsync");

        var osdReadIndex = multiModelBody.IndexOf("TryReadAnalyzedFrameOsdMeterAsync", StringComparison.Ordinal);
        var classifierIndex = multiModelBody.IndexOf("var currentMeterForClassifier", StringComparison.Ordinal);
        var addIndex = multiModelBody.IndexOf("AddMultiModelFindingsAsEvents(", StringComparison.Ordinal);

        Assert.True(osdReadIndex >= 0, "Multi-Model muss den OSD-Meter aus exakt dem analysierten Frame lesen.");
        Assert.True(classifierIndex >= 0, "Test erwartet den Klassifikator-Meter im Multi-Model-Pfad.");
        Assert.True(osdReadIndex < classifierIndex, "OSD-Meter muss vor Klassifikator/Boundary-Logik vorliegen.");
        Assert.Contains("frameOsdMeter", multiModelBody);
        Assert.Contains("frameOsdMeter", multiModelBody[addIndex..]);
        Assert.Contains("result = result with { MeterReading = frameOsdMeter }", runBody);
        Assert.Contains("TryReadOsdMeterFromFrameBytesAsync", readerBody);
        Assert.Contains("CodingOsdMeterService", helperBody);
        Assert.Contains("CodingOsdMeterReader.BuildOsdSearchImage", osdService);
        Assert.Contains("CodingOsdMeterReader.AcceptMeterCandidate", osdService);
    }

    [Fact]
    public void Player_auto_boundary_events_attach_passed_frame_before_add_event()
    {
        var coding = ReadCodingPartials();
        var bcdBody = ExtractMethodBody(coding, "private void EnsureRohranfangExists");
        var bceBody = ExtractMethodBody(coding, "private void EnsureRohrendeExists");

        AssertBoundaryFrameAttachedBeforeAddEvent(bcdBody);
        AssertBoundaryFrameAttachedBeforeAddEvent(bceBody);
    }

    [Fact]
    public void Player_event_seek_allows_zero_timestamp()
    {
        var coding = ReadCodingPartials();
        var seekBody = ExtractMethodBody(coding, "private void CodingEventSeek_Click");

        Assert.Contains("CodingEventSeekPolicy.TryGetSeekMilliseconds(codingEvent", seekBody);
        Assert.DoesNotContain("codingEvent.VideoTimestamp.TotalMilliseconds > 0", seekBody);
    }

    [Fact]
    public void Player_import_seek_allows_zero_timestamp()
    {
        var coding = ReadCodingPartials();
        var seekBody = ExtractMethodBody(coding, "private void SeekToImportEvent");

        Assert.Contains("CodingEventSeekPolicy.TryGetSeekMilliseconds(importEvent", seekBody);
        Assert.DoesNotContain("importEvent.VideoTimestamp.TotalMilliseconds > 0", seekBody);
    }

    [Fact]
    public void Player_manual_photo_aligns_event_time_to_current_frame_before_snapshot()
    {
        var coding = ReadCodingPartials();
        var photoBody = ExtractMethodBody(coding, "private void CodingTakePhotoForSelectedEvent");

        var timeIndex = photoBody.IndexOf("GetCurrentPlayerTimestamp()", StringComparison.Ordinal);
        var scopeIndex = photoBody.IndexOf("CodingEventPhotoTimestampScope.Apply", StringComparison.Ordinal);
        var snapshotIndex = photoBody.IndexOf("CodingCaptureSnapshot(entry)", StringComparison.Ordinal);

        Assert.True(timeIndex >= 0, "Manuelles Foto muss den aktuellen Player-Zeitpunkt lesen.");
        Assert.True(scopeIndex >= 0, "Befund- und Event-Zeit muessen vor dem Snapshot per Scope auf den Foto-Frame gesetzt werden.");
        Assert.True(snapshotIndex >= 0, "Manuelles Foto muss weiter den aktuellen Frame capturen.");
        Assert.True(scopeIndex < snapshotIndex, "Dateiname und Befund muessen den Foto-Zeitpunkt verwenden.");
        Assert.Contains("photoTimestamp.RestoreOriginalTime()", photoBody);
        Assert.DoesNotContain("entry.Zeit = photoTime.Value", photoBody);
        Assert.DoesNotContain("codingEvent.VideoTimestamp = photoTime.Value", photoBody);
        Assert.Contains("CodingEventPhotoApplier.Apply", photoBody);
    }

    [Fact]
    public void Player_coding_side_panel_exposes_protocol_match_controls()
    {
        var sidePanel = ReadUiFile("Views", "Windows", "PlayerCodingSidePanel.xaml");
        var sidePanelCode = ReadUiFile("Views", "Windows", "PlayerCodingSidePanel.xaml.cs");
        var accessors = ReadUiFile("Views", "Windows", "PlayerWindow.CodingSidePanelAccessors.cs");

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
        Assert.Contains("CodingSidePanelControl.CodingProtocolMatchRequested += RunCodingProtocolMatch_Click", accessors);
        Assert.Contains("CodingSidePanelControl.CodingAcceptGreenMatchesRequested += CodingAcceptGreenMatches_Click", accessors);
    }

    [Fact]
    public void Player_runs_coding_protocol_match_from_import_and_ki_events()
    {
        var coding = ReadCodingPartials();
        var runBody = ExtractMethodBody(coding, "private void RunCodingProtocolMatch()");

        Assert.Contains("using AuswertungPro.Next.Application.Ai.Evaluation;", coding);
        Assert.Contains("private CodingMatchRouting? _lastCodingMatch", coding);
        Assert.Contains("private readonly Dictionary<Guid, CodingProtocolMatchBucket>", coding);
        Assert.Contains("CodingProtocolMatchRunner.Run", runBody);
        Assert.DoesNotContain("CodingProtocolMatchService.Match", runBody);
        Assert.DoesNotContain("_codingImportEvents.Select(ev => ev.Entry).ToList()", runBody);
        Assert.DoesNotContain("_codingVm.Events.Select(ev => ev.Entry).ToList()", runBody);
        Assert.DoesNotContain("CodingProtocolMatchBucketBuilder.Rebuild", runBody);
        Assert.Contains("UpdateCodingProtocolMatchSummary(_lastCodingMatch)", runBody);
        Assert.Contains("RefreshCodingEventsList()", runBody);
        Assert.Contains("CodingProtocolMatchDisplayPolicy.BadgeText", coding);
    }

    [Fact]
    public void Player_green_match_training_button_reuses_import_confirm_core()
    {
        var coding = ReadCodingPartials();
        var importConfirmBody = ExtractMethodBody(coding, "private async Task HandleImportConfirmAsync");
        var greenBody = ExtractMethodBody(coding, "private async Task HandleCodingAcceptGreenMatchesAsync");
        var coreBody = ExtractMethodBody(coding, "private async Task<bool> ConfirmImportAsTrainingAsync");
        var workflow = ReadUiFile("Ai", "CodingProtocolImportTrainingWorkflowService.cs");
        var workflowFactory = ReadUiFile("Ai", "CodingProtocolImportTrainingWorkflowServiceFactory.cs");

        Assert.Contains("await ConfirmImportAsTrainingAsync(importEvent)", importConfirmBody);
        Assert.Contains("CodingProtocolGreenMatchTrainingRunner.AcceptGreenMatchesAsync", greenBody);
        Assert.DoesNotContain("_lastCodingMatch.Trainingskandidaten", greenBody);
        Assert.DoesNotContain("CodingProtocolTrainingCandidateResolver.ResolveImportEvents", greenBody);
        Assert.DoesNotContain("_codingImportEvents.FirstOrDefault", greenBody);
        Assert.DoesNotContain("foreach (var importEvent", greenBody);
        Assert.Contains("CodingProtocolImportTrainingWorkflowServiceFactory.Create", coreBody);
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
        if (methodBody.Contains("CodingLiveFindingSessionAppender.Append", StringComparison.Ordinal))
        {
            Assert.Contains("AttachAnalyzedFramePhoto(entry)", methodBody);
            return;
        }

        var attachIndex = FirstIndexOf(
            methodBody,
            "AttachAnalyzedFramePhoto(entry)",
            "AttachAnalyzedFramePhoto(draft.Entry)");
        var addIndex = FirstIndexOf(
            methodBody,
            "codingSessionService.AddEvent(entry)",
            "codingSessionService.AddEvent(draft.Entry)",
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
            "AttachBoundaryAnalyzedFramePhoto(draft.Entry, analyzedFrameBytes)");
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

