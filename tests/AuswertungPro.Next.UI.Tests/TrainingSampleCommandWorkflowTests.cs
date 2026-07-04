using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingSampleCommandWorkflowTests
{
    [Fact]
    public async Task RunAsync_approves_sample_sets_status_and_persists_changed_sample()
    {
        var calls = new List<string>();
        var sample = Sample("s1");

        await TrainingSampleCommandWorkflow.RunAsync(
            new TrainingSampleCommandWorkflowRequest(
                sample,
                TrainingSampleDecisionController.Approve,
                sampleId => calls.Add($"deindex:{sampleId}"),
                status => calls.Add($"status:{status}"),
                changed =>
                {
                    calls.Add($"persist:{changed?.SampleId ?? "all"}");
                    return Task.CompletedTask;
                }));

        Assert.Equal(TrainingSampleStatus.Approved, sample.Status);
        Assert.Equal(["status:Approved: s1", "persist:s1"], calls);
    }

    [Theory]
    [InlineData("Reject", TrainingSampleStatus.Rejected, "Rejected: s1")]
    [InlineData("Remove", TrainingSampleStatus.Removed, "Entfernt: s1")]
    public async Task RunAsync_deindexes_rejected_or_removed_sample_and_persists_all(
        string action,
        TrainingSampleStatus expectedStatus,
        string expectedStatusText)
    {
        var calls = new List<string>();
        var sample = Sample("s1");
        sample.KbIndexState = KbIndexState.Indexed;

        await TrainingSampleCommandWorkflow.RunAsync(
            new TrainingSampleCommandWorkflowRequest(
                sample,
                action == "Reject"
                    ? TrainingSampleDecisionController.Reject
                    : TrainingSampleDecisionController.Remove,
                sampleId => calls.Add($"deindex:{sampleId}"),
                status => calls.Add($"status:{status}"),
                changed =>
                {
                    calls.Add($"persist:{changed?.SampleId ?? "all"}");
                    return Task.CompletedTask;
                }));

        Assert.Equal(expectedStatus, sample.Status);
        Assert.Equal(KbIndexState.None, sample.KbIndexState);
        Assert.Equal(["deindex:s1", $"status:{expectedStatusText}", "persist:all"], calls);
    }

    [Fact]
    public async Task RunAsync_ignoriert_fehlende_auswahl()
    {
        var calls = new List<string>();

        await TrainingSampleCommandWorkflow.RunAsync(
            new TrainingSampleCommandWorkflowRequest(
                Sample: null,
                Decide: sample =>
                {
                    calls.Add($"decide:{sample.SampleId}");
                    return TrainingSampleDecisionController.Approve(sample);
                },
                DeindexSample: sampleId => calls.Add($"deindex:{sampleId}"),
                SetStatusText: status => calls.Add($"status:{status}"),
                PersistSamplesAsync: changed =>
                {
                    calls.Add($"persist:{changed?.SampleId ?? "all"}");
                    return Task.CompletedTask;
                }));

        Assert.Empty(calls);
    }

    private static TrainingSample Sample(string id)
        => new()
        {
            SampleId = id,
            CaseId = "case",
            Code = "BAA",
            FramePath = "frame.jpg",
            Status = TrainingSampleStatus.New
        };
}
