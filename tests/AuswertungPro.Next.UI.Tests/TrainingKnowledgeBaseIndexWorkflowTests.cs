using System.Net.Http;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingKnowledgeBaseIndexWorkflowTests
{
    [Fact]
    public async Task RunAsync_laedt_config_legt_client_an_und_startet_indexlauf()
    {
        var calls = new List<string>();
        HttpClient? cached = null;

        try
        {
            var outcome = await TrainingKnowledgeBaseIndexWorkflow.RunAsync(
                new TrainingKnowledgeBaseIndexWorkflowRequest(
                    Samples: [Sample("s1")],
                    CancellationToken: CancellationToken.None,
                    LoadConfig: () =>
                    {
                        calls.Add("load-config");
                        return Config();
                    },
                    GetCachedHttpClient: () => cached,
                    SetCachedHttpClient: client =>
                    {
                        cached = client;
                        calls.Add($"set-client:{client.Timeout.TotalSeconds:0}");
                    },
                    CreateIndexRunAsync: (config, client) =>
                    {
                        calls.Add($"create-run:{config.BaseUri.Port}:{ReferenceEquals(cached, client)}");
                        return (samples, ct) =>
                        {
                            calls.Add($"run:{samples.Count}:{ct.CanBeCanceled}");
                            return Task.FromResult(new KbIndexOutcome([samples[0].SampleId], []));
                        };
                    }));

            Assert.NotNull(cached);
            Assert.Equal(["s1"], outcome.IndexedIds);
            Assert.Equal(
                ["load-config", "set-client:7", "create-run:11434:True", "run:1:False"],
                calls);
        }
        finally
        {
            cached?.Dispose();
        }
    }

    [Fact]
    public async Task RunAsync_verwendet_cached_client_weiter()
    {
        using var cached = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        var calls = new List<string>();

        var outcome = await TrainingKnowledgeBaseIndexWorkflow.RunAsync(
            new TrainingKnowledgeBaseIndexWorkflowRequest(
                Samples: [Sample("s1")],
                CancellationToken: CancellationToken.None,
                LoadConfig: Config,
                GetCachedHttpClient: () => cached,
                SetCachedHttpClient: _ => calls.Add("set-client"),
                CreateIndexRunAsync: (_, client) =>
                {
                    calls.Add($"create-run:{ReferenceEquals(cached, client)}:{client.Timeout.TotalSeconds:0}");
                    return (_, _) => Task.FromResult(new KbIndexOutcome(["indexed"], []));
                }));

        Assert.Equal(["indexed"], outcome.IndexedIds);
        Assert.Equal(["create-run:True:3"], calls);
    }

    private static TrainingSample Sample(string sampleId)
        => new()
        {
            SampleId = sampleId,
            Beschreibung = "Beschreibung fuer KB"
        };

    private static OllamaConfig Config()
        => new(
            new Uri("http://localhost:11434"),
            "vision",
            "text",
            "embed",
            TimeSpan.FromSeconds(7));
}
