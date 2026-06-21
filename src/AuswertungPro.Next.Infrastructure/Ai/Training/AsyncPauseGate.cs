using System;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

internal sealed class AsyncPauseGate
{
    private readonly object _sync = new();
    private TaskCompletionSource? _resumeSignal;

    public bool IsPaused
    {
        get
        {
            lock (_sync)
                return _resumeSignal is not null;
        }
    }

    public void Pause()
    {
        lock (_sync)
            _resumeSignal ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public void Resume()
    {
        TaskCompletionSource? resumeSignal;
        lock (_sync)
        {
            resumeSignal = _resumeSignal;
            _resumeSignal = null;
        }

        resumeSignal?.TrySetResult();
    }

    public Task WaitIfPausedAsync(CancellationToken cancellationToken)
    {
        Task? waitTask;
        lock (_sync)
            waitTask = _resumeSignal?.Task;

        return waitTask is null
            ? Task.CompletedTask
            : waitTask.WaitAsync(cancellationToken);
    }
}
