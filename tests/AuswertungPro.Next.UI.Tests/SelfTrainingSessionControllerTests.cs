using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SelfTrainingSessionControllerTests
{
    [Fact]
    public void SelfTrainingSession_haelt_aktives_modell_und_orchestrator()
    {
        var orchestrator = new FakeSelfTrainingOrchestrator();

        using var session = new SelfTrainingSession(
            activeVisionModel: "qwen3-vl:2b",
            orchestrator,
            Array.Empty<IDisposable>());

        Assert.Equal("qwen3-vl:2b", session.ActiveVisionModel);
        Assert.Same(orchestrator, session.Orchestrator);
    }

    [Fact]
    public void SelfTrainingSession_dispose_gibt_owned_resources_frei()
    {
        var first = new DisposableProbe();
        var second = new DisposableProbe();
        var session = new SelfTrainingSession(
            activeVisionModel: "qwen3-vl:2b",
            new FakeSelfTrainingOrchestrator(),
            new IDisposable[] { first, second });

        session.Dispose();
        session.Dispose();

        Assert.Equal(1, first.DisposeCount);
        Assert.Equal(1, second.DisposeCount);
    }

    [Theory]
    [InlineData("qwen3-vl:2b", "qwen3-vl:2b")]
    [InlineData("", "qwen3-vl:2b")]
    [InlineData("   ", "qwen3-vl:2b")]
    public void ResolveVisionModel_liefert_default_wenn_modell_fehlt(
        string? configuredModel,
        string expected)
    {
        Assert.Equal(expected, SelfTrainingSessionController.ResolveVisionModel(configuredModel));
    }

    private sealed class DisposableProbe : IDisposable
    {
        public int DisposeCount { get; private set; }

        public void Dispose()
        {
            DisposeCount++;
        }
    }

    private sealed class FakeSelfTrainingOrchestrator : ISelfTrainingOrchestrator
    {
        public bool IsPaused => false;

        public Task<SelfTrainingResult> RunAsync(
            TrainingCaseInput tc,
            IProgress<SelfTrainingStep> progress,
            CancellationToken ct)
            => throw new NotSupportedException();

        public void Pause()
        {
        }

        public void Resume()
        {
        }
    }
}
