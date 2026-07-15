using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowOverlayInputArchitectureTests
{
    [Fact]
    public void PlayerWindow_overlay_measurement_panel_uses_formatter_state()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var overlayPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.OverlayRendering.MeasurementPanel.cs");
        var formatterPath = Path.Combine(uiRoot, "Ai", "CodingOverlayMeasurementFormatter.cs");
        var controlsPath = Path.Combine(uiRoot, "Ai", "CodingMeasurementPanelControls.cs");

        var overlay = File.ReadAllText(overlayPath);
        var formatter = File.ReadAllText(formatterPath);
        var controls = File.Exists(controlsPath) ? File.ReadAllText(controlsPath) : "";

        Assert.Contains("CodingOverlayMeasurementFormatter.BuildPanelState", overlay);
        Assert.Contains("CodingMeasurementPanelControls.Apply", overlay);
        AssertNoForbiddenTokens(
            overlay,
            "overlay.Q1Mm.HasValue ? $\"Q1:",
            "overlay.ToolType == OverlayToolType.Level && overlay.FillPercent.HasValue",
            "TxtCodingQ1.Text",
            "CodingMeasurementPanel.Visibility");
        Assert.Contains("public static CodingOverlayMeasurementPanelState BuildPanelState", formatter);
        Assert.Contains("public static void Apply", controls);
    }

    [Fact]
    public void PlayerWindow_overlay_cursor_decision_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var overlayInputPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.OverlayInput.cs");
        var toolsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.OverlayInput.Tools.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingOverlayCursorPolicy.cs");

        Assert.True(File.Exists(toolsPath), "Overlay-Cursor-Wiring soll im Tool-Partial liegen.");
        Assert.True(File.Exists(policyPath), "Overlay-Cursor-Entscheidung muss ausserhalb der PlayerWindow-Partials liegen.");

        var overlayInput = File.ReadAllText(overlayInputPath);
        var tools = File.ReadAllText(toolsPath);
        var policy = File.ReadAllText(policyPath);

        AssertNoForbiddenTokens(overlayInput, "CodingOverlayCursorPolicy.ShouldUseCrossCursor");
        Assert.Contains("CodingOverlayCursorPolicy.ShouldUseCrossCursor", tools);
        AssertNoForbiddenTokens(overlayInput, "var isInteractive = _codingIsCalibrating");
        AssertNoForbiddenTokens(tools, "var isInteractive = _codingIsCalibrating");
        Assert.Contains("public static bool ShouldUseCrossCursor", policy);
        Assert.Contains("activeTool != OverlayToolType.None", policy);
    }

    [Fact]
    public void PlayerWindow_overlay_input_mouseflow_keeps_only_direct_dependencies()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var overlayInputPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.OverlayInput.cs");

        var overlayInput = File.ReadAllText(overlayInputPath);

        Assert.Contains("using System.Windows.Input;", overlayInput);
        Assert.Contains("using AuswertungPro.Next.Domain.Models;", overlayInput);
        AssertNoForbiddenTokens(
            overlayInput,
            "using System.Collections",
            "using System.Globalization",
            "using System.IO",
            "using System.Threading",
            "AuswertungPro.Next.Application",
            "AuswertungPro.Next.Infrastructure",
            "InfraTeacher");
        Assert.Contains("_codingSessionHost", overlayInput);
    }

    [Fact]
    public void PlayerWindow_multipoint_overlay_input_lives_in_multipoint_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var overlayInputPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.OverlayInput.cs");
        var multiPointPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.OverlayInput.MultiPoint.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingMultiPointOverlayDrawWorkflow.cs");

        Assert.True(File.Exists(multiPointPath), "Multi-Point-OverlayInput soll aus dem allgemeinen Mouseflow heraus.");
        Assert.True(File.Exists(workflowPath), "Multi-Point-Overlay-Zeichenablauf soll ausserhalb der PlayerWindow-Partials orchestriert werden.");

        var overlayInput = File.ReadAllText(overlayInputPath);
        var multiPoint = File.ReadAllText(multiPointPath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";

        AssertNoForbiddenTokens(
            overlayInput,
            "OnCanvasMultiPointClick",
            "OnCanvasMultiPointMove");
        Assert.Contains("private void HandleCodingMultiPointMouseDown", multiPoint);
        Assert.Contains("private bool TryHandleCodingMultiPointMouseMove", multiPoint);
        Assert.Contains("CodingMultiPointOverlayDrawWorkflow.MouseDown", multiPoint);
        Assert.Contains("CodingMultiPointOverlayDrawWorkflow.MouseMove", multiPoint);
        Assert.Contains("_codingSessionHost", multiPoint);
        AssertNoForbiddenTokens(
            multiPoint,
            "OnCanvasMultiPointClick",
            "OnCanvasMultiPointMove");
        Assert.Contains("AddMultiPointOverlayPoint", multiPoint);
        Assert.Contains("UpdateMultiPointOverlayPreview", multiPoint);
        AssertNoForbiddenTokens(
            multiPoint,
            "if (!_codingOverlayToolHost.HasOverlayService",
            "if (_codingOverlayToolHost.DrawPointCount == 0)",
            "if (BtnCodingLiveAi.IsChecked == true");
        Assert.Contains("actions.AddMultiPointOverlayPoint()", workflow);
        Assert.Contains("actions.RenderPreviewOverlay()", workflow);
        Assert.Contains("actions.RenderFinalOverlay()", workflow);
        Assert.Contains("actions.AnalyzeWithOverlayHint()", workflow);
    }

    [Fact]
    public void PlayerWindow_overlay_input_mouseflow_uses_workflow()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var overlayInputPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.OverlayInput.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingOverlayInputMouseWorkflow.cs");

        Assert.True(File.Exists(workflowPath), "Allgemeiner OverlayInput-Mouseflow soll ausserhalb der PlayerWindow-Partials orchestriert werden.");

        var overlayInput = File.ReadAllText(overlayInputPath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";

        Assert.Contains("CodingOverlayInputMouseWorkflow.MouseDown", overlayInput);
        Assert.Contains("CodingOverlayInputMouseWorkflow.MouseMove", overlayInput);
        Assert.Contains("CodingOverlayInputMouseWorkflow.MouseUp", overlayInput);
        AssertNoForbiddenTokens(
            overlayInput,
            "if (_eingabemarkerPhase",
            "if (!_codingOverlayToolHost.HasOverlayService",
            "if (TryStartCodingCalibration",
            "if (_codingOverlayToolHost.ActiveTool",
            "if (TryHandleCodingSchemaMouseDown",
            "if (_codingOverlayToolHost.IsMultiPointTool");
        Assert.Contains("request.EingabemarkerState", workflow);
        Assert.Contains("actions.TryStartCalibration()", workflow);
        Assert.Contains("actions.TryHandleSchemaMouseDown()", workflow);
        Assert.Contains("actions.HandleMultiPointMouseDown()", workflow);
        Assert.Contains("actions.HandleStandardMouseDown()", workflow);
    }

    [Fact]
    public void PlayerWindow_standard_overlay_input_lives_in_standard_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var overlayInputPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.OverlayInput.cs");
        var standardPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.OverlayInput.Standard.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingStandardOverlayDrawWorkflow.cs");

        Assert.True(File.Exists(standardPath), "Standard-2-Punkt-OverlayInput soll aus dem allgemeinen Mouseflow heraus.");
        Assert.True(File.Exists(workflowPath), "Standard-Overlay-Zeichenablauf soll ausserhalb der PlayerWindow-Partials orchestriert werden.");

        var overlayInput = File.ReadAllText(overlayInputPath);
        var standard = File.ReadAllText(standardPath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";

        AssertNoForbiddenTokens(
            overlayInput,
            "OnCanvasMouseDown(norm)",
            "OnCanvasMouseMove(norm)",
            "OnCanvasMouseUp(norm)");
        Assert.Contains("private void HandleCodingStandardMouseDown", standard);
        Assert.Contains("private bool TryHandleCodingStandardMouseMove", standard);
        Assert.Contains("private bool TryHandleCodingStandardMouseUp", standard);
        Assert.Contains("CodingStandardOverlayDrawWorkflow.MouseDown", standard);
        Assert.Contains("CodingStandardOverlayDrawWorkflow.MouseMove", standard);
        Assert.Contains("CodingStandardOverlayDrawWorkflow.MouseUp", standard);
        Assert.Contains("HandleMarkDrawingComplete", standard);
        Assert.Contains("_codingSessionHost", standard);
        AssertNoForbiddenTokens(
            standard,
            "if (!_codingSessionHost.HasViewModel)",
            "if (!_codingOverlayToolHost.HasOverlayService",
            "_ = AnalyzeWithOverlayHintAsync");
        Assert.Contains("AnalyzeWithOverlayHintAsync(_codingSessionHost.CurrentOverlay!)", standard);
        Assert.Contains(".SafeFireAndForget(\"OverlayHint\")", standard);
        Assert.Contains("actions.BeginOverlayDraw()", workflow);
        Assert.Contains("actions.RenderPreviewOverlay()", workflow);
        Assert.Contains("actions.RenderFinalOverlay()", workflow);
        Assert.Contains("actions.HandleMarkDrawingComplete()", workflow);
    }

    [Fact]
    public void PlayerWindow_overlay_input_visibility_lives_in_visibility_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var overlayInputPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.OverlayInput.cs");
        var visibilityPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.OverlayInput.Visibility.cs");
        var playerStatePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.State.cs");
        var lifecycleExitPath = Path.Combine(windowsRoot, "PlayerWindow.xaml.cs");
        var wiringPath = Path.Combine(windowsRoot, "PlayerWindow.Wiring.cs");
        var visibilityWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingOverlayInputVisibilityWorkflow.cs");
        var interactionWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingOverlayInputInteractionWorkflow.cs");
        var stateControllerPath = Path.Combine(uiRoot, "Player", "CodingOverlayInputVisibilityStateController.cs");

        Assert.True(File.Exists(visibilityPath), "Overlay-Suspend/Restore soll aus dem allgemeinen OverlayInput-Partial heraus.");
        Assert.True(File.Exists(visibilityWorkflowPath), "Overlay-Suspend/Restore-Entscheidungen sollen ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(interactionWorkflowPath), "Suspendierte Dialog-/Edit-Interaktionen sollen ihre Resume-Garantie ausserhalb der PlayerWindow-Partials orchestrieren.");
        Assert.True(File.Exists(stateControllerPath), "Overlay-Suspend-Zustand soll ausserhalb der PlayerWindow-Partials liegen.");

        var overlayInput = File.ReadAllText(overlayInputPath);
        var visibility = File.ReadAllText(visibilityPath);
        var playerState = File.ReadAllText(playerStatePath);
        var lifecycleExit = File.ReadAllText(lifecycleExitPath);
        var wiring = File.ReadAllText(wiringPath);
        var visibilityWorkflow = File.Exists(visibilityWorkflowPath) ? File.ReadAllText(visibilityWorkflowPath) : "";
        var interactionWorkflow = File.Exists(interactionWorkflowPath) ? File.ReadAllText(interactionWorkflowPath) : "";
        var stateController = File.Exists(stateControllerPath) ? File.ReadAllText(stateControllerPath) : "";
        var codingPartialsWithoutVisibility = string.Join(
            Environment.NewLine,
            Directory.GetFiles(windowsRoot, "PlayerWindow.Coding*.cs")
                .Where(path => !string.Equals(path, visibilityPath, StringComparison.OrdinalIgnoreCase))
                .Select(File.ReadAllText));

        AssertNoForbiddenTokens(
            overlayInput,
            "private void SuspendCodingOverlayInput",
            "private void ResumeCodingOverlayInput",
            "private void HideCodingOverlayForExternalWindow",
            "private void RestoreCodingOverlayAfterExternalWindow");
        Assert.Contains("private void SuspendCodingOverlayInput", visibility);
        Assert.Contains("CodingOverlayInputVisibilityWorkflow.Suspend", visibility);
        Assert.Contains("CodingOverlayInputVisibilityWorkflow.Resume", visibility);
        Assert.Contains("CodingOverlayInputVisibilityWorkflow.HideForExternalWindow", visibility);
        Assert.Contains("CodingOverlayInputVisibilityWorkflow.RestoreAfterExternalWindow", visibility);
        Assert.Contains("_codingOverlayInputVisibilityState", visibility);
        Assert.Contains("_codingOverlayInputVisibilityState", playerState + lifecycleExit + wiring);
        AssertNoForbiddenTokens(
            playerState,
            "private int _codingOverlaySuspendDepth",
            "private bool _codingOverlayWasOpenBeforeSuspend",
            "private bool _codingOverlayWasOpenBeforeExternalHide",
            "private bool _deactivatedByExternalWindow");
        AssertNoForbiddenTokens(
            visibility,
            "_codingOverlaySuspendDepth++",
            "if (_codingOverlaySuspendDepth > 1)",
            "_codingOverlayWasOpenBeforeExternalHide");
        AssertNoForbiddenTokens(visibility + lifecycleExit + wiring, "_codingOverlaySuspendDepth");
        AssertNoForbiddenTokens(visibility + lifecycleExit, "_codingOverlayWasOpenBeforeSuspend");
        AssertNoForbiddenTokens(wiring, "_deactivatedByExternalWindow");
        Assert.Contains("CodingOverlayInputControls.SuspendCanvas", visibility);
        Assert.Contains("CodingOverlayInputControls.ResumeCanvas", visibility);
        Assert.Contains("_codingSessionHost", visibility);
        AssertNoForbiddenTokens(
            visibility,
            "CodingOverlayCanvas.Visibility = Visibility.Hidden",
            "CodingOverlayCanvas.Visibility = Visibility.Visible",
            "CodingOverlayCanvas.IsHitTestVisible = false",
            "CodingOverlayCanvas.IsHitTestVisible = true");
        Assert.Contains("CodingOverlayInputControls.IsPopupOpen", visibility);
        Assert.Contains("CodingOverlayInputControls.OpenPopup", visibility);
        Assert.Contains("CodingOverlayInputControls.ClosePopup", visibility);
        AssertNoForbiddenTokens(visibility, "CodingOverlayPopup.IsOpen");
        Assert.Contains("private void RestoreCodingOverlayAfterExternalWindow", visibility);
        Assert.Contains("CodingOverlayInputInteractionWorkflow.Run", visibility);
        Assert.Contains("CodingOverlayInputInteractionWorkflow.RunAsync", visibility);
        AssertNoForbiddenTokens(
            codingPartialsWithoutVisibility,
            "SuspendCodingOverlayInput();",
            "ResumeCodingOverlayInput();");
        Assert.Contains("request.SuspendDepth", visibilityWorkflow);
        Assert.Contains("actions.SuspendCanvas()", visibilityWorkflow);
        Assert.Contains("actions.ResumeCanvas()", visibilityWorkflow);
        Assert.Contains("actions.RedrawCanvas(request.HasCurrentOverlay)", visibilityWorkflow);
        Assert.Contains("actions.Suspend()", interactionWorkflow);
        Assert.Contains("finally", interactionWorkflow);
        Assert.Contains("actions.Resume()", interactionWorkflow);
        Assert.Contains("public sealed class CodingOverlayInputVisibilityStateController", stateController);
        Assert.Contains("public int SuspendDepth", stateController);
        Assert.Contains("public void ResetSuspendState", stateController);
    }

    [Fact]
    public void PlayerWindow_overlay_input_create_event_state_uses_controls_adapter()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var controlsPath = Path.Combine(uiRoot, "Ai", "CodingOverlayInputControls.cs");
        var relevantPartials = new[]
        {
            "PlayerWindow.Coding.cs",
            "PlayerWindow.Coding.AiEvents.cs",
            "PlayerWindow.Coding.OverlayInput.Viewport.cs",
            "PlayerWindow.Coding.OverlayInput.Visibility.cs",
            "PlayerWindow.Coding.OverlayInput.Tools.cs",
            "PlayerWindow.Coding.OverlayInput.Standard.cs",
            "PlayerWindow.Coding.OverlayInput.Schema.cs",
            "PlayerWindow.Coding.OverlayInput.Calibration.cs",
            "PlayerWindow.Coding.OverlayInput.MultiPoint.cs",
            "PlayerWindow.xaml.cs",
            "PlayerWindow.Keyboard.cs"
        };

        Assert.True(File.Exists(controlsPath), "OverlayInput-Toollabel und Create-Event-Button sollen ausserhalb der PlayerWindow-Partials gesetzt werden.");

        var joinedPartials = string.Join(
            Environment.NewLine,
            relevantPartials.Select(file => File.ReadAllText(Path.Combine(windowsRoot, file))));
        var controls = File.Exists(controlsPath) ? File.ReadAllText(controlsPath) : "";

        Assert.Contains("CodingOverlayInputControls.ApplyActiveToolSelection", joinedPartials);
        Assert.Contains("CodingOverlayInputControls.SetCreateEventEnabled", joinedPartials);
        Assert.Contains("CodingOverlayInputControls.CaptureCanvasMouse", joinedPartials);
        Assert.Contains("CodingOverlayInputControls.ReleaseCanvasMouse", joinedPartials);
        Assert.Contains("CodingOverlayInputControls.GetCanvasSize", joinedPartials);
        Assert.Contains("CodingOverlayInputControls.SetCanvasSize", joinedPartials);
        Assert.Contains("CodingOverlayInputControls.GetCanvasActualSize", joinedPartials);
        Assert.Contains("CodingOverlayInputControls.IsCanvasMouseCaptured", joinedPartials);
        Assert.Contains("CodingOverlayInputControls.IsPopupOpen", joinedPartials);
        Assert.Contains("CodingOverlayInputControls.OpenPopup", joinedPartials);
        Assert.Contains("CodingOverlayInputControls.ClosePopup", joinedPartials);
        AssertNoForbiddenTokens(
            joinedPartials,
            "TxtActiveToolLabel.Text =",
            "BtnCodingCreateEvent.IsEnabled =",
            "CodingOverlayCanvas.CaptureMouse",
            "CodingOverlayCanvas.ReleaseMouseCapture",
            "CodingOverlayCanvas.Width",
            "CodingOverlayCanvas.Height",
            "CodingOverlayCanvas.ActualWidth",
            "CodingOverlayCanvas.ActualHeight",
            "CodingOverlayCanvas.IsMouseCaptured",
            "CodingOverlayPopup.IsOpen",
            "ToolsDropdownPopup.IsOpen");
        Assert.Contains("public static class CodingOverlayInputControls", controls);
        Assert.Contains("public static void ApplyActiveToolSelection", controls);
        Assert.Contains("public static void SetCreateEventEnabled", controls);
        Assert.Contains("public static void CaptureCanvasMouse", controls);
        Assert.Contains("public static void ReleaseCanvasMouse", controls);
        Assert.Contains("public static Size GetCanvasSize", controls);
        Assert.Contains("public static void SetCanvasSize", controls);
        Assert.Contains("public static Size GetCanvasActualSize", controls);
        Assert.Contains("public static bool IsCanvasMouseCaptured", controls);
        Assert.Contains("public static bool IsPopupOpen", controls);
        Assert.Contains("public static void OpenPopup", controls);
        Assert.Contains("public static void ClosePopup", controls);
    }

    [Fact]
    public void PlayerWindow_overlay_viewport_mapping_lives_in_viewport_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var overlayInputPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.OverlayInput.cs");
        var viewportPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.OverlayInput.Viewport.cs");
        var refreshWorkflowPath = Path.Combine(uiRoot, "Player", "CodingOverlayViewportRefreshWorkflow.cs");
        var redrawWorkflowPath = Path.Combine(uiRoot, "Player", "CodingCanvasRedrawWorkflow.cs");

        Assert.True(File.Exists(viewportPath), "Overlay-Viewport-Mapping soll aus dem allgemeinen OverlayInput-Partial heraus.");
        Assert.True(File.Exists(refreshWorkflowPath), "Overlay-Viewport-Refresh-Entscheidung soll ausserhalb von PlayerWindow orchestriert werden.");
        Assert.True(File.Exists(redrawWorkflowPath), "Canvas-Redraw-Reihenfolge soll ausserhalb von PlayerWindow orchestriert werden.");

        var overlayInput = File.ReadAllText(overlayInputPath);
        var viewport = File.ReadAllText(viewportPath);
        var refreshWorkflow = File.Exists(refreshWorkflowPath) ? File.ReadAllText(refreshWorkflowPath) : "";
        var redrawWorkflow = File.ReadAllText(redrawWorkflowPath);

        AssertNoForbiddenTokens(
            overlayInput,
            "private Rect GetCodingContentRect",
            "private NormalizedPoint CodingPixelToNorm",
            "private Point CodingNormToPixel",
            "private void RedrawCodingCanvas");
        Assert.Contains("private Rect GetCodingContentRect", viewport);
        Assert.Contains("CodingOverlayViewportMapper.GetContentRect", viewport);
        Assert.Contains("CodingOverlayViewportRefreshWorkflow.Execute", viewport);
        AssertNoForbiddenTokens(viewport, "if (CodingOverlayCanvas.ActualWidth <= 0 || CodingOverlayCanvas.ActualHeight <= 0)");
        Assert.Contains("if (request.ActualWidth <= 0 || request.ActualHeight <= 0)", refreshWorkflow);
        Assert.Contains("actions.UpdateViewport()", refreshWorkflow);
        Assert.Contains("_codingOverlayRenderController.ClearTransient", viewport);
        Assert.Contains("_codingSessionHost", viewport);
        Assert.Contains("private void RedrawCodingCanvas", viewport);
        Assert.Contains("CodingCanvasRedrawWorkflow.Execute", viewport);
        AssertNoForbiddenTokens(
            viewport,
            "if (_codingSchemaManager.IsActive)",
            "else if (includeManualOverlay");
        Assert.Contains("actions.RenderActiveSchema()", redrawWorkflow);
        Assert.Contains("actions.RenderManualOverlay()", redrawWorkflow);
    }

    [Fact]
    public void PlayerWindow_eingabemarker_geometry_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var interactionControllerPath = Path.Combine(uiRoot, "Player", "CodingEingabemarkerInteractionController.cs");
        var windowRootPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.xaml.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingEingabemarkerGeometryPolicy.cs");
        var canvasWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingEingabemarkerCanvasInputWorkflow.cs");
        var rendererPath = Path.Combine(uiRoot, "Player", "CodingEingabemarkerPreviewRenderer.cs");

        Assert.True(File.Exists(policyPath), "Eingabemarker-Rechteckgeometrie muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(canvasWorkflowPath), "Eingabemarker-Canvas-Entscheidungen sollen die Geometrie-Policy ausserhalb von PlayerWindow verwenden.");
        Assert.True(File.Exists(rendererPath), "Eingabemarker-Preview-Rendering muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(interactionControllerPath), "Eingabemarker-Mausinteraktion soll in einem eigenen Controller liegen.");

        var interactionController = File.ReadAllText(interactionControllerPath);
        var windowRoot = File.ReadAllText(windowRootPath);
        var policy = File.ReadAllText(policyPath);
        var canvasWorkflow = File.ReadAllText(canvasWorkflowPath);
        var renderer = File.ReadAllText(rendererPath);

        AssertNoForbiddenTokens(
            interactionController,
            "CodingEingabemarkerGeometryPolicy.BuildPreviewRect",
            "CodingEingabemarkerGeometryPolicy.BuildNormalizedSelection");
        Assert.Contains("CodingEingabemarkerGeometryPolicy.BuildPreviewRect", canvasWorkflow);
        Assert.Contains("CodingEingabemarkerGeometryPolicy.BuildNormalizedSelection", canvasWorkflow);
        Assert.Contains("CodingEingabemarkerPreviewRenderer.Create", windowRoot);
        Assert.Contains("CodingEingabemarkerPreviewRenderer.Update", windowRoot);
        Assert.Contains("CodingEingabemarkerPreviewRenderer.Clear", windowRoot);
        AssertNoForbiddenTokens(
            interactionController,
            "Math.Min(_eingabemarkerDragStart.X",
            "Math.Abs(canvasPos.X - _eingabemarkerDragStart.X)",
            "Math.Max(_eingabemarkerDragStart.X",
            "new System.Windows.Shapes.Rectangle",
            "Canvas.SetLeft(_eingabemarkerPreviewRect",
            "CodingOverlayCanvas.Children.Remove(_eingabemarkerPreviewRect)");
        Assert.Contains("public static Rect BuildPreviewRect", policy);
        Assert.Contains("public static Rect? BuildNormalizedSelection", policy);
        Assert.Contains("public static class CodingEingabemarkerPreviewRenderer", renderer);
        Assert.Contains("new System.Windows.Shapes.Rectangle", renderer);
        Assert.Contains("public static System.Windows.Shapes.Rectangle? Clear", renderer);
    }

    [Fact]
    public void PlayerWindow_eingabemarker_input_wiring_lives_in_controller()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var markerPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Eingabemarker.cs");
        var interactionControllerPath = Path.Combine(uiRoot, "Player", "CodingEingabemarkerInteractionController.cs");
        var inputControllerPath = Path.Combine(uiRoot, "Player", "CodingEingabemarkerInputController.cs");
        var windowRootPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.xaml.cs");
        var codingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.cs");
        var inputPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Eingabemarker.Input.cs");
        var popupControlsPath = Path.Combine(uiRoot, "Views", "Windows", "CodingEingabemarkerPopupControls.cs");
        var focusControlsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerFocusControls.cs");
        var inputWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingEingabemarkerInputWorkflow.cs");
        var canvasWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingEingabemarkerCanvasInputWorkflow.cs");

        Assert.False(File.Exists(inputPath), "Eingabemarker-Eingabe soll nicht mehr in einem PlayerWindow-Partial liegen.");
        Assert.True(File.Exists(inputControllerPath), "Eingabemarker-Eingabe soll in einem eigenen Controller liegen.");
        Assert.True(File.Exists(popupControlsPath), "Eingabemarker-Popup-Zustand soll ausserhalb der PlayerWindow-Partials gesetzt werden.");
        Assert.True(File.Exists(focusControlsPath), "Eingabemarker-Focus soll ueber die Player-Focus-Controls laufen.");
        Assert.True(File.Exists(inputWorkflowPath), "Eingabemarker-Key- und Auswahlentscheidungen sollen ausserhalb von PlayerWindow laufen.");
        Assert.True(File.Exists(canvasWorkflowPath), "Eingabemarker-Mausentscheidungen sollen ausserhalb von PlayerWindow laufen.");
        Assert.False(File.Exists(markerPath), "Die Eingabemarker-Interaktion soll nicht mehr in einem PlayerWindow-Partial liegen.");
        Assert.True(File.Exists(interactionControllerPath), "Die Eingabemarker-Interaktion soll in einem eigenen Controller liegen.");

        var interactionController = File.ReadAllText(interactionControllerPath);
        var inputController = File.ReadAllText(inputControllerPath);
        var windowRoot = File.ReadAllText(windowRootPath);
        var coding = File.ReadAllText(codingPath);
        var popupControls = File.Exists(popupControlsPath) ? File.ReadAllText(popupControlsPath) : "";
        var focusControls = File.Exists(focusControlsPath) ? File.ReadAllText(focusControlsPath) : "";
        var inputWorkflow = File.Exists(inputWorkflowPath) ? File.ReadAllText(inputWorkflowPath) : "";
        var canvasWorkflow = File.Exists(canvasWorkflowPath) ? File.ReadAllText(canvasWorkflowPath) : "";

        AssertNoForbiddenTokens(
            interactionController,
            "private void CmbEingabemarker_KeyDown",
            "private void CmbEingabemarker_SelectionChanged",
            "private static string? ResolveEingabemarkerCodeHint");
        Assert.Contains("CodingEingabemarkerCanvasInputWorkflow.MouseDown", interactionController);
        Assert.Contains("CodingEingabemarkerCanvasInputWorkflow.MouseMove", interactionController);
        Assert.Contains("CodingEingabemarkerCanvasInputWorkflow.MouseUp", interactionController);
        AssertNoForbiddenTokens(interactionController, "if (_eingabemarkerPhase != EingabemarkerPhase.Drawing)");
        Assert.Contains("PlayerDispatcherScheduler.ScheduleInput", windowRoot);
        Assert.Contains("PlayerFocusControls.FocusElement", windowRoot);
        AssertNoForbiddenTokens(
            interactionController + windowRoot,
            "Dispatcher.BeginInvoke",
            "new Action(() => TxtEingabemarker.Focus())",
            "TxtEingabemarker.Focus()",
            "System.Windows.Threading.DispatcherPriority.Input",
            "_eingabemarkerPreviewRect == null",
            "if (normalizedRect is null)");
        Assert.Contains("CodingEingabemarkerPopupControls.ShowInput", windowRoot);
        Assert.Contains("CodingEingabemarkerPopupControls.Hide", windowRoot);
        Assert.Contains("CodingEingabemarkerPopupControls.IsVisible", coding);
        Assert.Contains("CodingEingabemarkerPopupControls.ApplyQuickSelection", windowRoot);
        Assert.Contains("CodingEingabemarkerPopupControls.ResolveSelectedText", coding);
        Assert.Contains("CodingEingabemarkerKeyInputWorkflow.Execute", inputController);
        Assert.Contains("CodingEingabemarkerSelectionInputWorkflow.Execute", inputController);
        AssertNoForbiddenTokens(
            inputController + coding,
            "if (e.Key == Key.Escape)",
            "if (e.Key != Key.Enter)",
            "CmbEingabemarker.SelectedItem is ComboBoxItem",
            "TxtEingabemarker.Text = text",
            "EingabemarkerPopup.Visibility != Visibility.Visible");
        AssertNoForbiddenTokens(
            windowRoot,
            "EingabemarkerPopup.Visibility = Visibility.Visible",
            "EingabemarkerPopup.Visibility = Visibility.Collapsed",
            "TxtEingabemarker.Text = \"\"",
            "CmbEingabemarker.SelectedIndex = -1");
        Assert.Contains("private void CmbEingabemarker_KeyDown", coding);
        Assert.Contains("private void CmbEingabemarker_SelectionChanged", coding);
        AssertNoForbiddenTokens(coding, "private static string? ResolveEingabemarkerCodeHint");
        Assert.Contains("PlayerVsaCodeHintResolver.ResolveKeyword", windowRoot);
        Assert.Contains("_codingEingabemarkerSubmissionController", windowRoot);
        Assert.Contains(".SubmitAsync(TxtEingabemarker.Text)", windowRoot);
        Assert.Contains("public static void ShowInput", popupControls);
        Assert.Contains("public static void Hide", popupControls);
        Assert.Contains("public static bool IsVisible", popupControls);
        Assert.Contains("public static void ApplyQuickSelection", popupControls);
        Assert.Contains("public static string? ResolveSelectedText", popupControls);
        Assert.Contains("public static bool FocusElement", focusControls);
        Assert.Contains("request.IsEscape", inputWorkflow);
        Assert.Contains("request.IsEnter", inputWorkflow);
        Assert.Contains("request.IsPopupVisible", inputWorkflow);
        Assert.Contains("string.IsNullOrEmpty(request.SelectedText)", inputWorkflow);
        Assert.Contains("request.IsDrawing", canvasWorkflow);
        Assert.Contains("request.HasPreview", canvasWorkflow);
        Assert.Contains("BuildNormalizedSelection", canvasWorkflow);
        Assert.Contains("actions.CancelMarker()", canvasWorkflow);
        Assert.Contains("actions.SetInputPhase()", canvasWorkflow);
    }

    [Fact]
    public void PlayerWindow_eingabemarker_canvas_state_uses_controls_adapter()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var interactionControllerPath = Path.Combine(uiRoot, "Player", "CodingEingabemarkerInteractionController.cs");
        var windowRootPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.xaml.cs");
        var controlsPath = Path.Combine(uiRoot, "Ai", "CodingOverlayInputControls.cs");
        var toggleWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingEingabemarkerToggleWorkflow.cs");

        Assert.True(File.Exists(controlsPath), "Eingabemarker-Canvas-Zustand soll ueber den OverlayInput-Control-Adapter laufen.");
        Assert.True(File.Exists(toggleWorkflowPath), "Eingabemarker-Toggle-Reihenfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");

        var interactionController = File.ReadAllText(interactionControllerPath);
        var windowRoot = File.ReadAllText(windowRootPath);
        var controls = File.Exists(controlsPath) ? File.ReadAllText(controlsPath) : "";
        var toggleWorkflow = File.Exists(toggleWorkflowPath) ? File.ReadAllText(toggleWorkflowPath) : "";

        Assert.Contains("CodingEingabemarkerToggleWorkflow.Execute", interactionController);
        AssertNoForbiddenTokens(windowRoot, "if (BtnEingabemarker.IsChecked == true)");
        Assert.Contains("CodingOverlayInputControls.EnableDrawingCanvas", windowRoot);
        Assert.Contains("CodingOverlayInputControls.DisableDrawingCanvas", windowRoot);
        Assert.Contains("CodingOverlayInputControls.ResetCanvasCursor", windowRoot);
        AssertNoForbiddenTokens(
            interactionController + windowRoot,
            "CodingOverlayCanvas.IsHitTestVisible =",
            "CodingOverlayCanvas.Cursor =");
        Assert.Contains("request.IsChecked", toggleWorkflow);
        Assert.Contains("actions.PauseForCodingInteraction()", toggleWorkflow);
        Assert.Contains("actions.SetDrawingPhase()", toggleWorkflow);
        Assert.Contains("actions.SetInactivePhase()", toggleWorkflow);
        Assert.Contains("actions.ResetCanvasCursor()", toggleWorkflow);
        Assert.Contains("public static void EnableDrawingCanvas", controls);
        Assert.Contains("public static void DisableDrawingCanvas", controls);
        Assert.Contains("public static void ResetCanvasCursor", controls);
    }

    [Fact]
    public void PlayerWindow_overlay_canvas_cursor_uses_controls_adapter()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var controlsPath = Path.Combine(uiRoot, "Ai", "CodingOverlayInputControls.cs");

        var joinedPartials = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs").Select(File.ReadAllText));
        var tools = File.ReadAllText(Path.Combine(windowsRoot, "PlayerWindow.Coding.OverlayInput.Tools.cs"));
        var marking = File.ReadAllText(Path.Combine(windowsRoot, "PlayerWindow.LiveDetection.Marking.cs"));
        var controls = File.Exists(controlsPath) ? File.ReadAllText(controlsPath) : "";

        Assert.Contains("CodingOverlayInputControls.ApplyCanvasCursor", tools);
        Assert.Contains("CodingOverlayInputControls.ApplyCanvasCursor", marking);
        AssertNoForbiddenTokens(joinedPartials, "CodingOverlayCanvas.Cursor =");
        Assert.Contains("public static void ApplyCanvasCursor", controls);
    }

    [Fact]
    public void PlayerWindow_eingabemarker_submission_lives_in_controller()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var submissionPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Eingabemarker.Submission.cs");
        var submissionControllerPath = Path.Combine(uiRoot, "Player", "CodingEingabemarkerSubmissionController.cs");
        var windowRootPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.xaml.cs");
        var popupControlsPath = Path.Combine(uiRoot, "Views", "Windows", "CodingEingabemarkerPopupControls.cs");
        var submissionWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingEingabemarkerSubmissionWorkflow.cs");
        var directEventWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingEingabemarkerDirectEventWorkflow.cs");

        Assert.False(File.Exists(submissionPath), "Eingabemarker-Übernahme soll nicht mehr in einem PlayerWindow-Partial liegen.");
        Assert.True(File.Exists(submissionControllerPath), "Eingabemarker-Übernahme soll in einem eigenen Controller liegen.");
        Assert.True(File.Exists(popupControlsPath), "Eingabemarker-Popup-Zustand soll ausserhalb der PlayerWindow-Partials gesetzt werden.");
        Assert.True(File.Exists(submissionWorkflowPath), "Eingabemarker-Submission-Entscheidungen sollen ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(directEventWorkflowPath), "Eingabemarker-Direkt-Event-Ablauf soll ausserhalb der PlayerWindow-Partials orchestriert werden.");

        var submissionController = File.ReadAllText(submissionControllerPath);
        var windowRoot = File.ReadAllText(windowRootPath);
        var submissionWorkflow = File.Exists(submissionWorkflowPath) ? File.ReadAllText(submissionWorkflowPath) : "";
        var directEventWorkflow = File.Exists(directEventWorkflowPath) ? File.ReadAllText(directEventWorkflowPath) : "";

        AssertNoForbiddenTokens(
            windowRoot,
            "private async Task SubmitEingabemarker",
            "CodingEingabemarkerDuplicatePolicy.FindDuplicate");
        Assert.Contains("CodingEingabemarkerSubmissionWorkflow.ExecuteAsync", submissionController);
        Assert.Contains("CodingEingabemarkerDirectEventWorkflow.Execute", submissionController);
        Assert.Contains("CodingEingabemarkerDuplicatePolicy.FindDuplicate", submissionController);
        AssertNoForbiddenTokens(
            submissionController,
            "CodingEingabemarkerEventFactory.CreateAccepted",
            "CodingProtocolEntryPhotoPathAppender.AddIfPresent",
            "CodingEingabemarkerEventAppender.Apply");
        Assert.Contains("_codingEingabemarkerSubmissionController", windowRoot);
        Assert.Contains(".SubmitAsync(TxtEingabemarker.Text)", windowRoot);
        Assert.Contains("CodingEingabemarkerPopupControls.Hide", windowRoot);
        AssertNoForbiddenTokens(windowRoot, "EingabemarkerPopup.Visibility = Visibility.Collapsed");
        Assert.Contains("RunCodingAnalysisAsync", windowRoot);
        AssertNoForbiddenTokens(
            submissionController,
            "if (string.IsNullOrEmpty(keyword))",
            "if (_codingSessionHost.HasViewModel && codeHint != null)",
            "if (codeHint != null && _codingSessionHost.HasViewModel",
            "catch (Exception ex)");
        Assert.Contains("request.RawKeyword", submissionWorkflow);
        Assert.Contains("actions.ShowDuplicateStatus", submissionWorkflow);
        Assert.Contains("actions.AddDirectEvent", submissionWorkflow);
        Assert.Contains("actions.RunAiFallbackAsync", submissionWorkflow);
        Assert.Contains("finally", submissionWorkflow);
        Assert.Contains("actions.CancelMarker()", submissionWorkflow);
        Assert.Contains("CodingEingabemarkerEventFactory.CreateAccepted", directEventWorkflow);
        Assert.Contains("CodingProtocolEntryPhotoPathAppender.AddIfPresent", directEventWorkflow);
        Assert.Contains("CodingEingabemarkerEventAppender.Apply", directEventWorkflow);
        Assert.Contains("actions.PersistTraining(ev)", directEventWorkflow);
    }

    [Fact]
    public void PlayerWindow_overlay_viewport_size_decision_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var playerCodingPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.cs");
        var policyPath = Path.Combine(uiRoot, "Player", "CodingOverlayViewportSizePolicy.cs");
        var controllerPath = Path.Combine(uiRoot, "Player", "CodingOverlayViewportController.cs");

        Assert.True(File.Exists(policyPath), "Overlay-Viewport-Groessenentscheidung muss ausserhalb von PlayerWindow liegen.");
        Assert.True(File.Exists(controllerPath), "Overlay-Viewport-Anwendung soll ausserhalb von PlayerWindow liegen.");

        var playerCoding = File.ReadAllText(playerCodingPath);
        var policy = File.ReadAllText(policyPath);
        var controller = File.Exists(controllerPath) ? File.ReadAllText(controllerPath) : "";

        Assert.Contains("CodingOverlayViewportController.Update", playerCoding);
        AssertNoForbiddenTokens(
            playerCoding,
            "CodingOverlayViewportSizePolicy.Build",
            "double.IsNaN(w)");
        Assert.Contains("public static CodingOverlayViewportSizeUpdate Build", policy);
        Assert.Contains("CodingOverlayViewportSizePolicy.Build", controller);
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
            "Verbotene alte PlayerWindow-OverlayInput-Logik gefunden: " + string.Join(", ", hits));
    }
}
