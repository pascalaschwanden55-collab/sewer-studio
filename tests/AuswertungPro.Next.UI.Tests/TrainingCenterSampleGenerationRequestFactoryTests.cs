using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingCenterSampleGenerationRequestFactoryTests
{
    [Fact]
    public void CreateWithDefaults_verdrahtet_activity_default()
    {
        var request = TrainingCenterSampleGenerationRequestFactory.CreateWithDefaults(
            new TrainingCenterSampleGenerationDefaultRequestFactoryRequest(
                SelectedCase: null,
                GetIsBusy: () => false,
                SetIsBusy: _ => { },
                ResetCancellation: () => CancellationToken.None,
                CodeCatalog: null,
                AppendSamples: _ => { },
                SetStatusText: _ => { }));

        Assert.NotNull(request.BeginActivity);
    }

    [Fact]
    public async Task Create_uebernimmt_ui_zustand_und_runtime_delegates()
    {
        var state = new FactoryState();
        var selected = new TrainingCase { CaseId = "case-1" };
        var generatedSample = new TrainingSample { SampleId = "sample-1" };
        var loadedSample = new TrainingSample { SampleId = "existing-1", Signature = "sig-1" };
        var calls = new List<string>();
        var token = new CancellationToken(canceled: false);
        var activity = new DisposableProbe();

        var request = TrainingCenterSampleGenerationRequestFactory.Create(
            new TrainingCenterSampleGenerationRequestFactoryRequest(
                SelectedCase: selected,
                GetIsBusy: () => state.IsBusy,
                SetIsBusy: value =>
                {
                    calls.Add($"busy:{value}");
                    state.IsBusy = value;
                },
                ResetCancellation: () =>
                {
                    calls.Add("reset");
                    return token;
                },
                BeginActivity: () =>
                {
                    calls.Add("activity");
                    return activity;
                },
                CodeCatalog: null,
                AppendSamples: samples => calls.Add($"append:{samples.Count}"),
                SetStatusText: value => calls.Add($"status:{value}")),
            LoadSamplesAsync: () =>
            {
                calls.Add("load");
                return Task.FromResult(new List<TrainingSample> { loadedSample });
            },
            GenerateWithDiagnosticsAsync: (_, signatures, actualToken) =>
            {
                calls.Add($"generate:{signatures.Count}:{actualToken == token}");
                return Task.FromResult(new TrainingSampleGenerationResult(
                    [generatedSample],
                    ParsedEntries: 1,
                    DuplicateSkipped: 0,
                    TrainingSampleGenerationOutcome.Success));
            },
            SaveSamplesAsync: samples =>
            {
                calls.Add($"save:{samples.Count}");
                return Task.CompletedTask;
            });

        Assert.Same(selected, request.SelectedCase);
        Assert.False(request.GetIsBusy());
        request.SetIsBusy(true);
        Assert.True(state.IsBusy);
        Assert.Equal(token, request.ResetCancellation());
        Assert.Same(activity, request.BeginActivity());
        var loaded = await request.LoadSamplesAsync();
        var generation = await request.GenerateWithDiagnosticsAsync(
            new TrainingCaseInput("case-1", "", "", "", null),
            new HashSet<string> { "sig-1" },
            token);
        await request.SaveSamplesAsync(generation.Samples);
        request.AppendSamples(generation.Samples);
        request.SetStatusText("fertig");

        Assert.Equal([loadedSample], loaded);
        Assert.Equal([generatedSample], generation.Samples);
        Assert.Equal(
            ["busy:True", "reset", "activity", "load", "generate:1:True", "save:1", "append:1", "status:fertig"],
            calls);
    }

    private sealed class FactoryState
    {
        public bool IsBusy { get; set; }
    }

    private sealed class DisposableProbe : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
