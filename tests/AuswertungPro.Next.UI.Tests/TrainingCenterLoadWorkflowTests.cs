using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingCenterLoadWorkflowTests
{
    [Fact]
    public async Task RunAsync_laesst_state_cases_rootfolders_und_refreshes_laden()
    {
        var calls = new List<string>();
        var cases = new List<TrainingCase>();
        var rootFolders = new List<string> { @"C:\Alt" };
        var state = new TrainingCenterState
        {
            Cases =
            [
                new TrainingCase { CaseId = "H1" },
                new TrainingCase { CaseId = "H2" }
            ],
            RootFolders = [@"C:\Neu", @"C:\Fehlt"]
        };

        await TrainingCenterLoadWorkflow.RunAsync(
            new TrainingCenterLoadWorkflowRequest(
                LoadStateAsync: () =>
                {
                    calls.Add("load-state");
                    return Task.FromResult(state);
                },
                RootFolders: rootFolders,
                DirectoryExists: folder => folder == @"C:\Neu",
                ReplaceCases: items =>
                {
                    calls.Add($"replace-cases:{items.Count}");
                    cases.Clear();
                    cases.AddRange(items);
                },
                UpdateRootFolderDisplay: () => calls.Add("update-roots"),
                SetStatusText: value => calls.Add($"status:{value}"),
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
                    calls.Add("load-match-rate");
                    return Task.CompletedTask;
                }));

        Assert.Equal(["H1", "H2"], cases.Select(c => c.CaseId));
        Assert.Equal([@"C:\Neu"], rootFolders);
        Assert.Equal(
            [
                "load-state",
                "replace-cases:2",
                "update-roots",
                "status:Geladen: 2 Fälle",
                "load-samples",
                "refresh-kb",
                "load-match-rate"
            ],
            calls);
    }

    [Fact]
    public async Task RunAsync_aktualisiert_rootfolder_display_nicht_ohne_existierende_rootfolders()
    {
        var calls = new List<string>();
        var rootFolders = new List<string> { @"C:\Alt" };
        var state = new TrainingCenterState
        {
            Cases = [new TrainingCase { CaseId = "H1" }],
            RootFolders = [@"C:\Fehlt"]
        };

        await TrainingCenterLoadWorkflow.RunAsync(
            new TrainingCenterLoadWorkflowRequest(
                LoadStateAsync: () => Task.FromResult(state),
                RootFolders: rootFolders,
                DirectoryExists: _ => false,
                ReplaceCases: _ => { },
                UpdateRootFolderDisplay: () => calls.Add("update-roots"),
                SetStatusText: value => calls.Add($"status:{value}"),
                LoadSamplesAsync: () => Task.CompletedTask,
                RefreshKbStatusAsync: () => Task.CompletedTask,
                LoadLastMatchRateAsync: () => Task.CompletedTask));

        Assert.Equal([@"C:\Alt"], rootFolders);
        Assert.DoesNotContain("update-roots", calls);
        Assert.Equal(["status:Geladen: 1 Fälle"], calls);
    }
}
