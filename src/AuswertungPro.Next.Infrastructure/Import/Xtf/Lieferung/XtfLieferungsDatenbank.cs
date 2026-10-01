using Microsoft.Data.Sqlite;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf.Lieferung;

internal static class XtfLieferungsDatenbank
{
    internal const int Kennung = 1397971028;
    internal static SqliteConnection Verbinde(string datei, bool schreiben = false, bool neu = false)
    {
        var c = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(datei), Mode = schreiben ? SqliteOpenMode.ReadWrite : SqliteOpenMode.ReadOnly,
            Pooling = false, DefaultTimeout = 10
        }.ToString());
        try
        {
            c.Open();
            if (!neu && (Convert.ToInt32(Skalar(c, "PRAGMA application_id")) != Kennung
                || Convert.ToInt32(Skalar(c, "PRAGMA user_version")) != 1 || Meta(c, "fertig") != "1"))
                throw new InvalidDataException("Dies ist keine vollständige SewerStudio-Lieferungsarbeitsdatei.");
            return c;
        }
        catch { c.Dispose(); throw; }
    }

    internal static SqliteCommand Befehl(SqliteConnection c, string sql, params (string Name, object? Wert)[] werte)
    {
        var cmd = c.CreateCommand(); cmd.CommandText = sql;
        foreach (var (name, wert) in werte) cmd.Parameters.AddWithValue(name, wert ?? DBNull.Value);
        return cmd;
    }
    internal static object? Skalar(SqliteConnection c, string sql, params (string Name, object? Wert)[] werte)
    { using var cmd = Befehl(c, sql, werte); return cmd.ExecuteScalar(); }
    internal static void Ausfuehren(SqliteConnection c, string sql, params (string Name, object? Wert)[] werte)
    { using var cmd = Befehl(c, sql, werte); cmd.ExecuteNonQuery(); }
    internal static string Meta(SqliteConnection c, string key) => (string?)Skalar(c, "SELECT wert FROM meta WHERE name=$key", ("$key", key)) ?? "";
    internal static void SetzeMeta(SqliteConnection c, string key, string wert)
        => Ausfuehren(c, "INSERT OR REPLACE INTO meta(name,wert) VALUES($key,$value)", ("$key", key), ("$value", wert));
    internal static void Anlegen(SqliteConnection c) => Ausfuehren(c, $"""
        PRAGMA application_id={Kennung}; PRAGMA user_version=1;
        CREATE TABLE meta(name TEXT PRIMARY KEY, wert TEXT NOT NULL);
        CREATE TABLE koerbe(id INTEGER PRIMARY KEY, xml TEXT NOT NULL);
        CREATE TABLE objekte(id INTEGER PRIMARY KEY, korb INTEGER NOT NULL, klasse TEXT NOT NULL, tid TEXT,
          name TEXT NOT NULL, original TEXT NOT NULL, aktuell TEXT, version INTEGER NOT NULL DEFAULT 0,
          namensschluessel TEXT, problem TEXT NOT NULL DEFAULT '');
        """);
    internal static void Indizes(SqliteConnection c) => Ausfuehren(c, """
        CREATE INDEX objekte_tid ON objekte(tid);
        CREATE INDEX objekte_klasse ON objekte(klasse,id);
        CREATE INDEX objekte_name ON objekte(namensschluessel);
        CREATE INDEX objekte_korb ON objekte(korb,id);
        """);
}
