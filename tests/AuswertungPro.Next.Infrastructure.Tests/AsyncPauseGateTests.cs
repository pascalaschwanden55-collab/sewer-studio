using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class AsyncPauseGateTests
{
    [Fact]
    public async Task WaitIfPausedAsync_completes_immediately_when_running()
    {
        var gate = new AsyncPauseGate();

        await gate.WaitIfPausedAsync(CancellationToken.None);

        Assert.False(gate.IsPaused);
    }

    [Fact]
    public async Task WaitIfPausedAsync_waits_until_resume()
    {
        var gate = new AsyncPauseGate();
        gate.Pause();

        var waitTask = gate.WaitIfPausedAsync(CancellationToken.None);
        Assert.False(waitTask.IsCompleted);

        gate.Resume();
        await waitTask;

        Assert.False(gate.IsPaused);
    }

    [Fact]
    public async Task WaitIfPausedAsync_observes_cancellation()
    {
        var gate = new AsyncPauseGate();
        gate.Pause();
        using var cts = new CancellationTokenSource();

        var waitTask = gate.WaitIfPausedAsync(cts.Token);
        await cts.CancelAsync();

        await Assert.ThrowsAsync<TaskCanceledException>(() => waitTask);
        Assert.True(gate.IsPaused);
    }

    [Fact]
    public async Task Multiple_waiters_resume_together()
    {
        var gate = new AsyncPauseGate();
        gate.Pause();

        var first = gate.WaitIfPausedAsync(CancellationToken.None);
        var second = gate.WaitIfPausedAsync(CancellationToken.None);

        gate.Resume();
        await Task.WhenAll(first, second);

        Assert.False(gate.IsPaused);
    }
}
