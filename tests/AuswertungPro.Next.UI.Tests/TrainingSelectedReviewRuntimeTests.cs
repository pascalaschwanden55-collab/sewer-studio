using AuswertungPro.Next.Infrastructure.Ai.SelfImproving;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingSelectedReviewRuntimeTests
{
    [Fact]
    public async Task ApproveAsync_oeffnet_scope_baut_feedback_und_disposed_scope()
    {
        var scope = new ScopeProbe();
        var item = Item();
        var calls = new List<string>();

        await TrainingSelectedReviewRuntime.ApproveAsync(
            item,
            new ReviewQueueService(),
            CancellationToken.None,
            box: null,
            mask: null,
            openScope: () =>
            {
                calls.Add("open");
                return scope;
            },
            createFeedback: currentScope =>
            {
                Assert.Same(scope, currentScope);
                calls.Add("feedback");
                return "feedback";
            },
            approveAsync: (currentItem, feedback, _, _, _, _) =>
            {
                Assert.Same(item, currentItem);
                Assert.Equal("feedback", feedback);
                calls.Add("approve");
                return Task.CompletedTask;
            });

        Assert.True(scope.Disposed);
        Assert.Equal(["open", "feedback", "approve"], calls);
    }

    [Fact]
    public async Task CorrectAsync_reicht_code_und_beschreibung_weiter()
    {
        var scope = new ScopeProbe();
        var item = Item();
        var calls = new List<string>();

        await TrainingSelectedReviewRuntime.CorrectAsync(
            item,
            correctedCode: "BAG",
            correctedDescription: "Beschreibung",
            new ReviewQueueService(),
            CancellationToken.None,
            openScope: () => scope,
            createFeedback: _ => "feedback",
            rejectAsync: (_, code, feedback, _, _, description) =>
            {
                calls.Add($"{code}:{feedback}:{description}");
                return Task.CompletedTask;
            });

        Assert.True(scope.Disposed);
        Assert.Equal(["BAG:feedback:Beschreibung"], calls);
    }

    [Fact]
    public async Task ApproveAsync_gibt_den_erzeugten_Feedback_Dienst_frei()
    {
        var feedback = new FeedbackProbe();

        await TrainingSelectedReviewRuntime.ApproveAsync(
            Item(),
            new ReviewQueueService(),
            CancellationToken.None,
            box: null,
            mask: null,
            openScope: () => new ScopeProbe(),
            createFeedback: _ => feedback,
            approveAsync: (_, current, _, _, _, _) =>
            {
                Assert.Same(feedback, current);
                Assert.False(feedback.Disposed);
                return Task.CompletedTask;
            });

        Assert.True(feedback.Disposed);
    }

    private static ReviewQueueItem Item()
        => new("review-1", null, 0.5, DateTime.UnixEpoch);

    private sealed class ScopeProbe : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose()
        {
            Disposed = true;
        }
    }

    private sealed class FeedbackProbe : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }
}
