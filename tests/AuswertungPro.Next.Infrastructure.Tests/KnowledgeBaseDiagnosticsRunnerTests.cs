using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class KnowledgeBaseDiagnosticsRunnerTests
{
    [Fact]
    public async Task ReadSummaryAsync_ReturnsDatabaseCounts()
    {
        var root = Path.Combine(Path.GetTempPath(), "AuswertungPro.Next.Tests", Guid.NewGuid().ToString("N"));
        var dbPath = Path.Combine(root, "KnowledgeBase.db");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

            using (var db = new KnowledgeBaseContext(dbPath))
            {
                using var cmd = db.Connection.CreateCommand();
                cmd.CommandText = """
                    INSERT INTO Versions (VersionId, CreatedAt, SampleCount, Notes)
                    VALUES ('v1', '2026-05-26T12:00:00Z', 1, 'test');

                    INSERT INTO Samples
                        (SampleId, CaseId, VsaCode, Beschreibung, MeterStart, MeterEnd, IsStreck, FramePath, ExportedUtc, VersionId)
                    VALUES
                        ('s1', 'c1', 'BAB', 'Riss', 1.0, 1.0, 0, '', '2026-05-26T12:00:00Z', 'v1');

                    INSERT INTO Embeddings (SampleId, Model, Vector, CreatedAt)
                    VALUES ('s1', 'embed', X'0102', '2026-05-26T12:00:00Z');
                    """;
                cmd.ExecuteNonQuery();
            }

            var runner = new KnowledgeBaseDiagnosticsRunner(dbPath);

            var summary = await runner.ReadSummaryAsync();

            Assert.Equal(1, summary.SampleCount);
            Assert.Equal(1, summary.EmbeddingCount);
            Assert.Equal(1, summary.VersionCount);
            Assert.Equal(1, summary.LatestVersionSampleCount);
            Assert.Equal("test", summary.LatestVersionNotes);
            var code = Assert.Single(summary.TopCodes);
            Assert.Equal("BAB", code.VsaCode);
            Assert.Equal(1, code.Count);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ReadQualityAsync_WhenDatabaseMissing_ReturnsEmptyDashboardText()
    {
        var root = Path.Combine(Path.GetTempPath(), "AuswertungPro.Next.Tests", Guid.NewGuid().ToString("N"));
        var dbPath = Path.Combine(root, "missing.db");
        try
        {
            var runner = new KnowledgeBaseDiagnosticsRunner(dbPath);

            var quality = await runner.ReadQualityAsync();

            Assert.Equal("KB noch nicht erstellt", quality.CoverageGapsText);
            Assert.Equal(0, quality.CoverageGapsCount);
            Assert.Equal("Noch keine Validierungsdaten", quality.AccuracyText);
            Assert.Equal(0, quality.StaleSampleCount);
            Assert.False(File.Exists(dbPath));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
