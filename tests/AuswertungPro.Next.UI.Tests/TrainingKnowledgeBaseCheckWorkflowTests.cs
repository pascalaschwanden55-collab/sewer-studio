using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingKnowledgeBaseCheckWorkflowTests
{
    [Fact]
    public async Task RunAsync_laesst_busy_unveraendert_wenn_bereits_busy()
    {
        var calls = new List<string>();

        await TrainingKnowledgeBaseCheckWorkflow.RunAsync(
            new TrainingKnowledgeBaseCheckWorkflowRequest(
                IsBusy: true,
                SetBusy: value => calls.Add($"busy:{value}"),
                SetStatus: value => calls.Add($"status:{value}"),
                ReadSummaryAsync: _ =>
                {
                    calls.Add("read-summary");
                    return Task.FromResult(Summary());
                },
                RefreshKbStatusAsync: () =>
                {
                    calls.Add("refresh");
                    return Task.CompletedTask;
                },
                Log: value => calls.Add($"log:{value}"),
                CancellationToken.None));

        Assert.Empty(calls);
    }

    [Fact]
    public async Task RunAsync_liest_summary_loggt_und_refreshes_status()
    {
        var calls = new List<string>();

        await TrainingKnowledgeBaseCheckWorkflow.RunAsync(
            new TrainingKnowledgeBaseCheckWorkflowRequest(
                IsBusy: false,
                SetBusy: value => calls.Add($"busy:{value}"),
                SetStatus: value => calls.Add($"status:{value}"),
                ReadSummaryAsync: topCodes =>
                {
                    calls.Add($"read-summary:{topCodes}");
                    return Task.FromResult(Summary());
                },
                RefreshKbStatusAsync: () =>
                {
                    calls.Add("refresh");
                    return Task.CompletedTask;
                },
                Log: value => calls.Add($"log:{value}"),
                CancellationToken.None));

        Assert.Equal("busy:True", calls[0]);
        Assert.Equal("status:Prüfe Knowledge Base...", calls[1]);
        Assert.Contains("read-summary:12", calls);
        Assert.Contains("log:KB-Stand: Samples=3, Embeddings=2, Versionen=1", calls);
        Assert.Contains("status:KB geprüft: 3 Samples, 2 Embeddings, 1 Versionen.", calls);
        Assert.Contains("refresh", calls);
        Assert.Equal("busy:False", calls[^1]);
    }

    [Fact]
    public async Task RunAsync_loggt_fehler_und_finalisiert_busy()
    {
        var calls = new List<string>();

        await TrainingKnowledgeBaseCheckWorkflow.RunAsync(
            new TrainingKnowledgeBaseCheckWorkflowRequest(
                IsBusy: false,
                SetBusy: value => calls.Add($"busy:{value}"),
                SetStatus: value => calls.Add($"status:{value}"),
                ReadSummaryAsync: _ => throw new InvalidOperationException("kaputt"),
                RefreshKbStatusAsync: () =>
                {
                    calls.Add("refresh");
                    return Task.CompletedTask;
                },
                Log: value => calls.Add($"log:{value}"),
                CancellationToken.None));

        Assert.Contains("status:KB-Prüfung fehlgeschlagen: kaputt", calls);
        Assert.Contains("log:KB-Prüfung FEHLER: kaputt", calls);
        Assert.DoesNotContain("refresh", calls);
        Assert.Equal("busy:False", calls[^1]);
    }

    private static KnowledgeBaseDiagnosticsSummary Summary()
        => new(
            SampleCount: 3,
            EmbeddingCount: 2,
            VersionCount: 1,
            LatestVersionAtUtc: null,
            LatestVersionSampleCount: 0,
            LatestVersionNotes: "",
            TopCodes: []);
}
