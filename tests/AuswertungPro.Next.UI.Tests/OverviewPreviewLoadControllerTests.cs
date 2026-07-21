using System.Collections.Concurrent;
using System.Diagnostics;
using AuswertungPro.Next.Application.Dashboard;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Tests;

public sealed class OverviewPreviewLoadControllerTests
{
    [Fact]
    public void Angezeigtes_A_verwirft_wartendes_B_wenn_A_erneut_gewaehlt_wird()
    {
        var owner = new OwnerQueue();
        var delays = new ManualDelayQueue();
        var state = new PublishedState();
        var loadCalls = 0;
        using var controller = new OverviewPreviewLoadController(
            load: (request, _) =>
            {
                Interlocked.Increment(ref loadCalls);
                return Preview(request);
            },
            owner.TryPost,
            state.Publish,
            delays.DelayAsync);

        controller.Update(Request("A"), showFullDashboard: false);
        delays.TakeNext().TrySetResult(true);
        Assert.True(owner.PumpUntil(() => state.Preview?.Path == "A"));

        controller.Update(Request("B"), showFullDashboard: false);
        var pendingB = delays.TakeNext();
        controller.Update(Request("A"), showFullDashboard: false);

        Assert.True(SpinWait.SpinUntil(() => pendingB.Task.IsCanceled, TimeSpan.FromSeconds(2)));
        owner.Drain();
        Assert.Equal("A", state.Preview?.Path);
        Assert.False(state.IsLoading);
        Assert.Equal(1, Volatile.Read(ref loadCalls));
    }

    [Fact]
    public void Neuer_lauf_gewinnt_auch_wenn_alter_loader_abbruch_ignoriert()
    {
        var owner = new OwnerQueue();
        var state = new PublishedState();
        using var firstStarted = new ManualResetEventSlim();
        using var firstRelease = new ManualResetEventSlim();
        using var firstFinished = new ManualResetEventSlim();
        var loadCalls = 0;
        using var controller = new OverviewPreviewLoadController(
            load: (request, _) =>
            {
                Interlocked.Increment(ref loadCalls);
                if (request.Path == "A")
                {
                    firstStarted.Set();
                    firstRelease.Wait(TimeSpan.FromSeconds(3));
                    firstFinished.Set();
                }

                return Preview(request);
            },
            owner.TryPost,
            state.Publish,
            (_, _) => Task.CompletedTask);

        controller.Update(Request("A"), showFullDashboard: false);
        Assert.True(owner.PumpUntil(() => firstStarted.IsSet));

        controller.Update(Request("B"), showFullDashboard: false);
        Assert.True(owner.PumpUntil(() => state.Preview?.Path == "B"));

        var postsBeforeOldCompletion = owner.PostCount;
        firstRelease.Set();
        Assert.True(firstFinished.Wait(TimeSpan.FromSeconds(2)));
        Assert.True(SpinWait.SpinUntil(
            () => owner.PostCount > postsBeforeOldCompletion,
            TimeSpan.FromSeconds(2)));
        owner.Drain();

        Assert.Equal("B", state.Preview?.Path);
        Assert.False(state.IsLoading);
        Assert.Equal(2, Volatile.Read(ref loadCalls));
    }

    [Fact]
    public void Laufendes_A_darf_nach_verworfenem_wartendem_B_fertig_werden()
    {
        var owner = new OwnerQueue();
        var delays = new ManualDelayQueue();
        var state = new PublishedState();
        using var firstStarted = new ManualResetEventSlim();
        using var firstRelease = new ManualResetEventSlim();
        var loadCalls = 0;
        using var controller = new OverviewPreviewLoadController(
            load: (request, _) =>
            {
                Interlocked.Increment(ref loadCalls);
                firstStarted.Set();
                firstRelease.Wait(TimeSpan.FromSeconds(3));
                return Preview(request);
            },
            owner.TryPost,
            state.Publish,
            delays.DelayAsync);

        controller.Update(Request("A"), showFullDashboard: false);
        delays.TakeNext().TrySetResult(true);
        Assert.True(owner.PumpUntil(() => firstStarted.IsSet));

        controller.Update(Request("B"), showFullDashboard: false);
        var pendingB = delays.TakeNext();
        controller.Update(Request("A"), showFullDashboard: false);
        Assert.True(SpinWait.SpinUntil(() => pendingB.Task.IsCanceled, TimeSpan.FromSeconds(2)));

        firstRelease.Set();
        Assert.True(owner.PumpUntil(() => state.Preview?.Path == "A"));

        Assert.False(state.IsLoading);
        Assert.Equal(1, Volatile.Read(ref loadCalls));
    }

    [Fact]
    public void Bereits_geladener_pfad_wird_nicht_erneut_geladen()
    {
        var owner = new OwnerQueue();
        var state = new PublishedState();
        var loadCalls = 0;
        using var controller = new OverviewPreviewLoadController(
            load: (request, _) =>
            {
                Interlocked.Increment(ref loadCalls);
                return Preview(request);
            },
            owner.TryPost,
            state.Publish,
            (_, _) => Task.CompletedTask);

        controller.Update(Request("A"), showFullDashboard: false);
        Assert.True(owner.PumpUntil(() => state.Preview?.Path == "A"));

        controller.Update(Request("a"), showFullDashboard: false);
        owner.Drain();

        Assert.Equal(1, Volatile.Read(ref loadCalls));
        Assert.Equal("A", state.Preview?.Path);
        Assert.Equal(
            new[] { OverviewPreviewTransitionKind.LoadingStarted, OverviewPreviewTransitionKind.Loaded },
            state.Transitions);
    }

    [Fact]
    public void Leere_auswahl_waehrend_debounce_bricht_ab_und_laedt_nicht()
    {
        var owner = new OwnerQueue();
        var delays = new ManualDelayQueue();
        var state = new PublishedState();
        var loadCalls = 0;
        using var controller = new OverviewPreviewLoadController(
            load: (request, _) =>
            {
                Interlocked.Increment(ref loadCalls);
                return Preview(request);
            },
            owner.TryPost,
            state.Publish,
            delays.DelayAsync);

        controller.Update(Request("A"), showFullDashboard: false);
        var pending = delays.TakeNext();
        controller.Update(request: null, showFullDashboard: false);

        Assert.True(SpinWait.SpinUntil(() => pending.Task.IsCanceled, TimeSpan.FromSeconds(2)));
        owner.Drain();
        Assert.Null(state.Preview);
        Assert.False(state.IsLoading);
        Assert.Equal(0, Volatile.Read(ref loadCalls));
        Assert.Equal(OverviewPreviewTransitionKind.Cleared, Assert.Single(state.Transitions));
    }

    [Fact]
    public void Abgelehnter_start_post_laesst_sich_sicher_disposen()
    {
        var loadCalls = 0;
        var controller = new OverviewPreviewLoadController(
            load: (request, _) =>
            {
                Interlocked.Increment(ref loadCalls);
                return Preview(request);
            },
            tryPostToOwner: _ => false,
            publish: _ => { },
            delayAsync: (_, _) => Task.CompletedTask);

        controller.Update(Request("A"), showFullDashboard: false);
        controller.Dispose();

        Assert.Equal(0, Volatile.Read(ref loadCalls));
    }

    [Fact]
    public void Abgelehnter_completion_post_laesst_sich_sicher_disposen()
    {
        var state = new PublishedState();
        var postCalls = 0;
        var controller = new OverviewPreviewLoadController(
            load: PreviewIgnoringCancellation,
            tryPostToOwner: action =>
            {
                if (Interlocked.Increment(ref postCalls) != 1)
                    return false;

                action();
                return true;
            },
            state.Publish,
            (_, _) => Task.CompletedTask);

        controller.Update(Request("A"), showFullDashboard: false);
        Assert.True(SpinWait.SpinUntil(
            () => Volatile.Read(ref postCalls) >= 2,
            TimeSpan.FromSeconds(2)));

        controller.Dispose();

        Assert.False(state.IsLoading);
        Assert.DoesNotContain(OverviewPreviewTransitionKind.Loaded, state.Transitions);

        static ProjectPreview PreviewIgnoringCancellation(
            OverviewPreviewRequest request,
            CancellationToken _)
            => Preview(request);
    }

    [Fact]
    public void Dispose_waehrend_load_stoppt_loading_und_verwirft_spaetes_ergebnis()
    {
        var owner = new OwnerQueue();
        var state = new PublishedState();
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var finished = new ManualResetEventSlim();
        var controller = new OverviewPreviewLoadController(
            load: (request, _) =>
            {
                started.Set();
                release.Wait(TimeSpan.FromSeconds(3));
                finished.Set();
                return Preview(request);
            },
            owner.TryPost,
            state.Publish,
            (_, _) => Task.CompletedTask);

        controller.Update(Request("A"), showFullDashboard: false);
        Assert.True(owner.PumpUntil(() => started.IsSet));
        Assert.True(state.IsLoading);

        controller.Dispose();
        controller.Dispose();
        var postsBeforeCompletion = owner.PostCount;
        release.Set();
        Assert.True(finished.Wait(TimeSpan.FromSeconds(2)));
        Assert.True(SpinWait.SpinUntil(
            () => owner.PostCount > postsBeforeCompletion,
            TimeSpan.FromSeconds(2)));
        owner.Drain();

        Assert.Null(state.Preview);
        Assert.False(state.IsLoading);
        Assert.DoesNotContain(OverviewPreviewTransitionKind.Loaded, state.Transitions);
        Assert.Equal(OverviewPreviewTransitionKind.LoadingStopped, state.Transitions[^1]);
    }

    private static OverviewPreviewRequest Request(string path)
        => new(
            Path: path,
            Name: $"Projekt {path}",
            Description: string.Empty,
            ModifiedAtUtc: null,
            HoldingCount: 0,
            ShaftCount: 0);

    private static ProjectPreview Preview(OverviewPreviewRequest request)
        => ProjectPreviewFactory.FromProject(
            new Project { Name = request.Name },
            request.Path,
            haltungCosts: null,
            schachtCosts: null);

    private sealed class PublishedState
    {
        public ProjectPreview? Preview { get; private set; }
        public bool IsLoading { get; private set; }
        public List<OverviewPreviewTransitionKind> Transitions { get; } = new();

        public void Publish(OverviewPreviewTransition transition)
        {
            Transitions.Add(transition.Kind);
            switch (transition.Kind)
            {
                case OverviewPreviewTransitionKind.LoadingStarted:
                    IsLoading = true;
                    Preview = null;
                    break;
                case OverviewPreviewTransitionKind.Loaded:
                    Preview = transition.Preview;
                    IsLoading = false;
                    break;
                case OverviewPreviewTransitionKind.Cleared:
                    IsLoading = false;
                    Preview = null;
                    break;
                case OverviewPreviewTransitionKind.LoadingStopped:
                    IsLoading = false;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(transition));
            }
        }
    }

    private sealed class OwnerQueue
    {
        private readonly ConcurrentQueue<Action> _actions = new();
        private int _postCount;

        public int PostCount => Volatile.Read(ref _postCount);

        public bool TryPost(Action action)
        {
            Interlocked.Increment(ref _postCount);
            _actions.Enqueue(action);
            return true;
        }

        public void Drain()
        {
            while (_actions.TryDequeue(out var action))
                action();
        }

        public bool PumpUntil(Func<bool> condition)
        {
            var stopwatch = Stopwatch.StartNew();
            var spinner = new SpinWait();
            while (stopwatch.Elapsed < TimeSpan.FromSeconds(3))
            {
                Drain();
                if (condition())
                    return true;
                spinner.SpinOnce();
            }

            Drain();
            return condition();
        }
    }

    private sealed class ManualDelayQueue
    {
        private readonly ConcurrentQueue<TaskCompletionSource<bool>> _delays = new();

        public Task DelayAsync(TimeSpan _, CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
            _delays.Enqueue(completion);
            return completion.Task;
        }

        public TaskCompletionSource<bool> TakeNext()
        {
            Assert.True(_delays.TryDequeue(out var completion));
            return completion;
        }
    }
}
