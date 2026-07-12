using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.UI.Ai.Training;
using System.Windows.Media;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingCenterKnowledgeBaseDashboardControllerTests
{
    [Fact]
    public void ApplyStatus_UebernimmtAlleDashboardWerte()
    {
        var state = new DashboardState();
        var controller = CreateController(state);
        var brush = new SolidColorBrush(Colors.LimeGreen);

        controller.ApplyStatus(new TrainingKnowledgeBaseStatusPresentation(
            120, 2, 7, 118, 34, "heute", "bereit", brush, "BAB: 50"));

        Assert.Equal(120, state.SampleCount);
        Assert.Equal(2, state.ErrorCount);
        Assert.Equal(7, state.NewCount);
        Assert.Equal(118, state.EmbeddingCount);
        Assert.Equal(34, state.CodesCovered);
        Assert.Equal("heute", state.LastUpdate);
        Assert.Equal("bereit", state.ReadinessLabel);
        Assert.Same(brush, state.ReadinessBrush);
        Assert.Equal("BAB: 50", state.TopCodesText);
    }

    [Fact]
    public void ApplyQuality_UebernimmtMetrikenUndTrendreihe()
    {
        var state = new DashboardState();
        var controller = CreateController(state);

        controller.ApplyQuality(new TrainingKnowledgeBaseQualityPresentation(
            "BCA fehlt",
            3,
            "91 %",
            4,
            "steigend",
            "up",
            [],
            [0.75, 0.82, 0.91]));

        Assert.Equal("BCA fehlt", state.CoverageGapsText);
        Assert.Equal(3, state.CoverageGapsCount);
        Assert.Equal("91 %", state.AccuracyText);
        Assert.Equal(4, state.StaleSampleCount);
        Assert.Equal("steigend", state.TrendText);
        Assert.Equal("up", state.TrendDirection);
        Assert.Equal([0.75, 0.82, 0.91], state.TrendSeries);
    }

    private static TrainingCenterKnowledgeBaseDashboardController CreateController(DashboardState state)
        => new(
            new FakeDiagnostics(),
            new TrainingCenterKnowledgeBaseDashboardUi(
                value => state.SampleCount = value,
                value => state.ErrorCount = value,
                value => state.NewCount = value,
                value => state.EmbeddingCount = value,
                value => state.CodesCovered = value,
                value => state.LastUpdate = value,
                value => state.ReadinessLabel = value,
                value => state.ReadinessBrush = value,
                value => state.TopCodesText = value,
                value => state.CoverageGapsText = value,
                value => state.CoverageGapsCount = value,
                value => state.AccuracyText = value,
                value => state.StaleSampleCount = value,
                value => state.TrendText = value,
                value => state.TrendDirection = value,
                value => state.TrendSeries = value,
                _ => { },
                action => action()));

    private sealed class DashboardState
    {
        public int SampleCount { get; set; }
        public int ErrorCount { get; set; }
        public int NewCount { get; set; }
        public int EmbeddingCount { get; set; }
        public int CodesCovered { get; set; }
        public string LastUpdate { get; set; } = "";
        public string ReadinessLabel { get; set; } = "";
        public Brush? ReadinessBrush { get; set; }
        public string TopCodesText { get; set; } = "";
        public string CoverageGapsText { get; set; } = "";
        public int CoverageGapsCount { get; set; }
        public string AccuracyText { get; set; } = "";
        public int StaleSampleCount { get; set; }
        public string TrendText { get; set; } = "";
        public string TrendDirection { get; set; } = "";
        public IReadOnlyList<double> TrendSeries { get; set; } = [];
    }

    private sealed class FakeDiagnostics : IKnowledgeBaseDiagnosticsRunner
    {
        public Task<KnowledgeBaseStatusReport> ReadStatusAsync(int topCodes = 20, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<KnowledgeBaseQualityReport> ReadQualityAsync(CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task<KnowledgeBaseDiagnosticsSummary> ReadSummaryAsync(int topCodes = 12, CancellationToken ct = default)
            => throw new NotSupportedException();
    }
}
