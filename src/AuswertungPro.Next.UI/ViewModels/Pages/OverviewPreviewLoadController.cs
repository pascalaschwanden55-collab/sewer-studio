using AuswertungPro.Next.Application.Dashboard;

namespace AuswertungPro.Next.UI.ViewModels.Pages;

internal sealed record OverviewPreviewRequest(
    string Path,
    string Name,
    string Description,
    DateTime? ModifiedAtUtc,
    int HoldingCount,
    int ShaftCount)
{
    internal static OverviewPreviewRequest From(ProjectOverviewEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return new OverviewPreviewRequest(
            entry.Path,
            entry.Name,
            entry.Description,
            entry.ModifiedAtUtc,
            entry.RecordCount,
            entry.SchachtCount);
    }
}

internal enum OverviewPreviewTransitionKind
{
    LoadingStarted,
    Loaded,
    Cleared,
    LoadingStopped
}

internal readonly record struct OverviewPreviewTransition(
    OverviewPreviewTransitionKind Kind,
    ProjectPreview? Preview = null);

/// <summary>
/// Besitzt Debounce, Abbruch und Gewinnerregel der Projektvorschau.
/// Alle Zustandsaenderungen laufen ueber den eingefangenen Besitzer-Thread.
/// </summary>
internal sealed class OverviewPreviewLoadController : IDisposable
{
    internal static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(200);

    private readonly Func<OverviewPreviewRequest, CancellationToken, ProjectPreview> _load;
    private readonly Func<Action, bool> _tryPostToOwner;
    private readonly Action<OverviewPreviewTransition> _publish;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;

    private CancellationTokenSource? _pendingCts;
    private OverviewPreviewRequest? _pendingRequest;
    private OverviewPreviewRequest? _latestRequest;
    private long _requestVersion;
    private CancellationTokenSource? _activeCts;
    private string? _activePath;
    private long _activeRunId;
    private long _nextRunId;
    private string? _displayedPath;
    private ProjectPreview? _currentPreview;
    private bool _showFullDashboard;
    private bool _isLoading;
    private bool _disposed;

    internal OverviewPreviewLoadController(
        Func<OverviewPreviewRequest, CancellationToken, ProjectPreview> load,
        Func<Action, bool> tryPostToOwner,
        Action<OverviewPreviewTransition> publish,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
    {
        _load = load ?? throw new ArgumentNullException(nameof(load));
        _tryPostToOwner = tryPostToOwner ?? throw new ArgumentNullException(nameof(tryPostToOwner));
        _publish = publish ?? throw new ArgumentNullException(nameof(publish));
        _delayAsync = delayAsync ?? Task.Delay;
    }

    internal void Update(OverviewPreviewRequest? request, bool showFullDashboard)
    {
        if (_disposed)
            return;

        _showFullDashboard = showFullDashboard;
        if (showFullDashboard || request is null)
        {
            Clear();
            return;
        }

        if (_pendingCts is not null && SamePath(_pendingRequest?.Path, request.Path))
            return;
        if (_pendingCts is null && _activeCts is not null && SamePath(_activePath, request.Path))
            return;
        if (_pendingCts is null && _currentPreview is not null && SamePath(_displayedPath, request.Path))
            return;

        var version = ++_requestVersion;
        _latestRequest = request;
        CancelPending();

        // Eine Rueckkehr zum noch laufenden oder bereits sichtbaren Projekt verwirft
        // einen fremden wartenden Request, ohne einen zweiten Load zu starten.
        if (_activeCts is not null && SamePath(_activePath, request.Path))
            return;
        if (_currentPreview is not null && SamePath(_displayedPath, request.Path))
            return;

        var cts = new CancellationTokenSource();
        _pendingCts = cts;
        _pendingRequest = request;
        _ = DelayThenPostStartAsync(request, version, cts);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _latestRequest = null;
        _requestVersion++;
        CancelPending();
        CancelActive();
        if (_isLoading)
        {
            _isLoading = false;
            _publish(new OverviewPreviewTransition(OverviewPreviewTransitionKind.LoadingStopped));
        }
    }

    private async Task DelayThenPostStartAsync(
        OverviewPreviewRequest request,
        long version,
        CancellationTokenSource cts)
    {
        try
        {
            await _delayAsync(DebounceDelay, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            cts.Dispose();
            return;
        }
        catch
        {
            if (!TryPost(() => DropFailedPending(cts)))
                AbandonPendingOffOwner(cts);
            return;
        }

        if (!TryPost(() => StartPendingLoad(request, version, cts)))
            AbandonPendingOffOwner(cts);
    }

    private void StartPendingLoad(
        OverviewPreviewRequest request,
        long version,
        CancellationTokenSource pendingCts)
    {
        try
        {
            if (_disposed
                || !ReferenceEquals(_pendingCts, pendingCts)
                || pendingCts.IsCancellationRequested
                || version != _requestVersion
                || _showFullDashboard
                || !SamePath(_latestRequest?.Path, request.Path))
            {
                return;
            }

            _pendingCts = null;
            _pendingRequest = null;
            CancelActive();

            var activeCts = new CancellationTokenSource();
            var runId = ++_nextRunId;
            _activeCts = activeCts;
            _activePath = request.Path;
            _activeRunId = runId;
            _currentPreview = null;
            _isLoading = true;
            _publish(new OverviewPreviewTransition(OverviewPreviewTransitionKind.LoadingStarted));

            var task = Task.Run(() => _load(request, activeCts.Token), activeCts.Token);
            _ = task.ContinueWith(
                completed =>
                {
                    if (!TryPost(() => CompleteLoad(request, runId, activeCts, completed)))
                    {
                        ObserveFault(completed);
                        AbandonActiveOffOwner(activeCts);
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);
        }
        finally
        {
            if (ReferenceEquals(_pendingCts, pendingCts))
            {
                _pendingCts = null;
                _pendingRequest = null;
            }

            pendingCts.Dispose();
        }
    }

    private void CompleteLoad(
        OverviewPreviewRequest request,
        long runId,
        CancellationTokenSource cts,
        Task<ProjectPreview> task)
    {
        try
        {
            ObserveFault(task);
            if (!ReferenceEquals(_activeCts, cts) || _activeRunId != runId)
                return;

            _activeCts = null;
            _activePath = null;
            _activeRunId = 0;

            if (_disposed
                || cts.IsCancellationRequested
                || _showFullDashboard
                || !SamePath(_latestRequest?.Path, request.Path)
                || !task.IsCompletedSuccessfully)
            {
                StopLoading();
                return;
            }

            _displayedPath = request.Path;
            _currentPreview = task.Result;
            _isLoading = false;
            _publish(new OverviewPreviewTransition(
                OverviewPreviewTransitionKind.Loaded,
                _currentPreview));
        }
        finally
        {
            cts.Dispose();
        }
    }

    private void Clear()
    {
        _latestRequest = null;
        _requestVersion++;
        CancelPending();
        CancelActive();
        _displayedPath = null;
        _currentPreview = null;
        _isLoading = false;
        _publish(new OverviewPreviewTransition(OverviewPreviewTransitionKind.Cleared));
    }

    private void CancelPending()
    {
        var cts = _pendingCts;
        _pendingCts = null;
        _pendingRequest = null;
        TryCancel(cts);
    }

    private void CancelActive()
    {
        var cts = _activeCts;
        _activeCts = null;
        _activePath = null;
        _activeRunId = 0;
        TryCancel(cts);
    }

    private void StopLoading()
    {
        if (!_isLoading)
            return;

        _isLoading = false;
        _publish(new OverviewPreviewTransition(OverviewPreviewTransitionKind.LoadingStopped));
    }

    private void DropFailedPending(CancellationTokenSource cts)
    {
        if (ReferenceEquals(_pendingCts, cts))
        {
            _pendingCts = null;
            _pendingRequest = null;
        }

        cts.Dispose();
    }

    private void AbandonPendingOffOwner(CancellationTokenSource cts)
    {
        Interlocked.CompareExchange(ref _pendingCts, null, cts);
        cts.Dispose();
    }

    private void AbandonActiveOffOwner(CancellationTokenSource cts)
    {
        Interlocked.CompareExchange(ref _activeCts, null, cts);
        cts.Dispose();
    }

    private bool TryPost(Action action)
    {
        try
        {
            return _tryPostToOwner(action);
        }
        catch
        {
            return false;
        }
    }

    private static void ObserveFault(Task task)
    {
        if (task.IsFaulted)
            _ = task.Exception;
    }

    private static void TryCancel(CancellationTokenSource? cts)
    {
        if (cts is null)
            return;

        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Der Besitzer-Dispatcher kann zwischen Abschluss und Dispose schliessen.
        }
    }

    private static bool SamePath(string? left, string? right)
        => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
