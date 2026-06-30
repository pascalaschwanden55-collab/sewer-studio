using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingSampleDecisionControllerTests
{
    [Fact]
    public void Approve_sets_approved_status_and_requests_changed_sample_persist()
    {
        var sample = Sample("s1");

        var result = TrainingSampleDecisionController.Approve(sample);

        Assert.Equal(TrainingSampleStatus.Approved, sample.Status);
        Assert.Equal("Approved: s1", result.StatusText);
        Assert.False(result.ShouldDeindex);
        Assert.True(result.PersistChangedSample);
    }

    [Theory]
    [InlineData("Reject", TrainingSampleStatus.Rejected, "Rejected: s1")]
    [InlineData("Remove", TrainingSampleStatus.Removed, "Entfernt: s1")]
    public void Reject_and_remove_clear_kb_state_and_request_deindex_plus_full_persist(
        string action,
        TrainingSampleStatus expectedStatus,
        string expectedStatusText)
    {
        var sample = Sample("s1");
        sample.KbIndexState = KbIndexState.Indexed;

        var result = action == "Reject"
            ? TrainingSampleDecisionController.Reject(sample)
            : TrainingSampleDecisionController.Remove(sample);

        Assert.Equal(expectedStatus, sample.Status);
        Assert.Equal(KbIndexState.None, sample.KbIndexState);
        Assert.Equal(expectedStatusText, result.StatusText);
        Assert.True(result.ShouldDeindex);
        Assert.False(result.PersistChangedSample);
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
