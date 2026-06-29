using AuswertungPro.Next.Application.Ai.KnowledgeBase;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingKnowledgeBaseCheckPresentationBuilderTests
{
    [Fact]
    public void Build_formats_summary_latest_version_and_top_codes_like_view_model()
    {
        var latest = new DateTimeOffset(2026, 6, 30, 8, 15, 0, TimeSpan.Zero);
        var summary = new KnowledgeBaseDiagnosticsSummary(
            SampleCount: 12,
            EmbeddingCount: 10,
            VersionCount: 3,
            LatestVersionAtUtc: latest,
            LatestVersionSampleCount: 9,
            LatestVersionNotes: "ok",
            TopCodes:
            [
                new KnowledgeBaseDiagnosticsCodeCount("BAA", 4),
                new KnowledgeBaseDiagnosticsCodeCount("BAB", 2)
            ]);

        var result = TrainingKnowledgeBaseCheckPresentationBuilder.Build(summary);
        var localLatest = latest.ToLocalTime();

        Assert.Equal("KB gepr\u00fcft: 12 Samples, 10 Embeddings, 3 Versionen.", result.StatusText);
        Assert.Equal(
            [
                "KB-Stand: Samples=12, Embeddings=10, Versionen=3",
                $"Letzte Version: {localLatest:yyyy-MM-dd HH:mm} (9 Samples) | Notiz: ok",
                "Top-Codes:",
                "  BAA: 4",
                "  BAB: 2"
            ],
            result.LogLines);
    }

    [Fact]
    public void Build_uses_dash_for_blank_notes_and_logs_empty_top_codes()
    {
        var summary = new KnowledgeBaseDiagnosticsSummary(
            SampleCount: 0,
            EmbeddingCount: 0,
            VersionCount: 0,
            LatestVersionAtUtc: DateTimeOffset.UtcNow,
            LatestVersionSampleCount: 0,
            LatestVersionNotes: " ",
            TopCodes: []);

        var result = TrainingKnowledgeBaseCheckPresentationBuilder.Build(summary);

        Assert.Contains(result.LogLines, line => line.EndsWith("Notiz: -", StringComparison.Ordinal));
        Assert.Contains("Top-Codes: keine Eintr\u00e4ge vorhanden.", result.LogLines);
    }
}
