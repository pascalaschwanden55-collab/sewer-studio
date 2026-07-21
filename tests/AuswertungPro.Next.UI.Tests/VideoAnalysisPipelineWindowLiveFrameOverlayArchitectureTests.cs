using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VideoAnalysisPipelineWindowLiveFrameOverlayArchitectureTests
{
    [Fact]
    public void Pipelinefenster_delegiert_Live_Ring_an_eigenen_Renderer()
    {
        var window = ReadUi("Views", "Windows", "VideoAnalysisPipelineWindow.xaml.cs");
        var start = window.IndexOf("private void RenderLiveFrameOverlay()", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var end = window.IndexOf("private void ForwardLiveFrame", start, StringComparison.Ordinal);
        Assert.True(end > start);
        var method = window[start..end];

        Assert.Contains("PipelineLiveFrameOverlayRenderer.Render(", method);
        Assert.Contains("Vm.LiveFrameImage is not null", method);
        Assert.Contains("_liveFrameFindings", method);
        Assert.Contains("LiveFrameOverlayCanvas.ActualWidth", method);
        Assert.Contains("LiveFrameOverlayCanvas.ActualHeight", method);
        Assert.DoesNotContain("Children.Add", method);
        Assert.DoesNotContain("new Ellipse", method);
        Assert.DoesNotContain("new Line", method);
        Assert.DoesNotContain("new Path", method);
        Assert.DoesNotContain("new Border", method);
        Assert.DoesNotContain("MapLiveSeverityColor", method);
    }

    [Fact]
    public void Fortschritt_mit_Bild_und_Befunden_zeichnet_Live_Ring_nur_einmal()
    {
        var window = ReadUi("Views", "Windows", "VideoAnalysisPipelineWindow.xaml.cs");
        var start = window.IndexOf("var effects = progressMapper.Apply(p);", StringComparison.Ordinal);
        Assert.True(start >= 0);
        var end = window.IndexOf("var result = await _pipeline.RunAsync", start, StringComparison.Ordinal);
        Assert.True(end > start);
        var progressBlock = window[start..end];

        Assert.Contains("effects.RenderLiveFrameOverlay", progressBlock);
        Assert.Equal(1, CountOccurrences(progressBlock, "RenderLiveFrameOverlay();"));
    }

    [Fact]
    public void Abgedocktes_Fenster_verwendet_gemeinsamen_Ring_und_zeichnet_nach_Groessenaenderung()
    {
        var window = ReadUi("Views", "Windows", "LiveFrameWindow.xaml.cs");

        Assert.Contains("LiveFrameRingOverlayRenderer.Draw(", window);
        Assert.Contains("OverlayCanvas.SizeChanged", window);
        Assert.DoesNotContain("OverlayCanvas.Children.Add", window);
        Assert.DoesNotContain("private static int? ParseClockHour", window);
        Assert.DoesNotContain("private static Geometry BuildRingSectorGeometry", window);
        Assert.DoesNotContain("private static Color MapSeverityColor", window);
        Assert.DoesNotContain("private static string BuildFindingLabel", window);

        var renderStart = window.IndexOf("private void RenderOverlay()", StringComparison.Ordinal);
        Assert.True(renderStart >= 0);
        var sizeGuard = window.IndexOf("if (width < 60 || height < 60)", renderStart, StringComparison.Ordinal);
        var clear = window.IndexOf("OverlayCanvas.Children.Clear();", renderStart, StringComparison.Ordinal);
        Assert.True(sizeGuard > renderStart && clear > sizeGuard);
    }

    [Fact]
    public void Player_Ringfallback_verwendet_gemeinsamen_Ring_ohne_BBox_Logik_zu_verlieren()
    {
        var renderer = ReadUi("Ai", "LiveDetectionOverlayRenderer.cs");

        Assert.Contains("LiveFrameRingOverlayRenderer.Draw(", renderer);
        Assert.Contains("LiveFrameRingOverlayRenderer.DrawFinding(", renderer);
        Assert.Contains("BBoxToCanvasRect", renderer);
        Assert.DoesNotContain("private static void RenderRingSectorOverlay", renderer);
        Assert.DoesNotContain("private static void RenderRingSectorFinding", renderer);
    }

    private static string ReadUi(params string[] path)
        => File.ReadAllText(RepoFile(["src", "AuswertungPro.Next.UI", .. path]));

    private static int CountOccurrences(string source, string value)
        => source.Split(value, StringSplitOptions.None).Length - 1;
}
