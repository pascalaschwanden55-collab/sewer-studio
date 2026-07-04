using System.Collections.ObjectModel;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingBatchImportCommandRequestFactoryTests
{
    [Fact]
    public async Task Create_verdrahtet_batch_import_command_request()
    {
        var calls = new List<string>();
        var roots = new[] { @"D:\Training" };
        using var cts = new CancellationTokenSource();
        CancellationToken runToken = default;

        var request = TrainingBatchImportCommandRequestFactory.Create(
            new TrainingBatchImportCommandRequestFactoryRequest(
                GetIsBusy: () => false,
                RootFolders: roots,
                CreateCancellationSource: () =>
                {
                    calls.Add("create-cts");
                    return cts;
                },
                StoreCancellationSource: source => calls.Add(ReferenceEquals(cts, source) ? "store-same-cts" : "store-other-cts"),
                ConfirmAutoApprove: () =>
                {
                    calls.Add("confirm");
                    return new TrainingBatchImportAutoApproveConfirmationResult(true, null);
                },
                SetStatusText: value => calls.Add($"status:{value}"),
                RunImportAsync: token =>
                {
                    runToken = token;
                    calls.Add("run");
                    return Task.CompletedTask;
                }));

        Assert.False(request.GetIsBusy());
        Assert.Same(roots, request.RootFolders);
        request.SetStatusText("bereit");
        var created = request.CreateCancellationSource();
        request.StoreCancellationSource(created);
        request.ConfirmAutoApprove();
        await request.RunImportAsync(created.Token);

        Assert.Equal(cts.Token, runToken);
        Assert.Equal(["status:bereit", "create-cts", "store-same-cts", "confirm", "run"], calls);
    }

    [Fact]
    public void CreateWithDefaults_verdrahtet_auto_approve_bestaetigung_ueber_dialogdienst()
    {
        var dialogs = new DialogFake(confirmWarnResult: true);
        var request = TrainingBatchImportCommandRequestFactory.CreateWithDefaults(
            new TrainingBatchImportCommandDefaultRequestFactoryRequest(
                GetIsBusy: () => false,
                RootFolders: [@"D:\Training"],
                CreateCancellationSource: () => new CancellationTokenSource(),
                StoreCancellationSource: _ => { },
                SetStatusText: _ => { },
                RunImportAsync: _ => Task.CompletedTask),
            dialogs);

        var result = request.ConfirmAutoApprove();

        Assert.True(result.ShouldContinue);
        Assert.Null(result.StatusText);
        Assert.Equal(1, dialogs.ConfirmWarnCalls);
        Assert.Contains("Knowledge Base", dialogs.LastMessage, StringComparison.Ordinal);
        Assert.StartsWith("Batch-Import + KB", dialogs.LastTitle, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateWithDefaults_mit_run_defaults_baut_run_workflow_request_in_factory()
    {
        var roots = new[] { @"D:\Training" };
        var cases = new List<TrainingCase>();
        var samples = new ObservableCollection<TrainingSample>();
        var calls = new List<string>();
        TrainingBatchImportRunWorkflowRequest? capturedRunRequest = null;
        CancellationToken capturedToken = default;

        var request = TrainingBatchImportCommandRequestFactory.CreateWithDefaults(
            new TrainingBatchImportCommandRunDefaultRequestFactoryRequest(
                GetIsBusy: () => false,
                RootFolders: roots,
                CreateCancellationSource: () => new CancellationTokenSource(),
                StoreCancellationSource: _ => { },
                ScanInputsAsync: folder =>
                {
                    calls.Add($"scan:{folder}");
                    return Task.FromResult(new List<TrainingCaseInput>
                    {
                        new("Case-1", folder, @"D:\Training\video.mp4", @"D:\Training\protocol.pdf")
                    });
                },
                Cases: cases,
                CodeCatalog: null,
                SaveStateAsync: () =>
                {
                    calls.Add("save");
                    return Task.CompletedTask;
                },
                GetSelfTrainingResultCount: () => 9,
                SetBusy: value => calls.Add($"busy:{value}"),
                SetLogText: value => calls.Add($"log-text:{value}"),
                SetProgressValue: value => calls.Add($"progress:{value}"),
                SetProgressMax: value => calls.Add($"max:{value}"),
                SetStatusText: value => calls.Add($"status:{value}"),
                Log: value => calls.Add($"log:{value}"),
                UpdateLivePreview: _ => calls.Add("preview"),
                OnUi: action =>
                {
                    calls.Add("ui");
                    action();
                },
                AddResult: _ => calls.Add("result"),
                UpdateCodeDistribution: (code, level) => calls.Add($"distribution:{code}:{level}"),
                SetKbSampleCount: value => calls.Add($"samples:{value}"),
                SetKbCodesCovered: value => calls.Add($"codes:{value}"),
                Samples: samples,
                RefreshKbStatusAsync: () =>
                {
                    calls.Add("refresh-kb");
                    return Task.CompletedTask;
                },
                ClearLivePreview: () => calls.Add("clear-preview"),
                ResetSelfTrainingVisuals: () => calls.Add("reset-visuals")),
            new DialogFake(confirmWarnResult: true),
            (runRequest, token) =>
            {
                capturedRunRequest = runRequest;
                capturedToken = token;
                calls.Add("run");
                return Task.CompletedTask;
            });
        using var cts = new CancellationTokenSource();

        await request.RunImportAsync(cts.Token);

        Assert.NotNull(capturedRunRequest);
        Assert.Equal(cts.Token, capturedToken);
        Assert.Same(roots, capturedRunRequest.RootFolders);
        Assert.Same(cases, capturedRunRequest.Cases);
        Assert.Equal(9, capturedRunRequest.GetSelfTrainingResultCount());

        var mappedCases = await capturedRunRequest.ScanFolderAsync(@"D:\Training");
        Assert.Equal("Case-1", Assert.Single(mappedCases).CaseId);

        await capturedRunRequest.SaveStateAsync();
        capturedRunRequest.SetBusy(true);
        capturedRunRequest.SetStatusText("bereit");
        capturedRunRequest.UpdateCodeDistribution("BAA", MatchLevel.ExactMatch);
        capturedRunRequest.ReplaceSamples([new TrainingSample { SampleId = "S-1" }]);

        Assert.Single(samples);
        Assert.Contains("run", calls);
        Assert.Contains(@"scan:D:\Training", calls);
        Assert.Contains("save", calls);
        Assert.Contains("busy:True", calls);
        Assert.Contains("status:bereit", calls);
        Assert.Contains("distribution:BAA:ExactMatch", calls);
    }

    private sealed class DialogFake(bool confirmWarnResult) : IDialogService
    {
        public int ConfirmWarnCalls { get; private set; }
        public string LastMessage { get; private set; } = "";
        public string LastTitle { get; private set; } = "";

        public string? OpenFile(string title, string filter, string? initialDirectory = null) => null;

        public string? SaveFile(string title, string filter, string? defaultExt = null, string? defaultFileName = null) => null;

        public string[] OpenFiles(string title, string filter) => [];

        public string? SelectFolder(string title, string? initialPath = null) => null;

        public void Info(string message, string title = "Hinweis") { }

        public void Warn(string message, string title = "Warnung") { }

        public void Error(string message, string title = "Fehler") { }

        public bool Confirm(string message, string title = "Bestaetigung") => false;

        public bool ConfirmWarn(string message, string title = "Bestaetigung", bool defaultNo = true)
        {
            ConfirmWarnCalls++;
            LastMessage = message;
            LastTitle = title;
            return confirmWarnResult;
        }

        public DialogConfirm ConfirmCancel(string message, string title = "Bestaetigung") => DialogConfirm.Cancel;
    }
}
