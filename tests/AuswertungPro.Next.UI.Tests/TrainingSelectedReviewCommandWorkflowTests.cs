using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.SelfImproving;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingSelectedReviewCommandWorkflowTests
{
    [Fact]
    public async Task ApproveAsync_capturet_box_und_maske_und_leert_pending_state_nach_erfolg()
    {
        var item = Item();
        var queue = new ReviewQueueService();
        var box = new BoundingBox(0.5, 0.6, 0.2, 0.3);
        var mask = new TrainingSegmentationMask("rle", 100, 80, 250, 0.91, "damage");
        var cleared = false;
        (BoundingBox? Box, TrainingSegmentationMask? Mask)? captured = null;
        var state = new WorkflowState();

        await TrainingSelectedReviewCommandWorkflow.ApproveAsync(
            new TrainingSelectedReviewApproveRequest(
                Item: item,
                QueueService: queue,
                GetPendingBox: () => box,
                GetPendingMask: () => mask,
                ApproveAsync: (_, _, _, capturedBox, capturedMask) =>
                {
                    captured = (capturedBox, capturedMask);
                    return Task.CompletedTask;
                },
                ClearPendingReviewGeometry: () => cleared = true,
                CancellationToken: CancellationToken.None,
                Log: state.Logs.Add,
                OnUi: action => action(),
                SetReviewStatusText: value => state.StatusText = value));

        Assert.Equal(box, captured?.Box);
        Assert.Same(mask, captured?.Mask);
        Assert.True(cleared);
        Assert.Empty(state.Logs);
        Assert.Equal("", state.StatusText);
    }

    [Fact]
    public async Task ApproveAsync_fehler_loggt_und_setzt_status_ohne_pending_state_zu_leeren()
    {
        var state = new WorkflowState();
        var cleared = false;

        await TrainingSelectedReviewCommandWorkflow.ApproveAsync(
            new TrainingSelectedReviewApproveRequest(
                Item: Item(),
                QueueService: new ReviewQueueService(),
                GetPendingBox: () => null,
                GetPendingMask: () => null,
                ApproveAsync: (_, _, _, _, _) => throw new InvalidOperationException("kaputt"),
                ClearPendingReviewGeometry: () => cleared = true,
                CancellationToken: CancellationToken.None,
                Log: state.Logs.Add,
                OnUi: action => action(),
                SetReviewStatusText: value => state.StatusText = value));

        Assert.False(cleared);
        Assert.Equal(["Review-Freigabe Fehler: kaputt"], state.Logs);
        Assert.Equal("Fehler: kaputt", state.StatusText);
    }

    [Fact]
    public async Task RejectAsync_ignoriert_fehlende_auswahl_und_ruft_action_sonst_auf()
    {
        var calls = 0;
        var state = new WorkflowState();

        await TrainingSelectedReviewCommandWorkflow.RejectAsync(
            new TrainingSelectedReviewRejectRequest(
                Item: null,
                QueueService: new ReviewQueueService(),
                RejectAsync: (_, _, _) =>
                {
                    calls++;
                    return Task.CompletedTask;
                },
                CancellationToken: CancellationToken.None,
                Log: state.Logs.Add,
                OnUi: action => action(),
                SetReviewStatusText: value => state.StatusText = value));

        Assert.Equal(0, calls);

        await TrainingSelectedReviewCommandWorkflow.RejectAsync(
            new TrainingSelectedReviewRejectRequest(
                Item: Item(),
                QueueService: new ReviewQueueService(),
                RejectAsync: (_, _, _) =>
                {
                    calls++;
                    return Task.CompletedTask;
                },
                CancellationToken: CancellationToken.None,
                Log: state.Logs.Add,
                OnUi: action => action(),
                SetReviewStatusText: value => state.StatusText = value));

        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task CorrectAsync_benoetigt_code_und_reicht_beschreibung_weiter()
    {
        var calls = new List<(string Code, string? Description)>();
        var state = new WorkflowState();

        await TrainingSelectedReviewCommandWorkflow.CorrectAsync(
            new TrainingSelectedReviewCorrectionRequest(
                Item: Item(),
                QueueService: new ReviewQueueService(),
                CorrectedCode: " ",
                CorrectedDescription: "Beschreibung",
                ApplyCorrectionAsync: (_, _, _, _) =>
                {
                    calls.Add(("unexpected", null));
                    return Task.CompletedTask;
                },
                CancellationToken: CancellationToken.None,
                Log: state.Logs.Add,
                OnUi: action => action(),
                SetReviewStatusText: value => state.StatusText = value));

        await TrainingSelectedReviewCommandWorkflow.CorrectAsync(
            new TrainingSelectedReviewCorrectionRequest(
                Item: Item(),
                QueueService: new ReviewQueueService(),
                CorrectedCode: "BAG",
                CorrectedDescription: "Beschreibung",
                ApplyCorrectionAsync: (_, code, description, _) =>
                {
                    calls.Add((code, description));
                    return Task.CompletedTask;
                },
                CancellationToken: CancellationToken.None,
                Log: state.Logs.Add,
                OnUi: action => action(),
                SetReviewStatusText: value => state.StatusText = value));

        Assert.Equal([("BAG", "Beschreibung")], calls);
    }

    private static ReviewQueueItem Item()
        => new("review-1", null, 0.5, DateTime.UnixEpoch);

    private sealed class WorkflowState
    {
        public string StatusText { get; set; } = "";
        public List<string> Logs { get; } = new();
    }
}
