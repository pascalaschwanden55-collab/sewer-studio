using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingKnowledgeBaseRefreshWorkflowTests
{
    [Fact]
    public async Task StatusWorkflow_liest_status_wendet_presentation_an_und_aktualisiert_quality()
    {
        var calls = new List<string>();

        await TrainingKnowledgeBaseStatusRefreshWorkflow.RunAsync(
            new TrainingKnowledgeBaseStatusRefreshWorkflowRequest(
                ReadStatusAsync: topCodes =>
                {
                    calls.Add($"read-status:{topCodes}");
                    return Task.FromResult(Status(sampleCount: 26));
                },
                ApplyPresentation: presentation => calls.Add($"apply-status:{presentation.SampleCount}:{presentation.ReadinessLabel}"),
                RefreshQualityAsync: () =>
                {
                    calls.Add("refresh-quality");
                    return Task.CompletedTask;
                },
                OnUi: action =>
                {
                    calls.Add("on-ui");
                    action();
                }));

        Assert.Equal(
            [
                "read-status:20",
                "on-ui",
                "apply-status:26:Lernbasis grundlegend",
                "refresh-quality"
            ],
            calls);
    }

    [Fact]
    public async Task StatusWorkflow_ignoriert_fehler_und_aktualisiert_quality_nicht()
    {
        var calls = new List<string>();

        await TrainingKnowledgeBaseStatusRefreshWorkflow.RunAsync(
            new TrainingKnowledgeBaseStatusRefreshWorkflowRequest(
                ReadStatusAsync: _ => throw new InvalidOperationException("kaputt"),
                ApplyPresentation: _ => calls.Add("apply-status"),
                RefreshQualityAsync: () =>
                {
                    calls.Add("refresh-quality");
                    return Task.CompletedTask;
                },
                OnUi: action =>
                {
                    calls.Add("on-ui");
                    action();
                }));

        Assert.Empty(calls);
    }

    [Fact]
    public async Task QualityWorkflow_liest_quality_und_history_wendet_presentation_an_und_loggt_warnungen()
    {
        var calls = new List<string>();

        await TrainingKnowledgeBaseQualityRefreshWorkflow.RunAsync(
            new TrainingKnowledgeBaseQualityRefreshWorkflowRequest(
                ReadQualityAsync: () =>
                {
                    calls.Add("read-quality");
                    return Task.FromResult(Quality(stale: 2));
                },
                LoadRunsAsync: () =>
                {
                    calls.Add("load-runs");
                    return Task.FromResult(new List<SelfTrainingRunSnapshot>());
                },
                ApplyPresentation: presentation => calls.Add($"apply-quality:{presentation.StaleSampleCount}:{presentation.TrendDirection}"),
                Log: value => calls.Add($"log:{value}"),
                OnUi: action =>
                {
                    calls.Add("on-ui");
                    action();
                }));

        Assert.Equal("read-quality", calls[0]);
        Assert.Equal("load-runs", calls[1]);
        Assert.Contains("apply-quality:2:", calls);
        Assert.Contains("log:KB-Qualitaet: 2 veraltete Samples erkannt (manuell pruefen im Tab 'Samples')", calls);
    }

    [Fact]
    public async Task QualityWorkflow_ignoriert_fehler()
    {
        var calls = new List<string>();

        await TrainingKnowledgeBaseQualityRefreshWorkflow.RunAsync(
            new TrainingKnowledgeBaseQualityRefreshWorkflowRequest(
                ReadQualityAsync: () => throw new InvalidOperationException("kaputt"),
                LoadRunsAsync: () =>
                {
                    calls.Add("load-runs");
                    return Task.FromResult(new List<SelfTrainingRunSnapshot>());
                },
                ApplyPresentation: _ => calls.Add("apply-quality"),
                Log: value => calls.Add($"log:{value}"),
                OnUi: action =>
                {
                    calls.Add("on-ui");
                    action();
                }));

        Assert.Empty(calls);
    }

    private static KnowledgeBaseStatusReport Status(int sampleCount)
        => new(
            sampleCount,
            ErrorCount: 1,
            NewCount: 2,
            EmbeddingCount: 3,
            CodesCovered: 4,
            LatestVersionAtUtc: null,
            TopCodes: []);

    private static KnowledgeBaseQualityReport Quality(int stale)
        => new(
            CoverageGapsText: "BAA fehlt",
            CoverageGapsCount: 1,
            AccuracyText: "80%",
            StaleSampleCount: stale);
}
