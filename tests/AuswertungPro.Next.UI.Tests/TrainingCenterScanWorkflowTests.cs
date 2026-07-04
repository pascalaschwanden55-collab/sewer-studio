using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingCenterScanWorkflowTests
{
    [Fact]
    public async Task RunAsync_ignoriert_aufruf_wenn_busy()
    {
        var calls = new List<string>();

        await TrainingCenterScanWorkflow.RunAsync(
            CreateRequest(
                getIsBusy: () => true,
                setStatusText: value => calls.Add($"status:{value}"),
                scanFolderAsync: _ =>
                {
                    calls.Add("scan");
                    return Task.FromResult<IReadOnlyList<TrainingCase>>([]);
                },
                saveStateAsync: () =>
                {
                    calls.Add("save");
                    return Task.CompletedTask;
                }));

        Assert.Empty(calls);
    }

    [Fact]
    public async Task RunAsync_stoppt_ohne_root_folders_mit_status()
    {
        var state = new WorkflowState();

        await TrainingCenterScanWorkflow.RunAsync(
            CreateRequest(state: state, rootFolders: []));

        Assert.Equal("Bitte zuerst einen oder mehrere Ordner waehlen.", state.StatusText);
        Assert.False(state.IsBusy);
        Assert.Empty(state.ReplaceCalls);
        Assert.Equal(0, state.SaveCalls);
    }

    [Fact]
    public async Task RunAsync_scannt_vorhandene_ordner_ersetzt_cases_setzt_summary_und_speichert()
    {
        var state = new WorkflowState();
        var scanned = new List<string>();

        await TrainingCenterScanWorkflow.RunAsync(
            CreateRequest(
                state: state,
                rootFolders: ["missing", "root-a"],
                directoryExists: folder => folder == "root-a",
                scanFolderAsync: folder =>
                {
                    scanned.Add(folder);
                    return Task.FromResult<IReadOnlyList<TrainingCase>>(
                    [
                        new()
                        {
                            CaseId = "case-pdf-only",
                            ProtocolPath = "protocol.pdf"
                        },
                        new()
                        {
                            CaseId = "case-no-protocol",
                            VideoPath = "video.mp4"
                        }
                    ]);
                }));

        Assert.Equal(new[] { "root-a" }, scanned);
        Assert.False(state.IsBusy);
        var replaceCall = Assert.Single(state.ReplaceCalls);
        Assert.Empty(replaceCall);
        Assert.Single(state.AppendCalls);
        Assert.Equal(
            new[] { "case-pdf-only", "case-no-protocol" },
            state.AppendCalls[0].Select(c => c.CaseId).ToArray());
        Assert.Equal(1, state.SaveCalls);
        Assert.Contains("Gefunden: 2", state.StatusText);
        Assert.Contains("1 ohne Protokoll", state.StatusText);
    }

    [Fact]
    public async Task RunAsync_setzt_busy_auch_bei_scan_fehler_zurueck()
    {
        var state = new WorkflowState();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TrainingCenterScanWorkflow.RunAsync(
                CreateRequest(
                    state: state,
                    rootFolders: ["root-a"],
                    scanFolderAsync: _ => throw new InvalidOperationException("kaputt"))));

        Assert.False(state.IsBusy);
    }

    private static TrainingCenterScanWorkflowRequest CreateRequest(
        WorkflowState? state = null,
        IReadOnlyCollection<string>? rootFolders = null,
        Func<bool>? getIsBusy = null,
        Action<bool>? setIsBusy = null,
        Func<string, bool>? directoryExists = null,
        Func<string, Task<IReadOnlyList<TrainingCase>>>? scanFolderAsync = null,
        Action<IReadOnlyList<TrainingCase>>? replaceCases = null,
        Action<IReadOnlyList<TrainingCase>>? appendCases = null,
        Action<string>? setStatusText = null,
        Func<Task>? saveStateAsync = null)
    {
        state ??= new WorkflowState();
        return new TrainingCenterScanWorkflowRequest(
            GetIsBusy: getIsBusy ?? (() => state.IsBusy),
            SetIsBusy: setIsBusy ?? (value => state.IsBusy = value),
            RootFolders: rootFolders ?? ["root-a"],
            DirectoryExists: directoryExists ?? (_ => true),
            ScanFolderAsync: scanFolderAsync ?? (_ => Task.FromResult<IReadOnlyList<TrainingCase>>([])),
            ReplaceCases: replaceCases ?? (items => state.ReplaceCalls.Add(items.ToList())),
            AppendCases: appendCases ?? (items => state.AppendCalls.Add(items.ToList())),
            SetStatusText: setStatusText ?? (value => state.StatusText = value),
            SaveStateAsync: saveStateAsync ?? (() =>
            {
                state.SaveCalls++;
                return Task.CompletedTask;
            }));
    }

    private sealed class WorkflowState
    {
        public bool IsBusy { get; set; }
        public string StatusText { get; set; } = "";
        public List<IReadOnlyList<TrainingCase>> ReplaceCalls { get; } = new();
        public List<IReadOnlyList<TrainingCase>> AppendCalls { get; } = new();
        public int SaveCalls { get; set; }
    }
}
