using System.Net.Http;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;
using InfraSelfImproving = AuswertungPro.Next.Infrastructure.Ai.SelfImproving;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SelfTrainingRunCommandRequestFactoryTests
{
    [Fact]
    public async Task CreateWithDefaults_verdrahtet_preparation_und_run_request()
    {
        var calls = new List<string>();
        var selectedCase = new TrainingCase { CaseId = "case-1", FolderPath = @"D:\Training" };
        var cases = new List<TrainingCase> { selectedCase };
        var roots = new[] { @"D:\Training" };
        using var cts = new CancellationTokenSource();
        var reviewQueue = new InfraSelfImproving.ReviewQueueService();
        var cachedHttp = new HttpClient();

        var request = SelfTrainingRunCommandRequestFactory.CreateWithDefaults(
            new SelfTrainingRunCommandDefaultRequestFactoryRequest(
                IsBusy: true,
                IsSelfTrainingRunning: false,
                Cases: cases,
                RootFolders: roots,
                ScanInputsAsync: _ => Task.FromResult(new List<TrainingCaseInput>()),
                SelectedCase: selectedCase,
                SetSelectedCase: value => calls.Add($"selected:{value.CaseId}"),
                ResetCancellation: () =>
                {
                    calls.Add("reset");
                    return cts.Token;
                },
                SetStatusText: value => calls.Add($"status:{value}"),
                SetBusy: value => calls.Add($"busy:{value}"),
                SetSelfTrainingRunning: value => calls.Add($"running:{value}"),
                SetLogText: value => calls.Add($"log-text:{value}"),
                Log: value => calls.Add($"log:{value}"),
                GetKbHttpClient: () => cachedHttp,
                SetKbHttpClient: _ => calls.Add("set-http"),
                AppSettings: null,
                CodeCatalog: null,
                SetActiveVisionModel: value => calls.Add($"active:{value}"),
                SetOrchestrator: _ => calls.Add("orchestrator"),
                OnProgress: _ => calls.Add("progress"),
                IndexSamplesAsync: (_, _) => Task.FromResult(new KbIndexOutcome([], [])),
                ReviewQueueService: reviewQueue,
                ReloadReviewQueue: queue => calls.Add($"reload:{ReferenceEquals(queue, reviewQueue)}"),
                LoadSamplesInternalAsync: () =>
                {
                    calls.Add("load-internal");
                    return Task.CompletedTask;
                },
                RefreshKbStatusAsync: () =>
                {
                    calls.Add("refresh-kb");
                    return Task.CompletedTask;
                },
                ResetVisuals: () => calls.Add("reset-visuals")));

        var preparation = await request.PrepareAsync();
        var runRequest = request.CreateRunRequest(selectedCase, cts.Token);

        Assert.True(preparation.ShouldStop);
        Assert.Same(selectedCase, runRequest.SelectedCase);
        Assert.Equal(cts.Token, runRequest.CancellationToken);
        Assert.Same(reviewQueue, runRequest.ReviewQueueService);

        runRequest.Ui.SetBusy(true);
        runRequest.Ui.SetSelfTrainingRunning(true);
        runRequest.Ui.SetLogText("");
        runRequest.Ui.SetStatusText("laeuft");
        runRequest.Ui.Log("meldung");
        runRequest.ReloadReviewQueue(reviewQueue);
        await runRequest.LoadSamplesInternalAsync();
        await runRequest.RefreshKbStatusAsync();
        runRequest.ResetVisuals();

        Assert.Contains("busy:True", calls);
        Assert.Contains("running:True", calls);
        Assert.Contains("log-text:", calls);
        Assert.Contains("status:laeuft", calls);
        Assert.Contains("log:meldung", calls);
        Assert.Contains("reload:True", calls);
        Assert.Contains("load-internal", calls);
        Assert.Contains("refresh-kb", calls);
        Assert.Contains("reset-visuals", calls);
    }
}
