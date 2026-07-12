using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Helpers;

/// <summary>
/// Fuehrt nur eine begrenzte Zahl gleichzeitiger Hintergrundaufgaben aus und behaelt
/// sie bis zum Ende im Blick. Weitere Aufgaben werden sauber abgelehnt.
/// </summary>
internal sealed class BoundedBackgroundTaskRunner
{
    private readonly SemaphoreSlim _slots;
    private readonly ILogger _logger;
    private readonly object _gate = new();
    private readonly HashSet<Task> _activeTasks = [];

    public BoundedBackgroundTaskRunner(int maxConcurrency, ILogger logger)
    {
        if (maxConcurrency <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxConcurrency));

        _slots = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    internal int ActiveCount
    {
        get
        {
            lock (_gate)
                return _activeTasks.Count;
        }
    }

    public bool TryRun(Func<Task> operation, string context)
    {
        ArgumentNullException.ThrowIfNull(operation);

        if (!_slots.Wait(0))
        {
            TryLog(() => _logger.LogWarning(
                "Hintergrundaufgabe {Context} abgelehnt: Parallelitaetsgrenze erreicht.",
                context));
            return false;
        }

        var task = RunCoreAsync(operation, context);
        lock (_gate)
            _activeTasks.Add(task);

        _ = task.ContinueWith(
            completed =>
            {
                lock (_gate)
                    _activeTasks.Remove(completed);
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        return true;
    }

    public Task WaitForIdleAsync()
    {
        Task[] snapshot;
        lock (_gate)
            snapshot = [.. _activeTasks];

        return snapshot.Length == 0 ? Task.CompletedTask : Task.WhenAll(snapshot);
    }

    private async Task RunCoreAsync(Func<Task> operation, string context)
    {
        try
        {
            await operation().ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Beim Beenden des Programms ist ein Abbruch normal.
        }
        catch (Exception ex)
        {
            TryLog(() => _logger.LogError(
                ex,
                "Hintergrundaufgabe {Context} ist fehlgeschlagen.",
                context));
        }
        finally
        {
            _slots.Release();
        }
    }

    private static void TryLog(Action write)
    {
        try { write(); }
        catch
        {
            // Ein voller/gesperrter Log-Datentraeger darf den Server nicht beenden.
        }
    }
}
