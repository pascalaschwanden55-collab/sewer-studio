using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingCenterDistributionRequestFactoryTests
{
    [Fact]
    public async Task Create_verdrahtet_distribution_workflow_request()
    {
        var calls = new List<string>();
        var rootFolders = new List<string>();
        var result = new TrainingCenterImportService.DistributeResult(
            TotalChunks: 1,
            Distributed: 1,
            VideosMatched: 1,
            Uncertain: 0,
            OutputFolder: "out",
            Messages: []);

        var request = TrainingCenterDistributionRequestFactory.Create(
            new TrainingCenterDistributionRequestFactoryRequest(
                GetIsBusy: () => false,
                SetIsBusy: value => calls.Add("busy:" + value),
                SelectPdfPath: () =>
                {
                    calls.Add("select-pdf");
                    return "input.pdf";
                },
                SelectVideoFolder: () =>
                {
                    calls.Add("select-video");
                    return "videos";
                },
                DistributeAsync: (pdfPath, videoFolder, outputFolder) =>
                {
                    calls.Add($"distribute:{pdfPath}:{videoFolder}:{outputFolder}");
                    return Task.FromResult(result);
                },
                RootFolders: rootFolders,
                UpdateRootFolderDisplay: () => calls.Add("update-roots"),
                SetLogText: value => calls.Add("log-text:" + value),
                SetStatusText: value => calls.Add("status:" + value),
                Log: value => calls.Add("log:" + value)));

        Assert.False(request.GetIsBusy());
        request.SetIsBusy(true);
        Assert.Equal("input.pdf", request.SelectPdfPath());
        Assert.Equal("videos", request.SelectVideoFolder());
        Assert.Same(result, await request.DistributeAsync("input.pdf", "videos", "out"));
        Assert.Same(rootFolders, request.RootFolders);
        request.UpdateRootFolderDisplay();
        request.SetLogText("");
        request.SetStatusText("ok");
        request.Log("fertig");

        Assert.Equal(
            [
                "busy:True",
                "select-pdf",
                "select-video",
                "distribute:input.pdf:videos:out",
                "update-roots",
                "log-text:",
                "status:ok",
                "log:fertig"
            ],
            calls);
    }
}
