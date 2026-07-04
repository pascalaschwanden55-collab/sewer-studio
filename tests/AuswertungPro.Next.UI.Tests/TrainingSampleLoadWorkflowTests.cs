using System.Collections.ObjectModel;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingSampleLoadWorkflowTests
{
    [Fact]
    public async Task RunAsync_laed_samples_und_ersetzt_collection_auf_ui_thread()
    {
        var existing = Sample("old");
        var first = Sample("first");
        var second = Sample("second");
        var samples = new ObservableCollection<TrainingSample> { existing };
        var calls = new List<string>();

        await TrainingSampleLoadWorkflow.RunAsync(
            new TrainingSampleLoadWorkflowRequest(
                samples,
                () =>
                {
                    calls.Add("load");
                    return Task.FromResult(new List<TrainingSample> { first, second });
                },
                action =>
                {
                    calls.Add("ui-before");
                    action();
                    calls.Add("ui-after");
                }));

        Assert.Equal(["load", "ui-before", "ui-after"], calls);
        Assert.Equal([first, second], samples);
    }

    private static TrainingSample Sample(string id)
        => new()
        {
            SampleId = id,
            CaseId = "case-" + id,
            Code = "BAB",
            Status = TrainingSampleStatus.New
        };
}
