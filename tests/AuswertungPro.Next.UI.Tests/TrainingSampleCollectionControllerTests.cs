using System.Collections.ObjectModel;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingSampleCollectionControllerTests
{
    [Fact]
    public void ReplaceWith_ersetzt_samples_und_haelt_reihenfolge()
    {
        var oldSample = Sample("old");
        var first = Sample("one");
        var second = Sample("two");
        var samples = new ObservableCollection<TrainingSample> { oldSample };

        TrainingSampleCollectionController.ReplaceWith(samples, [first, second]);

        Assert.Equal([first, second], samples);
    }

    [Fact]
    public void Append_fuegt_samples_hinten_an()
    {
        var oldSample = Sample("old");
        var next = Sample("next");
        var samples = new ObservableCollection<TrainingSample> { oldSample };

        TrainingSampleCollectionController.Append(samples, [next]);

        Assert.Equal([oldSample, next], samples);
    }

    [Fact]
    public void ReplaceOnUi_dispatches_replace()
    {
        var oldSample = Sample("old");
        var next = Sample("next");
        var samples = new ObservableCollection<TrainingSample> { oldSample };
        var dispatchCount = 0;

        TrainingSampleCollectionController.ReplaceOnUi(
            samples,
            [next],
            action =>
            {
                dispatchCount++;
                action();
            });

        Assert.Equal(1, dispatchCount);
        Assert.Equal([next], samples);
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
