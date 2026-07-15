using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using AuswertungPro.Next.UI.Player;
using AuswertungPro.Next.UI.Views.Windows;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowLiveDetectionMarkingArchitectureTests
{
    [Fact]
    public void PlayerWindow_manual_mark_bbox_mapping_lives_in_mapper()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var segmentationControllerPath = Path.Combine(uiRoot, "Player", "LiveDetectionMarkSegmentationController.cs");
        var mapperPath = Path.Combine(uiRoot, "Ai", "LiveDetectionGeometryMapper.cs");

        var segmentationController = File.ReadAllText(segmentationControllerPath);
        var mapper = File.ReadAllText(mapperPath);

        Assert.Contains("LiveDetectionGeometryMapper.BBoxFromOverlay", segmentationController);
        AssertNoForbiddenTokens(segmentationController, "NormalizedBoundingBox.FromPoints");
        Assert.Contains("public static NormalizedBoundingBox BBoxFromOverlay", mapper);
    }

    [Fact]
    public void PlayerWindow_mark_box_quantification_mapping_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var segmentationControllerPath = Path.Combine(uiRoot, "Player", "LiveDetectionMarkSegmentationController.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingMarkBoxQuantificationOverlayPolicy.cs");

        Assert.True(File.Exists(policyPath), "SAM-Quantifizierung-zu-Overlay-Mapping muss ausserhalb der PlayerWindow-Partials liegen.");

        var segmentationController = File.ReadAllText(segmentationControllerPath);
        var policy = File.ReadAllText(policyPath);

        Assert.Contains("CodingMarkBoxQuantificationOverlayPolicy.Apply", segmentationController);
        AssertNoForbiddenTokens(
            segmentationController,
            "result.Quant.HeightMm.HasValue",
            "double.TryParse(result.Quant.ClockPosition");
        Assert.Contains("public static void Apply", policy);
        Assert.Contains("quantification.CrossSectionReductionPercent", policy);
    }

    [Fact]
    public void PlayerWindow_mark_segmentation_lives_in_controller()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var windowRootPath = Path.Combine(windowsRoot, "PlayerWindow.xaml.cs");
        var markingPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Marking.cs");
        var oldSegmentationPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Marking.Segmentation.cs");
        var segmentationControllerPath = Path.Combine(uiRoot, "Player", "LiveDetectionMarkSegmentationController.cs");
        var maskOverlayControllerPath = Path.Combine(uiRoot, "Player", "CodingSamMaskOverlayController.cs");
        var segmentWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionMarkBoxSegmentationWorkflow.cs");
        var renderWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionMarkSamMaskRenderWorkflow.cs");
        var controllerField = typeof(PlayerWindow).GetField(
            "_liveDetectionMarkSegmentationController",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var playerWindowMethodNames = typeof(PlayerWindow)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Select(method => method.Name)
            .ToArray();

        Assert.False(File.Exists(oldSegmentationPath), "SAM-Segmentierung darf nicht als PlayerWindow-Partial zurueckkehren.");
        Assert.True(File.Exists(segmentationControllerPath), "SAM-Segmentierung und Maskensteuerung sollen in einem eigenen Controller liegen.");
        Assert.True(File.Exists(maskOverlayControllerPath), "SAM-Maskenrendering soll ueber einen Player-Controller laufen.");
        Assert.True(File.Exists(segmentWorkflowPath), "SAM-Segmentierungsentscheidung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(renderWorkflowPath), "SAM-Masken-Renderentscheidung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.NotNull(controllerField);
        Assert.Equal(typeof(ILiveDetectionMarkSegmentationController), controllerField.FieldType);

        var windowRoot = File.ReadAllText(windowRootPath);
        var marking = File.ReadAllText(markingPath);
        var segmentationController = File.ReadAllText(segmentationControllerPath);
        var maskOverlayController = File.Exists(maskOverlayControllerPath) ? File.ReadAllText(maskOverlayControllerPath) : "";
        var segmentWorkflow = File.Exists(segmentWorkflowPath) ? File.ReadAllText(segmentWorkflowPath) : "";
        var renderWorkflow = File.Exists(renderWorkflowPath) ? File.ReadAllText(renderWorkflowPath) : "";

        AssertNoForbiddenTokens(
            marking,
            "private async Task<Infrastructure.Ai.Pipeline.BoxSegmentationResult?> TrySegmentMarkBoxAsync",
            "private void ShowMarkSamMask");
        Assert.DoesNotContain("TrySegmentMarkBoxAsync", playerWindowMethodNames);
        Assert.DoesNotContain("ShowMarkSamMask", playerWindowMethodNames);
        Assert.Contains("_liveDetectionMarkSegmentationController.TrySegmentAsync", marking);
        Assert.Contains("_liveDetectionMarkSegmentationController.ShowMask", marking);
        Assert.Contains("public interface ILiveDetectionMarkSegmentationController", segmentationController);
        Assert.Contains("LiveDetectionMarkBoxSegmentationWorkflow.ExecuteAsync", segmentationController);
        Assert.Contains("LiveDetectionMarkSamMaskRenderWorkflow.Execute", segmentationController);
        Assert.Contains("CodingMarkBoxQuantificationOverlayPolicy.Apply", segmentationController);
        Assert.Contains("CodingSamMaskOverlayController.RenderMasks", windowRoot);
        AssertNoForbiddenTokens(
            segmentationController,
            "var result = await boxSegmentation.SegmentBoxAsync",
            "new Infrastructure.Ai.Pipeline.SamResponse",
            "Ai.Pipeline.SamMaskRenderer.RenderMasks");
        Assert.Contains("SamMaskRenderer.RenderMasks", maskOverlayController);
        Assert.Contains("CodingBendMarkerOverlayController.Show", windowRoot);
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
        var helperPath = Path.Combine(uiRoot, "Player", "PlayerManualMarkPlayback.cs");
        var controllerPath = Path.Combine(uiRoot, "Player", "LiveDetectionMarkToolController.cs");
        var activationWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionManualMarkActivationWorkflow.cs");
        var catalogOpenWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionMarkCatalogOpenWorkflow.cs");
        var markCatalogPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.LiveDetection.Marking.Catalog.cs");

        Assert.True(File.Exists(helperPath), "Manuelle Markier-Pause soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(activationWorkflowPath), "Manuelle Markier-Pause soll im Aktivierungsworkflow orchestriert werden.");
        Assert.True(File.Exists(catalogOpenWorkflowPath), "Katalog-Oeffnen soll die manuelle Markier-Pause ausserhalb von PlayerWindow orchestrieren.");

        var helper = File.ReadAllText(helperPath);
        var controller = File.ReadAllText(controllerPath);
        var activationWorkflow = File.Exists(activationWorkflowPath) ? File.ReadAllText(activationWorkflowPath) : "";
        var catalogOpenWorkflow = File.Exists(catalogOpenWorkflowPath) ? File.ReadAllText(catalogOpenWorkflowPath) : "";
        var markCatalog = File.ReadAllText(markCatalogPath);

        Assert.Contains("public static class PlayerManualMarkPlayback", helper);
        Assert.Contains("PauseForManualMarking", helper);
        Assert.Contains("PlayerManualMarkPlayback.PauseForManualMarking", activationWorkflow);
        Assert.Contains("PlayerManualMarkPlayback.PauseForManualMarking", catalogOpenWorkflow);
        AssertNoForbiddenTokens(
            markCatalog,
            "PlayerManualMarkPlayback.PauseForManualMarking",
            "_player.SetPause(true)",
            "_player.SetPause(false)");
        AssertNoForbiddenTokens(
            controller,
            "PlayerManualMarkPlayback.PauseForManualMarking",
            "_player.SetPause(true)",
            "_player.SetPause(false)");
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

        AssertNoForbiddenTokens(
            marking,
            "private void DetectionCanvas_MouseLeftButtonDown",
            "private void OnFindingClicked",
            "private void OpenCodeCatalogForMark");
        Assert.Contains("private void DetectionCanvas_MouseLeftButtonDown", catalog);
        Assert.Contains("private void OnFindingClicked", catalog);
        Assert.Contains("private void OpenCodeCatalogForMark", catalog);
        Assert.Contains("LiveDetectionMarkCatalogDisplayWorkflow.TryOpen", catalog);
        AssertNoForbiddenTokens(
            catalog,
            "LiveDetectionMarkCatalogWorkflowServiceFactory.Create",
            "LiveDetectionGeometryMapper.ClickToClockPosition",
            "CodingExplorerEntryFactory.CreateSeed");
        Assert.Contains("LiveDetectionMarkCatalogOpenWorkflow.ExecuteCanvasClick", catalog);
        Assert.Contains("LiveDetectionMarkCatalogOpenWorkflow.ExecuteFindingClick", catalog);
        Assert.Contains("LiveDetectionGeometryMapper.ClickToClockPosition", openWorkflow);
        Assert.Contains("CodingExplorerEntryFactory.CreateSeed", workflow);
        Assert.Contains("LiveDetectionMarkCatalogWorkflowServiceFactory.Create", displayWorkflow);
        Assert.Contains("service.TryOpen(", displayWorkflow);
        Assert.Contains("VsaCodeExplorerDialogServiceFactory.Create", workflowFactory);
        Assert.Contains("LiveDetectionDialogServiceFactory.Create", workflowFactory);
    }

    [Fact]
    public void PlayerWindow_mark_drawing_completion_uses_fire_and_forget_wrapper()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var markingPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Marking.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionManualMarkCompletionCommandWorkflow.cs");

        Assert.True(File.Exists(workflowPath), "Manual-Mark-Completion-Orchestrierung soll ausserhalb der PlayerWindow-Partials liegen.");
        var marking = File.ReadAllText(markingPath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";

        AssertNoForbiddenTokens(
            marking,
            "private async void HandleMarkDrawingComplete",
            "if (overlay == null)",
            "catch (Exception ex)",
            "Task.Delay(3000)");
        Assert.Contains("private void HandleMarkDrawingComplete", marking);
        Assert.Contains(".SafeFireAndForget(\"MarkDrawingComplete\")", marking);
        Assert.Contains("private async Task HandleMarkDrawingCompleteAsync", marking);
        Assert.Contains("LiveDetectionManualMarkCompletionCommandWorkflow.ExecuteAsync", marking);
        Assert.Contains("actions.GetCurrentOverlay()", workflow);
        Assert.Contains("actions.SegmentMarkAsync(overlay, frameBytes)", workflow);
        Assert.Contains("DelayAfterSegmentPreviewAsync", workflow);
        Assert.Contains("actions.SaveTrainingAsync(overlay, timestampSec, clockPosition, frameBytes)", workflow);
        Assert.Contains("actions.CompleteManualMark(saved)", workflow);
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
        AssertNoForbiddenTokens(
            marking,
            "if (saved && !_isCodingMode)",
            "_codingOverlayToolHost.SetActiveTool(_markToolType);");
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
        var controllerPath = Path.Combine(uiRoot, "Player", "LiveDetectionManualMarkTrainingController.cs");
        var controllerSetFactoryPath = Path.Combine(uiRoot, "Player", "LiveDetectionTrainingControllerSetFactory.cs");
        var playerWindowPath = Path.Combine(windowsRoot, "PlayerWindow.xaml.cs");

        Assert.True(File.Exists(trainingPath), "Manual-Mark-Training-Speicherung soll aus dem grossen Marking-Partial heraus.");
        Assert.True(File.Exists(appenderPath), "Manual-Mark-Session-Anlage soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(frameExporterPath), "Manual-Mark-Training soll den bestehenden FrameExporter fuer Tempframe-I/O nutzen.");
        Assert.True(File.Exists(annotationWriterPath), "Manual-Mark-Training soll den bestehenden AnnotationWriter nutzen.");
        Assert.True(File.Exists(seedSelectionWorkflowPath), "Manual-Mark-Codeauswahl soll den Code-Explorer ausserhalb der PlayerWindow-Partials orchestrieren.");
        Assert.True(File.Exists(commandWorkflowPath), "Manual-Mark-Training-Befehl soll Auswahl, Speichern, Ergebnis und Fehler ausserhalb der PlayerWindow-Partials orchestrieren.");
        Assert.True(File.Exists(workflowPath), "Manual-Mark-Training-Ablauf soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(resultWorkflowPath), "Manual-Mark-Training-Ergebnisbehandlung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(controllerPath), "Manual-Mark-Training-Steuerung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(controllerSetFactoryPath), "Detection-Trainingscontroller sollen ausserhalb von PlayerWindow aufgebaut werden.");

        var marking = File.ReadAllText(markingPath);
        var training = File.ReadAllText(trainingPath);
        var appender = File.Exists(appenderPath) ? File.ReadAllText(appenderPath) : "";
        var frameExporter = File.ReadAllText(frameExporterPath);
        var annotationWriter = File.ReadAllText(annotationWriterPath);
        var seedSelectionWorkflow = File.Exists(seedSelectionWorkflowPath) ? File.ReadAllText(seedSelectionWorkflowPath) : "";
        var commandWorkflow = File.Exists(commandWorkflowPath) ? File.ReadAllText(commandWorkflowPath) : "";
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";
        var resultWorkflow = File.Exists(resultWorkflowPath) ? File.ReadAllText(resultWorkflowPath) : "";
        var controller = File.ReadAllText(controllerPath);
        var controllerSetFactory = File.ReadAllText(controllerSetFactoryPath);
        var playerWindow = File.ReadAllText(playerWindowPath);

        AssertNoForbiddenTokens(
            marking,
            "private async Task<bool> SaveMarkAsTrainingAsync",
            "TrainingAnnotationExportServiceFactory.Create");
        Assert.Contains("private async Task<bool> SaveMarkAsTrainingAsync", training);
        Assert.Contains("_liveDetectionManualMarkTrainingController.SaveAsync", training);
        AssertNoForbiddenTokens(
            training,
            "if (selectedEntry == null)",
            "catch (Exception ex)",
            "if (!result.Saved)",
            "result.Code",
            "LiveDetectionManualMarkEventAppender.Apply",
            "CodingProtocolEntryPhotoPathAppender.AddIfPresent",
            ".SelectSeed(",
            "CodingCodeExplorerWorkflowServiceFactory.Create",
            "_codingSessionService.AddEvent(manualEntry",
            "new LiveDetectionTrainingFrameExporter",
            "TrainingAnnotationExportServiceFactory.Create",
            "VsaYoloClassMap.GetClassId",
            "TeacherAnnotationStore.AppendAsync",
            "File.WriteAllBytesAsync",
            "File.Delete(tempFrame)",
            "Path.GetTempPath",
            "LiveDetectionTeacherAnnotationFactory.CreateManualMark",
            "LiveDetectionTrainingAnnotationWriter.CreateDefault",
            "LiveDetectionManualMarkTrainingCommandWorkflow.ExecuteAsync",
            "CodingCodeExplorerSeedSelectionWorkflow.Execute",
            "LiveDetectionManualMarkTrainingWorkflow.SaveAsync",
            "LiveDetectionManualMarkTrainingResultWorkflow.Execute",
            "_codingSessionHost");
        Assert.Contains("public sealed class LiveDetectionManualMarkTrainingController", controller);
        Assert.Contains("ILiveDetectionTrainingAnnotationWriter", controller);
        Assert.Contains("LiveDetectionManualMarkTrainingCommandWorkflow.ExecuteAsync", controller);
        Assert.Contains("LiveDetectionManualMarkTrainingWorkflow.SaveAsync", controller);
        Assert.Contains("LiveDetectionManualMarkTrainingResultWorkflow.Execute", controller);
        Assert.Contains("_annotationWriter.SaveManualMarkAsync", controller);
        Assert.DoesNotContain("LiveDetectionTrainingAnnotationWriter.CreateDefault", controller);
        Assert.Contains("LiveDetectionTrainingControllerSetFactory.Create", playerWindow);
        Assert.DoesNotContain("LiveDetectionTrainingAnnotationWriter.CreateDefault", playerWindow);
        Assert.DoesNotContain("new LiveDetectionManualMarkTrainingController", playerWindow);
        Assert.Equal(1, CountOccurrences(controllerSetFactory, "LiveDetectionTrainingAnnotationWriter.CreateDefault()"));
        Assert.Contains("new LiveDetectionConfirmationTrainingController", controllerSetFactory);
        Assert.Contains("new LiveDetectionManualMarkTrainingController", controllerSetFactory);
        Assert.True(
            CountOccurrences(controllerSetFactory, "annotationWriter,") >= 2,
            "Bestaetigung und manuelle Markierung sollen denselben Trainings-Schreiber erhalten.");
        Assert.Contains(".SelectSeed(", seedSelectionWorkflow);
        Assert.Contains("actions.SelectEntry()", commandWorkflow);
        Assert.Contains("actions.SaveTrainingAsync(selectedEntry)", commandWorkflow);
        Assert.Contains("actions.HandleTrainingResult(trainingResult)", commandWorkflow);
        Assert.Contains("actions.ShowOsdMeterStatus", commandWorkflow);
        Assert.Contains("CodingExplorerEntryFactory.CreateManualFromSelected", appender);
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
    public void PlayerWindow_mark_tool_wiring_lives_in_controller()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var windowRootPath = Path.Combine(windowsRoot, "PlayerWindow.xaml.cs");
        var markingPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Marking.cs");
        var markToolsPath = Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.MarkTools.cs");
        var statePath = Path.Combine(windowsRoot, "PlayerWindow.State.cs");
        var controlsPath = Path.Combine(uiRoot, "Player", "PlayerMarkToolControls.cs");
        var controllerPath = Path.Combine(uiRoot, "Player", "LiveDetectionMarkToolController.cs");
        var liveDetectionControllerPath = Path.Combine(uiRoot, "Player", "LiveDetectionController.cs");
        var activationWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionManualMarkActivationWorkflow.cs");
        var overlayReadyWorkflowPath = Path.Combine(uiRoot, "Ai", "LiveDetectionMarkOverlayReadyWorkflow.cs");
        var controllerField = typeof(PlayerWindow).GetField(
            "_liveDetectionMarkToolController",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var playerWindowMethodNames = typeof(PlayerWindow)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Select(method => method.Name)
            .ToArray();

        Assert.False(File.Exists(markToolsPath), "Markierwerkzeug-Wiring darf nicht als PlayerWindow-Partial zurueckkehren.");
        Assert.True(File.Exists(controllerPath), "Markierwerkzeug-Wiring soll in einem eigenen Controller liegen.");
        Assert.True(File.Exists(controlsPath), "Markierwerkzeug-UI-Zustand soll in einem Player-Controller gekapselt sein.");
        Assert.True(File.Exists(activationWorkflowPath), "Markierwerkzeug-Aktivierungsentscheidung soll ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(overlayReadyWorkflowPath), "Markier-Overlay-Bereitstellung soll ausserhalb von PlayerWindow entschieden werden.");
        Assert.NotNull(controllerField);
        Assert.Equal(typeof(ILiveDetectionMarkToolController), controllerField.FieldType);

        var windowRoot = File.ReadAllText(windowRootPath);
        var marking = File.ReadAllText(markingPath);
        var state = File.ReadAllText(statePath);
        var controls = File.ReadAllText(controlsPath);
        var controller = File.ReadAllText(controllerPath);
        var liveDetectionController = File.ReadAllText(liveDetectionControllerPath);
        var activationWorkflow = File.Exists(activationWorkflowPath) ? File.ReadAllText(activationWorkflowPath) : "";
        var overlayReadyWorkflow = File.Exists(overlayReadyWorkflowPath) ? File.ReadAllText(overlayReadyWorkflowPath) : "";

        AssertNoForbiddenTokens(
            marking,
            "private void ActivateMarkTool",
            "private void EnsureMarkOverlayReady",
            "private void DeactivateMarkTool");
        AssertNoForbiddenTokens(
            controller,
            "private OverlayToolType _markToolType",
            "MarkToolPopup.IsOpen",
            "ToolsDropdownPopup.IsOpen",
            "TxtMarkToolName.Text",
            "DetectionCanvas.Cursor",
            "CodingOverlayPopup.IsOpen",
            "CodingOverlayCanvas.IsHitTestVisible",
            "if (tool == OverlayToolType.Point)",
            "if (_codingOverlayRuntimeOwner.HasService && _codingSessionHost.HasViewModel) return;",
            "CodingSessionStateFactory.Create",
            "CodingSessionServiceFactory.Create",
            "new OverlayToolService",
            "new ViewModels.Windows.CodingSessionViewModel",
            "CodingFeedbackRecorder");
        Assert.Contains("_codingSessionHost", marking);
        Assert.Contains("_liveDetectionMarkToolController.Activate", marking);
        Assert.Contains("_liveDetectionMarkToolController.Deactivate", marking);
        Assert.Contains("public interface ILiveDetectionMarkToolController", controller);
        Assert.Contains("LiveDetectionManualMarkActivationWorkflow.Execute", controller);
        Assert.Contains("LiveDetectionMarkOverlayReadyWorkflow.Execute", controller);
        Assert.Contains("LiveDetectionManualMarkDeactivationWorkflow.Execute", controller);
        Assert.DoesNotContain("ActivateMarkTool", playerWindowMethodNames);
        Assert.DoesNotContain("EnsureMarkOverlayReady", playerWindowMethodNames);
        Assert.DoesNotContain("DeactivateMarkTool", playerWindowMethodNames);
        AssertNoForbiddenTokens(
            state,
            "private OverlayToolType _markToolType",
            "private bool _isManualMarkMode");
        Assert.Contains("OverlayToolType MarkToolType", liveDetectionController);
        Assert.Contains("bool IsManualMarkMode", liveDetectionController);
        Assert.Contains("new LiveDetectionMarkToolController", windowRoot);
        Assert.Contains("_markToolControls.BeginActivation", windowRoot);
        Assert.Contains("_markToolControls.ActivatePointTool", windowRoot);
        Assert.Contains("_markToolControls.OpenCodingOverlay", windowRoot);
        Assert.Contains("_markToolControls.DeactivateDetectionSide", windowRoot);
        Assert.Contains("OverlayToolType.Point", activationWorkflow);
        Assert.Contains("PlayerManualMarkPlayback.PauseForManualMarking", activationWorkflow);
        Assert.Contains("CodingSessionStateFactory.Create", overlayReadyWorkflow);
        Assert.Contains("new LiveDetectionMarkOverlayReadyStateRequest", windowRoot);
        Assert.DoesNotContain("CodingSessionStateFactory.Create", windowRoot);
        Assert.Contains("if (request.HasOverlayService && request.HasViewModel)", overlayReadyWorkflow);
        Assert.Contains("actions.CreateState()", overlayReadyWorkflow);
        Assert.Contains("actions.SetSessionService(state.SessionService)", overlayReadyWorkflow);
        Assert.Contains("actions.SetOverlayService(state.OverlayService)", overlayReadyWorkflow);
        Assert.Contains("actions.SetViewModel(state.ViewModel)", overlayReadyWorkflow);
        Assert.Contains("public sealed class PlayerMarkToolControls", controls);
        Assert.Contains("_markToolPopup.IsOpen", controls);
        Assert.Contains("_detectionCanvas.Cursor", controls);
    }

    private static void AssertNoForbiddenTokens(string source, params string[] forbiddenTokens)
    {
        var hits = new List<string>();
        foreach (var token in forbiddenTokens)
        {
            if (source.Contains(token, StringComparison.Ordinal))
                hits.Add(token);
        }

        Assert.True(
            hits.Count == 0,
            "Verbotene alte PlayerWindow-LiveDetection-Markierlogik gefunden: " + string.Join(", ", hits));
    }

    private static int CountOccurrences(string source, string value)
        => (source.Length - source.Replace(value, "", StringComparison.Ordinal).Length) / value.Length;
}
