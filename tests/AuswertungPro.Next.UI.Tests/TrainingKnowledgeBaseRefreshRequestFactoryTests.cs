using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;
using System.Windows.Media;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingKnowledgeBaseRefreshRequestFactoryTests
{
    [Fact]
    public async Task StatusFactory_verdrahtet_status_refresh_request()
    {
        var calls = new List<string>();
        var report = new KnowledgeBaseStatusReport(
            SampleCount: 7,
            ErrorCount: 0,
            NewCount: 1,
            EmbeddingCount: 2,
            CodesCovered: 3,
            LatestVersionAtUtc: null,
            TopCodes: []);

        var request = TrainingKnowledgeBaseStatusRefreshRequestFactory.Create(
            new TrainingKnowledgeBaseStatusRefreshRequestFactoryRequest(
                ReadStatusAsync: topCodes =>
                {
                    calls.Add("read-status:" + topCodes);
                    return Task.FromResult(report);
                },
                ApplyPresentation: presentation => calls.Add("apply-status:" + presentation.SampleCount),
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

        Assert.Same(report, await request.ReadStatusAsync(20));
        request.OnUi(() => request.ApplyPresentation(new TrainingKnowledgeBaseStatusPresentation(
            SampleCount: 8,
            ErrorCount: 0,
            NewCount: 0,
            EmbeddingCount: 0,
            CodesCovered: 0,
            LastUpdateText: "-",
            ReadinessLabel: "ok",
            ReadinessBrush: Brushes.Green,
            TopCodesText: "-")));
        await request.RefreshQualityAsync();

        Assert.Equal(["read-status:20", "on-ui", "apply-status:8", "refresh-quality"], calls);
    }

    [Fact]
    public async Task QualityFactory_verdrahtet_quality_refresh_request()
    {
        var calls = new List<string>();
        var quality = new KnowledgeBaseQualityReport(
            CoverageGapsText: "keine",
            CoverageGapsCount: 0,
            AccuracyText: "100%",
            StaleSampleCount: 0);
        var runs = new List<SelfTrainingRunSnapshot>();

        var request = TrainingKnowledgeBaseQualityRefreshRequestFactory.Create(
            new TrainingKnowledgeBaseQualityRefreshRequestFactoryRequest(
                ReadQualityAsync: () =>
                {
                    calls.Add("read-quality");
                    return Task.FromResult(quality);
                },
                LoadRunsAsync: () =>
                {
                    calls.Add("load-runs");
                    return Task.FromResult(runs);
                },
                ApplyPresentation: presentation => calls.Add("apply-quality:" + presentation.AccuracyText),
                Log: value => calls.Add("log:" + value),
                OnUi: action =>
                {
                    calls.Add("on-ui");
                    action();
                }));

        Assert.Same(quality, await request.ReadQualityAsync());
        Assert.Same(runs, await request.LoadRunsAsync());
        request.OnUi(() => request.ApplyPresentation(new TrainingKnowledgeBaseQualityPresentation(
            CoverageGapsText: "keine",
            CoverageGapsCount: 0,
            AccuracyText: "99%",
            StaleSampleCount: 0,
            TrendText: "stabil",
            TrendDirection: "",
            LogLines: [])));
        request.Log("warnung");

        Assert.Equal(["read-quality", "load-runs", "on-ui", "apply-quality:99%", "log:warnung"], calls);
    }

    [Fact]
    public async Task QualityFactory_CreateWithDefaults_verdrahtet_history_store_default()
    {
        var calls = new List<string>();
        var quality = new KnowledgeBaseQualityReport(
            CoverageGapsText: "keine",
            CoverageGapsCount: 0,
            AccuracyText: "100%",
            StaleSampleCount: 0);

        var request = TrainingKnowledgeBaseQualityRefreshRequestFactory.CreateWithDefaults(
            new TrainingKnowledgeBaseQualityRefreshDefaultRequestFactoryRequest(
                ReadQualityAsync: () =>
                {
                    calls.Add("read-quality");
                    return Task.FromResult(quality);
                },
                ApplyPresentation: presentation => calls.Add("apply-quality:" + presentation.AccuracyText),
                Log: value => calls.Add("log:" + value),
                OnUi: action =>
                {
                    calls.Add("on-ui");
                    action();
                }));

        Assert.Same(quality, await request.ReadQualityAsync());
        Assert.NotNull(request.LoadRunsAsync);
        request.OnUi(() => request.ApplyPresentation(new TrainingKnowledgeBaseQualityPresentation(
            CoverageGapsText: "keine",
            CoverageGapsCount: 0,
            AccuracyText: "98%",
            StaleSampleCount: 0,
            TrendText: "stabil",
            TrendDirection: "",
            LogLines: [])));
        request.Log("warnung");

        Assert.Equal(["read-quality", "on-ui", "apply-quality:98%", "log:warnung"], calls);
    }
}
