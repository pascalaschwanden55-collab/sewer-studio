using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SelfTrainingRunExecutionControllerTests
{
    [Fact]
    public async Task RunAsync_startet_orchestrator_und_persistiert_snapshot()
    {
        var input = new TrainingCaseInput("case-1", "folder", "video.mp4", "protocol.pdf");
        var result = Result("case-1", exact: 1, partial: 0, mismatch: 0, noFindings: 0);
        var orchestrator = new FakeSelfTrainingOrchestrator(result);
        var progress = new Progress<SelfTrainingStep>();
        var timestamp = new DateTime(2026, 6, 29, 12, 0, 0, DateTimeKind.Utc);
        var snapshot = new SelfTrainingRunSnapshot(timestamp, "case-1", 1, 1, 0, 0, 0);
        var appended = new List<SelfTrainingRunSnapshot>();
        SelfTrainingResult? capturedResult = null;
        DateTime? capturedTimestamp = null;

        var returned = await SelfTrainingRunExecutionController.RunAsync(
            orchestrator,
            input,
            progress,
            (runResult, timestampUtc) =>
            {
                capturedResult = runResult;
                capturedTimestamp = timestampUtc;
                return snapshot;
            },
            savedSnapshot =>
            {
                appended.Add(savedSnapshot);
                return Task.CompletedTask;
            },
            () => timestamp,
            CancellationToken.None);

        Assert.Same(result, returned);
        Assert.Equal(input, orchestrator.Input);
        Assert.Same(progress, orchestrator.Progress);
        Assert.Equal(CancellationToken.None, orchestrator.CancellationToken);
        Assert.Same(result, capturedResult);
        Assert.Equal(timestamp, capturedTimestamp);
        Assert.Single(appended, snapshot);
    }

    [Fact]
    public async Task RunAsync_persistiert_keine_history_wenn_snapshot_fehlt()
    {
        var result = Result("case-1", exact: 0, partial: 0, mismatch: 0, noFindings: 0);
        var appendCalls = 0;

        await SelfTrainingRunExecutionController.RunAsync(
            new FakeSelfTrainingOrchestrator(result),
            new TrainingCaseInput("case-1", "folder", "video.mp4", "protocol.pdf"),
            new Progress<SelfTrainingStep>(),
            (_, _) => null,
            _ =>
            {
                appendCalls++;
                return Task.CompletedTask;
            },
            () => DateTime.UnixEpoch,
            CancellationToken.None);

        Assert.Equal(0, appendCalls);
    }

    private static SelfTrainingResult Result(
        string caseId,
        int exact,
        int partial,
        int mismatch,
        int noFindings)
        => new(
            caseId,
            exact + partial + mismatch + noFindings,
            exact,
            partial,
            mismatch,
            noFindings,
            OverallTechnique: null,
            Duration: TimeSpan.FromSeconds(1),
            SamplesGenerated: exact);

    private sealed class FakeSelfTrainingOrchestrator(SelfTrainingResult result) : ISelfTrainingOrchestrator
    {
        public bool IsPaused => false;

        public TrainingCaseInput? Input { get; private set; }

        public IProgress<SelfTrainingStep>? Progress { get; private set; }

        public CancellationToken CancellationToken { get; private set; }

        public Task<SelfTrainingResult> RunAsync(
            TrainingCaseInput tc,
            IProgress<SelfTrainingStep> progress,
            CancellationToken ct)
        {
            Input = tc;
            Progress = progress;
            CancellationToken = ct;
            return Task.FromResult(result);
        }

        public void Pause()
        {
        }

        public void Resume()
        {
        }
    }
}
