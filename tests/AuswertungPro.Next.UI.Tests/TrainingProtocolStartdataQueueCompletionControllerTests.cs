using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingProtocolStartdataQueueCompletionControllerTests
{
    [Fact]
    public void Apply_laed_review_queue_neu_setzt_status_auf_ui_thread_und_loggt()
    {
        var result = new TrainingProtocolStartdataQueueResult(
            AddedCount: 2,
            CandidateCount: 3,
            StatusText: "2 Protokoll-Startdaten als Kandidaten eingereiht (Freigabe ueber Review).",
            LogText: "Protokoll-Startdaten: 2 Kandidaten eingereiht (von 3 gefiltert).");
        var calls = new List<string>();

        TrainingProtocolStartdataQueueCompletionController.Apply(
            result,
            reloadReviewQueue: () => calls.Add("reload"),
            onUi: action =>
            {
                calls.Add("ui-before");
                action();
                calls.Add("ui-after");
            },
            setReviewStatusText: value => calls.Add("status:" + value),
            log: value => calls.Add("log:" + value));

        Assert.Equal(
            [
                "reload",
                "ui-before",
                "status:2 Protokoll-Startdaten als Kandidaten eingereiht (Freigabe ueber Review).",
                "ui-after",
                "log:Protokoll-Startdaten: 2 Kandidaten eingereiht (von 3 gefiltert)."
            ],
            calls);
    }
}
