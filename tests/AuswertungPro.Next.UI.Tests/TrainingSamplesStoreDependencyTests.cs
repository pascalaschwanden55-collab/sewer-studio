using System.IO;
using System.Reflection;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingSamplesStoreDependencyTests
{
    [Fact]
    public void ServiceProvider_registriert_den_projektbezogenen_Trainingsspeicher()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var services = new ServiceProvider(
            new AppSettings { EnableRestorePoints = false },
            new Application.Diagnostics.DiagnosticsOptions(),
            loggerFactory.CreateLogger("test"),
            loggerFactory);

        Assert.Same(
            services.TrainingSamples,
            services.GetService(typeof(ITrainingSampleStore)));
    }

    [Fact]
    public void ServiceProvider_setzt_den_Eval_Schutz_auch_auf_der_Kompatibilitaets_Fassade()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "sewer-eval-protection-test-" + Guid.NewGuid().ToString("N"));
        var previousRoot = Path.Combine(root, "vorher");
        var configuredRoot = Path.Combine(root, "eval-set");
        TrainingSamplesStore.ConfigureEvalProtection(previousRoot);

        try
        {
            using var loggerFactory = LoggerFactory.Create(_ => { });
            _ = new ServiceProvider(
                new AppSettings
                {
                    EnableRestorePoints = false,
                    EvalSetRoot = configuredRoot
                },
                new Application.Diagnostics.DiagnosticsOptions(),
                loggerFactory.CreateLogger("test"),
                loggerFactory);

            Assert.Equal(
                Path.GetFullPath(configuredRoot),
                TrainingSamplesStore.EffectiveEvalSetRoot);
        }
        finally
        {
            TrainingSamplesStore.ConfigureEvalProtection(null);
        }
    }

    [Fact]
    public void Statische_Trainingssample_Fassade_ist_unveraenderbar()
    {
        var before = TrainingSamplesStore.Current;
        var replacement = new TrainingSampleFileStore(Path.Combine(
            Path.GetTempPath(),
            "sewer-training-store-test-" + Guid.NewGuid().ToString("N"),
            "training_samples.json"));
        var use = typeof(TrainingSamplesStore).GetMethod(nameof(TrainingSamplesStore.Use));

        var error = Assert.Throws<TargetInvocationException>(
            () => use!.Invoke(null, [replacement]));

        Assert.IsType<NotSupportedException>(error.InnerException);
        Assert.Same(before, TrainingSamplesStore.Current);
    }

    [Fact]
    public async Task Sample_Fabrik_nutzt_den_uebergebenen_Trainingsspeicher()
    {
        var store = new RecordingTrainingSampleStore();
        var request = TrainingCenterSampleGenerationRequestFactory.CreateWithDefaults(
            new TrainingCenterSampleGenerationDefaultRequestFactoryRequest(
                SelectedCase: null,
                GetIsBusy: () => false,
                SetIsBusy: _ => { },
                ResetCancellation: () => CancellationToken.None,
                CodeCatalog: null,
                AppendSamples: _ => { },
                SetStatusText: _ => { }),
            trainingSamples: store);

        var loaded = await request.LoadSamplesAsync();
        await request.SaveSamplesAsync([new TrainingSample { SampleId = "neu" }]);

        Assert.Equal("vorhanden", Assert.Single(loaded).SampleId);
        Assert.Equal(1, store.LoadCalls);
        Assert.Equal(1, store.MergeAndSaveCalls);
    }

    [Fact]
    public void Codiermodus_und_Training_reichen_den_registrierten_Speicher_weiter()
    {
        var root = TestRepoPaths.FindRepoRoot();
        var serviceProvider = Read(root, "src", "AuswertungPro.Next.UI", "ServiceProvider.cs");
        var player = Read(root, "src", "AuswertungPro.Next.UI", "Views", "Windows", "PlayerWindow.xaml.cs");
        var training = Read(root, "src", "AuswertungPro.Next.UI", "ViewModels", "Windows", "TrainingCenterViewModel.cs");
        var codingSession = Read(root, "src", "AuswertungPro.Next.Infrastructure", "Ai", "CodingSessionService.cs");
        var selfTraining = Read(root, "src", "AuswertungPro.Next.Infrastructure", "Ai", "Training", "SelfTrainingOrchestrator.cs");

        Assert.DoesNotContain("TrainingSamplesStore.Use", serviceProvider);
        Assert.Contains("_protocolContext.TrainingSamples", player);
        Assert.Contains("trainingSamples: _trainingSamples", training);
        Assert.Contains("_trainingSamples.MergeAndSaveAsync", codingSession);
        Assert.Contains("_trainingSamples.MergeOrUpdateAsync", codingSession);
        Assert.Contains("_trainingSamples.MergeAndSaveAsync", selfTraining);
        Assert.DoesNotContain("TrainingSamplesStore.Merge", codingSession);
        Assert.DoesNotContain("TrainingSamplesStore.Merge", selfTraining);
    }

    private static string Read(string root, params string[] parts)
        => File.ReadAllText(Path.Combine([root, .. parts]));

    private sealed class RecordingTrainingSampleStore : ITrainingSampleStore
    {
        public int LoadCalls { get; private set; }
        public int MergeAndSaveCalls { get; private set; }

        public Task<List<TrainingSample>> LoadAsync()
        {
            LoadCalls++;
            return Task.FromResult(new List<TrainingSample>
            {
                new() { SampleId = "vorhanden" }
            });
        }

        public Task SaveAsync(List<TrainingSample> samples) => Task.CompletedTask;

        public Task MergeOrUpdateAsync(IEnumerable<TrainingSample> samples) => Task.CompletedTask;

        public Task MergeAndSaveAsync(List<TrainingSample> samples)
        {
            MergeAndSaveCalls++;
            return Task.CompletedTask;
        }

        public Task<bool> RemoveBySampleIdAsync(string sampleId) => Task.FromResult(false);
        public Task<bool> ReplaceBySampleIdAsync(TrainingSample sample) => Task.FromResult(false);
        public Task<bool> TryAddNewAsync(TrainingSample sample, CancellationToken ct = default) => Task.FromResult(true);
    }
}
