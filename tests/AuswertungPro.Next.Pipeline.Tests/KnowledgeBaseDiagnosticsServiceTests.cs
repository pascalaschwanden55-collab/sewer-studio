using System;
using System.IO;
using AuswertungPro.Next.UI.Ai.KnowledgeBase;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class KnowledgeBaseDiagnosticsServiceTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(),
        "sewerstudio-kb-diagnostics-" + Guid.NewGuid().ToString("N") + ".db");

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        TryDelete(_dbPath);
        TryDelete(_dbPath + "-wal");
        TryDelete(_dbPath + "-shm");
    }

    [Fact]
    public void ReadSummary_reports_empty_and_mismatched_version_rows()
    {
        using var db = new KnowledgeBaseContext(_dbPath);
        Execute(db.Connection, """
            INSERT INTO Versions(VersionId, CreatedAt, SampleCount, Notes)
            VALUES
                ('v-empty', '2026-05-20T12:14:00Z', 0, ''),
                ('v-mismatch', '2026-05-20T12:15:00Z', 3, 'stored too high');
            """);
        Execute(db.Connection, """
            INSERT INTO Samples(SampleId, CaseId, VsaCode, Beschreibung, ExportedUtc, VersionId)
            VALUES ('s1', 'case-1', 'BAB', 'one sample', '2026-05-20T12:15:10Z', 'v-mismatch');
            """);

        var summary = new KnowledgeBaseDiagnosticsService(db).ReadSummary();

        Assert.Equal(2, summary.VersionAnomalies.Count);
        Assert.Contains(summary.VersionAnomalies, a =>
            a.VersionId == "v-empty"
            && a.StoredSampleCount == 0
            && a.ActualSampleCount == 0
            && a.Kind == "empty");
        Assert.Contains(summary.VersionAnomalies, a =>
            a.VersionId == "v-mismatch"
            && a.StoredSampleCount == 3
            && a.ActualSampleCount == 1
            && a.Kind == "mismatch");
    }

    [Fact]
    public void ReadSummary_reports_suspicious_all_correct_validation_clusters()
    {
        using var db = new KnowledgeBaseContext(_dbPath);

        for (var i = 0; i < 6; i++)
        {
            Execute(db.Connection,
                "INSERT INTO ValidationLog(LogId, VsaCode, SuggestedCode, FinalCode, WasCorrect, EvidenceJson, CreatedUtc) " +
                $"VALUES ('spam-{i}', 'BAB', 'BAB', 'BAB', 1, '{{}}', '2026-05-20T12:14:0{i}Z');");
        }

        Execute(db.Connection, """
            INSERT INTO ValidationLog(LogId, VsaCode, SuggestedCode, FinalCode, WasCorrect, EvidenceJson, CreatedUtc)
            VALUES
                ('mixed-1', 'BBA', 'BBA', 'BBA', 1, '{}', '2026-05-20T12:20:00Z'),
                ('mixed-2', 'BBA', 'BBA', 'BBB', 0, '{}', '2026-05-20T12:20:01Z');
            """);

        var summary = new KnowledgeBaseDiagnosticsService(db).ReadSummary();

        var cluster = Assert.Single(summary.SuspiciousValidationClusters);
        Assert.Equal("2026-05-20T12:14", cluster.MinuteUtc);
        Assert.Equal(6, cluster.TotalCount);
        Assert.Equal(6, cluster.CorrectCount);
        Assert.Equal(1.0, cluster.Accuracy);
        Assert.Equal(1, cluster.DistinctSuggestedCodes);
        Assert.Equal(1, cluster.DistinctFinalCodes);
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // best effort test cleanup
        }
    }
}
