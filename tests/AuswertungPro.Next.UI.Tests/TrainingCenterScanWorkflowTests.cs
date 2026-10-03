using System.IO;
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
                scanFolderAsync: (_, _, _, _) =>
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

        Assert.Equal("Bitte zuerst einen oder mehrere Ordner wählen.", state.StatusText);
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
                scanFolderAsync: (folder, _, _, _) =>
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
                    scanFolderAsync: (_, _, _, _) => throw new InvalidOperationException("kaputt"))));

        Assert.False(state.IsBusy);
    }

    [Fact]
    public async Task RunAsync_reicht_das_abbruchtoken_an_jeden_ordner_weiter()
    {
        using var abbruch = new CancellationTokenSource();
        var tokens = new List<CancellationToken>();

        await TrainingCenterScanWorkflow.RunAsync(
            CreateRequest(
                rootFolders: ["root-a", "root-b"],
                resetCancellation: () => abbruch.Token,
                scanFolderAsync: (_, _, _, token) =>
                {
                    tokens.Add(token);
                    return Task.FromResult<IReadOnlyList<TrainingCase>>([]);
                }));

        Assert.Equal([abbruch.Token, abbruch.Token], tokens);
    }

    [Fact]
    public async Task RunAsync_abbruch_meldet_abgebrochen_und_speichert_nicht()
    {
        var state = new WorkflowState();

        await TrainingCenterScanWorkflow.RunAsync(
            CreateRequest(
                state: state,
                rootFolders: ["root-a"],
                scanFolderAsync: (_, _, _, _) => throw new OperationCanceledException()));

        Assert.False(state.IsBusy);
        Assert.Equal("Scan abgebrochen.", state.StatusText);
        Assert.Equal(0, state.SaveCalls);
    }

    [Fact]
    public async Task RunAsync_nennt_uebersprungene_ordner_im_protokoll_und_im_status()
    {
        var state = new WorkflowState();
        var unlesbar = Path.Combine(Path.GetTempPath(), "sewerstudio-fehlt-" + Guid.NewGuid().ToString("N"));

        await TrainingCenterScanWorkflow.RunAsync(
            CreateRequest(
                state: state,
                rootFolders: ["root-a"],
                scanFolderAsync: (_, uebersprungen, _, _) =>
                {
                    uebersprungen.Add(unlesbar);
                    return Task.FromResult<IReadOnlyList<TrainingCase>>([]);
                }));

        Assert.Equal([$"Ordner «{unlesbar}» übersprungen: nicht lesbar"], state.Logs);
        Assert.Equal("Gefunden: 0 Fälle · 1 Ordner übersprungen (siehe Protokoll)", state.StatusText);
        Assert.Equal(1, state.SaveCalls);
    }

    [Fact]
    public async Task RunAsync_nennt_ungueltige_videoverweise_im_protokoll_und_im_status()
    {
        // PR #85: Ein ungueltiger .link-Verweis laedt den Fall ohne Video; das bleibt sichtbar.
        var state = new WorkflowState();

        await TrainingCenterScanWorkflow.RunAsync(
            CreateRequest(
                state: state,
                rootFolders: ["root-a"],
                scanFolderAsync: (_, _, hinweise, _) =>
                {
                    hinweise.Add("Videoverweis «x.mpg.link» zeigt auf kein vorhandenes Video; Fall ohne Video geladen.");
                    return Task.FromResult<IReadOnlyList<TrainingCase>>([]);
                }));

        Assert.Equal(
            ["Videoverweis «x.mpg.link» zeigt auf kein vorhandenes Video; Fall ohne Video geladen."],
            state.Logs);
        Assert.Equal("Gefunden: 0 Fälle · 1 Videoverweis ungültig (siehe Protokoll)", state.StatusText);
    }

    private static TrainingCenterScanWorkflowRequest CreateRequest(
        WorkflowState? state = null,
        IReadOnlyCollection<string>? rootFolders = null,
        Func<bool>? getIsBusy = null,
        Action<bool>? setIsBusy = null,
        Func<string, bool>? directoryExists = null,
        Func<string, ICollection<string>, ICollection<string>, CancellationToken, Task<IReadOnlyList<TrainingCase>>>? scanFolderAsync = null,
        Action<IReadOnlyList<TrainingCase>>? replaceCases = null,
        Action<IReadOnlyList<TrainingCase>>? appendCases = null,
        Action<string>? setStatusText = null,
        Func<Task>? saveStateAsync = null,
        Func<CancellationToken>? resetCancellation = null)
    {
        state ??= new WorkflowState();
        return new TrainingCenterScanWorkflowRequest(
            GetIsBusy: getIsBusy ?? (() => state.IsBusy),
            SetIsBusy: setIsBusy ?? (value => state.IsBusy = value),
            RootFolders: rootFolders ?? ["root-a"],
            DirectoryExists: directoryExists ?? (_ => true),
            ScanFolderAsync: scanFolderAsync ?? ((_, _, _, _) => Task.FromResult<IReadOnlyList<TrainingCase>>([])),
            ReplaceCases: replaceCases ?? (items => state.ReplaceCalls.Add(items.ToList())),
            AppendCases: appendCases ?? (items => state.AppendCalls.Add(items.ToList())),
            SetStatusText: setStatusText ?? (value => state.StatusText = value),
            SaveStateAsync: saveStateAsync ?? (() =>
            {
                state.SaveCalls++;
                return Task.CompletedTask;
            }),
            ResetCancellation: resetCancellation ?? (() => CancellationToken.None),
            Log: state.Logs.Add);
    }

    private sealed class WorkflowState
    {
        public bool IsBusy { get; set; }
        public string StatusText { get; set; } = "";
        public List<IReadOnlyList<TrainingCase>> ReplaceCalls { get; } = new();
        public List<IReadOnlyList<TrainingCase>> AppendCalls { get; } = new();
        public int SaveCalls { get; set; }
        public List<string> Logs { get; } = new();
    }
}
