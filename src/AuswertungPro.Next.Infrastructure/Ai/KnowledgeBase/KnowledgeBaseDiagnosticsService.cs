using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

public sealed record KnowledgeBaseCodeCount(string VsaCode, int Count);

/// <summary>Verteilung der Samples nach QualityGate-Stufe.</summary>
public sealed record KnowledgeBaseQualityCounts(int Green, int Yellow, int Red, int Unknown);

/// <summary>Validation-Aggregation je Code (aus ValidationLog).</summary>
public sealed record KnowledgeBaseValidationStat(string VsaCode, int Total, int Correct);

/// <summary>Haeufige Verwechslung: SuggestedCode wurde N-mal auf FinalCode korrigiert.</summary>
public sealed record KnowledgeBaseConfusion(string SuggestedCode, string FinalCode, int Count);

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

    /// <summary>Alle Codes mit Anzahl — ohne LIMIT, fuer Coverage-Analyse.</summary>
    public List<KnowledgeBaseCodeCount> ReadAllCodeCounts()
    {
        var list = new List<KnowledgeBaseCodeCount>();
        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = "SELECT VsaCode, COUNT(*) AS Cnt FROM Samples GROUP BY VsaCode ORDER BY VsaCode";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var code = r.IsDBNull(0) ? "" : r.GetString(0);
            var count = r.IsDBNull(1) ? 0 : r.GetInt32(1);
            if (!string.IsNullOrWhiteSpace(code))
                list.Add(new KnowledgeBaseCodeCount(code, count));
        }
        return list;
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

    /// <summary>
    /// Zaehlt Samples gruppiert nach QualityGateLevel (Green/Yellow/Red).
    /// Samples ohne Wert zaehlen als "Unknown".
    /// </summary>
    public KnowledgeBaseQualityCounts ReadQualityDistribution()
    {
        int green = 0, yellow = 0, red = 0, unknown = 0;
        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = """
            SELECT COALESCE(LOWER(NULLIF(TRIM(QualityGateLevel), '')), '') AS Lvl, COUNT(*) AS Cnt
            FROM Samples
            GROUP BY Lvl
            """;
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var lvl = r.IsDBNull(0) ? "" : r.GetString(0);
            var cnt = r.IsDBNull(1) ? 0 : r.GetInt32(1);
            switch (lvl)
            {
                case "green": green += cnt; break;
                case "yellow": yellow += cnt; break;
                case "red": red += cnt; break;
                default: unknown += cnt; break;
            }
        }
        return new KnowledgeBaseQualityCounts(green, yellow, red, unknown);
    }

    /// <summary>
    /// Aggregiert ValidationLog je VsaCode: Total + Correct.
    /// Codes ohne Eintrag erscheinen nicht. Akkumuliert Total und Correct
    /// (kein Limit — wird in der Anwendung weitergefiltert).
    /// </summary>
    public List<KnowledgeBaseValidationStat> ReadValidationStats()
    {
        var list = new List<KnowledgeBaseValidationStat>();
        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = """
            SELECT VsaCode,
                   COUNT(*) AS Total,
                   SUM(CASE WHEN WasCorrect = 1 THEN 1 ELSE 0 END) AS Correct
            FROM ValidationLog
            WHERE VsaCode IS NOT NULL AND VsaCode <> ''
            GROUP BY VsaCode
            """;
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var code = r.IsDBNull(0) ? "" : r.GetString(0);
            var total = r.IsDBNull(1) ? 0 : r.GetInt32(1);
            var correct = r.IsDBNull(2) ? 0 : r.GetInt32(2);
            if (string.IsNullOrWhiteSpace(code)) continue;
            list.Add(new KnowledgeBaseValidationStat(code, total, correct));
        }
        return list;
    }

    /// <summary>Gesamt-Trefferquote ueber den ValidationLog (oder null bei leerem Log).</summary>
    public (int Total, int Correct) ReadOverallValidation()
    {
        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*) AS Total,
                   SUM(CASE WHEN WasCorrect = 1 THEN 1 ELSE 0 END) AS Correct
            FROM ValidationLog
            """;
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return (0, 0);
        var total = r.IsDBNull(0) ? 0 : r.GetInt32(0);
        var correct = r.IsDBNull(1) ? 0 : r.GetInt32(1);
        return (total, correct);
    }

    /// <summary>
    /// Liest die haeufigsten Verwechslungen aus dem ValidationLog: Wo hat die KI
    /// einen Code vorgeschlagen, den der Mensch auf einen anderen Code korrigiert hat?
    /// Zeigt Lern-Schwerpunkte (z.B. "BAB→BAC" = Riss vs. Bruch wird oft verwechselt).
    /// </summary>
    /// <param name="limit">Maximale Anzahl Zeilen.</param>
    public List<KnowledgeBaseConfusion> ReadTopConfusions(int limit = 10)
    {
        var list = new List<KnowledgeBaseConfusion>(limit);
        using var cmd = db.Connection.CreateCommand();
        cmd.CommandText = """
            SELECT SuggestedCode, FinalCode, COUNT(*) AS Cnt
            FROM ValidationLog
            WHERE WasCorrect = 0
              AND SuggestedCode IS NOT NULL AND SuggestedCode <> ''
              AND FinalCode IS NOT NULL AND FinalCode <> ''
              AND SuggestedCode <> FinalCode
            GROUP BY SuggestedCode, FinalCode
            ORDER BY Cnt DESC, SuggestedCode ASC, FinalCode ASC
            LIMIT $lim
            """;
        cmd.Parameters.AddWithValue("$lim", Math.Max(1, limit));
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var sug = r.IsDBNull(0) ? "" : r.GetString(0);
            var fin = r.IsDBNull(1) ? "" : r.GetString(1);
            var cnt = r.IsDBNull(2) ? 0 : r.GetInt32(2);
            list.Add(new KnowledgeBaseConfusion(sug, fin, cnt));
        }
        return list;
    }
}
