using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VideoAnalysisPipelineWindowRadarArchitectureTests
{
    [Fact]
    public void Fenster_delegiert_Radarzeichnung_an_eigenen_Renderer()
    {
        var window = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Windows",
            "VideoAnalysisPipelineWindow.xaml.cs"));
        var renderer = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Windows",
            "PipelinePipeRadarRenderer.cs"));

        var methodStart = window.IndexOf("private void RenderPipeRadar()", StringComparison.Ordinal);
        Assert.True(methodStart >= 0);
        var methodEnd = window.IndexOf("private void Undock_Click", methodStart, StringComparison.Ordinal);
        Assert.True(methodEnd > methodStart);

        var method = window[methodStart..methodEnd];
        Assert.Contains("PipelinePipeRadarRenderer.Render(", method);
        Assert.Contains("Vm.Detections", method);
        Assert.Contains("_overlayMode", method);
        Assert.Contains("PipeRadarCanvas.ActualWidth", method);
        Assert.Contains("PipeRadarCanvas.ActualHeight", method);
        Assert.DoesNotContain("PipeRadarCanvas.Children.Add", method);
        Assert.Contains("LiveDetectionGeometryMapper.ParseClockHour", renderer);
        Assert.Contains("LiveDetectionGeometryMapper.BuildRingSectorGeometry", renderer);
        Assert.Contains("LiveDetectionGeometryMapper.DegToRad", renderer);
    }

    [Fact]
    public void Fenster_zeichnet_beim_Ersetzen_der_Befundliste_nur_einmal_neu()
    {
        var window = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Windows",
            "VideoAnalysisPipelineWindow.xaml.cs"));

        var resultStart = window.IndexOf("var visibleDetections =", StringComparison.Ordinal);
        Assert.True(resultStart >= 0);
        var resultEnd = window.IndexOf("catch (OperationCanceledException)", resultStart, StringComparison.Ordinal);
        Assert.True(resultEnd > resultStart);
        var resultBlock = window[resultStart..resultEnd];
        Assert.Equal(1, CountOccurrences(resultBlock, "ReplaceVisibleDetections("));
        Assert.DoesNotContain("RenderPipeRadar();", resultBlock);
        Assert.DoesNotContain("Vm.Detections.Add", resultBlock);

        var helperStart = window.IndexOf("private void ReplaceVisibleDetections(", StringComparison.Ordinal);
        Assert.True(helperStart >= 0);
        var helperEnd = window.IndexOf("private double GetSelectedFrameStep", helperStart, StringComparison.Ordinal);
        Assert.True(helperEnd > helperStart);
        var helper = window[helperStart..helperEnd];
        Assert.Contains("CollectionChanged -= OnDetectionsChanged", helper);
        Assert.Contains("CollectionChanged += OnDetectionsChanged", helper);
        Assert.Equal(1, CountOccurrences(helper, "RenderPipeRadar();"));
    }

    private static int CountOccurrences(string source, string value)
        => source.Split(value, StringSplitOptions.None).Length - 1;
}
