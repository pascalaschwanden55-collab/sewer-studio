using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace AuswertungPro.Next.UI.Ai.KnowledgeBase;

public sealed record KnowledgeBaseCodeCount(string VsaCode, int Count);

public sealed record KnowledgeBaseSummary(
    int SampleCount,
    int EmbeddingCount,
    int VersionCount,
    DateTimeOffset? LatestVersionAtUtc,
    int LatestVersionSampleCount,
    string LatestVersionNotes,
    IReadOnlyList<KnowledgeBaseCodeCount> TopCodes);

/// <summary>
/// Liefert Diagnose- und Statistikdaten zur KI-Wissensdatenbank.
/// </summary>
public sealed class KnowledgeBaseDiagnosticsService(KnowledgeBaseContext db)
{
    public KnowledgeBaseSummary ReadSummary(int topCodes = 12)
    {
        var sampleCount = QueryCount("SELECT COUNT(*) FROM Samples");
        var embeddingCount = QueryCount("SELECT COUNT(*) FROM Embeddings");
        var versionCount = QueryCount("SELECT COUNT(*) FROM Versions");

        var (latestAt, latestSampleCount, latestNotes) = ReadLatestVersion();
        var top = ReadTopCodes(Math.Max(1, topCodes));

        return new KnowledgeBaseSummary(
            SampleCount: sampleCount,
            EmbeddingCount: embeddingCount,
            VersionCount: versionCount,
            LatestVersionAtUtc: latestAt,
            LatestVersionSampleCount: latestSampleCount,
            LatestVersionNotes: latestNotes,
            TopCodes: top);
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
}
