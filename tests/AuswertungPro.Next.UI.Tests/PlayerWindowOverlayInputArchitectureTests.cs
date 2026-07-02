using System;
using System.IO;
using System.Linq;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

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
        Assert.DoesNotContain("overlay.Q1Mm.HasValue ? $\"Q1:", overlay);
        Assert.DoesNotContain("overlay.ToolType == OverlayToolType.Level && overlay.FillPercent.HasValue", overlay);
        Assert.DoesNotContain("TxtCodingQ1.Text", overlay);
        Assert.DoesNotContain("CodingMeasurementPanel.Visibility", overlay);
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

        Assert.DoesNotContain("CodingOverlayCursorPolicy.ShouldUseCrossCursor", overlayInput);
        Assert.Contains("CodingOverlayCursorPolicy.ShouldUseCrossCursor", tools);
        Assert.DoesNotContain("var isInteractive = _codingIsCalibrating", overlayInput);
        Assert.DoesNotContain("var isInteractive = _codingIsCalibrating", tools);
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
        Assert.DoesNotContain("using System.Collections", overlayInput);
        Assert.DoesNotContain("using System.Globalization", overlayInput);
        Assert.DoesNotContain("using System.IO", overlayInput);
        Assert.DoesNotContain("using System.Threading", overlayInput);
        Assert.DoesNotContain("AuswertungPro.Next.Application", overlayInput);
        Assert.DoesNotContain("AuswertungPro.Next.Infrastructure", overlayInput);
        Assert.DoesNotContain("AuswertungPro.Next.UI.Services", overlayInput);
        Assert.DoesNotContain("InfraTeacher", overlayInput);
        Assert.Contains("_codingSessionHost", overlayInput);
        Assert.DoesNotContain("_codingVm", overlayInput);
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

        Assert.DoesNotContain("OnCanvasMultiPointClick", overlayInput);
        Assert.DoesNotContain("OnCanvasMultiPointMove", overlayInput);
        Assert.Contains("private void HandleCodingMultiPointMouseDown", multiPoint);
        Assert.Contains("private bool TryHandleCodingMultiPointMouseMove", multiPoint);
        Assert.Contains("CodingMultiPointOverlayDrawWorkflow.MouseDown", multiPoint);
        Assert.Contains("CodingMultiPointOverlayDrawWorkflow.MouseMove", multiPoint);
        Assert.Contains("_codingSessionHost", multiPoint);
        Assert.DoesNotContain("_codingVm", multiPoint);
        Assert.DoesNotContain("OnCanvasMultiPointClick", multiPoint);
        Assert.DoesNotContain("OnCanvasMultiPointMove", multiPoint);
        Assert.Contains("AddMultiPointOverlayPoint", multiPoint);
        Assert.Contains("UpdateMultiPointOverlayPreview", multiPoint);
        Assert.DoesNotContain("if (!_codingOverlayToolHost.HasOverlayService", multiPoint);
        Assert.DoesNotContain("if (_codingOverlayToolHost.DrawPointCount == 0)", multiPoint);
        Assert.DoesNotContain("if (BtnCodingLiveAi.IsChecked == true", multiPoint);
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
        Assert.DoesNotContain("if (_eingabemarkerPhase", overlayInput);
        Assert.DoesNotContain("if (!_codingOverlayToolHost.HasOverlayService", overlayInput);
        Assert.DoesNotContain("if (TryStartCodingCalibration", overlayInput);
        Assert.DoesNotContain("if (_codingOverlayToolHost.ActiveTool", overlayInput);
        Assert.DoesNotContain("if (TryHandleCodingSchemaMouseDown", overlayInput);
        Assert.DoesNotContain("if (_codingOverlayToolHost.IsMultiPointTool", overlayInput);
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

        Assert.DoesNotContain("OnCanvasMouseDown(norm)", overlayInput);
        Assert.DoesNotContain("OnCanvasMouseMove(norm)", overlayInput);
        Assert.DoesNotContain("OnCanvasMouseUp(norm)", overlayInput);
        Assert.Contains("private void HandleCodingStandardMouseDown", standard);
        Assert.Contains("private bool TryHandleCodingStandardMouseMove", standard);
        Assert.Contains("private bool TryHandleCodingStandardMouseUp", standard);
        Assert.Contains("CodingStandardOverlayDrawWorkflow.MouseDown", standard);
        Assert.Contains("CodingStandardOverlayDrawWorkflow.MouseMove", standard);
        Assert.Contains("CodingStandardOverlayDrawWorkflow.MouseUp", standard);
        Assert.Contains("HandleMarkDrawingComplete", standard);
        Assert.Contains("_codingSessionHost", standard);
        Assert.DoesNotContain("_codingVm", standard);
        Assert.DoesNotContain("if (!_codingSessionHost.HasViewModel)", standard);
        Assert.DoesNotContain("if (!_codingOverlayToolHost.HasOverlayService", standard);
        Assert.DoesNotContain("_ = AnalyzeWithOverlayHintAsync", standard);
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
        var lifecycleExitPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.Lifecycle.Exit.cs");
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

        Assert.DoesNotContain("private void SuspendCodingOverlayInput", overlayInput);
        Assert.DoesNotContain("private void ResumeCodingOverlayInput", overlayInput);
        Assert.DoesNotContain("private void HideCodingOverlayForExternalWindow", overlayInput);
        Assert.DoesNotContain("private void RestoreCodingOverlayAfterExternalWindow", overlayInput);
        Assert.Contains("private void SuspendCodingOverlayInput", visibility);
        Assert.Contains("CodingOverlayInputVisibilityWorkflow.Suspend", visibility);
        Assert.Contains("CodingOverlayInputVisibilityWorkflow.Resume", visibility);
        Assert.Contains("CodingOverlayInputVisibilityWorkflow.HideForExternalWindow", visibility);
        Assert.Contains("CodingOverlayInputVisibilityWorkflow.RestoreAfterExternalWindow", visibility);
        Assert.Contains("_codingOverlayInputVisibilityState", visibility);
        Assert.Contains("_codingOverlayInputVisibilityState", playerState + lifecycleExit + wiring);
        Assert.DoesNotContain("private int _codingOverlaySuspendDepth", playerState);
        Assert.DoesNotContain("private bool _codingOverlayWasOpenBeforeSuspend", playerState);
        Assert.DoesNotContain("private bool _codingOverlayWasOpenBeforeExternalHide", playerState);
        Assert.DoesNotContain("private bool _deactivatedByExternalWindow", playerState);
        Assert.DoesNotContain("_codingOverlaySuspendDepth++", visibility);
        Assert.DoesNotContain("if (_codingOverlaySuspendDepth > 1)", visibility);
        Assert.DoesNotContain("_codingOverlaySuspendDepth", visibility + lifecycleExit + wiring);
        Assert.DoesNotContain("_codingOverlayWasOpenBeforeSuspend", visibility + lifecycleExit);
        Assert.DoesNotContain("_codingOverlayWasOpenBeforeExternalHide", visibility);
        Assert.DoesNotContain("_deactivatedByExternalWindow", wiring);
        Assert.Contains("CodingOverlayInputControls.SuspendCanvas", visibility);
        Assert.Contains("CodingOverlayInputControls.ResumeCanvas", visibility);
        Assert.Contains("_codingSessionHost", visibility);
        Assert.DoesNotContain("_codingVm", visibility);
        Assert.DoesNotContain("CodingOverlayCanvas.Visibility = Visibility.Hidden", visibility);
        Assert.DoesNotContain("CodingOverlayCanvas.Visibility = Visibility.Visible", visibility);
        Assert.DoesNotContain("CodingOverlayCanvas.IsHitTestVisible = false", visibility);
        Assert.DoesNotContain("CodingOverlayCanvas.IsHitTestVisible = true", visibility);
        Assert.Contains("CodingOverlayInputControls.IsPopupOpen", visibility);
        Assert.Contains("CodingOverlayInputControls.OpenPopup", visibility);
        Assert.Contains("CodingOverlayInputControls.ClosePopup", visibility);
        Assert.DoesNotContain("CodingOverlayPopup.IsOpen", visibility);
        Assert.Contains("private void RestoreCodingOverlayAfterExternalWindow", visibility);
        Assert.Contains("CodingOverlayInputInteractionWorkflow.Run", visibility);
        Assert.Contains("CodingOverlayInputInteractionWorkflow.RunAsync", visibility);
        Assert.DoesNotContain("SuspendCodingOverlayInput();", codingPartialsWithoutVisibility);
        Assert.DoesNotContain("ResumeCodingOverlayInput();", codingPartialsWithoutVisibility);
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
            "PlayerWindow.Coding.Eingabemarker.cs",
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
        Assert.DoesNotContain("TxtActiveToolLabel.Text =", joinedPartials);
        Assert.DoesNotContain("BtnCodingCreateEvent.IsEnabled =", joinedPartials);
        Assert.DoesNotContain("CodingOverlayCanvas.CaptureMouse", joinedPartials);
        Assert.DoesNotContain("CodingOverlayCanvas.ReleaseMouseCapture", joinedPartials);
        Assert.DoesNotContain("CodingOverlayCanvas.Width", joinedPartials);
        Assert.DoesNotContain("CodingOverlayCanvas.Height", joinedPartials);
        Assert.DoesNotContain("CodingOverlayCanvas.ActualWidth", joinedPartials);
        Assert.DoesNotContain("CodingOverlayCanvas.ActualHeight", joinedPartials);
        Assert.DoesNotContain("CodingOverlayCanvas.IsMouseCaptured", joinedPartials);
        Assert.DoesNotContain("CodingOverlayPopup.IsOpen", joinedPartials);
        Assert.DoesNotContain("ToolsDropdownPopup.IsOpen", joinedPartials);
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

        Assert.DoesNotContain("private Rect GetCodingContentRect", overlayInput);
        Assert.DoesNotContain("private NormalizedPoint CodingPixelToNorm", overlayInput);
        Assert.DoesNotContain("private Point CodingNormToPixel", overlayInput);
        Assert.DoesNotContain("private void RedrawCodingCanvas", overlayInput);
        Assert.Contains("private Rect GetCodingContentRect", viewport);
        Assert.Contains("CodingOverlayViewportMapper.GetContentRect", viewport);
        Assert.Contains("CodingOverlayViewportRefreshWorkflow.Execute", viewport);
        Assert.DoesNotContain("if (CodingOverlayCanvas.ActualWidth <= 0 || CodingOverlayCanvas.ActualHeight <= 0)", viewport);
        Assert.Contains("if (request.ActualWidth <= 0 || request.ActualHeight <= 0)", refreshWorkflow);
        Assert.Contains("actions.UpdateViewport()", refreshWorkflow);
        Assert.Contains("_codingOverlayRenderController.ClearTransient", viewport);
        Assert.Contains("_codingSessionHost", viewport);
        Assert.DoesNotContain("_codingVm", viewport);
        Assert.Contains("private void RedrawCodingCanvas", viewport);
        Assert.Contains("CodingCanvasRedrawWorkflow.Execute", viewport);
        Assert.DoesNotContain("if (_codingSchemaManager.IsActive)", viewport);
        Assert.DoesNotContain("else if (includeManualOverlay", viewport);
        Assert.Contains("actions.RenderActiveSchema()", redrawWorkflow);
        Assert.Contains("actions.RenderManualOverlay()", redrawWorkflow);
    }

    [Fact]
    public void PlayerWindow_eingabemarker_geometry_lives_in_policy()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var markerPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Eingabemarker.cs");
        var policyPath = Path.Combine(uiRoot, "Ai", "CodingEingabemarkerGeometryPolicy.cs");
        var canvasWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingEingabemarkerCanvasInputWorkflow.cs");
        var rendererPath = Path.Combine(uiRoot, "Player", "CodingEingabemarkerPreviewRenderer.cs");

        Assert.True(File.Exists(policyPath), "Eingabemarker-Rechteckgeometrie muss ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(canvasWorkflowPath), "Eingabemarker-Canvas-Entscheidungen sollen die Geometrie-Policy ausserhalb von PlayerWindow verwenden.");
        Assert.True(File.Exists(rendererPath), "Eingabemarker-Preview-Rendering muss ausserhalb der PlayerWindow-Partials liegen.");

        var marker = File.ReadAllText(markerPath);
        var policy = File.ReadAllText(policyPath);
        var canvasWorkflow = File.ReadAllText(canvasWorkflowPath);
        var renderer = File.ReadAllText(rendererPath);

        Assert.DoesNotContain("CodingEingabemarkerGeometryPolicy.BuildPreviewRect", marker);
        Assert.DoesNotContain("CodingEingabemarkerGeometryPolicy.BuildNormalizedSelection", marker);
        Assert.Contains("CodingEingabemarkerGeometryPolicy.BuildPreviewRect", canvasWorkflow);
        Assert.Contains("CodingEingabemarkerGeometryPolicy.BuildNormalizedSelection", canvasWorkflow);
        Assert.Contains("CodingEingabemarkerPreviewRenderer.Create", marker);
        Assert.Contains("CodingEingabemarkerPreviewRenderer.Update", marker);
        Assert.Contains("CodingEingabemarkerPreviewRenderer.Clear", marker);
        Assert.DoesNotContain("Math.Min(_eingabemarkerDragStart.X", marker);
        Assert.DoesNotContain("Math.Abs(canvasPos.X - _eingabemarkerDragStart.X)", marker);
        Assert.DoesNotContain("Math.Max(_eingabemarkerDragStart.X", marker);
        Assert.DoesNotContain("new System.Windows.Shapes.Rectangle", marker);
        Assert.DoesNotContain("Canvas.SetLeft(_eingabemarkerPreviewRect", marker);
        Assert.DoesNotContain("CodingOverlayCanvas.Children.Remove(_eingabemarkerPreviewRect)", marker);
        Assert.Contains("public static Rect BuildPreviewRect", policy);
        Assert.Contains("public static Rect? BuildNormalizedSelection", policy);
        Assert.Contains("public static class CodingEingabemarkerPreviewRenderer", renderer);
        Assert.Contains("new System.Windows.Shapes.Rectangle", renderer);
        Assert.Contains("public static System.Windows.Shapes.Rectangle? Clear", renderer);
    }

    [Fact]
    public void PlayerWindow_eingabemarker_input_wiring_lives_in_input_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var markerPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Eingabemarker.cs");
        var inputPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Eingabemarker.Input.cs");
        var popupControlsPath = Path.Combine(uiRoot, "Views", "Windows", "CodingEingabemarkerPopupControls.cs");
        var focusControlsPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerFocusControls.cs");
        var inputWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingEingabemarkerInputWorkflow.cs");
        var canvasWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingEingabemarkerCanvasInputWorkflow.cs");

        Assert.True(File.Exists(inputPath), "Eingabemarker-Eingabe-Wiring muss in einer eigenen PlayerWindow-Partial liegen.");
        Assert.True(File.Exists(popupControlsPath), "Eingabemarker-Popup-Zustand soll ausserhalb der PlayerWindow-Partials gesetzt werden.");
        Assert.True(File.Exists(focusControlsPath), "Eingabemarker-Focus soll ueber die Player-Focus-Controls laufen.");
        Assert.True(File.Exists(inputWorkflowPath), "Eingabemarker-Key- und Auswahlentscheidungen sollen ausserhalb von PlayerWindow laufen.");
        Assert.True(File.Exists(canvasWorkflowPath), "Eingabemarker-Mausentscheidungen sollen ausserhalb von PlayerWindow laufen.");

        var marker = File.ReadAllText(markerPath);
        var input = File.ReadAllText(inputPath);
        var popupControls = File.Exists(popupControlsPath) ? File.ReadAllText(popupControlsPath) : "";
        var focusControls = File.Exists(focusControlsPath) ? File.ReadAllText(focusControlsPath) : "";
        var inputWorkflow = File.Exists(inputWorkflowPath) ? File.ReadAllText(inputWorkflowPath) : "";
        var canvasWorkflow = File.Exists(canvasWorkflowPath) ? File.ReadAllText(canvasWorkflowPath) : "";

        Assert.DoesNotContain("private void CmbEingabemarker_KeyDown", marker);
        Assert.DoesNotContain("private void CmbEingabemarker_SelectionChanged", marker);
        Assert.DoesNotContain("private static string? ResolveEingabemarkerCodeHint", marker);
        Assert.Contains("CodingEingabemarkerCanvasInputWorkflow.MouseDown", marker);
        Assert.Contains("CodingEingabemarkerCanvasInputWorkflow.MouseMove", marker);
        Assert.Contains("CodingEingabemarkerCanvasInputWorkflow.MouseUp", marker);
        Assert.DoesNotContain("if (_eingabemarkerPhase != EingabemarkerPhase.Drawing)", marker);
        Assert.Contains("PlayerDispatcherScheduler.ScheduleInput", marker);
        Assert.Contains("PlayerFocusControls.FocusElement", marker);
        Assert.DoesNotContain("Dispatcher.BeginInvoke", marker);
        Assert.DoesNotContain("new Action(() => TxtEingabemarker.Focus())", marker);
        Assert.DoesNotContain("TxtEingabemarker.Focus()", marker);
        Assert.DoesNotContain("System.Windows.Threading.DispatcherPriority.Input", marker);
        Assert.DoesNotContain("_eingabemarkerPreviewRect == null", marker);
        Assert.DoesNotContain("if (normalizedRect is null)", marker);
        Assert.Contains("CodingEingabemarkerPopupControls.ShowInput", marker);
        Assert.Contains("CodingEingabemarkerPopupControls.Hide", marker);
        Assert.Contains("CodingEingabemarkerPopupControls.IsVisible", input);
        Assert.Contains("CodingEingabemarkerPopupControls.ApplyQuickSelection", input);
        Assert.Contains("CodingEingabemarkerPopupControls.ResolveSelectedText", input);
        Assert.Contains("CodingEingabemarkerKeyInputWorkflow.Execute", input);
        Assert.Contains("CodingEingabemarkerSelectionInputWorkflow.Execute", input);
        Assert.DoesNotContain("if (e.Key == Key.Escape)", input);
        Assert.DoesNotContain("if (e.Key != Key.Enter)", input);
        Assert.DoesNotContain("CmbEingabemarker.SelectedItem is ComboBoxItem", input);
        Assert.DoesNotContain("EingabemarkerPopup.Visibility = Visibility.Visible", marker);
        Assert.DoesNotContain("EingabemarkerPopup.Visibility = Visibility.Collapsed", marker);
        Assert.DoesNotContain("TxtEingabemarker.Text = \"\"", marker);
        Assert.DoesNotContain("TxtEingabemarker.Text = text", input);
        Assert.DoesNotContain("CmbEingabemarker.SelectedIndex = -1", marker);
        Assert.DoesNotContain("EingabemarkerPopup.Visibility != Visibility.Visible", input);
        Assert.Contains("private void CmbEingabemarker_KeyDown", input);
        Assert.Contains("private void CmbEingabemarker_SelectionChanged", input);
        Assert.Contains("private static string? ResolveEingabemarkerCodeHint", input);
        Assert.Contains("SubmitEingabemarker().SafeFireAndForget", input);
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
        var markerPath = Path.Combine(uiRoot, "Views", "Windows", "PlayerWindow.Coding.Eingabemarker.cs");
        var controlsPath = Path.Combine(uiRoot, "Ai", "CodingOverlayInputControls.cs");
        var toggleWorkflowPath = Path.Combine(uiRoot, "Ai", "CodingEingabemarkerToggleWorkflow.cs");

        Assert.True(File.Exists(controlsPath), "Eingabemarker-Canvas-Zustand soll ueber den OverlayInput-Control-Adapter laufen.");
        Assert.True(File.Exists(toggleWorkflowPath), "Eingabemarker-Toggle-Reihenfolge soll ausserhalb der PlayerWindow-Partials orchestriert werden.");

        var marker = File.ReadAllText(markerPath);
        var controls = File.Exists(controlsPath) ? File.ReadAllText(controlsPath) : "";
        var toggleWorkflow = File.Exists(toggleWorkflowPath) ? File.ReadAllText(toggleWorkflowPath) : "";

        Assert.Contains("CodingEingabemarkerToggleWorkflow.Execute", marker);
        Assert.DoesNotContain("if (BtnEingabemarker.IsChecked == true)", marker);
        Assert.Contains("CodingOverlayInputControls.EnableDrawingCanvas", marker);
        Assert.Contains("CodingOverlayInputControls.DisableDrawingCanvas", marker);
        Assert.Contains("CodingOverlayInputControls.ResetCanvasCursor", marker);
        Assert.DoesNotContain("CodingOverlayCanvas.IsHitTestVisible =", marker);
        Assert.DoesNotContain("CodingOverlayCanvas.Cursor =", marker);
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
        Assert.DoesNotContain("CodingOverlayCanvas.Cursor =", joinedPartials);
        Assert.Contains("public static void ApplyCanvasCursor", controls);
    }
}
