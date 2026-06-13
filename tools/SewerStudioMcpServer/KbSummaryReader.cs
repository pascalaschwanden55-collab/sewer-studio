using System.Globalization;
using Microsoft.Data.Sqlite;

namespace AuswertungPro.Tools.SewerStudioMcpServer;

// Liest die KnowledgeBase (SQLite) STRIKT read-only (Mode=ReadOnly) und liefert
// Trainings-relevante Kennzahlen: Sample-/Embedding-Anzahl, Code-Verteilung,
// haeufigste Codes und unterrepraesentierte Codes (Trainingsluecken).
// Bewusst KEIN KnowledgeBaseContext: dessen Konstruktor ruft EnsureSchema() auf
// und wuerde die DB schreibend oeffnen/migrieren — das widerspraeche dem
// read-only-Prinzip des MCP-Servers.
public static class KbSummaryReader
{
    public static KbSummaryResult Read(string dbPath, int topCodes, int gapThreshold)
    {
        if (topCodes <= 0)
            topCodes = 20;
        if (gapThreshold <= 0)
            gapThreshold = 3;

        if (string.IsNullOrWhiteSpace(dbPath) || !File.Exists(dbPath))
            return Empty(dbPath, "KnowledgeBase.db nicht gefunden.", gapThreshold);

        try
        {
            var connString = new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode = SqliteOpenMode.ReadOnly
            }.ToString();

            using var conn = new SqliteConnection(connString);
            conn.Open();

            var sampleCount = Scalar(conn, "SELECT COUNT(*) FROM Samples");
            var embeddingCount = Scalar(conn, "SELECT COUNT(*) FROM Embeddings");
            var versionCount = Scalar(conn, "SELECT COUNT(*) FROM Versions");
            var codeCounts = ReadCodeCounts(conn);
            var (latestUtc, latestSamples) = ReadLatestVersion(conn);

            var top = codeCounts
                .OrderByDescending(c => c.Count)
                .ThenBy(c => c.Code, StringComparer.OrdinalIgnoreCase)
                .Take(topCodes)
                .ToList();

            var gaps = codeCounts
                .Where(c => c.Count < gapThreshold)
                .OrderBy(c => c.Count)
                .ThenBy(c => c.Code, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new KbSummaryResult(
                DbPath: dbPath,
                Found: true,
                SampleCount: sampleCount,
                EmbeddingCount: embeddingCount,
                DistinctCodes: codeCounts.Count,
                VersionCount: versionCount,
                LatestVersionUtc: latestUtc,
                LatestVersionSampleCount: latestSamples,
                GapThreshold: gapThreshold,
                TopCodes: top,
                UnderRepresented: gaps,
                Note: codeCounts.Count == 0
                    ? "KB enthaelt keine Samples (leer oder Schema noch nicht befuellt)."
                    : null);
        }
        catch (Exception ex)
        {
            return Empty(dbPath, $"KB nicht lesbar: {ex.Message}", gapThreshold);
        }
    }

    private static int Scalar(SqliteConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        var scalar = cmd.ExecuteScalar();
        return scalar is null or DBNull ? 0 : Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
    }

    private static IReadOnlyList<KbCodeCount> ReadCodeCounts(SqliteConnection conn)
    {
        var list = new List<KbCodeCount>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT VsaCode, COUNT(*) AS Cnt FROM Samples GROUP BY VsaCode";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var code = r.IsDBNull(0) ? "" : r.GetString(0);
            var count = r.IsDBNull(1) ? 0 : r.GetInt32(1);
            if (!string.IsNullOrWhiteSpace(code))
                list.Add(new KbCodeCount(code, count));
        }

        return list;
    }

    private static (string? Utc, int SampleCount) ReadLatestVersion(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT CreatedAt, SampleCount FROM Versions ORDER BY datetime(CreatedAt) DESC LIMIT 1";
        using var r = cmd.ExecuteReader();
        if (!r.Read())
            return (null, 0);

        var utc = r.IsDBNull(0) ? null : r.GetString(0);
        var sampleCount = r.IsDBNull(1) ? 0 : r.GetInt32(1);
        return (utc, sampleCount);
    }

    private static KbSummaryResult Empty(string dbPath, string note, int gapThreshold)
        => new(
            DbPath: dbPath ?? "",
            Found: false,
            SampleCount: 0,
            EmbeddingCount: 0,
            DistinctCodes: 0,
            VersionCount: 0,
            LatestVersionUtc: null,
            LatestVersionSampleCount: 0,
            GapThreshold: gapThreshold,
            TopCodes: Array.Empty<KbCodeCount>(),
            UnderRepresented: Array.Empty<KbCodeCount>(),
            Note: note);
}
