using System.IO;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowLiveDetectionMarkingArchitectureTests
{
    [Fact]
    public void PlayerWindow_manual_mark_bbox_mapping_lives_in_mapper()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var segmentationPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.LiveDetection.Marking.Segmentation.cs");
        var mapperPath = Path.Combine(uiRoot, "Ai", "LiveDetectionGeometryMapper.cs");

        var segmentation = File.ReadAllText(segmentationPath);
        var mapper = File.ReadAllText(mapperPath);

        Assert.Contains("LiveDetectionGeometryMapper.BBoxFromOverlay", segmentation);
        Assert.DoesNotContain("NormalizedBoundingBox.FromPoints", segmentation);
        Assert.Contains("public static NormalizedBoundingBox BBoxFromOverlay", mapper);
    }

    [Fact]
    public void PlayerWindow_mark_box_quantification_mapping_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var segmentationPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.LiveDetection.Marking.Segmentation.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingMarkBoxQuantificationOverlayPolicy.cs");

        Assert.True(File.Exists(policyPath), "SAM-Quantifizierung-zu-Overlay-Mapping muss ausserhalb der PlayerWindow-Partials liegen.");

        var segmentation = File.ReadAllText(segmentationPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingMarkBoxQuantificationOverlayPolicy.Apply", segmentation);
        Assert.DoesNotContain("result.Quant.HeightMm.HasValue", segmentation);
        Assert.DoesNotContain("double.TryParse(result.Quant.ClockPosition", segmentation);
        Assert.Contains("public static void Apply", policy);
        Assert.Contains("quantification.CrossSectionReductionPercent", policy);
    }

    [Fact]
    public void PlayerWindow_mark_segmentation_lives_in_segmentation_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var markingPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Marking.cs");
        var segmentationPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Marking.Segmentation.cs");
        var controllerPath = Path.Combine(uiRoot, "Player", "CodingSamMaskOverlayController.cs");
        var segmentWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionMarkBoxSegmentationWorkflow.cs");
        var renderWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionMarkSamMaskRenderWorkflow.cs");

        Assert.True(File.Exists(segmentationPath), "SAM-Segmentierung und Maskenrendering sollen aus dem Marking-Orchestrator heraus.");
        Assert.True(File.Exists(controllerPath), "SAM-Maskenrendering soll ueber einen Player-Controller laufen.");
        Assert.True(File.Exists(segmentWorkflowPath), "SAM-Segmentierungsentscheidung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(renderWorkflowPath), "SAM-Masken-Renderentscheidung soll ausserhalb der PlayerWindow-Partials liegen.");

        var marking = File.ReadAllText(markingPath);
        var segmentation = File.ReadAllText(segmentationPath);
        var controller = File.Exists(controllerPath) ? File.ReadAllText(controllerPath) : "";
        var segmentWorkflow = File.Exists(segmentWorkflowPath) ? File.ReadAllText(segmentWorkflowPath) : "";
        var renderWorkflow = File.Exists(renderWorkflowPath) ? File.ReadAllText(renderWorkflowPath) : "";

        Assert.DoesNotContain("private async Task<Infrastructure.Ai.Pipeline.BoxSegmentationResult?> TrySegmentMarkBoxAsync", marking);
        Assert.DoesNotContain("private void ShowMarkSamMask", marking);
        Assert.Contains("private async Task<Infrastructure.Ai.Pipeline.BoxSegmentationResult?> TrySegmentMarkBoxAsync", segmentation);
        Assert.Contains("private void ShowMarkSamMask", segmentation);
        Assert.Contains("LiveDetectionMarkBoxSegmentationWorkflow.ExecuteAsync", segmentation);
        Assert.Contains("LiveDetectionMarkSamMaskRenderWorkflow.Execute", segmentation);
        Assert.Contains("CodingMarkBoxQuantificationOverlayPolicy.Apply", segmentation);
        Assert.Contains("CodingSamMaskOverlayController.RenderMasks", segmentation);
        Assert.DoesNotContain("var result = await boxSegmentation.SegmentBoxAsync", segmentation);
        Assert.DoesNotContain("new Infrastructure.Ai.Pipeline.SamResponse", segmentation);
        Assert.DoesNotContain("Ai.Pipeline.SamMaskRenderer.RenderMasks", segmentation);
        Assert.Contains("SamMaskRenderer.RenderMasks", controller);
        Assert.Contains("CodingBendMarkerOverlayController.Show", segmentation);
        Assert.Contains("actions.SegmentBoxAsync", segmentWorkflow);
        Assert.Contains("actions.ApplyQuantification", segmentWorkflow);
        Assert.Contains("actions.RenderMasks", renderWorkflow);
        Assert.Contains("BendMarkerShown", renderWorkflow);
    }

    [Fact]
    public void PlayerWindow_live_detection_marking_playback_uses_player_helper()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var helperPath = Path.Combine(uiRoot, "Player", "PlayerManualMarkPlayback.cs");
        var activationWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionManualMarkActivationWorkflow.cs");
        var catalogOpenWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionMarkCatalogOpenWorkflow.cs");
        var markToolsPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.MarkTools.cs");
        var markCatalogPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Marking.Catalog.cs");

        Assert.True(File.Exists(helperPath), "Manuelle Markier-Pause soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(activationWorkflowPath), "Manuelle Markier-Pause soll im Aktivierungsworkflow orchestriert werden.");
        Assert.True(File.Exists(catalogOpenWorkflowPath), "Katalog-Oeffnen soll die manuelle Markier-Pause ausserhalb von PlayerWindow orchestrieren.");

        var helper = File.ReadAllText(helperPath);
        var activationWorkflow = File.Exists(activationWorkflowPath) ? File.ReadAllText(activationWorkflowPath) : "";
        var catalogOpenWorkflow = File.Exists(catalogOpenWorkflowPath) ? File.ReadAllText(catalogOpenWorkflowPath) : "";
        var markTools = File.ReadAllText(markToolsPath);
        var markCatalog = File.ReadAllText(markCatalogPath);

        Assert.Contains("public static class PlayerManualMarkPlayback", helper);
        Assert.Contains("PauseForManualMarking", helper);
        Assert.Contains("PlayerManualMarkPlayback.PauseForManualMarking", activationWorkflow);
        Assert.Contains("PlayerManualMarkPlayback.PauseForManualMarking", catalogOpenWorkflow);
        Assert.DoesNotContain("PlayerManualMarkPlayback.PauseForManualMarking", markCatalog);
        Assert.DoesNotContain("PlayerManualMarkPlayback.PauseForManualMarking", markTools);
        Assert.DoesNotContain("_player.SetPause(true)", markTools);
        Assert.DoesNotContain("_player.SetPause(false)", markTools);
        Assert.DoesNotContain("_player.SetPause(true)", markCatalog);
        Assert.DoesNotContain("_player.SetPause(false)", markCatalog);
    }

    [Fact]
    public void PlayerWindow_live_detection_mark_catalog_lives_in_catalog_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var markingPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Marking.cs");
        var catalogPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Marking.Catalog.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionMarkCatalogWorkflowService.cs");
        var workflowFactoryPath = Path.Combine(uiRoot, "Ai", "LiveDetectionMarkCatalogWorkflowServiceFactory.cs");
        var displayWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionMarkCatalogDisplayWorkflow.cs");
        var openWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionMarkCatalogOpenWorkflow.cs");

        Assert.True(File.Exists(catalogPath), "LiveDetection-Markkatalog-Wiring soll aus dem grossen Marking-Partial heraus.");
        Assert.True(File.Exists(workflowPath), "LiveDetection-Markkatalog-Workflow soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(workflowFactoryPath), "LiveDetection-Markkatalog-Workflow soll ueber Factory verdrahtet werden.");
        Assert.True(File.Exists(displayWorkflowPath), "LiveDetection-Markkatalog-Serviceaufruf soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(openWorkflowPath), "LiveDetection-Markkatalog-Oeffnen soll ausserhalb von PlayerWindow entschieden werden.");

        var marking = File.ReadAllText(markingPath);
        var catalog = File.ReadAllText(catalogPath);
        var workflow = File.ReadAllText(workflowPath);
        var workflowFactory = File.ReadAllText(workflowFactoryPath);
        var displayWorkflow = File.Exists(displayWorkflowPath) ? File.ReadAllText(displayWorkflowPath) : "";
        var openWorkflow = File.Exists(openWorkflowPath) ? File.ReadAllText(openWorkflowPath) : "";

        Assert.DoesNotContain("private void DetectionCanvas_MouseLeftButtonDown", marking);
        Assert.DoesNotContain("private void OnFindingClicked", marking);
        Assert.DoesNotContain("private void OpenCodeCatalogForMark", marking);
        Assert.Contains("private void DetectionCanvas_MouseLeftButtonDown", catalog);
        Assert.Contains("private void OnFindingClicked", catalog);
        Assert.Contains("private void OpenCodeCatalogForMark", catalog);
        Assert.Contains("LiveDetectionMarkCatalogDisplayWorkflow.TryOpen", catalog);
        Assert.DoesNotContain("LiveDetectionMarkCatalogWorkflowServiceFactory.Create", catalog);
        Assert.Contains("LiveDetectionMarkCatalogOpenWorkflow.ExecuteCanvasClick", catalog);
        Assert.Contains("LiveDetectionMarkCatalogOpenWorkflow.ExecuteFindingClick", catalog);
        Assert.DoesNotContain("LiveDetectionGeometryMapper.ClickToClockPosition", catalog);
        Assert.DoesNotContain("CodingExplorerEntryFactory.CreateSeed", catalog);
        Assert.Contains("LiveDetectionGeometryMapper.ClickToClockPosition", openWorkflow);
        Assert.Contains("CodingExplorerEntryFactory.CreateSeed", workflow);
        Assert.Contains("LiveDetectionMarkCatalogWorkflowServiceFactory.Create", displayWorkflow);
        Assert.Contains("service.TryOpen(", displayWorkflow);
        Assert.Contains("VsaCodeExplorerDialogServiceFactory.Create", workflowFactory);
        Assert.Contains("LiveDetectionDialogServiceFactory.Create", workflowFactory);
    }

    [Fact]
    public void PlayerWindow_manual_mark_completion_decision_lives_in_workflow()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var markingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.LiveDetection.Marking.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionManualMarkCompletionWorkflow.cs");

        Assert.True(File.Exists(workflowPath), "Manual-Mark-Abschlussentscheidung soll ausserhalb der PlayerWindow-Partials liegen.");

        var marking = File.ReadAllText(markingPath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";

        Assert.Contains("LiveDetectionManualMarkCompletionWorkflow.Execute", marking);
        Assert.DoesNotContain("if (saved && !_isCodingMode)", marking);
        Assert.DoesNotContain("_codingOverlayToolHost.SetActiveTool(_markToolType);", marking);
        Assert.Contains("ClearSamMasks", workflow);
        Assert.Contains("ClearBendMarker", workflow);
        Assert.Contains("DeactivateMarkTool", workflow);
        Assert.Contains("SetActiveTool", workflow);
    }

    [Fact]
    public void PlayerWindow_manual_mark_training_save_lives_in_training_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var markingPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Marking.cs");
        var trainingPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Marking.Training.cs");
        var appenderPath = Path.Combine(uiRoot, "Ai", "LiveDetectionManualMarkEventAppender.cs");
        var frameExporterPath = Path.Combine(uiRoot, "Ai", "LiveDetectionTrainingFrameExporter.cs");
        var annotationWriterPath = Path.Combine(uiRoot, "Ai", "LiveDetectionTrainingAnnotationWriter.cs");
        var seedSelectionWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingCodeExplorerSeedSelectionWorkflow.cs");
        var commandWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionManualMarkTrainingCommandWorkflow.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionManualMarkTrainingWorkflow.cs");
        var resultWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionManualMarkTrainingResultWorkflow.cs");

        Assert.True(File.Exists(trainingPath), "Manual-Mark-Training-Speicherung soll aus dem grossen Marking-Partial heraus.");
        Assert.True(File.Exists(appenderPath), "Manual-Mark-Session-Anlage soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(frameExporterPath), "Manual-Mark-Training soll den bestehenden FrameExporter fuer Tempframe-I/O nutzen.");
        Assert.True(File.Exists(annotationWriterPath), "Manual-Mark-Training soll den bestehenden AnnotationWriter nutzen.");
        Assert.True(File.Exists(seedSelectionWorkflowPath), "Manual-Mark-Codeauswahl soll den Code-Explorer ausserhalb der PlayerWindow-Partials orchestrieren.");
        Assert.True(File.Exists(commandWorkflowPath), "Manual-Mark-Training-Befehl soll Auswahl, Speichern, Ergebnis und Fehler ausserhalb der PlayerWindow-Partials orchestrieren.");
        Assert.True(File.Exists(workflowPath), "Manual-Mark-Training-Ablauf soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(resultWorkflowPath), "Manual-Mark-Training-Ergebnisbehandlung soll ausserhalb der PlayerWindow-Partials liegen.");

        var marking = File.ReadAllText(markingPath);
        var training = File.ReadAllText(trainingPath);
        var appender = File.Exists(appenderPath) ? File.ReadAllText(appenderPath) : "";
        var frameExporter = File.ReadAllText(frameExporterPath);
        var annotationWriter = File.ReadAllText(annotationWriterPath);
        var seedSelectionWorkflow = File.Exists(seedSelectionWorkflowPath) ? File.ReadAllText(seedSelectionWorkflowPath) : "";
        var commandWorkflow = File.Exists(commandWorkflowPath) ? File.ReadAllText(commandWorkflowPath) : "";
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";
        var resultWorkflow = File.Exists(resultWorkflowPath) ? File.ReadAllText(resultWorkflowPath) : "";

        Assert.DoesNotContain("private async Task<bool> SaveMarkAsTrainingAsync", marking);
        Assert.DoesNotContain("TrainingAnnotationExportServiceFactory.Create", marking);
        Assert.Contains("private async Task<bool> SaveMarkAsTrainingAsync", training);
        Assert.Contains("LiveDetectionManualMarkTrainingCommandWorkflow.ExecuteAsync", training);
        Assert.Contains("CodingCodeExplorerSeedSelectionWorkflow.Execute", training);
        Assert.Contains("LiveDetectionManualMarkTrainingWorkflow.SaveAsync", training);
        Assert.Contains("LiveDetectionManualMarkTrainingResultWorkflow.Execute", training);
        Assert.Contains("_codingSessionHost", training);
        Assert.DoesNotContain("_codingVm", training);
        Assert.DoesNotContain("if (selectedEntry == null)", training);
        Assert.DoesNotContain("catch (Exception ex)", training);
        Assert.DoesNotContain("if (!result.Saved)", training);
        Assert.DoesNotContain("result.Code", training);
        Assert.DoesNotContain("LiveDetectionManualMarkEventAppender.Apply", training);
        Assert.DoesNotContain("CodingProtocolEntryPhotoPathAppender.AddIfPresent", training);
        Assert.DoesNotContain(".SelectSeed(", training);
        Assert.DoesNotContain("CodingCodeExplorerWorkflowServiceFactory.Create", training);
        Assert.DoesNotContain("_codingSessionService.AddEvent(manualEntry", training);
        Assert.Contains(".SelectSeed(", seedSelectionWorkflow);
        Assert.Contains("actions.SelectEntry()", commandWorkflow);
        Assert.Contains("actions.SaveTrainingAsync(selectedEntry)", commandWorkflow);
        Assert.Contains("actions.HandleTrainingResult(trainingResult)", commandWorkflow);
        Assert.Contains("actions.ShowOsdMeterStatus", commandWorkflow);
        Assert.Contains("CodingExplorerEntryFactory.CreateManualFromSelected", appender);
        Assert.Contains("LiveDetectionTrainingAnnotationWriter.CreateDefault", training);
        Assert.DoesNotContain("new LiveDetectionTrainingFrameExporter", training);
        Assert.DoesNotContain("TrainingAnnotationExportServiceFactory.Create", training);
        Assert.DoesNotContain("VsaYoloClassMap.GetClassId", training);
        Assert.DoesNotContain("TeacherAnnotationStore.AppendAsync", training);
        Assert.DoesNotContain("File.WriteAllBytesAsync", training);
        Assert.DoesNotContain("File.Delete(tempFrame)", training);
        Assert.DoesNotContain("Path.GetTempPath", training);
        Assert.DoesNotContain("LiveDetectionTeacherAnnotationFactory.CreateManualMark", training);
        Assert.Contains("LiveDetectionManualMarkEventAppender.Apply", workflow);
        Assert.Contains("CodingProtocolEntryPhotoPathAppender.AddIfPresent", workflow);
        Assert.Contains("saveManualMarkAsync", workflow);
        Assert.Contains("File.WriteAllBytesAsync", frameExporter);
        Assert.Contains("BestEffort.Try", frameExporter);
        Assert.Contains("SaveManualMarkAsync", annotationWriter);
        Assert.Contains("LiveDetectionTeacherAnnotationFactory.CreateManualMark", annotationWriter);
        Assert.Contains("if (!trainingResult.Saved)", resultWorkflow);
        Assert.Contains("actions.ShowOsdMeterStatus($\"\\u2713 {trainingResult.Code} gespeichert\", true)", resultWorkflow);
    }

    [Fact]
    public void PlayerWindow_mark_tool_wiring_lives_in_mark_tools_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var markingPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Marking.cs");
        var markToolsPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.MarkTools.cs");
        var statePath = Path.Combine(windowsRoot, "PlayerWindow.State.cs");
        var controlsPath = Path.Combine(uiRoot, "Player", "PlayerMarkToolControls.cs");
        var liveDetectionControllerPath = Path.Combine(uiRoot, "Player", "LiveDetectionController.cs");
        var activationWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionManualMarkActivationWorkflow.cs");
        var overlayReadyWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionMarkOverlayReadyWorkflow.cs");

        Assert.True(File.Exists(markToolsPath), "Markierwerkzeug-Wiring soll aus dem grossen Marking-Partial heraus.");
        Assert.True(File.Exists(controlsPath), "Markierwerkzeug-UI-Zustand soll in einem Player-Controller gekapselt sein.");
        Assert.True(File.Exists(activationWorkflowPath), "Markierwerkzeug-Aktivierungsentscheidung soll ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(overlayReadyWorkflowPath), "Markier-Overlay-Bereitstellung soll ausserhalb von PlayerWindow entschieden werden.");

        var marking = File.ReadAllText(markingPath);
        var markTools = File.ReadAllText(markToolsPath);
        var state = File.ReadAllText(statePath);
        var controls = File.ReadAllText(controlsPath);
        var liveDetectionController = File.ReadAllText(liveDetectionControllerPath);
        var activationWorkflow = File.Exists(activationWorkflowPath) ? File.ReadAllText(activationWorkflowPath) : "";
        var overlayReadyWorkflow = File.Exists(overlayReadyWorkflowPath) ? File.ReadAllText(overlayReadyWorkflowPath) : "";

        Assert.DoesNotContain("private void ActivateMarkTool", marking);
        Assert.DoesNotContain("private void EnsureMarkOverlayReady", marking);
        Assert.DoesNotContain("private void DeactivateMarkTool", marking);
        Assert.DoesNotContain("private OverlayToolType _markToolType", markTools);
        Assert.DoesNotContain("MarkToolPopup.IsOpen", markTools);
        Assert.DoesNotContain("ToolsDropdownPopup.IsOpen", markTools);
        Assert.DoesNotContain("TxtMarkToolName.Text", markTools);
        Assert.DoesNotContain("DetectionCanvas.Cursor", markTools);
        Assert.DoesNotContain("CodingOverlayPopup.IsOpen", markTools);
        Assert.DoesNotContain("CodingOverlayCanvas.IsHitTestVisible", markTools);
        Assert.Contains("_codingSessionHost", marking);
        Assert.DoesNotContain("_codingVm", marking);
        Assert.Contains("_codingSessionHost.ClearCurrentOverlay", markTools);
        Assert.Contains("_codingSessionHost.HasViewModel", markTools);
        Assert.DoesNotContain("_codingVm.CurrentOverlay = null", markTools);
        Assert.DoesNotContain("_codingOverlayService != null && _codingVm != null", markTools);
        Assert.DoesNotContain("_codingVm", markTools);
        Assert.Contains("private void ActivateMarkTool", markTools);
        Assert.Contains("LiveDetectionManualMarkActivationWorkflow.Execute", markTools);
        Assert.DoesNotContain("if (tool == OverlayToolType.Point)", markTools);
        Assert.Contains("private void EnsureMarkOverlayReady", markTools);
        Assert.Contains("LiveDetectionMarkOverlayReadyWorkflow.Execute", markTools);
        Assert.DoesNotContain("if (_codingOverlayRuntimeOwner.HasService && _codingSessionHost.HasViewModel) return;", markTools);
        Assert.Contains("private void DeactivateMarkTool", markTools);
        Assert.DoesNotContain("private OverlayToolType _markToolType", state);
        Assert.DoesNotContain("private bool _isManualMarkMode", state);
        Assert.Contains("OverlayToolType MarkToolType", liveDetectionController);
        Assert.Contains("bool IsManualMarkMode", liveDetectionController);
        Assert.Contains("_markToolControls.BeginActivation", markTools);
        Assert.Contains("_markToolControls.ActivatePointTool", markTools);
        Assert.Contains("_markToolControls.OpenCodingOverlay", markTools);
        Assert.Contains("_markToolControls.DeactivateDetectionSide", markTools);
        Assert.Contains("OverlayToolType.Point", activationWorkflow);
        Assert.Contains("PlayerManualMarkPlayback.PauseForManualMarking", activationWorkflow);
        Assert.DoesNotContain("CodingSessionStateFactory.Create", markTools);
        Assert.Contains("CodingSessionStateFactory.Create", overlayReadyWorkflow);
        Assert.Contains("if (request.HasOverlayService && request.HasViewModel)", overlayReadyWorkflow);
        Assert.Contains("actions.CreateState()", overlayReadyWorkflow);
        Assert.Contains("actions.SetSessionService(state.SessionService)", overlayReadyWorkflow);
        Assert.Contains("actions.SetOverlayService(state.OverlayService)", overlayReadyWorkflow);
        Assert.Contains("actions.SetViewModel(state.ViewModel)", overlayReadyWorkflow);
        Assert.DoesNotContain("CodingSessionServiceFactory.Create", markTools);
        Assert.DoesNotContain("new OverlayToolService", markTools);
        Assert.DoesNotContain("new ViewModels.Windows.CodingSessionViewModel", markTools);
        Assert.DoesNotContain("CodingFeedbackRecorder", markTools);
        Assert.Contains("public sealed class PlayerMarkToolControls", controls);
        Assert.Contains("_markToolPopup.IsOpen", controls);
        Assert.Contains("_detectionCanvas.Cursor", controls);
    }
}
