using System;
using System.IO;
using System.Linq;
using static AuswertungPro.Next.UI.Tests.SourceTextTestHelpers;

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

        Assert.DoesNotContain("CodingOverlayGeometryRenderer.Render", playerText);
        Assert.DoesNotContain("CodingAiOverlayRenderer.Render", playerText);
        Assert.DoesNotContain("ReferenceDnOverlayRenderer.Render", playerText);
        Assert.DoesNotContain("CodingActivePipeBendSchemaRenderer.Render", playerText);
        Assert.DoesNotContain("CodingActiveFillLevelSchemaRenderer.Render", playerText);
        Assert.DoesNotContain("CodingActiveIntrusionSchemaRenderer.Render", playerText);
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

        Assert.DoesNotContain("RenderLevelOverlay", overlayRendering);
        Assert.Contains("_codingOverlayRenderController.RenderOverlayGeometry", overlayRendering);
        Assert.DoesNotContain("CodingLevelOverlayRenderer.Render", overlayRendering);
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
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingActiveSchemaRenderWorkflow.cs");
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
        Assert.DoesNotContain("if (!_codingSchemaManager.IsActive || _codingSchemaManager.Active == null)", active);
        Assert.DoesNotContain("switch (_codingSchemaManager.Active)", active);
        Assert.DoesNotContain("case PipeBendSchema bend", active);
        Assert.DoesNotContain("case FillLevelSchema fill", active);
        Assert.DoesNotContain("case IntrusionSchema intrusion", active);
        Assert.Contains("if (!request.IsActive)", workflow);
        Assert.Contains("actions.BuildOverlay()", workflow);
        Assert.Contains("actions.RenderPipeBend", workflow);
        Assert.Contains("PipeBendSchema bend => CodingActivePipeBendSchemaRenderer.Render", controller);
        Assert.Contains("FillLevelSchema fill => CodingActiveFillLevelSchemaRenderer.Render", controller);
        Assert.Contains("IntrusionSchema intrusion => CodingActiveIntrusionSchemaRenderer.Render", controller);
        Assert.DoesNotContain("RenderPipeBendOverlay(overlay, true, Brushes.Gold", active);
        Assert.DoesNotContain("new Rectangle", active);
        Assert.DoesNotContain("new System.Windows.Shapes.Polygon", active);
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
        var workflowPath = Path.Combine(uiRoot, "Ai", "CodingActiveSchemaRenderWorkflow.cs");
        var rendererPath = Path.Combine(uiRoot, "Player", "CodingSchemaOverlayRenderer.cs");

        Assert.True(File.Exists(activePath), "Aktive Schema-Vorschau soll aus dem allgemeinen Schema-Rendering-Partial heraus.");
        Assert.True(File.Exists(workflowPath), "Aktive Schema-Render-Entscheidung soll ausserhalb der PlayerWindow-Partials orchestriert werden.");
        Assert.True(File.Exists(rendererPath), "Schema-Canvas-Helfer sollen ausserhalb der PlayerWindow-Partials liegen.");

        var schema = File.ReadAllText(schemaPath);
        var active = File.ReadAllText(activePath);
        var renderer = File.ReadAllText(rendererPath);

        Assert.DoesNotContain("private void RenderActiveCodingSchema", schema);
        Assert.DoesNotContain("private void RenderSchemaPipeReference", schema);
        Assert.DoesNotContain("private void AddSchemaLabel", schema);
        Assert.Contains("private void RenderActiveCodingSchema", active);
        Assert.Contains("CodingActiveSchemaRenderWorkflow.Execute", active);
        Assert.DoesNotContain("case PipeBendSchema bend", active);
        Assert.DoesNotContain("case FillLevelSchema fill", active);
        Assert.DoesNotContain("case IntrusionSchema intrusion", active);
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
        Assert.DoesNotContain("_showReferenceDn", schema + state);
        Assert.DoesNotContain("ReferenceDnGeometry.BuildCircleRect", schema);
        Assert.DoesNotContain("Ref: DN", schema);
        Assert.Contains("public static class ReferenceDnOverlayRenderer", renderer);
        Assert.Contains("ReferenceDnGeometry.BuildCircleRect", renderer);
        Assert.Contains("new System.Windows.Shapes.Ellipse", renderer);
        Assert.Contains("public void ShowReferenceDiameter", stateController);
    }
}
