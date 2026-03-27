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

static int ScalarInt(SqliteConnection con, string sql)
{
    using var cmd = con.CreateCommand();
    cmd.CommandText = sql;
    var scalar = cmd.ExecuteScalar();
    return scalar is null or DBNull
        ? 0
        : Convert.ToInt32(scalar, CultureInfo.InvariantCulture);
}
