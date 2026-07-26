using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Live;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionConfirmationTrainingResultWorkflowTests
{
    [Fact]
    public void ExecuteAccepted_resumes_without_status_when_result_was_not_saved()
    {
        var calls = new List<string>();

        var result = LiveDetectionConfirmationTrainingResultWorkflow.ExecuteAccepted(
            new LiveDetectionConfirmationTrainingResult(Saved: false, SavedCount: 0, Code: null),
            Actions(calls));

        Assert.Equal(LiveDetectionConfirmationTrainingResultOutcome.NotSaved, result.Outcome);
        Assert.False(result.Saved);
        Assert.Equal(["resume"], calls);
    }

    [Fact]
    public void ExecuteAccepted_shows_saved_count_then_resumes()
    {
        var calls = new List<string>();

        var result = LiveDetectionConfirmationTrainingResultWorkflow.ExecuteAccepted(
            new LiveDetectionConfirmationTrainingResult(Saved: true, SavedCount: 2, Code: null),
            Actions(calls));

        Assert.Equal(LiveDetectionConfirmationTrainingResultOutcome.AcceptedSaved, result.Outcome);
        Assert.True(result.Saved);
        Assert.Equal(["status:\u2713 2 Befund(e) gespeichert:True", "resume"], calls);
    }

    [Fact]
    public void ExecuteCorrected_shows_corrected_code_then_resumes()
    {
        var calls = new List<string>();

        var result = LiveDetectionConfirmationTrainingResultWorkflow.ExecuteCorrected(
            new LiveDetectionConfirmationTrainingResult(Saved: true, SavedCount: 1, Code: "BCA"),
            Actions(calls));

        Assert.Equal(LiveDetectionConfirmationTrainingResultOutcome.CorrectedSaved, result.Outcome);
        Assert.True(result.Saved);
        Assert.Equal(["status:\u2713 Training: BCA (korrigiert):True", "resume"], calls);
    }

    private static LiveDetectionConfirmationTrainingResultActions Actions(List<string> calls)
        => new(
            ShowOsdMeterStatus: (message, resetAfterDelay) => calls.Add($"status:{message}:{resetAfterDelay}"),
            ResumeDetection: () => calls.Add("resume"));
}
