using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace AuswertungPro.Next.UI.Ai.KnowledgeBase;

public sealed record KnowledgeBaseCodeCount(string VsaCode, int Count);

public sealed record KnowledgeBaseVersionAnomaly(
    string VersionId,
    DateTimeOffset? CreatedAtUtc,
    int StoredSampleCount,
    int ActualSampleCount,
    string Notes,
    string Kind);

public sealed record KnowledgeBaseValidationCluster(
    string MinuteUtc,
    int TotalCount,
    int CorrectCount,
    double Accuracy,
    int DistinctSuggestedCodes,
    int DistinctFinalCodes);

public sealed record KnowledgeBaseSummary(
    int SampleCount,
    int EmbeddingCount,
    int VersionCount,
    DateTimeOffset? LatestVersionAtUtc,
    int LatestVersionSampleCount,
    string LatestVersionNotes,
    IReadOnlyList<KnowledgeBaseCodeCount> TopCodes,
    IReadOnlyList<KnowledgeBaseVersionAnomaly> VersionAnomalies,
    IReadOnlyList<KnowledgeBaseValidationCluster> SuspiciousValidationClusters);

/// <summary>
/// Liefert Diagnose- und Statistikdaten zur KI-Wissensdatenbank.
/// </summary>
public sealed class KnowledgeBaseDiagnosticsService(KnowledgeBaseContext db)
{
    public KnowledgeBaseSummary ReadSummary(int topCodes = 12, int suspiciousClusterMinCount = 5)
    {
        var sampleCount = QueryCount("SELECT COUNT(*) FROM Samples");
        var embeddingCount = QueryCount("SELECT COUNT(*) FROM Embeddings");
        var versionCount = QueryCount("SELECT COUNT(*) FROM Versions");

        var (latestAt, latestSampleCount, latestNotes) = ReadLatestVersion();
        var top = ReadTopCodes(Math.Max(1, topCodes));
        var versionAnomalies = ReadVersionAnomalies(limit: 20);
        var clusters = ReadSuspiciousValidationClusters(Math.Max(2, suspiciousClusterMinCount), limit: 20);

        return new KnowledgeBaseSummary(
            SampleCount: sampleCount,
            EmbeddingCount: embeddingCount,
            VersionCount: versionCount,
            LatestVersionAtUtc: latestAt,
            LatestVersionSampleCount: latestSampleCount,
            LatestVersionNotes: latestNotes,
            TopCodes: top,
            VersionAnomalies: versionAnomalies,
            SuspiciousValidationClusters: clusters);
    }

    private int QueryCount(string sql)
    {
        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = sql;
        var scalar = cmd.ExecuteScalar();
        return scalar is null or DBNull ? 0 : Convert.ToInt32(scalar);
    }

    private (DateTimeOffset? CreatedAtUtc, int SampleCount, string Notes) ReadLatestVersion()
    {
        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = """
            SELECT CreatedAt, SampleCount, Notes
            FROM Versions
            ORDER BY datetime(CreatedAt) DESC
            LIMIT 1
            """;

        using var r = cmd.ExecuteReader();
        if (!r.Read())
            return (null, 0, "");

        var createdAtRaw = r.IsDBNull(0) ? null : r.GetString(0);
        DateTimeOffset? createdAt = DateTimeOffset.TryParse(createdAtRaw, out var parsed)
            ? parsed
            : null;
        var sampleCount = r.IsDBNull(1) ? 0 : r.GetInt32(1);
        var notes = r.IsDBNull(2) ? "" : r.GetString(2);

        return (createdAt, sampleCount, notes);
    }

    private List<KnowledgeBaseCodeCount> ReadTopCodes(int topCodes)
    {
        var list = new List<KnowledgeBaseCodeCount>(topCodes);
        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = """
            SELECT VsaCode, COUNT(*) AS Cnt
            FROM Samples
            GROUP BY VsaCode
            ORDER BY Cnt DESC, VsaCode ASC
            LIMIT $top
            """;
        cmd.Parameters.AddWithValue("$top", topCodes);

        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var code = r.IsDBNull(0) ? "" : r.GetString(0);
            var count = r.IsDBNull(1) ? 0 : r.GetInt32(1);
            if (string.IsNullOrWhiteSpace(code))
                continue;
            list.Add(new KnowledgeBaseCodeCount(code, count));
        }

        return list;
    }

    private List<KnowledgeBaseVersionAnomaly> ReadVersionAnomalies(int limit)
    {
        var list = new List<KnowledgeBaseVersionAnomaly>();
        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = """
            SELECT
                v.VersionId,
                v.CreatedAt,
                v.SampleCount,
                (SELECT COUNT(*) FROM Samples s WHERE s.VersionId = v.VersionId) AS ActualSampleCount,
                v.Notes
            FROM Versions v
            WHERE
                v.SampleCount = 0
                OR v.SampleCount <> (SELECT COUNT(*) FROM Samples s WHERE s.VersionId = v.VersionId)
            ORDER BY datetime(v.CreatedAt) DESC
            LIMIT $limit
            """;
        cmd.Parameters.AddWithValue("$limit", limit);

        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var versionId = r.IsDBNull(0) ? "" : r.GetString(0);
            var createdAtRaw = r.IsDBNull(1) ? null : r.GetString(1);
            DateTimeOffset? createdAt = DateTimeOffset.TryParse(createdAtRaw, out var parsed)
                ? parsed
                : null;
            var stored = r.IsDBNull(2) ? 0 : r.GetInt32(2);
            var actual = r.IsDBNull(3) ? 0 : r.GetInt32(3);
            var notes = r.IsDBNull(4) ? "" : r.GetString(4);
            var kind = stored == 0 && actual == 0 ? "empty" : "mismatch";

            list.Add(new KnowledgeBaseVersionAnomaly(
                VersionId: versionId,
                CreatedAtUtc: createdAt,
                StoredSampleCount: stored,
                ActualSampleCount: actual,
                Notes: notes,
                Kind: kind));
        }

        return list;
    }

    private List<KnowledgeBaseValidationCluster> ReadSuspiciousValidationClusters(int minCount, int limit)
    {
        var list = new List<KnowledgeBaseValidationCluster>();
        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = """
            SELECT
                substr(CreatedUtc, 1, 16) AS MinuteUtc,
                COUNT(*) AS TotalCount,
                SUM(CASE WHEN WasCorrect = 1 THEN 1 ELSE 0 END) AS CorrectCount,
                COUNT(DISTINCT SuggestedCode) AS DistinctSuggestedCodes,
                COUNT(DISTINCT FinalCode) AS DistinctFinalCodes
            FROM ValidationLog
            GROUP BY substr(CreatedUtc, 1, 16)
            HAVING
                COUNT(*) >= $minCount
                AND SUM(CASE WHEN WasCorrect = 1 THEN 1 ELSE 0 END) = COUNT(*)
            ORDER BY TotalCount DESC, MinuteUtc DESC
            LIMIT $limit
            """;
        cmd.Parameters.AddWithValue("$minCount", minCount);
        cmd.Parameters.AddWithValue("$limit", limit);

        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var minute = r.IsDBNull(0) ? "" : r.GetString(0);
            var total = r.IsDBNull(1) ? 0 : r.GetInt32(1);
            var correct = r.IsDBNull(2) ? 0 : r.GetInt32(2);
            var distinctSuggested = r.IsDBNull(3) ? 0 : r.GetInt32(3);
            var distinctFinal = r.IsDBNull(4) ? 0 : r.GetInt32(4);
            var accuracy = total == 0 ? 0 : (double)correct / total;

            list.Add(new KnowledgeBaseValidationCluster(
                MinuteUtc: minute,
                TotalCount: total,
                CorrectCount: correct,
                Accuracy: accuracy,
                DistinctSuggestedCodes: distinctSuggested,
                DistinctFinalCodes: distinctFinal));
        }

        return list;
    }
}
