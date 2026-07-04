using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingApprovedProtocolExportRequestFactoryTests
{
    [Fact]
    public void CreateWithDefaults_verdrahtet_utc_now_in_factory()
    {
        var sample = Sample("sample-default");
        var before = DateTime.UtcNow;

        var request = TrainingApprovedProtocolExportRequestFactory.CreateWithDefaults(
            new TrainingApprovedProtocolExportDefaultRequestFactoryRequest(
                GetIsBusy: () => false,
                SetIsBusy: _ => { },
                Samples: [sample],
                IsExportEligible: _ => true,
                PersistSamplesAsync: () => Task.CompletedTask,
                Log: _ => { },
                SetStatusText: _ => { }));

        var now = request.UtcNow();
        var after = DateTime.UtcNow;

        Assert.InRange(now, before, after);
    }

    [Fact]
    public async Task Create_verdrahtet_export_request_mit_store_defaults()
    {
        var sample = Sample("sample-1");
        var calls = new List<string>();
        var entry = new ProtocolEntry { Code = "BAA" };

        var request = TrainingApprovedProtocolExportRequestFactory.Create(
            new TrainingApprovedProtocolExportRequestFactoryRequest(
                GetIsBusy: () => false,
                SetIsBusy: value => calls.Add("busy:" + value),
                Samples: [sample],
                IsExportEligible: actual =>
                {
                    calls.Add("eligible:" + actual.SampleId);
                    return true;
                },
                PersistSamplesAsync: () =>
                {
                    calls.Add("persist");
                    return Task.CompletedTask;
                },
                UtcNow: () => DateTime.UnixEpoch,
                Log: value => calls.Add("log:" + value),
                SetStatusText: value => calls.Add("status:" + value)),
            new TrainingApprovedProtocolExportRequestFactoryDefaults(
                AddProtocolTrainingSample: (actualEntry, caseId) =>
                    calls.Add($"add:{actualEntry.Code}:{caseId}"),
                TargetPath: "target.json"));

        Assert.False(request.GetIsBusy());
        request.SetIsBusy(true);
        Assert.Equal([sample], request.Samples);
        Assert.True(request.IsExportEligible(sample));
        request.AddProtocolTrainingSample(entry, "case-1");
        await request.PersistSamplesAsync();
        Assert.Equal(DateTime.UnixEpoch, request.UtcNow());
        Assert.Equal("target.json", request.TargetPath);
        request.Log("line");
        request.SetStatusText("done");

        Assert.Equal(
            ["busy:True", "eligible:sample-1", "add:BAA:case-1", "persist", "log:line", "status:done"],
            calls);
    }

    private static TrainingSample Sample(string id)
        => new()
        {
            SampleId = id,
            CaseId = "case-" + id,
            Code = "BAB",
            Status = TrainingSampleStatus.Approved
        };
}
