using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SelfTrainingRunControlControllerTests
{
    [Fact]
    public void RequestCancel_bricht_cts_ab_und_liefert_status()
    {
        using var cts = new CancellationTokenSource();

        var status = SelfTrainingRunControlController.RequestCancel(cts);

        Assert.True(cts.IsCancellationRequested);
        Assert.Equal("Selbsttraining wird abgebrochen...", status);
    }

    [Fact]
    public void RequestCancel_ohne_cts_liefert_status()
    {
        var status = SelfTrainingRunControlController.RequestCancel(null);

        Assert.Equal("Selbsttraining wird abgebrochen...", status);
    }

    [Fact]
    public void TogglePause_ohne_orchestrator_macht_nichts()
    {
        var result = SelfTrainingRunControlController.TogglePause(null);

        Assert.False(result.Handled);
        Assert.Null(result.StatusText);
        Assert.Null(result.LogMessage);
    }

    [Fact]
    public void TogglePause_pausiert_laufenden_orchestrator()
    {
        var orchestrator = new FakeSelfTrainingOrchestrator(isPaused: false);

        var result = SelfTrainingRunControlController.TogglePause(orchestrator);

        Assert.True(result.Handled);
        Assert.Equal(1, orchestrator.PauseCalls);
        Assert.Equal(0, orchestrator.ResumeCalls);
        Assert.Equal("Selbsttraining pausiert.", result.StatusText);
        Assert.Equal("Pipeline pausiert.", result.LogMessage);
    }

    [Fact]
    public void TogglePause_setzt_pausierten_orchestrator_fort()
    {
        var orchestrator = new FakeSelfTrainingOrchestrator(isPaused: true);

        var result = SelfTrainingRunControlController.TogglePause(orchestrator);

        Assert.True(result.Handled);
        Assert.Equal(0, orchestrator.PauseCalls);
        Assert.Equal(1, orchestrator.ResumeCalls);
        Assert.Equal("Selbsttraining fortgesetzt.", result.StatusText);
        Assert.Equal("Pipeline fortgesetzt.", result.LogMessage);
    }

    private sealed class FakeSelfTrainingOrchestrator(bool isPaused) : ISelfTrainingOrchestrator
    {
        public int PauseCalls { get; private set; }

        public int ResumeCalls { get; private set; }

        public bool IsPaused { get; private set; } = isPaused;

        public Task<SelfTrainingResult> RunAsync(
            TrainingCaseInput tc,
            IProgress<SelfTrainingStep> progress,
            CancellationToken ct)
            => throw new NotSupportedException();

        public void Pause()
        {
            PauseCalls++;
            IsPaused = true;
        }

        public void Resume()
        {
            ResumeCalls++;
            IsPaused = false;
        }
    }
}
