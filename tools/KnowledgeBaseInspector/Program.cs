using System.Globalization;
using Microsoft.Data.Sqlite;

var dbPath = args.Length > 0
    ? args[0]
    : Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AuswertungPro",
        "KiVideoanalyse",
        "KnowledgeBase.db");

Console.WriteLine($"KB: {dbPath}");
if (!File.Exists(dbPath))
{
    Console.WriteLine("Status: nicht gefunden");
    return 2;
}

using var con = new SqliteConnection($"Data Source={dbPath}");
con.Open();

var sampleCount = ScalarInt(con, "SELECT COUNT(*) FROM Samples");
var embeddingCount = ScalarInt(con, "SELECT COUNT(*) FROM Embeddings");
var versionCount = ScalarInt(con, "SELECT COUNT(*) FROM Versions");

Console.WriteLine($"Samples:    {sampleCount}");
Console.WriteLine($"Embeddings: {embeddingCount}");
Console.WriteLine($"Versionen:  {versionCount}");

using (var cmd = con.CreateCommand())
{
    cmd.CommandText = """
        SELECT CreatedAt, SampleCount, Notes
        FROM Versions
        ORDER BY datetime(CreatedAt) DESC
        LIMIT 1
        """;
    using var r = cmd.ExecuteReader();
    if (r.Read())
    {
        var createdAt = r.IsDBNull(0) ? "" : r.GetString(0);
        var samples = r.IsDBNull(1) ? 0 : r.GetInt32(1);
        var notes = r.IsDBNull(2) ? "" : r.GetString(2);
        Console.WriteLine($"Letzte Version: {createdAt} | Samples: {samples} | Notiz: {notes}");
    }
}

PrintVersionAnomalies(con);
PrintValidationClusters(con, minCount: 5);

Console.WriteLine("Top-Codes:");
using (var cmd = con.CreateCommand())
{
    cmd.CommandText = """
        SELECT VsaCode, COUNT(*) AS Cnt
        FROM Samples
        GROUP BY VsaCode
        ORDER BY Cnt DESC, VsaCode ASC
        LIMIT 15
        """;
    using var r = cmd.ExecuteReader();
    while (r.Read())
    {
        var code = r.IsDBNull(0) ? "" : r.GetString(0);
        var count = r.IsDBNull(1) ? 0 : r.GetInt32(1);
        if (string.IsNullOrWhiteSpace(code))
            continue;
        Console.WriteLine($"  {code,-6} {count,6}");
    }
}

return 0;

static void PrintVersionAnomalies(SqliteConnection con)
{
    if (!TableExists(con, "Versions") || !TableExists(con, "Samples"))
        return;

    Console.WriteLine("Versions-Auffaelligkeiten:");
    using var cmd = con.CreateCommand();
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
        LIMIT 10
        """;

    using var r = cmd.ExecuteReader();
    var any = false;
    while (r.Read())
    {
        any = true;
        var versionId = r.IsDBNull(0) ? "" : r.GetString(0);
        var createdAt = r.IsDBNull(1) ? "" : r.GetString(1);
        var stored = r.IsDBNull(2) ? 0 : r.GetInt32(2);
        var actual = r.IsDBNull(3) ? 0 : r.GetInt32(3);
        var notes = r.IsDBNull(4) ? "" : r.GetString(4);
        var kind = stored == 0 && actual == 0 ? "empty" : "mismatch";
        Console.WriteLine($"  {createdAt} | {kind,-8} | stored={stored,4} actual={actual,4} | {versionId} | {notes}");
    }

    if (!any)
        Console.WriteLine("  keine");
}

static void PrintValidationClusters(SqliteConnection con, int minCount)
{
    if (!TableExists(con, "ValidationLog"))
        return;

    Console.WriteLine($"ValidationLog-Cluster (>= {minCount} Eintraege, 100% correct):");
    using var cmd = con.CreateCommand();
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
        LIMIT 10
        """;
    cmd.Parameters.AddWithValue("$minCount", minCount);

    using var r = cmd.ExecuteReader();
    var any = false;
    while (r.Read())
    {
        any = true;
        var minute = r.IsDBNull(0) ? "" : r.GetString(0);
        var total = r.IsDBNull(1) ? 0 : r.GetInt32(1);
        var correct = r.IsDBNull(2) ? 0 : r.GetInt32(2);
        var suggested = r.IsDBNull(3) ? 0 : r.GetInt32(3);
        var final = r.IsDBNull(4) ? 0 : r.GetInt32(4);
        Console.WriteLine($"  {minute} | total={total,4} correct={correct,4} | suggested={suggested,3} final={final,3}");
    }

    if (!any)
        Console.WriteLine("  keine");
}

static bool TableExists(SqliteConnection con, string tableName)
{
    using var cmd = con.CreateCommand();
    cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name";
    cmd.Parameters.AddWithValue("$name", tableName);
    return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture) > 0;
}

static int ScalarInt(SqliteConnection con, string sql)
{
    using var cmd = con.CreateCommand();
    cmd.CommandText = sql;
    var scalar = cmd.ExecuteScalar();
    return scalar is null or DBNull
        ? 0
        : Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
}
