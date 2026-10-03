using System.IO;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingCenterScanRequestFactoryTests
{
    [Fact]
    public async Task Create_verdrahtet_scan_request_und_mappt_import_cases()
    {
        var calls = new List<string>();
        var rootFolders = new List<string> { "root-a" };
        var input = new TrainingCaseInput(
            CaseId: "case-1",
            FolderPath: "folder",
            VideoPath: "video.mp4",
            ProtocolPath: "protocol.pdf",
            InspectionDate: null);

        var request = TrainingCenterScanRequestFactory.Create(
            new TrainingCenterScanRequestFactoryRequest(
                GetIsBusy: () => false,
                SetIsBusy: value => calls.Add("busy:" + value),
                RootFolders: rootFolders,
                DirectoryExists: folder =>
                {
                    calls.Add("exists:" + folder);
                    return true;
                },
                ScanInputsAsync: (folder, _, _) =>
                {
                    calls.Add("scan:" + folder);
                    return Task.FromResult(new List<TrainingCaseInput> { input });
                },
                ToTrainingCase: value =>
                {
                    calls.Add("map:" + value.CaseId);
                    return new TrainingCase { CaseId = value.CaseId };
                },
                ReplaceCases: items => calls.Add("replace:" + items.Count),
                AppendCases: items => calls.Add("append:" + items.Count),
                SetStatusText: value => calls.Add("status:" + value),
                SaveStateAsync: () =>
                {
                    calls.Add("save");
                    return Task.CompletedTask;
                },
                ResetCancellation: () =>
                {
                    calls.Add("reset");
                    return CancellationToken.None;
                },
                Log: value => calls.Add("log:" + value)));

        Assert.False(request.GetIsBusy());
        request.SetIsBusy(true);
        Assert.Same(rootFolders, request.RootFolders);
        Assert.True(request.DirectoryExists("root-a"));

        Assert.Equal(CancellationToken.None, request.ResetCancellation());
        var scanned = await request.ScanFolderAsync("root-a", [], CancellationToken.None);
        request.ReplaceCases([]);
        request.AppendCases(scanned);
        request.SetStatusText("ok");
        request.Log("ordner");
        await request.SaveStateAsync();

        Assert.Equal(["case-1"], scanned.Select(item => item.CaseId));
        Assert.Equal(
            [
                "busy:True",
                "exists:root-a",
                "reset",
                "scan:root-a",
                "map:case-1",
                "replace:0",
                "append:1",
                "status:ok",
                "log:ordner",
                "save"
            ],
            calls);
    }

    [Fact]
    public async Task CreateWithDefaults_verdrahtet_directory_exists_und_standard_mapping_aus_factory()
    {
        var inspectionDate = new DateTime(2026, 1, 2);
        var input = new TrainingCaseInput(
            CaseId: "case-default",
            FolderPath: "folder-default",
            VideoPath: "video-default.mp4",
            ProtocolPath: "protocol-default.pdf",
            InspectionDate: inspectionDate);

        var request = TrainingCenterScanRequestFactory.CreateWithDefaults(
            new TrainingCenterScanDefaultRequestFactoryRequest(
                GetIsBusy: () => false,
                SetIsBusy: _ => { },
                RootFolders: [],
                ScanInputsAsync: (_, _, _) => Task.FromResult(new List<TrainingCaseInput> { input }),
                ReplaceCases: _ => { },
                AppendCases: _ => { },
                SetStatusText: _ => { },
                SaveStateAsync: () => Task.CompletedTask,
                ResetCancellation: () => CancellationToken.None,
                Log: _ => { }));

        Assert.True(request.DirectoryExists(AppContext.BaseDirectory));
        Assert.False(request.DirectoryExists(Path.Combine(AppContext.BaseDirectory, Guid.NewGuid().ToString("N"))));

        var cases = await request.ScanFolderAsync(AppContext.BaseDirectory, [], CancellationToken.None);

        var item = Assert.Single(cases);
        Assert.Equal("case-default", item.CaseId);
        Assert.Equal("folder-default", item.FolderPath);
        Assert.Equal("video-default.mp4", item.VideoPath);
        Assert.Equal("protocol-default.pdf", item.ProtocolPath);
        Assert.Equal(inspectionDate, item.InspectionDate);
        Assert.Equal(TrainingCaseStatus.New, item.Status);
    }
}
