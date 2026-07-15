using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingKnowledgeBaseQualityPresentationBuilderTests
{
    [Fact]
    public void Build_formats_last_five_runs_and_upward_direction_like_view_model()
    {
        var runs = new[]
        {
            Run(1, exact: 0.10),
            Run(2, exact: 0.20),
            Run(3, exact: 0.30),
            Run(4, exact: 0.40),
            Run(5, exact: 0.50),
            Run(6, exact: 0.54)
        };

        var result = TrainingKnowledgeBaseQualityPresentationBuilder.Build(Quality(), runs);

        var expectedTrendText = string.Join(
            "\n",
            runs.Skip(1).Select(r =>
                $"{r.TimestampUtc.ToLocalTime():dd.MM. HH:mm} \u2014 " +
                $"Exact: {r.ExactPercent:P0} | Partial: {r.PartialPercent:P0} | " +
                $"Miss: {r.MismatchPercent:P0} | Leer: {r.NoFindingsPercent:P0}"));
        Assert.Equal(expectedTrendText, result.TrendText);
        Assert.Equal("\uE70E", result.TrendDirection);
    }

    [Theory]
    [InlineData(0.52, 0.50, "\uE72A")]
    [InlineData(0.47, 0.50, "\uE70D")]
    public void Build_keeps_existing_trend_direction_threshold(double latestExact, double previousExact, string expected)
    {
        var runs = new[]
        {
            Run(1, exact: previousExact),
            Run(2, exact: latestExact)
        };

        var result = TrainingKnowledgeBaseQualityPresentationBuilder.Build(Quality(), runs);

        Assert.Equal(expected, result.TrendDirection);
    }

    [Fact]
    public void Build_uses_empty_history_text_and_stale_sample_log()
    {
        var quality = Quality(staleSampleCount: 3);

        var result = TrainingKnowledgeBaseQualityPresentationBuilder.Build(quality, []);

        Assert.Equal("Noch keine Selbsttraining-Laeufe", result.TrendText);
        Assert.Equal("", result.TrendDirection);
        Assert.Equal(["KB-Qualitaet: 3 veraltete Samples erkannt (manuell pruefen im Tab 'Samples')"], result.LogLines);
    }

    private static KnowledgeBaseQualityReport Quality(int staleSampleCount = 0)
        => new(
            CoverageGapsText: "BAA fehlt",
            CoverageGapsCount: 1,
            AccuracyText: "80%",
            StaleSampleCount: staleSampleCount);

    private static SelfTrainingRunSnapshot Run(int day, double exact)
        => new(
            new DateTime(2026, 1, day, 12, 30, 0, DateTimeKind.Utc),
            $"case-{day}",
            TotalEntries: 10,
            ExactPercent: exact,
            PartialPercent: 0.20,
            MismatchPercent: 0.10,
            NoFindingsPercent: 0.05);
}
