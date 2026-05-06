using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FirebirdSql.Data.FirebirdClient;
using Xunit;
using Xunit.Abstractions;

namespace AuswertungPro.Next.Infrastructure.Tests;

// Diagnose-Test: Schema und Inhalt der Arizona.fdb (KIAS-Export) inspizieren.
// Ausfuehren: dotnet test --filter "Category=Diag&FullyQualifiedName~Fdb"
public sealed class ErstfeldFdbInspectTests
{
    private readonly ITestOutputHelper _out;
    public ErstfeldFdbInspectTests(ITestOutputHelper output) => _out = output;

    private const string FdbPath = @"D:\Videoprojekte\Erstfeld_Jagdmatt_38454_0426\Erstfeld_Jagdmatt_38454_0426_Export\Data\Arizona.fdb";

    [Fact(DisplayName = "Diag: Fdb Schema + Stichprobe")]
    [Trait("Category", "Diag")]
    public void Diag_FdbSchemaAndSample()
    {
        if (!File.Exists(FdbPath))
        {
            _out.WriteLine($"SKIP: {FdbPath} fehlt");
            return;
        }

        // KIAS-Default Login (sysdba/masterkey ist Firebird-Standard)
        var users = new[]
        {
            (Environment.GetEnvironmentVariable("IBAK_FDB_USER") ?? "sysdba",
             Environment.GetEnvironmentVariable("IBAK_FDB_PASSWORD") ?? "masterkey"),
            ("SYSDBA", "masterkey"),
        };

        FbConnection? conn = null;
        Exception? lastEx = null;
        foreach (var (u, p) in users)
        {
            try
            {
                var cs = new FbConnectionStringBuilder
                {
                    Database = FdbPath,
                    UserID = u,
                    Password = p,
                    ServerType = FbServerType.Embedded,
                    Charset = "NONE"
                };
                conn = new FbConnection(cs.ToString());
                conn.Open();
                _out.WriteLine($"Verbunden als {u} (Embedded)");
                break;
            }
            catch (Exception ex)
            {
                lastEx = ex;
                conn?.Dispose();
                conn = null;
            }
        }

        if (conn == null)
        {
            _out.WriteLine($"FAIL: Keine Verbindung. Letzter Fehler: {lastEx?.Message}");
            return;
        }

        try
        {
            // 1. Tabellen-Liste
            var tables = new List<string>();
            using (var cmd = new FbCommand(
                @"SELECT TRIM(RDB$RELATION_NAME) FROM RDB$RELATIONS
                  WHERE RDB$VIEW_BLR IS NULL
                    AND (RDB$SYSTEM_FLAG IS NULL OR RDB$SYSTEM_FLAG=0)
                  ORDER BY 1", conn))
            using (var r = cmd.ExecuteReader())
                while (r.Read()) tables.Add(r.GetString(0));

            _out.WriteLine($"Tabellen ({tables.Count}):");
            foreach (var t in tables)
                _out.WriteLine($"  {t}");

            // 2. Heuristik: Tabellen mit "Halt"/"Pipe"/"Conduit"/"Section"/"Inspekt"/"Material"/"DN"/"Profile"
            var keys = new[] { "halt", "pipe", "conduit", "section", "inspekt", "material", "DN", "profil", "leitung", "haltung", "stamm" };
            var interesting = tables.Where(t => keys.Any(k => t.Contains(k, StringComparison.OrdinalIgnoreCase))).ToList();

            _out.WriteLine("");
            _out.WriteLine($"Interessante Tabellen ({interesting.Count}):");
            foreach (var t in interesting)
            {
                var cols = LoadColumns(conn, t);
                _out.WriteLine($"  {t}  ({cols.Count} Spalten): {string.Join(", ", cols.Take(20))}{(cols.Count > 20 ? " ..." : "")}");
            }

            // 3. Fuer die TOP-5 interessanten Tabellen: erste 2 Datenzeilen + Zaehlung
            _out.WriteLine("");
            _out.WriteLine("--- Stichproben ---");
            foreach (var t in interesting.Take(8))
            {
                try
                {
                    long count;
                    using (var c = new FbCommand($"SELECT COUNT(*) FROM \"{t}\"", conn))
                        count = Convert.ToInt64(c.ExecuteScalar());
                    _out.WriteLine($"\n[{t}]  Zeilen: {count}");
                    if (count == 0) continue;

                    using var cmd = new FbCommand($"SELECT FIRST 2 * FROM \"{t}\"", conn);
                    using var r = cmd.ExecuteReader();
                    var fields = Enumerable.Range(0, r.FieldCount).Select(r.GetName).ToArray();
                    while (r.Read())
                    {
                        var sb = new System.Text.StringBuilder();
                        for (var i = 0; i < r.FieldCount; i++)
                        {
                            var v = r.IsDBNull(i) ? "NULL" : r.GetValue(i)?.ToString() ?? "";
                            if (v.Length > 40) v = v.Substring(0, 40) + "...";
                            sb.Append($"{fields[i]}={v}; ");
                        }
                        _out.WriteLine($"  -> {sb}");
                    }
                }
                catch (Exception ex)
                {
                    _out.WriteLine($"  Fehler: {ex.Message}");
                }
            }
        }
        finally
        {
            conn.Dispose();
        }
    }

    private static List<string> LoadColumns(FbConnection conn, string table)
    {
        var cols = new List<string>();
        using var cmd = new FbCommand(
            @"SELECT TRIM(RDB$FIELD_NAME) FROM RDB$RELATION_FIELDS
              WHERE RDB$RELATION_NAME = @t
              ORDER BY RDB$FIELD_POSITION", conn);
        cmd.Parameters.AddWithValue("@t", table);
        using var r = cmd.ExecuteReader();
        while (r.Read()) cols.Add(r.GetString(0));
        return cols;
    }
}
