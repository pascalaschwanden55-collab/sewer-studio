using System.Net.Http;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SelfTrainingRuntimeSetupControllerTests
{
    [Fact]
    public async Task PrepareAsync_laedt_runtime_training_retrieval_session_und_loggt_ollama()
    {
        var runtime = new AiRuntimeSettings(
            Enabled: true,
            OllamaBaseUri: new Uri("http://localhost:11434"),
            VisionModel: "vision-model",
            TextModel: "text-model",
            EmbedModel: "embed-model",
            FfmpegPath: "ffmpeg",
            OllamaRequestTimeout: TimeSpan.FromSeconds(30),
            OllamaKeepAlive: "5m",
            OllamaNumCtx: 2048);
        var retrieval = new OllamaConfig(
            runtime.OllamaBaseUri,
            runtime.VisionModel,
            runtime.TextModel,
            runtime.EmbedModel!,
            TimeSpan.FromSeconds(12));
        var settings = new TrainingCenterSettings { GpuConcurrency = 3 };
        using var client = new HttpClient();
        var orchestrator = new FakeSelfTrainingOrchestrator();
        using var session = new SelfTrainingSession("active-model", orchestrator, Array.Empty<IDisposable>());
        var calls = new List<string>();

        var setup = await SelfTrainingRuntimeSetupController.PrepareAsync(
            loadRuntimeSettings: () =>
            {
                calls.Add("runtime");
                return runtime;
            },
            loadTrainingSettingsAsync: () =>
            {
                calls.Add("training-settings");
                return Task.FromResult(settings);
            },
            loadRetrievalConfig: () =>
            {
                calls.Add("retrieval");
                return retrieval;
            },
            getOrCreateKbHttpClient: config =>
            {
                calls.Add($"client:{config.RequestTimeout.TotalSeconds}");
                return client;
            },
            createSession: (loadedRuntime, loadedRetrieval, loadedClient, loadedSettings) =>
            {
                calls.Add($"session:{loadedRuntime.VisionModel}:{loadedRetrieval.EmbedModel}:{loadedSettings.GpuConcurrency}");
                Assert.Same(client, loadedClient);
                return session;
            },
            log: value => calls.Add($"log:{value}"));

        Assert.Same(runtime, setup.RuntimeSettings);
        Assert.Same(retrieval, setup.RetrievalConfig);
        Assert.Same(settings, setup.TrainingSettings);
        Assert.Same(client, setup.KbHttpClient);
        Assert.Same(session, setup.Session);
        Assert.Equal(
            new[]
            {
                "runtime",
                "log:Ollama: http://localhost:11434/, Modell: vision-model",
                "training-settings",
                "retrieval",
                "client:12",
                "session:vision-model:embed-model:3"
            },
            calls);
    }

    private sealed class FakeSelfTrainingOrchestrator : ISelfTrainingOrchestrator
    {
        public bool IsPaused => false;

        public Task<SelfTrainingResult> RunAsync(
            TrainingCaseInput tc,
            IProgress<SelfTrainingStep> progress,
            CancellationToken ct)
            => throw new NotSupportedException();

        public void Pause()
        {
        }

        public void Resume()
        {
        }
    }
}
