using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingCenterSaveWorkflowTests
{
    [Fact]
    public async Task RunAsync_ignoriert_aufruf_wenn_busy()
    {
        var calls = new List<string>();

        await TrainingCenterSaveWorkflow.RunAsync(
            CreateRequest(
                getIsBusy: () => true,
                buildState: () =>
                {
                    calls.Add("build");
                    return State();
                },
                saveStateAsync: _ =>
                {
                    calls.Add("save");
                    return Task.CompletedTask;
                },
                setStatusText: value => calls.Add($"status:{value}")));

        Assert.Empty(calls);
    }

    [Fact]
    public async Task RunAsync_speichert_state_setzt_status_und_finalisiert_busy()
    {
        var state = new WorkflowState();
        TrainingCenterState? saved = null;
        var built = State(caseCount: 2, rootFolderCount: 3);

        await TrainingCenterSaveWorkflow.RunAsync(
            CreateRequest(
                state: state,
                buildState: () => built,
                saveStateAsync: value =>
                {
                    saved = value;
                    return Task.CompletedTask;
                }));

        Assert.Same(built, saved);
        Assert.False(state.IsBusy);
        Assert.Equal("Gespeichert: 2 Fälle, 3 Ordner", state.StatusText);
    }

    [Fact]
    public async Task RunAsync_setzt_busy_auch_bei_speicherfehler_zurueck()
    {
        var state = new WorkflowState();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TrainingCenterSaveWorkflow.RunAsync(
                CreateRequest(
                    state: state,
                    saveStateAsync: _ => throw new InvalidOperationException("kaputt"))));

        Assert.False(state.IsBusy);
    }

    private static TrainingCenterSaveWorkflowRequest CreateRequest(
        WorkflowState? state = null,
        Func<bool>? getIsBusy = null,
        Action<bool>? setIsBusy = null,
        Func<TrainingCenterState>? buildState = null,
        Func<TrainingCenterState, Task>? saveStateAsync = null,
        Action<string>? setStatusText = null)
    {
        state ??= new WorkflowState();

        return new TrainingCenterSaveWorkflowRequest(
            GetIsBusy: getIsBusy ?? (() => state.IsBusy),
            SetIsBusy: setIsBusy ?? (value => state.IsBusy = value),
            BuildState: buildState ?? (() => State()),
            SaveStateAsync: saveStateAsync ?? (_ => Task.CompletedTask),
            SetStatusText: setStatusText ?? (value => state.StatusText = value));
    }

    private static TrainingCenterState State(int caseCount = 0, int rootFolderCount = 0)
        => new()
        {
            Cases = Enumerable.Range(0, caseCount)
                .Select(index => new TrainingCase { CaseId = $"case-{index}" })
                .ToList(),
            RootFolders = Enumerable.Range(0, rootFolderCount)
                .Select(index => $"root-{index}")
                .ToList()
        };

    private sealed class WorkflowState
    {
        public bool IsBusy { get; set; }
        public string StatusText { get; set; } = "";
    }
}
