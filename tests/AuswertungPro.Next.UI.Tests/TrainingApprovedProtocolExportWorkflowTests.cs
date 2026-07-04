using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingApprovedProtocolExportWorkflowTests
{
    [Fact]
    public async Task RunAsync_exportiert_eligible_samples_loggt_und_setzt_status()
    {
        var calls = new List<string>();
        var exported = new List<(ProtocolEntry Entry, string? CaseId)>();
        var sample = Sample("s1", "BAA", TrainingSampleStatus.Approved);

        await TrainingApprovedProtocolExportWorkflow.RunAsync(
            new TrainingApprovedProtocolExportWorkflowRequest(
                GetIsBusy: () => false,
                SetIsBusy: value => calls.Add($"busy:{value}"),
                Samples: [sample],
                IsExportEligible: _ => true,
                AddProtocolTrainingSample: (entry, caseId) => exported.Add((entry, caseId)),
                PersistSamplesAsync: () =>
                {
                    calls.Add("persist");
                    return Task.CompletedTask;
                },
                UtcNow: () => new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc),
                TargetPath: "target.json",
                Log: value => calls.Add($"log:{value}"),
                SetStatusText: value => calls.Add($"status:{value}")));

        Assert.Equal(TrainingSampleStatus.Approved, sample.Status);
        Assert.NotNull(sample.ExportedUtc);
        Assert.Single(exported);
        Assert.Equal("case-s1", exported[0].CaseId);
        Assert.Equal("busy:True", calls[0]);
        Assert.Contains("persist", calls);
        Assert.Contains("log:  Ziel: target.json", calls);
        Assert.Contains("status:Protokoll-Training: 1 Samples als Few-Shot-Beispiele gespeichert (1 Codes).", calls);
        Assert.Equal("busy:False", calls[^1]);
    }

    [Fact]
    public async Task RunAsync_ignoriert_aufruf_wenn_busy()
    {
        var calls = new List<string>();

        await TrainingApprovedProtocolExportWorkflow.RunAsync(
            new TrainingApprovedProtocolExportWorkflowRequest(
                GetIsBusy: () => true,
                SetIsBusy: value => calls.Add($"busy:{value}"),
                Samples: [Sample("s1", "BAA", TrainingSampleStatus.Approved)],
                IsExportEligible: _ => true,
                AddProtocolTrainingSample: (_, _) => calls.Add("add"),
                PersistSamplesAsync: () =>
                {
                    calls.Add("persist");
                    return Task.CompletedTask;
                },
                UtcNow: () => DateTime.UtcNow,
                TargetPath: "target.json",
                Log: value => calls.Add($"log:{value}"),
                SetStatusText: value => calls.Add($"status:{value}")));

        Assert.Empty(calls);
    }

    [Fact]
    public async Task RunAsync_setzt_busy_auch_bei_fehler_zurueck()
    {
        var calls = new List<string>();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TrainingApprovedProtocolExportWorkflow.RunAsync(
                new TrainingApprovedProtocolExportWorkflowRequest(
                    GetIsBusy: () => false,
                    SetIsBusy: value => calls.Add($"busy:{value}"),
                    Samples: [Sample("s1", "BAA", TrainingSampleStatus.Approved)],
                    IsExportEligible: _ => true,
                    AddProtocolTrainingSample: (_, _) => throw new InvalidOperationException("kaputt"),
                    PersistSamplesAsync: () => Task.CompletedTask,
                    UtcNow: () => DateTime.UtcNow,
                    TargetPath: "target.json",
                    Log: _ => { },
                    SetStatusText: _ => { })));

        Assert.Equal(["busy:True", "busy:False"], calls);
    }

    private static TrainingSample Sample(string id, string code, TrainingSampleStatus status)
        => new()
        {
            SampleId = id,
            CaseId = $"case-{id}",
            Code = code,
            Status = status,
            FramePath = $"{id}.jpg"
        };
}
