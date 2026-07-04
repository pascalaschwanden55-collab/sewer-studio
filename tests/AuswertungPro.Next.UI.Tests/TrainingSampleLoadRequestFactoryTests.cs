using System.Collections.ObjectModel;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingSampleLoadRequestFactoryTests
{
    [Fact]
    public async Task Create_uebernimmt_collection_ui_callback_und_loader()
    {
        var samples = new ObservableCollection<TrainingSample>();
        var calls = new List<string>();
        var loadedSample = new TrainingSample { SampleId = "sample-1" };

        var request = TrainingSampleLoadRequestFactory.Create(
            samples,
            action =>
            {
                calls.Add("ui-before");
                action();
                calls.Add("ui-after");
            },
            LoadSamplesAsync: () =>
            {
                calls.Add("load");
                return Task.FromResult(new List<TrainingSample> { loadedSample });
            });

        var loaded = await request.LoadSamplesAsync();
        request.OnUi(() => samples.Add(loaded[0]));

        Assert.Same(samples, request.Samples);
        Assert.Equal([loadedSample], loaded);
        Assert.Equal([loadedSample], samples);
        Assert.Equal(["load", "ui-before", "ui-after"], calls);
    }
}
