using System.Text;
using Microsoft.Data.Sqlite;
using AuswertungPro.Next.Domain.VsaCatalog;

// Einmaliges KB-Wartungstool: normalisiert kaputte VSA-Codes in der Samples-Tabelle
// (Punkt-Trenner, Meter-Suffixe) per VsaCodeValidator.TryNormalizeKnownCode — also EXAKT
// derselben Logik wie der WinCan-Import-Fix. Datensicher: kein DELETE, nur UPDATE des Codes;
// unklare Codes (Normalisierung -> null oder unveraendert) bleiben unangetastet.
//
//   dotnet run --project tools/KbCodeCleanup --              (Dry-Run, read-only)
//   dotnet run --project tools/KbCodeCleanup -- --apply      (schreibt + Protokoll)
//   optional: --db <pfad>   (Default: <SEWERSTUDIO_KNOWLEDGE_ROOT>/KnowledgeBase.db bzw. C:\KI_BRAIN)

var apply = args.Contains("--apply");
var dbPath = GetArg("--db")
    ?? Path.Combine(Environment.GetEnvironmentVariable("SEWERSTUDIO_KNOWLEDGE_ROOT") ?? @"C:\KI_BRAIN", "KnowledgeBase.db");

if (!File.Exists(dbPath))
{
    Console.Error.WriteLine($"FEHLER: KnowledgeBase.db nicht gefunden: {dbPath}");
    return 2;
}

var connString = new SqliteConnectionStringBuilder
{
    DataSource = dbPath,
    Mode = apply ? SqliteOpenMode.ReadWrite : SqliteOpenMode.ReadOnly
}.ToString();

using var conn = new SqliteConnection(connString);
conn.Open();

var codes = new List<(string Code, int Count)>();
using (var cmd = conn.CreateCommand())
{
    cmd.CommandText = "SELECT VsaCode, COUNT(*) FROM Samples GROUP BY VsaCode";
    using var r = cmd.ExecuteReader();
    while (r.Read())
    {
        var c = r.IsDBNull(0) ? "" : r.GetString(0);
        var n = r.IsDBNull(1) ? 0 : r.GetInt32(1);
        if (!string.IsNullOrWhiteSpace(c))
            codes.Add((c, n));
    }
}

// Korrektur nur, wenn TryNormalizeKnownCode einen ANDEREN, gueltigen Code liefert.
// null (unbekannt/zu lang) oder unveraendert -> NICHT anfassen (im Zweifel stehen lassen).
var fixes = codes
    .Select(x => (x.Code, New: VsaCodeValidator.TryNormalizeKnownCode(x.Code), x.Count))
    .Where(x => x.New is not null && !string.Equals(x.New, x.Code, StringComparison.Ordinal))
    .Select(x => (Old: x.Code, New: x.New!, x.Count))
    .OrderByDescending(x => x.Count)
    .ThenBy(x => x.Old, StringComparer.Ordinal)
    .ToList();

Console.WriteLine($"DB:     {dbPath}");
Console.WriteLine($"Modus:  {(apply ? "APPLY (schreibt)" : "DRY-RUN (read-only)")}");
Console.WriteLine($"Codes:  {fixes.Count} zu korrigieren, betroffene Samples: {fixes.Sum(x => x.Count)}");
Console.WriteLine();
foreach (var f in fixes)
    Console.WriteLine($"  {f.Old,-14} -> {f.New,-7} ({f.Count})");
Console.WriteLine();

if (!apply)
{
    Console.WriteLine("DRY-RUN: nichts geschrieben. Zum Ausfuehren mit --apply starten.");
    return 0;
}

var stamp = DateTimeOffset.Now.ToString("yyyyMMdd_HHmmss");
var logPath = Path.Combine(Path.GetDirectoryName(dbPath)!, $"_codefix_log_{stamp}.csv");
using (var log = new StreamWriter(logPath, false, new UTF8Encoding(false)))
{
    log.WriteLine("old_code,new_code,samples_updated");

    using var tx = conn.BeginTransaction();
    var total = 0;
    foreach (var f in fixes)
    {
        using var up = conn.CreateCommand();
        up.Transaction = tx;
        up.CommandText = "UPDATE Samples SET VsaCode = $new WHERE VsaCode = $old";
        up.Parameters.AddWithValue("$new", f.New);
        up.Parameters.AddWithValue("$old", f.Old);
        var n = up.ExecuteNonQuery();
        log.WriteLine($"{f.Old},{f.New},{n}");
        total += n;
    }
    tx.Commit();

    using (var ck = conn.CreateCommand())
    {
        ck.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
        ck.ExecuteNonQuery();
    }

    Console.WriteLine($"FERTIG: {total} Samples aktualisiert (kein Eintrag geloescht).");
    Console.WriteLine($"Protokoll: {logPath}");
}

return 0;

string? GetArg(string name)
{
    for (var i = 0; i < args.Length - 1; i++)
        if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            return args[i + 1];
    return null;
}
