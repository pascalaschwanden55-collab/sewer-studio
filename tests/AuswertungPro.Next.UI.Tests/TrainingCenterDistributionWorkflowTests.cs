using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingCenterDistributionWorkflowTests
{
    [Fact]
    public async Task RunAsync_busy_oder_abbruch_startet_keine_verteilung()
    {
        var busy = true;
        var selectors = 0;
        var imports = 0;
        var request = CreateRequest(
            getIsBusy: () => busy,
            selectPdfPath: () =>
            {
                selectors++;
                return @"D:\Projekt\Input\Uri.pdf";
            },
            selectVideoFolder: () =>
            {
                selectors++;
                return @"V:\Videos";
            },
            distributeAsync: (_, _, _) =>
            {
                imports++;
                return Task.FromResult(Result(0));
            });

        await TrainingCenterDistributionWorkflow.RunAsync(request);

        Assert.Equal(0, selectors);
        Assert.Equal(0, imports);

        busy = false;
        request = request with
        {
            SelectPdfPath = () => null,
            SelectVideoFolder = () =>
            {
                selectors++;
                return @"V:\Videos";
            }
        };

        await TrainingCenterDistributionWorkflow.RunAsync(request);

        Assert.Equal(0, selectors);
        Assert.Equal(0, imports);
    }

    [Fact]
    public async Task RunAsync_erfolg_protokolliert_resultat_und_fuegt_output_ordner_hinzu()
    {
        var state = new WorkflowState();
        var rootFolders = new List<string>();
        var updateCount = 0;
        var calls = new List<string>();

        var request = CreateRequest(
            state: state,
            rootFolders: rootFolders,
            updateRootFolderDisplay: () => updateCount++,
            log: message => state.Logs.Add(message),
            distributeAsync: (pdf, video, output) =>
            {
                calls.Add($"{pdf}|{video}|{output}");
                return Task.FromResult(new TrainingCenterImportService.DistributeResult(
                    TotalChunks: 3,
                    Distributed: 2,
                    VideosMatched: 1,
                    Uncertain: 1,
                    OutputFolder: output,
                    Messages: new List<string> { "Haltung A: ok", "Haltung B: kein Video" }));
            });

        await TrainingCenterDistributionWorkflow.RunAsync(request);

        Assert.Equal(
            [@"D:\Projekt\Input\Uri.pdf|V:\Videos|D:\Projekt\Uri_Training"],
            calls);
        Assert.False(state.IsBusy);
        Assert.Equal("", state.LogText);
        Assert.Equal(["D:\\Projekt\\Uri_Training"], rootFolders);
        Assert.Equal(1, updateCount);
        Assert.Equal("Verteilt: 2 Haltungen, 1 Videos -> D:\\Projekt\\Uri_Training", state.StatusText);
        Assert.Contains("PDF: D:\\Projekt\\Input\\Uri.pdf", state.Logs);
        Assert.Contains("Videos: V:\\Videos", state.Logs);
        Assert.Contains("Output: D:\\Projekt\\Uri_Training", state.Logs);
        Assert.Contains("  Haltung A: ok", state.Logs);
        Assert.Contains("--- Fertig: 2 Haltungen verteilt, 1 Videos zugeordnet ---", state.Logs);
        Assert.Contains("  1 Chunks ohne Haltungs-ID uebersprungen.", state.Logs);
        Assert.Contains("Output-Ordner als Trainings-Ordner hinzugefuegt. Klicke 'Scannen' zum Laden.", state.Logs);
    }

    [Fact]
    public async Task RunAsync_ohne_verteilte_haltungen_fuegt_keinen_root_ordner_hinzu()
    {
        var state = new WorkflowState();
        var rootFolders = new List<string>();
        var updateCount = 0;
        var request = CreateRequest(
            state: state,
            rootFolders: rootFolders,
            updateRootFolderDisplay: () => updateCount++,
            distributeAsync: (_, _, output) =>
                Task.FromResult(new TrainingCenterImportService.DistributeResult(
                    TotalChunks: 1,
                    Distributed: 0,
                    VideosMatched: 0,
                    Uncertain: 0,
                    OutputFolder: output,
                    Messages: new List<string>())));

        await TrainingCenterDistributionWorkflow.RunAsync(request);

        Assert.Empty(rootFolders);
        Assert.Equal(0, updateCount);
        Assert.Equal("Verteilt: 0 Haltungen, 0 Videos -> D:\\Projekt\\Uri_Training", state.StatusText);
    }

    [Fact]
    public async Task RunAsync_fehler_wird_protokolliert_und_busy_zurueckgesetzt()
    {
        var state = new WorkflowState();
        var request = CreateRequest(
            state: state,
            log: message => state.Logs.Add(message),
            distributeAsync: (_, _, _) => throw new InvalidOperationException("kaputt"));

        await TrainingCenterDistributionWorkflow.RunAsync(request);

        Assert.False(state.IsBusy);
        Assert.Equal("Fehler bei Verteilung: kaputt", state.StatusText);
        Assert.Contains("Fehler: kaputt", state.Logs);
    }

    private static TrainingCenterDistributionWorkflowRequest CreateRequest(
        WorkflowState? state = null,
        IList<string>? rootFolders = null,
        Func<bool>? getIsBusy = null,
        Action<bool>? setIsBusy = null,
        Func<string?>? selectPdfPath = null,
        Func<string?>? selectVideoFolder = null,
        Func<string, string, string, Task<TrainingCenterImportService.DistributeResult>>? distributeAsync = null,
        Action? updateRootFolderDisplay = null,
        Action<string>? setLogText = null,
        Action<string>? setStatusText = null,
        Action<string>? log = null)
    {
        state ??= new WorkflowState();

        return new TrainingCenterDistributionWorkflowRequest(
            GetIsBusy: getIsBusy ?? (() => state.IsBusy),
            SetIsBusy: setIsBusy ?? (value => state.IsBusy = value),
            SelectPdfPath: selectPdfPath ?? (() => @"D:\Projekt\Input\Uri.pdf"),
            SelectVideoFolder: selectVideoFolder ?? (() => @"V:\Videos"),
            DistributeAsync: distributeAsync ?? ((_, _, output) => Task.FromResult(Result(1, output))),
            RootFolders: rootFolders ?? new List<string>(),
            UpdateRootFolderDisplay: updateRootFolderDisplay ?? (() => { }),
            SetLogText: setLogText ?? (value => state.LogText = value),
            SetStatusText: setStatusText ?? (value => state.StatusText = value),
            Log: log ?? (message => state.Logs.Add(message)));
    }

    private static TrainingCenterImportService.DistributeResult Result(
        int distributed,
        string outputFolder = @"D:\Projekt\Uri_Training")
        => new(
            TotalChunks: distributed,
            Distributed: distributed,
            VideosMatched: 0,
            Uncertain: 0,
            OutputFolder: outputFolder,
            Messages: new List<string>());

    private sealed class WorkflowState
    {
        public bool IsBusy { get; set; }
        public string LogText { get; set; } = "alt";
        public string StatusText { get; set; } = "";
        public List<string> Logs { get; } = new();
    }
}
