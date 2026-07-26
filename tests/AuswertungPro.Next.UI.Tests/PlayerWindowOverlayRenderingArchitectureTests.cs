using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowOverlayRenderingArchitectureTests
{
    [Fact]
    public void PlayerWindow_coding_overlay_rendering_lives_in_controller()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var controllerPath = Path.Combine(uiRoot, "Player", "CodingOverlayRenderController.cs");
        var surfacePath = Path.Combine(uiRoot, "Player", "IOverlaySurface.cs");
        var mapperPath = Path.Combine(uiRoot, "Player", "IOverlayCoordinateMapper.cs");

        Assert.True(File.Exists(controllerPath), "Coding-Overlay-Rendering soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(surfacePath), "Coding-Overlay-Rendering braucht eine schmale Surface-Abstraktion statt direkten Canvas-Zugriff im Window.");
        Assert.True(File.Exists(mapperPath), "Coding-Overlay-Rendering braucht einen injizierten Koordinaten-Mapper.");

        var playerText = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(windowsRoot, "PlayerWindow*.cs").Select(File.ReadAllText));
        var controller = File.Exists(controllerPath) ? File.ReadAllText(controllerPath) : "";

        AssertNoForbiddenTokens(
            playerText,
            "CodingOverlayGeometryRenderer.Render",
            "CodingAiOverlayRenderer.Render",
            "ReferenceDnOverlayRenderer.Render",
            "CodingActivePipeBendSchemaRenderer.Render",
            "CodingActiveFillLevelSchemaRenderer.Render",
            "CodingActiveIntrusionSchemaRenderer.Render");
        Assert.Contains("public sealed class CodingOverlayRenderController", controller);
        Assert.Contains("IOverlaySurface", controller);
        Assert.Contains("IOverlayCoordinateMapper", controller);
        Assert.Contains("CodingOverlayGeometryRenderer.Render", controller);
        Assert.Contains("CodingAiOverlayRenderer.Render", controller);
        Assert.Contains("ReferenceDnOverlayRenderer.Render", controller);
    }

    [Fact]
    public void PlayerWindow_level_overlay_rendering_lives_in_level_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var specialShapesPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.SpecialShapes.cs");
        var levelPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.SpecialShapes.Level.cs");
        var dispatcherPath = Path.Combine(uiRoot, "Player", "CodingOverlayGeometryRenderer.cs");
        var rendererPath = Path.Combine(uiRoot, "Player", "CodingLevelOverlayRenderer.cs");

        Assert.False(File.Exists(specialShapesPath), "Das allgemeine SpecialShapes-Partial soll entfernt bleiben.");
        Assert.False(File.Exists(levelPath), "Level-Overlay-Wrapper soll nicht mehr als PlayerWindow-Partial leben.");
        Assert.True(File.Exists(dispatcherPath), "Overlay-Dispatcher soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(rendererPath), "Level-Overlay-Rendering soll ausserhalb der PlayerWindow-Partials liegen.");

        var overlayRendering = File.ReadAllText(Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.cs"));
        var dispatcher = File.ReadAllText(dispatcherPath);
        var renderer = File.ReadAllText(rendererPath);

        AssertNoForbiddenTokens(
            overlayRendering,
            "RenderLevelOverlay",
            "CodingLevelOverlayRenderer.Render");
        Assert.Contains("_codingOverlayRenderController.RenderOverlayGeometry", overlayRendering);
        Assert.Contains("CodingLevelOverlayRenderer.Render", dispatcher);
        Assert.Contains("public static class CodingLevelOverlayRenderer", renderer);
        Assert.Contains("LevelMode.Obstacle", renderer);
        Assert.Contains("CodingSchemaOverlayRenderer.AddPipeReference", renderer);
    }

    [Fact]
    public void PlayerWindow_active_schema_rendering_delegates_to_render_controller()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var activePath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.Schema.Active.cs");
        var pipeBendPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.Schema.Active.PipeBend.cs");
        var fillLevelPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.Schema.Active.FillLevel.cs");
        var intrusionPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.Schema.Active.Intrusion.cs");
        var controllerPath = Path.Combine(uiRoot, "Player", "CodingOverlayRenderController.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "Coding", "CodingActiveSchemaRenderWorkflow.cs");
        var pipeBendRendererPath = Path.Combine(uiRoot, "Player", "CodingActivePipeBendSchemaRenderer.cs");
        var intrusionRendererPath = Path.Combine(uiRoot, "Player", "CodingActiveIntrusionSchemaRenderer.cs");
        var fillLevelRendererPath = Path.Combine(uiRoot, "Player", "CodingActiveFillLevelSchemaRenderer.cs");

        Assert.False(File.Exists(pipeBendPath), "Aktives PipeBend-Rendering soll nicht mehr als PlayerWindow-Partial leben.");
        Assert.False(File.Exists(fillLevelPath), "Aktives FillLevel-Rendering soll nicht mehr als PlayerWindow-Partial leben.");
        Assert.False(File.Exists(intrusionPath), "Aktives Intrusion-Rendering soll nicht mehr als PlayerWindow-Partial leben.");
        Assert.True(File.Exists(workflowPath), "Aktive Schema-Render-Entscheidung soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(controllerPath), "Aktive Schema-Render-Orchestrierung soll im CodingOverlayRenderController liegen.");
        Assert.True(File.Exists(pipeBendRendererPath), "Aktives PipeBend-Rendering soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(intrusionRendererPath), "Aktives Intrusion-Rendering soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(fillLevelRendererPath), "Aktives FillLevel-Rendering soll ausserhalb der PlayerWindow-Partials liegen.");

        var active = File.ReadAllText(activePath);
        var workflow = File.Exists(workflowPath) ? File.ReadAllText(workflowPath) : "";
        var controller = File.ReadAllText(controllerPath);
        var pipeBendRenderer = File.ReadAllText(pipeBendRendererPath);
        var intrusionRenderer = File.ReadAllText(intrusionRendererPath);
        var fillLevelRenderer = File.ReadAllText(fillLevelRendererPath);

        Assert.Contains("CodingActiveSchemaRenderWorkflow.Execute", active);
        Assert.Contains("_codingOverlayRenderController.RenderActiveSchema", active);
        AssertNoForbiddenTokens(
            active,
            "if (!_codingSchemaManager.IsActive || _codingSchemaManager.Active == null)",
            "switch (_codingSchemaManager.Active)",
            "case PipeBendSchema bend",
            "case FillLevelSchema fill",
            "case IntrusionSchema intrusion",
            "RenderPipeBendOverlay(overlay, true, Brushes.Gold",
            "new Rectangle",
            "new System.Windows.Shapes.Polygon");
        Assert.Contains("if (!request.IsActive)", workflow);
        Assert.Contains("actions.BuildOverlay()", workflow);
        Assert.Contains("actions.RenderPipeBend", workflow);
        Assert.Contains("PipeBendSchema bend => CodingActivePipeBendSchemaRenderer.Render", controller);
        Assert.Contains("FillLevelSchema fill => CodingActiveFillLevelSchemaRenderer.Render", controller);
        Assert.Contains("IntrusionSchema intrusion => CodingActiveIntrusionSchemaRenderer.Render", controller);
        Assert.Contains("public static class CodingActivePipeBendSchemaRenderer", pipeBendRenderer);
        Assert.Contains("CodingPipeBendOverlayRenderer.Render", pipeBendRenderer);
        Assert.Contains("new System.Windows.Shapes.Line", pipeBendRenderer);
        Assert.Contains("CodingOverlayDotMarkerRenderer.Add", pipeBendRenderer);
        Assert.Contains("public static class CodingActiveFillLevelSchemaRenderer", fillLevelRenderer);
        Assert.Contains("new Rectangle", fillLevelRenderer);
        Assert.Contains("new System.Windows.Shapes.Line", fillLevelRenderer);
        Assert.Contains("CodingOverlayDotMarkerRenderer.Add", fillLevelRenderer);
        Assert.Contains("public static class CodingActiveIntrusionSchemaRenderer", intrusionRenderer);
        Assert.Contains("new System.Windows.Shapes.Polygon", intrusionRenderer);
        Assert.Contains("new System.Windows.Shapes.Line", intrusionRenderer);
        Assert.Contains("CodingOverlayDotMarkerRenderer.Add", intrusionRenderer);
    }

    [Fact]
    public void PlayerWindow_active_schema_rendering_lives_in_active_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var schemaPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.Schema.cs");
        var activePath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.Schema.Active.cs");
        var workflowPath = Path.Combine(uiRoot, "Ai", "Coding", "CodingActiveSchemaRenderWorkflow.cs");
        var rendererPath = Path.Combine(uiRoot, "Player", "CodingSchemaOverlayRenderer.cs");

        Assert.True(File.Exists(activePath), "Aktive Schema-Vorschau soll aus dem allgemeinen Schema-Rendering-Partial heraus.");
        Assert.True(File.Exists(workflowPath), "Aktive Schema-Render-Entscheidung soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(rendererPath), "Schema-Canvas-Helfer sollen ausserhalb der PlayerWindow-Partials liegen.");

        var schema = File.ReadAllText(schemaPath);
        var active = File.ReadAllText(activePath);
        var renderer = File.ReadAllText(rendererPath);

        AssertNoForbiddenTokens(
            schema,
            "private void RenderActiveCodingSchema",
            "private void RenderSchemaPipeReference",
            "private void AddSchemaLabel");
        Assert.Contains("private void RenderActiveCodingSchema", active);
        Assert.Contains("CodingActiveSchemaRenderWorkflow.Execute", active);
        AssertNoForbiddenTokens(
            active,
            "case PipeBendSchema bend",
            "case FillLevelSchema fill",
            "case IntrusionSchema intrusion");
        Assert.Contains("public static class CodingSchemaOverlayRenderer", renderer);
        Assert.Contains("AddPipeReference", renderer);
        Assert.Contains("AddLabel", renderer);
    }

    [Fact]
    public void PlayerWindow_reference_dn_rendering_lives_in_renderer()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var schemaPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.Schema.cs");
        var statePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.State.cs");
        var rendererPath = Path.Combine(uiRoot, "Player", "ReferenceDnOverlayRenderer.cs");
        var stateControllerPath = Path.Combine(uiRoot, "Player", "CodingOverlayRenderStateController.cs");

        Assert.True(File.Exists(rendererPath), "Ref-DN-Canvas-Rendering soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(stateControllerPath), "Ref-DN-Sichtbarkeit soll in einem kleinen Overlay-Render-State liegen.");

        var schema = File.ReadAllText(schemaPath);
        var state = File.ReadAllText(statePath);
        var renderer = File.ReadAllText(rendererPath);
        var stateController = File.Exists(stateControllerPath) ? File.ReadAllText(stateControllerPath) : "";

        Assert.Contains("_codingOverlayRenderController.RenderReferenceDn", schema);
        Assert.Contains("_codingOverlayRenderState.ShowReferenceDn", schema);
        Assert.Contains("_codingOverlayRenderState", state);
        AssertNoForbiddenTokens(schema + state, "_showReferenceDn");
        AssertNoForbiddenTokens(
            schema,
            "ReferenceDnGeometry.BuildCircleRect",
            "Ref: DN");
        Assert.Contains("public static class ReferenceDnOverlayRenderer", renderer);
        Assert.Contains("ReferenceDnGeometry.BuildCircleRect", renderer);
        Assert.Contains("new System.Windows.Shapes.Ellipse", renderer);
        Assert.Contains("public void ShowReferenceDiameter", stateController);
    }

    [Fact]
    public void PlayerWindow_arc_overlay_rendering_lives_in_renderer()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var overlayRenderingPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.cs");
        var aiRenderingPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.AiOverlayRendering.cs");
        var specialShapesPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.SpecialShapes.cs");
        var dispatcherPath = Path.Combine(uiRoot, "Player", "CodingOverlayGeometryRenderer.cs");
        var rendererPath = Path.Combine(uiRoot, "Player", "CodingArcOverlayRenderer.cs");
        var aiRendererPath = Path.Combine(uiRoot, "Player", "CodingAiOverlayRenderer.cs");

        Assert.False(File.Exists(specialShapesPath), "Das allgemeine SpecialShapes-Partial soll nach der Arc-Extraktion entfernt bleiben.");
        Assert.True(File.Exists(dispatcherPath), "Overlay-Dispatcher soll Arc-Rendering ausserhalb von PlayerWindow erreichen.");
        Assert.True(File.Exists(rendererPath), "Arc-Overlay-Rendering soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(aiRendererPath), "AI-Overlay-Orchestrierung soll Arc-Rendering ebenfalls ausserhalb von PlayerWindow erreichen.");

        var overlayRendering = File.ReadAllText(overlayRenderingPath);
        var aiRendering = File.ReadAllText(aiRenderingPath);
        var dispatcher = File.ReadAllText(dispatcherPath);
        var renderer = File.ReadAllText(rendererPath);
        var aiRenderer = File.ReadAllText(aiRendererPath);

        Assert.Contains("_codingOverlayRenderController.RenderOverlayGeometry", overlayRendering);
        AssertNoForbiddenTokens(
            overlayRendering,
            "CodingArcOverlayRenderer.Render",
            "CreateArcPath");
        Assert.Contains("CodingArcOverlayRenderer.Render", dispatcher);
        Assert.Contains("_codingOverlayRenderController.RenderAiOverlays", aiRendering);
        Assert.Contains("CodingArcOverlayRenderer.Render", aiRenderer);
        AssertNoForbiddenTokens(aiRendering, "CreateArcPath");
        Assert.Contains("public static class CodingArcOverlayRenderer", renderer);
        Assert.Contains("new System.Windows.Shapes.Path", renderer);
        Assert.Contains("new ArcSegment", renderer);
    }

    [Fact]
    public void PlayerWindow_ruler_overlay_rendering_lives_in_ruler_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var specialShapesPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.SpecialShapes.cs");
        var rulerPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.SpecialShapes.Ruler.cs");
        var dispatcherPath = Path.Combine(uiRoot, "Player", "CodingOverlayGeometryRenderer.cs");
        var rendererPath = Path.Combine(uiRoot, "Player", "CodingRulerOverlayRenderer.cs");

        Assert.False(File.Exists(specialShapesPath), "Das allgemeine SpecialShapes-Partial soll entfernt bleiben.");
        Assert.False(File.Exists(rulerPath), "Ruler-Overlay-Wrapper soll nicht mehr als PlayerWindow-Partial leben.");
        Assert.True(File.Exists(dispatcherPath), "Overlay-Dispatcher soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(rendererPath), "Ruler-Overlay-Rendering soll ausserhalb der PlayerWindow-Partials liegen.");

        var overlayRendering = File.ReadAllText(Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.cs"));
        var dispatcher = File.ReadAllText(dispatcherPath);
        var renderer = File.ReadAllText(rendererPath);

        AssertNoForbiddenTokens(
            overlayRendering,
            "RenderRulerOverlay",
            "CodingRulerOverlayRenderer.Render");
        Assert.Contains("_codingOverlayRenderController.RenderOverlayGeometry", overlayRendering);
        Assert.Contains("CodingRulerOverlayRenderer.Render", dispatcher);
        Assert.Contains("public static class CodingRulerOverlayRenderer", renderer);
        Assert.Contains("new System.Windows.Shapes.Line", renderer);
        Assert.Contains("new TextBlock", renderer);
        Assert.Contains("TickInterval", renderer);
        Assert.Contains("totalMm:F1", renderer);
    }

    [Fact]
    public void PlayerWindow_pipe_bend_overlay_rendering_lives_in_pipe_bend_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var specialShapesPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.SpecialShapes.cs");
        var pipeBendPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.SpecialShapes.PipeBend.cs");
        var helperPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.Helpers.cs");
        var dispatcherPath = Path.Combine(uiRoot, "Player", "CodingOverlayGeometryRenderer.cs");
        var dotRendererPath = Path.Combine(uiRoot, "Player", "CodingOverlayDotMarkerRenderer.cs");
        var rendererPath = Path.Combine(uiRoot, "Player", "CodingPipeBendOverlayRenderer.cs");

        Assert.False(File.Exists(specialShapesPath), "Das allgemeine SpecialShapes-Partial soll entfernt bleiben.");
        Assert.False(File.Exists(pipeBendPath), "Pipe-Bend-Overlay-Wrapper soll nicht mehr als PlayerWindow-Partial leben.");
        Assert.False(File.Exists(helperPath), "Dot-Marker-Rendering soll nicht mehr als PlayerWindow-Partial leben.");
        Assert.True(File.Exists(dispatcherPath), "Overlay-Dispatcher soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(dotRendererPath), "Dot-Marker-Rendering soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(rendererPath), "Pipe-Bend-Overlay-Rendering soll ausserhalb der PlayerWindow-Partials liegen.");

        var overlayRendering = File.ReadAllText(Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.cs"));
        var dispatcher = File.ReadAllText(dispatcherPath);
        var dotRenderer = File.ReadAllText(dotRendererPath);
        var renderer = File.ReadAllText(rendererPath);

        AssertNoForbiddenTokens(
            overlayRendering,
            "RenderPipeBendOverlay",
            "CodingPipeBendOverlayRenderer.Render");
        Assert.Contains("_codingOverlayRenderController.RenderOverlayGeometry", overlayRendering);
        Assert.Contains("CodingPipeBendOverlayRenderer.Render", dispatcher);
        Assert.Contains("public static class CodingOverlayDotMarkerRenderer", dotRenderer);
        Assert.Contains("new System.Windows.Shapes.Ellipse", dotRenderer);
        Assert.Contains("public static class CodingPipeBendOverlayRenderer", renderer);
        Assert.Contains("overlay.ArcDegrees", renderer);
        Assert.Contains("new System.Windows.Shapes.Line", renderer);
        Assert.Contains("CodingOverlayDotMarkerRenderer.Add", renderer);
    }

    [Fact]
    public void PlayerWindow_lateral_circle_overlay_rendering_lives_in_lateral_circle_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var specialShapesPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.SpecialShapes.cs");
        var lateralCirclePath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.SpecialShapes.LateralCircle.cs");
        var dispatcherPath = Path.Combine(uiRoot, "Player", "CodingOverlayGeometryRenderer.cs");
        var rendererPath = Path.Combine(uiRoot, "Player", "CodingLateralCircleOverlayRenderer.cs");

        Assert.False(File.Exists(specialShapesPath), "Das allgemeine SpecialShapes-Partial soll entfernt bleiben.");
        Assert.False(File.Exists(lateralCirclePath), "Lateral-Circle-Overlay-Wrapper soll nicht mehr als PlayerWindow-Partial leben.");
        Assert.True(File.Exists(dispatcherPath), "Overlay-Dispatcher soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(rendererPath), "Lateral-Circle-Overlay-Rendering soll ausserhalb der PlayerWindow-Partials liegen.");

        var overlayRendering = File.ReadAllText(Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.cs"));
        var dispatcher = File.ReadAllText(dispatcherPath);
        var renderer = File.ReadAllText(rendererPath);

        AssertNoForbiddenTokens(
            overlayRendering,
            "RenderLateralCircleOverlay",
            "CodingLateralCircleOverlayRenderer.Render");
        Assert.Contains("_codingOverlayRenderController.RenderOverlayGeometry", overlayRendering);
        Assert.Contains("CodingLateralCircleOverlayRenderer.Render", dispatcher);
        Assert.Contains("public static class CodingLateralCircleOverlayRenderer", renderer);
        Assert.Contains("overlay.DnRatioPercent", renderer);
        Assert.Contains("DN {overlay.Q1Mm.Value:F0}", renderer);
        Assert.Contains("new System.Windows.Shapes.Ellipse", renderer);
    }

    [Fact]
    public void PlayerWindow_overlay_measurement_panel_lives_in_measurement_panel_partial()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var overlayRenderingPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.cs");
        var measurementPanelPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.MeasurementPanel.cs");
        var controlsPath = Path.Combine(uiRoot, "Ai", "Coding", "CodingMeasurementPanelControls.cs");

        Assert.True(File.Exists(measurementPanelPath), "Overlay-Messwert-Panel soll aus dem allgemeinen OverlayRendering-Partial heraus.");
        Assert.True(File.Exists(controlsPath), "Overlay-Messwert-Panel-Control-Zuweisungen sollen ausserhalb des PlayerWindow-Partials liegen.");

        var overlayRendering = File.ReadAllText(overlayRenderingPath);
        var measurementPanel = File.ReadAllText(measurementPanelPath);
        var controls = File.ReadAllText(controlsPath);

        AssertNoForbiddenTokens(overlayRendering, "private void UpdateCodingOverlayInfo");
        Assert.Contains("private void UpdateCodingOverlayInfo", measurementPanel);
        Assert.Contains("CodingOverlayMeasurementFormatter.BuildPanelState", measurementPanel);
        Assert.Contains("CodingMeasurementPanelControls.Apply", measurementPanel);
        AssertNoForbiddenTokens(
            measurementPanel,
            "CodingMeasurementPanel.Visibility",
            "TxtCodingMeasurement.Text");
        Assert.Contains("public static void Apply", controls);
    }

    [Fact]
    public void PlayerWindow_overlay_measurement_label_lives_in_renderer()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var overlayRenderingPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.cs");
        var dispatcherPath = Path.Combine(uiRoot, "Player", "CodingOverlayGeometryRenderer.cs");
        var rendererPath = Path.Combine(uiRoot, "Player", "CodingOverlayMeasurementLabelRenderer.cs");

        Assert.True(File.Exists(dispatcherPath), "Overlay-Dispatcher soll Messlabel ausserhalb von PlayerWindow erreichen.");
        Assert.True(File.Exists(rendererPath), "Overlay-Messlabel soll ausserhalb der PlayerWindow-Partials gerendert werden.");

        var overlayRendering = File.ReadAllText(overlayRenderingPath);
        var dispatcher = File.ReadAllText(dispatcherPath);
        var renderer = File.ReadAllText(rendererPath);

        Assert.Contains("_codingOverlayRenderController.RenderOverlayGeometry", overlayRendering);
        AssertNoForbiddenTokens(
            overlayRendering,
            "CodingOverlayMeasurementLabelRenderer.Add",
            "new TextBlock",
            "FontWeights.SemiBold");
        Assert.Contains("CodingOverlayMeasurementLabelRenderer.Add", dispatcher);
        Assert.Contains("public static class CodingOverlayMeasurementLabelRenderer", renderer);
        Assert.Contains("new TextBlock", renderer);
        Assert.Contains("FontWeights.SemiBold", renderer);
    }

    [Fact]
    public void PlayerWindow_basic_overlay_shape_rendering_lives_in_renderer()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var overlayRenderingPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.cs");
        var basicShapesPath = Path.Combine(windowsRoot, "PlayerWindow.OverlayRendering.BasicShapes.cs");
        var dispatcherPath = Path.Combine(uiRoot, "Player", "CodingOverlayGeometryRenderer.cs");
        var rendererPath = Path.Combine(uiRoot, "Player", "CodingBasicOverlayRenderer.cs");

        Assert.False(File.Exists(basicShapesPath), "Basisformen-Wrapper sollen nicht mehr als PlayerWindow-Partial leben.");
        Assert.True(File.Exists(dispatcherPath), "Overlay-Dispatcher soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(rendererPath), "Basisformen-Rendering soll ausserhalb der PlayerWindow-Partials gerendert werden.");

        var overlayRendering = File.ReadAllText(overlayRenderingPath);
        var dispatcher = File.ReadAllText(dispatcherPath);
        var renderer = File.ReadAllText(rendererPath);

        AssertNoForbiddenTokens(
            overlayRendering,
            "var rect = new Rectangle",
            "var dot = new System.Windows.Shapes.Ellipse",
            "var poly = new System.Windows.Shapes.Polygon",
            "RenderLineOverlay",
            "RenderRectangleOverlay",
            "RenderPointOverlay",
            "RenderEllipseOverlay",
            "RenderFreehandOverlay",
            "switch (overlay.ToolType)",
            "new SolidColorBrush",
            "CodingBasicOverlayRenderer.Render");
        Assert.Contains("_codingOverlayRenderController.RenderOverlayGeometry", overlayRendering);
        Assert.Contains("public static class CodingOverlayGeometryRenderer", dispatcher);
        Assert.Contains("switch (overlay.ToolType)", dispatcher);
        Assert.Contains("CodingBasicOverlayRenderer.Render", dispatcher);
        Assert.Contains("public static class CodingBasicOverlayRenderer", renderer);
        Assert.Contains("new Rectangle", renderer);
        Assert.Contains("new System.Windows.Shapes.Line", renderer);
        Assert.Contains("new System.Windows.Shapes.Polygon", renderer);
    }

    [Fact]
    public void PlayerWindow_ai_overlay_shape_rendering_lives_in_player_renderers()
    {
        var root = FindRepositoryRoot();
        var uiRoot = Path.Combine(root, "src", "AuswertungPro.Next.UI");
        var windowsRoot = Path.Combine(uiRoot, "Views", "Windows");
        var aiOverlayPath = Path.Combine(windowsRoot, "PlayerWindow.Coding.AiOverlayRendering.cs");
        var rectanglePath = Path.Combine(windowsRoot, "PlayerWindow.Coding.AiOverlayRendering.Rectangle.cs");
        var cleanupPolicyPath = Path.Combine(uiRoot, "Player", "CodingOverlayCleanupPolicy.cs");
        var renderCommandWorkflowPath = Path.Combine(uiRoot, "Ai", "Coding", "CodingAiOverlayRenderCommandWorkflow.cs");
        var aiRendererPath = Path.Combine(uiRoot, "Player", "CodingAiOverlayRenderer.cs");
        var primitiveRendererPath = Path.Combine(uiRoot, "Player", "CodingAiPrimitiveOverlayRenderer.cs");
        var rectangleRendererPath = Path.Combine(uiRoot, "Player", "CodingAiRectangleOverlayRenderer.cs");

        Assert.False(File.Exists(rectanglePath), "AI-Rechteck-Overlay soll nicht mehr als PlayerWindow-Partial leben.");
        Assert.True(File.Exists(cleanupPolicyPath), "AI-Overlay-Cleanup-Regel soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(renderCommandWorkflowPath), "AI-Overlay-Render-Gate soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(aiRendererPath), "AI-Overlay-Orchestrierung soll ausserhalb der PlayerWindow-Partials liegen.");
        Assert.True(File.Exists(primitiveRendererPath), "AI-Primitive sollen ausserhalb der PlayerWindow-Partials gerendert werden.");
        Assert.True(File.Exists(rectangleRendererPath), "AI-Rechteck-Overlay mit Label soll ausserhalb der PlayerWindow-Partials gerendert werden.");

        var aiOverlay = File.ReadAllText(aiOverlayPath);
        var cleanupPolicy = File.ReadAllText(cleanupPolicyPath);
        var renderCommandWorkflow = File.Exists(renderCommandWorkflowPath) ? File.ReadAllText(renderCommandWorkflowPath) : "";
        var aiRenderer = File.ReadAllText(aiRendererPath);
        var primitiveRenderer = File.ReadAllText(primitiveRendererPath);
        var rectangleRenderer = File.ReadAllText(rectangleRendererPath);

        Assert.Contains("CodingAiOverlayRenderCommandWorkflow.Execute", aiOverlay);
        Assert.Contains("_codingOverlayRenderController.RenderAiOverlays", aiOverlay);
        Assert.Contains("_codingSessionHost", aiOverlay);
        AssertNoForbiddenTokens(
            aiOverlay,
            "RenderAiRectangleOverlay(",
            "if (!_codingSessionHost.HasViewModel) return",
            "CodingAiRectangleOverlayRenderer.Render",
            "CodingAiPrimitiveOverlayRenderer.Render",
            "CodingOverlayCleanupPolicy.ShouldRemoveAiOverlayTag",
            "CodingAiOverlayDisplayPolicy.StrokeColor",
            "switch (geo.ToolType)",
            "StartsWith(OverlayTags.AiPrefix",
            "var labelBorder = new Border",
            "CodingAiOverlayDisplayPolicy.LabelText",
            "new System.Windows.Shapes.Line",
            "new System.Windows.Shapes.Ellipse");
        Assert.Contains("if (!request.HasCodingViewModel)", renderCommandWorkflow);
        Assert.Contains("actions.RenderAiOverlays()", renderCommandWorkflow);
        Assert.Contains("public static bool ShouldRemoveAiOverlayTag", cleanupPolicy);
        Assert.Contains("StartsWith(OverlayTags.AiPrefix", cleanupPolicy);
        Assert.Contains("public static class CodingAiOverlayRenderer", aiRenderer);
        Assert.Contains("CodingOverlayCleanupPolicy.ShouldRemoveAiOverlayTag", aiRenderer);
        Assert.Contains("CodingAiOverlayDisplayPolicy.StrokeColor", aiRenderer);
        Assert.Contains("CodingAiPrimitiveOverlayRenderer.Render", aiRenderer);
        Assert.Contains("CodingAiRectangleOverlayRenderer.Render", aiRenderer);
        Assert.Contains("CodingArcOverlayRenderer.Render", aiRenderer);
        Assert.Contains("public static class CodingAiPrimitiveOverlayRenderer", primitiveRenderer);
        Assert.Contains("new System.Windows.Shapes.Line", primitiveRenderer);
        Assert.Contains("new System.Windows.Shapes.Ellipse", primitiveRenderer);
        Assert.Contains("public static class CodingAiRectangleOverlayRenderer", rectangleRenderer);
        Assert.Contains("var labelBorder = new Border", rectangleRenderer);
        Assert.Contains("CodingAiOverlayDisplayPolicy.LabelText", rectangleRenderer);
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
            "Verbotene alte PlayerWindow-Overlay-Rendering-Logik gefunden: " + string.Join(", ", hits));
    }
}
