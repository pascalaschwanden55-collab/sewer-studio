using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SelfTrainingRunControlControllerTests
{
    [Fact]
    public void Stop_bricht_training_ab_und_setzt_status()
    {
        var calls = new List<string>();

        SelfTrainingRunControlController.Stop(
            () => calls.Add("cancel"),
            value => calls.Add($"status:{value}"));

        Assert.Equal(
            [
                "cancel",
                "status:Selbsttraining wird abgebrochen..."
            ],
            calls);
    }

    [Fact]
    public void TogglePause_ohne_orchestrator_macht_nichts()
    {
        var calls = new List<string>();

        SelfTrainingRunControlController.TogglePause(
            null,
            value => calls.Add($"status:{value}"),
            value => calls.Add($"log:{value}"));

        Assert.Empty(calls);
    }

    [Fact]
    public void TogglePause_pausiert_laufendes_training()
    {
        var orchestrator = new FakeSelfTrainingOrchestrator();
        var calls = new List<string>();

        SelfTrainingRunControlController.TogglePause(
            orchestrator,
            value => calls.Add($"status:{value}"),
            value => calls.Add($"log:{value}"));

        Assert.True(orchestrator.IsPaused);
        Assert.Equal(
            [
                "status:Selbsttraining pausiert.",
                "log:Pipeline pausiert."
            ],
            calls);
    }

    [Fact]
    public void TogglePause_setzt_pausiertes_training_fort()
    {
        var orchestrator = new FakeSelfTrainingOrchestrator();
        orchestrator.Pause();
        var calls = new List<string>();

        SelfTrainingRunControlController.TogglePause(
            orchestrator,
            value => calls.Add($"status:{value}"),
            value => calls.Add($"log:{value}"));

        Assert.False(orchestrator.IsPaused);
        Assert.Equal(
            [
                "status:Selbsttraining fortgesetzt.",
                "log:Pipeline fortgesetzt."
            ],
            calls);
    }

    private sealed class FakeSelfTrainingOrchestrator : ISelfTrainingOrchestrator
    {
        public bool IsPaused { get; private set; }

        public Task<SelfTrainingResult> RunAsync(
            TrainingCaseInput tc,
            IProgress<SelfTrainingStep> progress,
            CancellationToken ct)
            => throw new NotSupportedException();

        public void Pause()
        {
            IsPaused = true;
        }

        public void Resume()
        {
            IsPaused = false;
        }
    }
}
