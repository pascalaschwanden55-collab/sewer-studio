using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using FirebirdSql.Data.FirebirdClient;

namespace AuswertungPro.Next.Infrastructure.Import.Ibak;

// IbakExportImportService Firebird-FDB-PhotoMap-Loader: Liest Arizona.fdb
// (KIAS Firebird-DB) um die Foto-Zuordnung pro Haltung aus den IBAK-eigenen
// Tabellen zu extrahieren — Tabellen + Spalten dynamisch erkannt, da
// IBAK-Schema versions-abhaengig variiert. Plus Firebird-Client-Resolver.
// Aus dem Hauptdatei extrahiert (Slice 34a).
public sealed partial class IbakExportImportService
{
    private static Dictionary<string, List<string>> TryLoadPhotoMapFromFdb(string exportRoot, List<string> messages)
    {
        var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var fdbPath = FindFdb(exportRoot);
        if (string.IsNullOrWhiteSpace(fdbPath))
            return result;

        try
        {
            var cs = new FbConnectionStringBuilder
            {
                Database = fdbPath,
                UserID = Environment.GetEnvironmentVariable("IBAK_FDB_USER")
                    ?? throw new InvalidOperationException("IBAK_FDB_USER muss als Umgebungsvariable gesetzt sein."),
                Password = Environment.GetEnvironmentVariable("IBAK_FDB_PASSWORD")
                    ?? throw new InvalidOperationException("IBAK_FDB_PASSWORD muss als Umgebungsvariable gesetzt sein."),
                Charset = "WIN1252",
                Dialect = 3,
                Pooling = false
            };

            var client = TryFindFbClient(exportRoot);
            if (!string.IsNullOrWhiteSpace(client))
                cs.ClientLibrary = client;

            using var conn = new FbConnection(cs.ToString());
            conn.Open();

            var tables = LoadTables(conn);
            if (tables.Count == 0)
            {
                messages.Add("IBAK FDB: Keine Tabellen gefunden.");
                return result;
            }

            var columns = LoadColumns(conn);
            var photoTable = PickPhotoTable(tables, columns);
            if (photoTable is null)
            {
                messages.Add("IBAK FDB: Keine Foto-Tabelle erkannt (Fallback auf Dateinamen).");
                return result;
            }

            var cols = columns[photoTable];
            var fileCol = FindColumn(cols, "FILE", "FILENAME", "NAME", "PATH", "DATEI", "BILD", "FOTO", "IMAGE");
            var holdingCol = FindColumn(cols, "HALT", "HOLD", "LINE", "SECTION", "ROHR", "PIPE", "OBJ", "OBJECT");

            if (string.IsNullOrWhiteSpace(fileCol))
            {
                messages.Add($"IBAK FDB: Foto-Tabelle {photoTable} ohne Dateiname-Spalte.");
                return result;
            }

            // Identifier quoten und validieren (Schutz gegen SQL-Injection via Schema)
            static string QuoteId(string id) => "\"" + id.Replace("\"", "\"\"") + "\"";

            var sql = holdingCol is null
                ? $"SELECT {QuoteId(fileCol)} FROM {QuoteId(photoTable)}"
                : $"SELECT {QuoteId(holdingCol)}, {QuoteId(fileCol)} FROM {QuoteId(photoTable)}";

            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var fileName = r.IsDBNull(holdingCol is null ? 0 : 1) ? "" : r.GetString(holdingCol is null ? 0 : 1);
                if (string.IsNullOrWhiteSpace(fileName))
                    continue;

                var holding = "";
                if (holdingCol is not null && !r.IsDBNull(0))
                    holding = NormalizeHoldingKey(r.GetValue(0)?.ToString());

                if (string.IsNullOrWhiteSpace(holding))
                {
                    // fallback: extract from filename L_<holding>_###.jpg
                    holding = ExtractHoldingFromPhoto(fileName);
                }

                if (string.IsNullOrWhiteSpace(holding))
                    continue;

                if (!result.TryGetValue(holding, out var list))
                {
                    list = new List<string>();
                    result[holding] = list;
                }
                list.Add(Path.GetFileName(fileName));
            }

            foreach (var kv in result)
                kv.Value.Sort((a, b) => ExtractPhotoIndex(a).CompareTo(ExtractPhotoIndex(b)));
        }
        catch (Exception ex)
        {
            messages.Add($"IBAK FDB: Zugriff fehlgeschlagen ({ex.Message}). Fallback auf Dateinamen. Falls no client library: Firebird Client installieren oder fbclient.dll bereitstellen.");
        }

        return result;
    }

    private static string ExtractHoldingFromPhoto(string fileName)
    {
        var m = Regex.Match(fileName, @"^(?:L__|L_|H__)(.+?)_(\d+)\.(jpg|jpeg|png|bmp)$", RegexOptions.IgnoreCase);
        if (m.Success)
            return NormalizeHoldingKey(m.Groups[1].Value);
        return "";
    }

    private static List<string> LoadTables(FbConnection conn)
    {
        var list = new List<string>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT TRIM(RDB$RELATION_NAME) FROM RDB$RELATIONS WHERE RDB$SYSTEM_FLAG = 0 AND RDB$VIEW_BLR IS NULL";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var name = r.IsDBNull(0) ? "" : r.GetString(0).Trim();
            if (!string.IsNullOrWhiteSpace(name))
                list.Add(name);
        }
        return list;
    }

    private static Dictionary<string, List<string>> LoadColumns(FbConnection conn)
    {
        var dict = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT TRIM(RDB$RELATION_NAME), TRIM(RDB$FIELD_NAME) FROM RDB$RELATION_FIELDS";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var table = r.IsDBNull(0) ? "" : r.GetString(0).Trim();
            var col = r.IsDBNull(1) ? "" : r.GetString(1).Trim();
            if (string.IsNullOrWhiteSpace(table) || string.IsNullOrWhiteSpace(col))
                continue;
            if (!dict.TryGetValue(table, out var list))
            {
                list = new List<string>();
                dict[table] = list;
            }
            list.Add(col);
        }
        return dict;
    }

    private static string? PickPhotoTable(List<string> tables, Dictionary<string, List<string>> columns)
    {
        string? best = null;
        var bestScore = 0;

        foreach (var t in tables)
        {
            if (!columns.TryGetValue(t, out var cols))
                continue;

            var score = 0;
            var nameUpper = t.ToUpperInvariant();
            if (nameUpper.Contains("PHOTO") || nameUpper.Contains("FOTO") || nameUpper.Contains("BILD") || nameUpper.Contains("IMAGE") || nameUpper.Contains("PIC"))
                score += 6;
            if (nameUpper.Contains("MEDIA"))
                score += 3;

            if (cols.Any(c => ContainsAny(c, "FILE", "FILENAME", "PATH", "NAME", "DATEI")))
                score += 4;
            if (cols.Any(c => ContainsAny(c, "HALT", "HOLD", "LINE", "SECTION", "ROHR", "PIPE", "OBJ", "OBJECT")))
                score += 2;

            if (score > bestScore)
            {
                bestScore = score;
                best = t;
            }
        }

        return bestScore >= 6 ? best : null;
    }

    private static string? FindColumn(List<string> cols, params string[] keys)
    {
        foreach (var key in keys)
        {
            var col = cols.FirstOrDefault(c => c.Contains(key, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(col))
                return col;
        }
        return null;
    }

    private static bool ContainsAny(string text, params string[] keys)
    {
        foreach (var key in keys)
            if (text.Contains(key, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    private static string? FindFdb(string root)
    {
        var candidates = Directory.EnumerateFiles(root, "*.fdb", SearchOption.AllDirectories).ToList();
        if (candidates.Count == 0)
            return null;
        var preferred = candidates.FirstOrDefault(p => p.IndexOf(Path.DirectorySeparatorChar + "Data" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0);
        return preferred ?? candidates[0];
    }

    private static string? TryFindFbClient(string exportRoot)
    {
        var candidates = new[]
        {
            Path.Combine(exportRoot, "fbclient.dll"),
            Path.Combine(exportRoot, "Data", "fbclient.dll"),
            Path.Combine(AppContext.BaseDirectory, "fbclient.dll")
        };

        foreach (var c in candidates)
            if (File.Exists(c))
                return c;

        return null;
    }
}
