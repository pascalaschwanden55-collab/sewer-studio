using System.Net.Http;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingSampleCommandRequestFactoryTests
{
    [Fact]
    public async Task Create_verdrahtet_sample_command_request_und_deindex_runtime()
    {
        using var cachedClient = new HttpClient();
        using var newClient = new HttpClient();
        var sample = Sample("sample-1");
        var calls = new List<string>();

        var request = TrainingSampleCommandRequestFactory.Create(
            new TrainingSampleCommandRequestFactoryRequest(
                SelectedSample: sample,
                Decide: TrainingSampleDecisionController.Reject,
                GetKbHttpClient: () => cachedClient,
                SetKbHttpClient: client => calls.Add("set-client:" + ReferenceEquals(newClient, client)),
                SetStatusText: status => calls.Add("status:" + status),
                PersistSamplesAsync: changed =>
                {
                    calls.Add("persist:" + (changed?.SampleId ?? "all"));
                    return Task.CompletedTask;
                }),
            new TrainingSampleCommandRequestFactoryDefaults(
                DeindexSample: (sampleId, getCachedHttpClient, setCachedHttpClient) =>
                {
                    calls.Add("deindex:" + sampleId + ":" + ReferenceEquals(cachedClient, getCachedHttpClient()));
                    setCachedHttpClient(newClient);
                }));

        Assert.Same(sample, request.Sample);
        Assert.Same((Func<TrainingSample, TrainingSampleDecisionResult>)TrainingSampleDecisionController.Reject, request.Decide);

        request.DeindexSample("sample-1");
        request.SetStatusText("status-text");
        await request.PersistSamplesAsync(sample);

        Assert.Equal(
            ["deindex:sample-1:True", "set-client:True", "status:status-text", "persist:sample-1"],
            calls);
    }

    private static TrainingSample Sample(string id)
        => new()
        {
            SampleId = id,
            CaseId = "case-" + id,
            Code = "BAB",
            Status = TrainingSampleStatus.New
        };
}
