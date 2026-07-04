using System.IO;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingCenterLoadRequestFactoryTests
{
    [Fact]
    public async Task Create_verdrahtet_load_workflow_request()
    {
        var calls = new List<string>();
        var state = new TrainingCenterState
        {
            Cases = [new TrainingCase { CaseId = "case-1" }],
            RootFolders = ["root-1"]
        };
        var rootFolders = new List<string>();

        var request = TrainingCenterLoadRequestFactory.Create(
            new TrainingCenterLoadRequestFactoryRequest(
                LoadStateAsync: () =>
                {
                    calls.Add("load-state");
                    return Task.FromResult(state);
                },
                RootFolders: rootFolders,
                DirectoryExists: folder =>
                {
                    calls.Add("exists:" + folder);
                    return true;
                },
                ReplaceCases: items => calls.Add("replace:" + items.Count),
                UpdateRootFolderDisplay: () => calls.Add("update-roots"),
                SetStatusText: value => calls.Add("status:" + value),
                LoadSamplesAsync: () =>
                {
                    calls.Add("load-samples");
                    return Task.CompletedTask;
                },
                RefreshKbStatusAsync: () =>
                {
                    calls.Add("refresh-kb");
                    return Task.CompletedTask;
                },
                LoadLastMatchRateAsync: () =>
                {
                    calls.Add("load-rate");
                    return Task.CompletedTask;
                }));

        Assert.Same(state, await request.LoadStateAsync());
        Assert.Same(rootFolders, request.RootFolders);
        Assert.True(request.DirectoryExists("root-1"));
        request.ReplaceCases(state.Cases);
        request.UpdateRootFolderDisplay();
        request.SetStatusText("ok");
        await request.LoadSamplesAsync();
        await request.RefreshKbStatusAsync();
        await request.LoadLastMatchRateAsync();

        Assert.Equal(
            [
                "load-state",
                "exists:root-1",
                "replace:1",
                "update-roots",
                "status:ok",
                "load-samples",
                "refresh-kb",
                "load-rate"
            ],
            calls);
    }

    [Fact]
    public void CreateWithDefaults_verdrahtet_directory_exists_aus_factory()
    {
        var request = TrainingCenterLoadRequestFactory.CreateWithDefaults(
            new TrainingCenterLoadDefaultRequestFactoryRequest(
                LoadStateAsync: () => Task.FromResult(new TrainingCenterState()),
                RootFolders: new List<string>(),
                ReplaceCases: _ => { },
                UpdateRootFolderDisplay: () => { },
                SetStatusText: _ => { },
                LoadSamplesAsync: () => Task.CompletedTask,
                RefreshKbStatusAsync: () => Task.CompletedTask,
                LoadLastMatchRateAsync: () => Task.CompletedTask));

        Assert.True(request.DirectoryExists(AppContext.BaseDirectory));
        Assert.False(request.DirectoryExists(Path.Combine(AppContext.BaseDirectory, Guid.NewGuid().ToString("N"))));
    }
}
