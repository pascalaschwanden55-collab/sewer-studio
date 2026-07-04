using System.Collections.Generic;
using System.Collections.ObjectModel;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.SelfImproving;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingReviewItemDecisionWorkflowTests
{
    [Fact]
    public async Task RunAsync_approve_self_training_ruft_service_loggt_und_schliesst_queue_ab()
    {
        var queueService = CreateSelfTrainingQueue(sampleId: "sample-1");
        var item = Assert.Single(queueService.GetAll());
        var reviewQueue = new ObservableCollection<ReviewQueueItem> { item };
        var service = new ReviewApprovalServiceFake
        {
            ApproveResult = new ReviewApplyResult(Found: true, Indexed: true, Deindexed: false, CorrectedSampleId: null)
        };
        var state = new WorkflowState();

        await TrainingReviewItemDecisionWorkflow.RunAsync(CreateRequest(
            item,
            TrainingReviewItemDecision.Approve,
            queueService,
            reviewQueue,
            state,
            resolveSampleIdAsync: _ => Task.FromResult<string?>("sample-1"),
            createApprovalService: () => service));

        Assert.Equal("sample-1", service.ApprovedSampleId);
        Assert.Equal(["load-samples"], state.Calls);
        Assert.Empty(reviewQueue);
        Assert.Equal(0, state.QueueCount);
        Assert.Equal("Approved: BAB | 0 verbleibend", state.StatusText);
        Assert.Contains("Self-Training Review: BAA@12.3m \u2192 Approved, KB: Indexed", state.Logs);
        Assert.Contains("Review Approved: BAA @ 12.3m (PartialMatch) \u2192 BAB", state.Logs);
    }

    [Fact]
    public async Task RunAsync_reject_self_training_mit_korrektur_loggt_korrigiertes_sample()
    {
        var queueService = CreateSelfTrainingQueue(sampleId: "sample-1");
        var item = Assert.Single(queueService.GetAll());
        var reviewQueue = new ObservableCollection<ReviewQueueItem> { item };
        var service = new ReviewApprovalServiceFake
        {
            RejectResult = new ReviewApplyResult(Found: true, Indexed: false, Deindexed: true, CorrectedSampleId: "sample-1_corr")
        };
        var state = new WorkflowState();

        await TrainingReviewItemDecisionWorkflow.RunAsync(CreateRequest(
            item,
            TrainingReviewItemDecision.Reject,
            queueService,
            reviewQueue,
            state,
            correctedCode: "BAG",
            correctedDescription: "Korrigiert",
            resolveSampleIdAsync: _ => Task.FromResult<string?>("sample-1"),
            createApprovalService: () => service));

        Assert.Equal(("sample-1", "BAG", "Korrigiert"), service.RejectedCall);
        Assert.Equal(["load-samples"], state.Calls);
        Assert.Equal("Rejected: BAB \u2192 BAG | 0 verbleibend", state.StatusText);
        Assert.Contains("Korrigiertes Sample sample-1_corr erzeugt", state.Logs);
    }

    [Fact]
    public async Task RunAsync_self_training_ohne_sample_loggt_fehler_und_schliesst_queue_ab()
    {
        var queueService = CreateSelfTrainingQueue(sampleId: null);
        var item = Assert.Single(queueService.GetAll());
        var reviewQueue = new ObservableCollection<ReviewQueueItem> { item };
        var service = new ReviewApprovalServiceFake();
        var state = new WorkflowState();

        await TrainingReviewItemDecisionWorkflow.RunAsync(CreateRequest(
            item,
            TrainingReviewItemDecision.Approve,
            queueService,
            reviewQueue,
            state,
            resolveSampleIdAsync: _ => Task.FromResult<string?>(null),
            createApprovalService: () => service));

        Assert.Null(service.ApprovedSampleId);
        Assert.DoesNotContain("load-samples", state.Calls);
        Assert.Empty(reviewQueue);
        Assert.Contains("Self-Training Review: Sample nicht gefunden (case-1/BAA@12.3m)", state.Logs);
    }

    [Fact]
    public async Task RunAsync_normaler_feedback_eintrag_wird_ohne_self_training_service_verarbeitet()
    {
        var queueService = CreateMappedQueue();
        var item = Assert.Single(queueService.GetAll());
        var reviewQueue = new ObservableCollection<ReviewQueueItem> { item };
        var state = new WorkflowState();
        var feedbackCalls = new List<(string FinalCode, bool Accepted)>();
        var service = new ReviewApprovalServiceFake();

        await TrainingReviewItemDecisionWorkflow.RunAsync(CreateRequest(
            item,
            TrainingReviewItemDecision.Reject,
            queueService,
            reviewQueue,
            state,
            correctedCode: "BAG",
            processFeedbackAsync: (_, finalCode, accepted, _) =>
            {
                feedbackCalls.Add((finalCode, accepted));
                return Task.CompletedTask;
            },
            createApprovalService: () => service));

        Assert.Equal([("BAG", false)], feedbackCalls);
        Assert.Null(service.RejectedCall.SampleId);
        Assert.Empty(reviewQueue);
        Assert.Equal("Rejected: BAB \u2192 BAG | 0 verbleibend", state.StatusText);
    }

    private static TrainingReviewItemDecisionWorkflowRequest CreateRequest(
        ReviewQueueItem item,
        TrainingReviewItemDecision decision,
        ReviewQueueService queueService,
        ICollection<ReviewQueueItem> reviewQueue,
        WorkflowState state,
        string correctedCode = "",
        string? correctedDescription = null,
        Func<ReviewQueueItem, Task<string?>>? resolveSampleIdAsync = null,
        Func<IReviewApprovalService>? createApprovalService = null,
        Func<ReviewQueueItem, string, bool, CancellationToken, Task>? processFeedbackAsync = null)
        => new(
            Item: item,
            Decision: decision,
            CorrectedCode: correctedCode,
            CorrectedDescription: correctedDescription,
            Box: null,
            Mask: null,
            QueueService: queueService,
            ReviewQueue: reviewQueue,
            CancellationToken: CancellationToken.None,
            ConfirmedByUser: "tester",
            ProcessFeedbackAsync: processFeedbackAsync ?? ((_, _, _, _) => Task.CompletedTask),
            ResolveSampleIdAsync: resolveSampleIdAsync ?? (_ => Task.FromResult<string?>("sample-1")),
            CreateApprovalService: createApprovalService ?? (() => new ReviewApprovalServiceFake()),
            ReloadSamplesAsync: () =>
            {
                state.Calls.Add("load-samples");
                return Task.CompletedTask;
            },
            OnUi: action => action(),
            SetReviewQueueCount: value => state.QueueCount = value,
            SetReviewStatusText: value => state.StatusText = value,
            Log: state.Logs.Add);

    private static ReviewQueueService CreateSelfTrainingQueue(string? sampleId)
    {
        var queueService = new ReviewQueueService();
        queueService.EnqueueFromSelfTraining(
            caseId: "case-1",
            vsaCode: "BAA",
            suggestedCode: "BAB",
            meter: 12.3,
            framePath: "frame.jpg",
            matchLevel: "PartialMatch",
            sampleId: sampleId);
        return queueService;
    }

    private static ReviewQueueService CreateMappedQueue()
    {
        var queueService = new ReviewQueueService();
        var detection = new RawVideoDetection("Riss", 1.0, 1.0, "high");
        var quality = new QualityGateResult(
            CompositeConfidence: 0.6,
            TrafficLight: TrafficLight.Yellow,
            WeightsUsed: new Dictionary<string, double>(),
            Explanation: "review");
        queueService.Enqueue(new MappedProtocolEntry(
            Detection: detection,
            SuggestedCode: "BAB",
            Confidence: 0.6,
            Reason: "review",
            Warnings: [],
            QualityGateResult: quality));
        return queueService;
    }

    private sealed class WorkflowState
    {
        public int QueueCount { get; set; }
        public string StatusText { get; set; } = "";
        public List<string> Logs { get; } = new();
        public List<string> Calls { get; } = new();
    }

    private sealed class ReviewApprovalServiceFake : IReviewApprovalService
    {
        public ReviewApplyResult ApproveResult { get; set; } = new(true, true, false, null);
        public ReviewApplyResult RejectResult { get; set; } = new(true, false, true, null);
        public string? ApprovedSampleId { get; private set; }
        public (string? SampleId, string? CorrectedCode, string? CorrectedDescription) RejectedCall { get; private set; }

        public Task<ReviewApplyResult> ApproveSelfTrainingAsync(
            string sampleId,
            BoundingBox? box,
            CancellationToken ct,
            string confirmedByUser,
            TrainingSegmentationMask? mask = null)
        {
            ApprovedSampleId = sampleId;
            return Task.FromResult(ApproveResult);
        }

        public Task<ReviewApplyResult> RejectSelfTrainingAsync(
            string sampleId,
            string? correctedCode,
            CancellationToken ct,
            string confirmedByUser,
            string? correctedDescription = null)
        {
            RejectedCall = (sampleId, correctedCode, correctedDescription);
            return Task.FromResult(RejectResult);
        }
    }
}
