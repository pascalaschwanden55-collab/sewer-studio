using System.IO;
using static AuswertungPro.Next.UI.Tests.TestRepoPaths;

namespace AuswertungPro.Next.UI.Tests;

public sealed class VideoAnalysisPipelineWindowResultArchitectureTests
{
    [Fact]
    public void Fenster_delegiert_Ergebnisdarstellung_an_Presenter()
    {
        var window = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Windows",
            "VideoAnalysisPipelineWindow.xaml.cs"));
        var resultStart = window.IndexOf("_result = result;", StringComparison.Ordinal);
        var resultEnd = window.IndexOf("catch (OperationCanceledException)", resultStart, StringComparison.Ordinal);

        Assert.True(resultStart >= 0 && resultEnd > resultStart);
        var resultBlock = window[resultStart..resultEnd];
        Assert.Contains("PipelineResultPresenter.ApplySuccessful(Vm, result)", resultBlock);
        Assert.Contains("var visibleDetections =", resultBlock);
        Assert.Contains("ReplaceVisibleDetections(visibleDetections)", resultBlock);
        Assert.Contains("Vm.SetError", resultBlock);
        Assert.DoesNotContain("PillarDetectionCount", resultBlock);
        Assert.DoesNotContain("PipelineTelemetryFormatter", resultBlock);
        Assert.DoesNotContain("TelemetryFmt", resultBlock);
        Assert.DoesNotContain("DetectionItem.From", resultBlock);
        Assert.DoesNotContain("result.Stats", resultBlock);
        Assert.DoesNotContain("result.Telemetry", resultBlock);
        Assert.DoesNotContain("result.Detections", resultBlock);
        Assert.DoesNotContain("result.MappedEntries", resultBlock);

        var guard = resultBlock.IndexOf("if (!result.IsSuccess)", StringComparison.Ordinal);
        var setError = resultBlock.IndexOf("Vm.SetError", guard, StringComparison.Ordinal);
        var guardReturn = resultBlock.IndexOf("return;", setError, StringComparison.Ordinal);
        var presenter = resultBlock.IndexOf("PipelineResultPresenter.ApplySuccessful", StringComparison.Ordinal);
        Assert.True(guard >= 0 && setError > guard && guardReturn > setError && presenter > guardReturn);
        Assert.Equal(1, CountOccurrences(resultBlock, "ReplaceVisibleDetections("));
        Assert.Contains("Vm.StatusText = \"Fertig. Du kannst jetzt übertragen.\"", resultBlock);
        Assert.Contains("Vm.PhaseLabel = \"Fertig\"", resultBlock);
    }

    [Fact]
    public void Presenter_bleibt_ohne_Fenster_Zeichnung_und_Lifecycle()
    {
        var presenter = File.ReadAllText(RepoFile(
            "src",
            "AuswertungPro.Next.UI",
            "Views",
            "Windows",
            "PipelineResultPresenter.cs"));

        Assert.DoesNotContain("Canvas", presenter);
        Assert.DoesNotContain("using System.Windows", presenter);
        Assert.DoesNotContain("RenderPipeRadar", presenter);
        Assert.DoesNotContain("Vm.Detections", presenter);
        Assert.DoesNotContain("viewModel.Detections", presenter);
        Assert.DoesNotContain("IsDone", presenter);
        Assert.DoesNotContain("HasError", presenter);
        Assert.DoesNotContain("StatusText", presenter);
        Assert.DoesNotContain("PhaseLabel", presenter);
        Assert.DoesNotContain("Dialog", presenter);
        Assert.DoesNotContain("Document", presenter);
    }

    private static int CountOccurrences(string source, string value)
        => source.Split(value, StringSplitOptions.None).Length - 1;
}
