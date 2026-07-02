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
}
