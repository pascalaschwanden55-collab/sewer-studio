using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingCenterSaveRequestFactoryTests
{
    [Fact]
    public void CreateWithDefaults_baut_state_mit_cases_rootfolders_und_aktueller_zeit()
    {
        var cases = new List<TrainingCase> { new() { CaseId = "case-default" } };
        var roots = new List<string> { "root-default" };
        var before = DateTime.UtcNow;

        var request = TrainingCenterSaveRequestFactory.CreateWithDefaults(
            new TrainingCenterSaveDefaultRequestFactoryRequest(
                GetIsBusy: () => false,
                SetIsBusy: _ => { },
                Cases: cases,
                RootFolders: roots,
                SaveStateAsync: _ => Task.CompletedTask,
                SetStatusText: _ => { }));

        var state = request.BuildState();
        var after = DateTime.UtcNow;

        Assert.Equal(["case-default"], state.Cases.Select(item => item.CaseId));
        Assert.Equal(["root-default"], state.RootFolders);
        Assert.InRange(state.UpdatedUtc, before, after);
    }

    [Fact]
    public async Task Create_verdrahtet_save_workflow_request()
    {
        var state = new TrainingCenterState
        {
            Cases = [new TrainingCase { CaseId = "case-1" }],
            RootFolders = ["root-1"]
        };
        var calls = new List<string>();
        TrainingCenterState? saved = null;

        var request = TrainingCenterSaveRequestFactory.Create(
            new TrainingCenterSaveRequestFactoryRequest(
                GetIsBusy: () => false,
                SetIsBusy: value => calls.Add("busy:" + value),
                BuildState: () => state,
                SaveStateAsync: value =>
                {
                    saved = value;
                    calls.Add("save:" + value.Cases.Count);
                    return Task.CompletedTask;
                },
                SetStatusText: value => calls.Add("status:" + value)));

        Assert.False(request.GetIsBusy());
        request.SetIsBusy(true);
        Assert.Same(state, request.BuildState());
        await request.SaveStateAsync(state);
        request.SetStatusText("ok");

        Assert.Same(state, saved);
        Assert.Equal(["busy:True", "save:1", "status:ok"], calls);
    }
}
