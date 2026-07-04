using System.IO;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SelfTrainingRunPreparationRequestFactoryTests
{
    [Fact]
    public async Task Create_uebernimmt_ui_zustand_und_loader_in_workflow_request()
    {
        var cases = new List<TrainingCase>();
        var roots = new List<string> { "root" };
        var selected = new TrainingCase { CaseId = "case-1" };
        var calls = new List<string>();
        var token = new CancellationToken(canceled: false);

        var request = SelfTrainingRunPreparationRequestFactory.Create(
            new SelfTrainingRunPreparationRequestFactoryRequest(
                IsBusy: true,
                IsSelfTrainingRunning: false,
                Cases: cases,
                RootFolders: roots,
                DirectoryExists: folder =>
                {
                    calls.Add($"exists:{folder}");
                    return true;
                },
                ScanFolderAsync: folder =>
                {
                    calls.Add($"scan:{folder}");
                    return Task.FromResult<IReadOnlyList<TrainingCase>>([]);
                },
                SelectedCase: selected,
                SetSelectedCase: value => calls.Add($"selected:{value.CaseId}"),
                ResetCancellation: () =>
                {
                    calls.Add("reset");
                    return token;
                },
                SetStatusText: value => calls.Add($"status:{value}")),
            LoadSamplesAsync: () =>
            {
                calls.Add("load");
                return Task.FromResult(new List<TrainingSample> { new() { CaseId = "case-1" } });
            });

        Assert.True(request.IsBusy);
        Assert.False(request.IsSelfTrainingRunning);
        Assert.Same(cases, request.Cases);
        Assert.Same(roots, request.RootFolders);
        Assert.Same(selected, request.SelectedCase);

        Assert.True(request.DirectoryExists("root"));
        await request.ScanFolderAsync("root");
        var loaded = await request.LoadSamplesAsync();
        request.SetSelectedCase(selected);
        Assert.Equal(token, request.ResetCancellation());
        request.SetStatusText("bereit");

        Assert.Single(loaded);
        Assert.Equal(
            ["exists:root", "scan:root", "load", "selected:case-1", "reset", "status:bereit"],
            calls);
    }

    [Fact]
    public async Task CreateWithDefaults_verdrahtet_directory_exists_und_standard_mapping_aus_factory()
    {
        var inspectionDate = new DateTime(2026, 3, 4);
        var input = new TrainingCaseInput(
            CaseId: "self-case",
            FolderPath: "self-folder",
            VideoPath: "self-video.mp4",
            ProtocolPath: "self-protocol.pdf",
            InspectionDate: inspectionDate);

        var request = SelfTrainingRunPreparationRequestFactory.CreateWithDefaults(
            new SelfTrainingRunPreparationDefaultRequestFactoryRequest(
                IsBusy: false,
                IsSelfTrainingRunning: false,
                Cases: [],
                RootFolders: [],
                ScanInputsAsync: _ => Task.FromResult(new List<TrainingCaseInput> { input }),
                SelectedCase: null,
                SetSelectedCase: _ => { },
                ResetCancellation: () => CancellationToken.None,
                SetStatusText: _ => { }));

        Assert.True(request.DirectoryExists(AppContext.BaseDirectory));
        Assert.False(request.DirectoryExists(Path.Combine(AppContext.BaseDirectory, Guid.NewGuid().ToString("N"))));

        var cases = await request.ScanFolderAsync(AppContext.BaseDirectory);

        var item = Assert.Single(cases);
        Assert.Equal("self-case", item.CaseId);
        Assert.Equal("self-folder", item.FolderPath);
        Assert.Equal("self-video.mp4", item.VideoPath);
        Assert.Equal("self-protocol.pdf", item.ProtocolPath);
        Assert.Equal(inspectionDate, item.InspectionDate);
        Assert.Equal(TrainingCaseStatus.New, item.Status);
    }
}
