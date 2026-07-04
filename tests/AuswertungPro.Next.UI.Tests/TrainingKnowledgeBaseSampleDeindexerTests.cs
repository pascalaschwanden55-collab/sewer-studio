using System.Net.Http;
using AuswertungPro.Next.Infrastructure.Ai.Ollama;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingKnowledgeBaseSampleDeindexerTests
{
    [Fact]
    public void TryDeindex_laedt_config_legt_client_an_und_deindexiert_sample()
    {
        var calls = new List<string>();
        HttpClient? cached = null;

        TrainingKnowledgeBaseSampleDeindexer.TryDeindex(
            new TrainingKnowledgeBaseSampleDeindexRequest(
                SampleId: "sample-1",
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
                DeindexSample: (_, _, sampleId) => calls.Add($"deindex:{sampleId}")));

        Assert.NotNull(cached);
        Assert.Equal(["load-config", "set-client:7", "deindex:sample-1"], calls);
    }

    [Fact]
    public void TryDeindex_verwendet_cached_client_weiter()
    {
        using var cached = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        var calls = new List<string>();

        TrainingKnowledgeBaseSampleDeindexer.TryDeindex(
            new TrainingKnowledgeBaseSampleDeindexRequest(
                SampleId: "sample-1",
                LoadConfig: Config,
                GetCachedHttpClient: () => cached,
                SetCachedHttpClient: _ => calls.Add("set-client"),
                DeindexSample: (client, _, sampleId) =>
                    calls.Add($"deindex:{ReferenceEquals(cached, client)}:{sampleId}")));

        Assert.Equal(["deindex:True:sample-1"], calls);
    }

    [Fact]
    public void TryDeindex_schluckt_deindex_fehler()
    {
        var calls = new List<string>();

        TrainingKnowledgeBaseSampleDeindexer.TryDeindex(
            new TrainingKnowledgeBaseSampleDeindexRequest(
                SampleId: "sample-1",
                LoadConfig: Config,
                GetCachedHttpClient: () => null,
                SetCachedHttpClient: _ => calls.Add("set-client"),
                DeindexSample: (_, _, _) => throw new InvalidOperationException("kaputt")));

        Assert.Equal(["set-client"], calls);
    }

    private static OllamaConfig Config()
        => new(
            new Uri("http://localhost:11434"),
            "vision",
            "text",
            "embed",
            TimeSpan.FromSeconds(7));
}
