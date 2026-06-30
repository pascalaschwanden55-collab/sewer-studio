using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingKnowledgeBaseStatusPresentationBuilderTests
{
    [Theory]
    [InlineData(100, "KI-Modell einsatzbereit", 0x4A, 0xDE, 0x80)]
    [InlineData(25, "Lernbasis grundlegend", 0xFA, 0xCC, 0x15)]
    [InlineData(1, "Lernbasis unzureichend", 0xF8, 0x71, 0x71)]
    [InlineData(0, "Keine Trainingsdaten", 0x94, 0xA3, 0xB8)]
    public void Build_keeps_existing_readiness_labels_and_colors(
        int sampleCount,
        string expectedLabel,
        byte expectedRed,
        byte expectedGreen,
        byte expectedBlue)
    {
        var status = Status(sampleCount, latestVersionAtUtc: null);

        var result = TrainingKnowledgeBaseStatusPresentationBuilder.Build(status);

        Assert.Equal(expectedLabel, result.ReadinessLabel);
        Assert.Equal(expectedRed, result.ReadinessBrush.Color.R);
        Assert.Equal(expectedGreen, result.ReadinessBrush.Color.G);
        Assert.Equal(expectedBlue, result.ReadinessBrush.Color.B);
    }

    [Fact]
    public void Build_maps_counts_last_update_and_top_codes_like_view_model()
    {
        var latest = new DateTimeOffset(2026, 6, 30, 8, 15, 0, TimeSpan.Zero);
        var status = new KnowledgeBaseStatusReport(
            SampleCount: 42,
            ErrorCount: 3,
            NewCount: 5,
            EmbeddingCount: 40,
            CodesCovered: 7,
            LatestVersionAtUtc: latest,
            TopCodes:
            [
                new KnowledgeBaseDiagnosticsCodeCount("BAA", 4),
                new KnowledgeBaseDiagnosticsCodeCount("BAB", 2)
            ]);

        var result = TrainingKnowledgeBaseStatusPresentationBuilder.Build(status);

        Assert.Equal(42, result.SampleCount);
        Assert.Equal(3, result.ErrorCount);
        Assert.Equal(5, result.NewCount);
        Assert.Equal(40, result.EmbeddingCount);
        Assert.Equal(7, result.CodesCovered);
        Assert.Equal(latest.ToLocalTime().ToString("dd.MM.yyyy HH:mm"), result.LastUpdateText);
        Assert.Equal("BAA: 4 Samples\nBAB: 2 Samples", result.TopCodesText);
    }

    [Fact]
    public void Build_uses_dash_when_latest_version_is_missing()
    {
        var result = TrainingKnowledgeBaseStatusPresentationBuilder.Build(Status(0, latestVersionAtUtc: null));

        Assert.Equal("\u2014", result.LastUpdateText);
    }

    private static KnowledgeBaseStatusReport Status(int sampleCount, DateTimeOffset? latestVersionAtUtc)
        => new(
            SampleCount: sampleCount,
            ErrorCount: 0,
            NewCount: 0,
            EmbeddingCount: 0,
            CodesCovered: 0,
            LatestVersionAtUtc: latestVersionAtUtc,
            TopCodes: []);
}
