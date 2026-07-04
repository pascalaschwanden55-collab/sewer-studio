using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingKnowledgeBaseCheckRequestFactoryTests
{
    [Fact]
    public async Task Create_verdrahtet_check_workflow_request()
    {
        var calls = new List<string>();
        var summary = new KnowledgeBaseDiagnosticsSummary(
            SampleCount: 1,
            EmbeddingCount: 2,
            VersionCount: 3,
            LatestVersionAtUtc: null,
            LatestVersionSampleCount: 0,
            LatestVersionNotes: "",
            TopCodes: []);
        using var cts = new CancellationTokenSource();

        var request = TrainingKnowledgeBaseCheckRequestFactory.Create(
            new TrainingKnowledgeBaseCheckRequestFactoryRequest(
                IsBusy: false,
                SetBusy: value => calls.Add("busy:" + value),
                SetStatus: value => calls.Add("status:" + value),
                ReadSummaryAsync: topCodes =>
                {
                    calls.Add("read:" + topCodes);
                    return Task.FromResult(summary);
                },
                RefreshKbStatusAsync: () =>
                {
                    calls.Add("refresh");
                    return Task.CompletedTask;
                },
                Log: value => calls.Add("log:" + value),
                CancellationToken: cts.Token));

        Assert.False(request.IsBusy);
        request.SetBusy(true);
        request.SetStatus("ok");
        Assert.Same(summary, await request.ReadSummaryAsync(12));
        await request.RefreshKbStatusAsync();
        request.Log("fertig");
        Assert.Equal(cts.Token, request.CancellationToken);

        Assert.Equal(["busy:True", "status:ok", "read:12", "refresh", "log:fertig"], calls);
    }
}
