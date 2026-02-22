using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using FirebirdSql.Data.FirebirdClient;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Infrastructure.Import.Ibak;

public sealed class IbakExportImportService : IIbakImportService
{
    private static readonly HashSet<string> MediaExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mpg", ".mpeg", ".mp4", ".avi", ".mov",
        ".jpg", ".jpeg", ".png", ".bmp",
        ".pdf", ".txt"
    };

    private static readonly Regex ObservationRegex = new(
        @"^\s*(\d{2}:\d{2}:\d{2})\s+([\d.,]+)\s*m\s+([A-Z0-9]+)\s+(.*)$",
        RegexOptions.Compiled);

    private static readonly Regex RangeIndexRegex = new(@"\((\d+)\)", RegexOptions.Compiled);

    public Result<ImportStats> ImportIbakExport(string exportRoot, Project project)
    {
        if (string.IsNullOrWhiteSpace(exportRoot) || !Directory.Exists(exportRoot))
            return Result<ImportStats>.Fail("IBAK_ROOT_MISSING", "IBAK Export-Ordner nicht gefunden.");

        var dataPath = FindDatenTxt(exportRoot);
        if (string.IsNullOrWhiteSpace(dataPath))
            return Result<ImportStats>.Fail("IBAK_DATEN_MISSING", "Keine IBAK Daten.txt im Export gefunden.");

        var messages = new List<string>();
        var found = 0;
        var updated = 0;
        var errors = 0;
        var uncertain = 0;

        var fileIndex = BuildFileIndex(exportRoot);
        var photoMap = LoadPhotoMap(exportRoot, fileIndex, messages);
        var protocolService = new ProtocolService();

        try
        {
            var parsed = ParseDatenTxt(dataPath, messages);
            foreach (var holding in parsed)
            {
                var key = NormalizeHoldingKey(holding.Holding);
                var record = FindRecord(project, key);
                if (record is null)
                {
                    uncertain++;
                    messages.Add($"Haltung nicht gefunden im Projekt: {holding.Holding}");
                    continue;
                }

                found++;

                if (holding.Entries.Count == 0)
                {
                    uncertain++;
                    messages.Add($"Keine Beobachtungen in Daten.txt fuer Haltung {holding.Holding}");
                    continue;
                }

                ApplyPhotosToEntries(holding.Holding, holding.Entries, fileIndex, photoMap, messages);
                ApplyProtocol(record, holding.Entries, protocolService);
                UpdateFindings(record, holding.Entries);
                LinkVideo(record, holding.Holding, fileIndex);
                LinkHoldingPdf(record, holding.Holding, fileIndex);

                updated++;
            }
        }
        catch (Exception ex)
        {
            errors++;
            messages.Add($"Fehler beim IBAK Import: {ex.Message}");
        }

        project.ModifiedAtUtc = DateTime.UtcNow;
        project.Dirty = true;

        var stats = new ImportStats(found, 0, updated, errors, uncertain, messages);
        return Result<ImportStats>.Success(stats);
    }

    private static HaltungRecord? FindRecord(Project project, string key)
    {
        var exact = project.Data.FirstOrDefault(r =>
            string.Equals(NormalizeHoldingKey(r.GetFieldValue("Haltungsname")), key, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
            return exact;

        // Fallback: contains-match (IBAK exports can omit prefixes or format differently)
        foreach (var r in project.Data)
        {
            var v = NormalizeHoldingKey(r.GetFieldValue("Haltungsname"));
            if (string.IsNullOrWhiteSpace(v))
                continue;
            if (v.Contains(key, StringComparison.OrdinalIgnoreCase) || key.Contains(v, StringComparison.OrdinalIgnoreCase))
                return r;
        }
        return null;
    }

    private static void ApplyProtocol(HaltungRecord record, List<ProtocolEntry> entries, ProtocolService protocolService)
    {
        if (record.Protocol is null)
        {
            record.Protocol = protocolService.EnsureProtocol(record.GetFieldValue("Haltungsname") ?? "", entries, null);
            return;
        }

        if (record.Protocol.Current.Entries.Count == 0 && record.Protocol.Original.Entries.Count == 0)
        {
            record.Protocol = protocolService.EnsureProtocol(record.GetFieldValue("Haltungsname") ?? "", entries, null);
            return;
        }

        record.Protocol.History.Add(record.Protocol.Current);
        record.Protocol.Current = new ProtocolRevision
        {
            Comment = "Import (IBAK Daten.txt)",
            CreatedAt = DateTimeOffset.UtcNow,
            Entries = entries
        };
    }

    private static void UpdateFindings(HaltungRecord record, List<ProtocolEntry> entries)
    {
        record.VsaFindings ??= new List<VsaFinding>();

        foreach (var entry in entries)
        {
            var existing = record.VsaFindings.FirstOrDefault(f =>
                string.Equals(f.KanalSchadencode, entry.Code, StringComparison.OrdinalIgnoreCase)
                && f.MeterStart.HasValue && entry.MeterStart.HasValue
                && Math.Abs(f.MeterStart.Value - entry.MeterStart.Value) <= 0.01);

            if (existing is null)
            {
                existing = new VsaFinding
                {
                    KanalSchadencode = entry.Code
                };
                record.VsaFindings.Add(existing);
            }

            if (string.IsNullOrWhiteSpace(existing.Raw))
                existing.Raw = entry.Beschreibung;
            existing.MeterStart = entry.MeterStart;
            existing.MeterEnd = entry.MeterEnd;
            existing.MPEG = entry.Mpeg;
            if (entry.FotoPaths.Count > 0)
                existing.FotoPath = entry.FotoPaths[0];
        }
    }

    private static void LinkVideo(HaltungRecord record, string holdingKey, Dictionary<string, List<string>> index)
    {
        var target = $"L_{holdingKey}".Trim();
        var matches = index.Keys
            .Where(k => k.StartsWith(target, StringComparison.OrdinalIgnoreCase))
            .Where(k => k.EndsWith(".mpg", StringComparison.OrdinalIgnoreCase)
                        || k.EndsWith(".mpeg", StringComparison.OrdinalIgnoreCase)
                        || k.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)
                        || k.EndsWith(".avi", StringComparison.OrdinalIgnoreCase))
            .Select(k => ResolveFile(index, k))
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        if (matches.Count == 0)
            return;

        record.SetFieldValue("Link", matches[0]!, FieldSource.Legacy, userEdited: false);
    }

    private static void LinkHoldingPdf(HaltungRecord record, string holdingKey, Dictionary<string, List<string>> index)
    {
        var matches = index.Keys
            .Where(k => k.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            .Where(k => k.Contains(holdingKey, StringComparison.OrdinalIgnoreCase))
            .Select(k => ResolveFile(index, k))
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        if (matches.Count == 0)
            return;

        var first = matches[0]!;
        record.SetFieldValue("PDF_Path", first, FieldSource.Legacy, userEdited: false);
        if (matches.Count > 1)
            record.SetFieldValue("PDF_All", string.Join(";", matches), FieldSource.Legacy, userEdited: false);
    }

    private static List<IbakHolding> ParseDatenTxt(string dataPath, List<string> messages)
    {
        var holdings = new List<IbakHolding>();
        var current = (IbakHolding?)null;

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var lines = File.ReadAllLines(dataPath, Encoding.GetEncoding(1252));

        foreach (var raw in lines)
        {
            var line = raw?.TrimEnd() ?? "";
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (line.Length > 0 && !char.IsWhiteSpace(line[0]) && !ObservationRegex.IsMatch(line))
            {
                current = new IbakHolding(line.Trim());
                holdings.Add(current);
                continue;
            }

            if (current is null)
            {
                messages.Add($"IBAK: Beobachtung ohne Haltung ignoriert: {line}");
                continue;
            }

            var m = ObservationRegex.Match(line);
            if (!m.Success)
            {
                messages.Add($"IBAK: Zeile nicht erkannt: {line}");
                continue;
            }

            var timeText = m.Groups[1].Value.Trim();
            var meterText = m.Groups[2].Value.Trim();
            var code = m.Groups[3].Value.Trim();
            var descRaw = m.Groups[4].Value.Trim();

            var desc = StripIbakMeta(descRaw);
            var meter = ParseMeter(meterText);
            var time = ParseTime(timeText);

            var (isStart, isEnd, index) = ExtractRange(desc);
            var rangeKey = $"{code}|{index}";

            if (isStart)
            {
                var entry = BuildEntry(code, desc, meter, timeText, time);
                entry.IsStreckenschaden = true;
                current.PendingRanges[rangeKey] = entry;
                current.Entries.Add(entry);
                continue;
            }

            if (isEnd)
            {
                if (current.PendingRanges.TryGetValue(rangeKey, out var startEntry))
                {
                    startEntry.MeterEnd = meter ?? startEntry.MeterEnd;
                    startEntry.IsStreckenschaden = true;
                }
                else
                {
                    var entry = BuildEntry(code, desc, meter, timeText, time);
                    entry.IsStreckenschaden = true;
                    current.Entries.Add(entry);
                }
                continue;
            }

            current.Entries.Add(BuildEntry(code, desc, meter, timeText, time));
        }

        return holdings;
    }

    private static Dictionary<string, List<string>> LoadPhotoMap(
        string exportRoot,
        Dictionary<string, List<string>> fileIndex,
        List<string> messages)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        var fdbMap = TryLoadPhotoMapFromFdb(exportRoot, messages);
        if (fdbMap.Count > 0)
        {
            foreach (var kv in fdbMap)
                map[kv.Key] = kv.Value;
        }

        var fsMap = BuildPhotoMapFromFiles(fileIndex);
        foreach (var kv in fsMap)
        {
            if (!map.ContainsKey(kv.Key))
                map[kv.Key] = kv.Value;
        }

        return map;
    }

    private static Dictionary<string, List<string>> BuildPhotoMapFromFiles(Dictionary<string, List<string>> index)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var rx = new Regex(@"^L_(.+?)_(\d+)\.(jpg|jpeg|png|bmp)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        foreach (var fileName in index.Keys)
        {
            var m = rx.Match(fileName.Trim());
            if (!m.Success)
                continue;

            var holding = NormalizeHoldingKey(m.Groups[1].Value.Trim());
            if (!map.TryGetValue(holding, out var list))
            {
                list = new List<string>();
                map[holding] = list;
            }
            list.Add(fileName);
        }

        foreach (var kv in map)
        {
            kv.Value.Sort((a, b) => ExtractPhotoIndex(a).CompareTo(ExtractPhotoIndex(b)));
        }

        return map;
    }

    private static int ExtractPhotoIndex(string fileName)
    {
        var m = Regex.Match(fileName, @"_(\d+)\.(jpg|jpeg|png|bmp)$", RegexOptions.IgnoreCase);
        if (m.Success && int.TryParse(m.Groups[1].Value, out var n))
            return n;
        return int.MaxValue;
    }

    private static void ApplyPhotosToEntries(
        string holdingKey,
        List<ProtocolEntry> entries,
        Dictionary<string, List<string>> fileIndex,
        Dictionary<string, List<string>> photoMap,
        List<string> messages)
    {
        var key = NormalizeHoldingKey(holdingKey);
        if (!photoMap.TryGetValue(key, out var photoNames) || photoNames.Count == 0)
            return;

        var queue = new Queue<string>(photoNames
            .Select(name => ResolveFile(fileIndex, name))
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!)
            .Distinct(StringComparer.OrdinalIgnoreCase));

        if (queue.Count == 0)
            return;

        foreach (var entry in entries)
        {
            if (queue.Count == 0)
                break;

            if (!EntryWantsPhoto(entry))
                continue;

            var wantCount = ExtractPhotoCount(entry.Beschreibung);
            for (var i = 0; i < wantCount && queue.Count > 0; i++)
            {
                var photo = queue.Dequeue();
                if (!string.IsNullOrWhiteSpace(photo))
                    entry.FotoPaths.Add(photo);
            }
        }

        if (queue.Count > 0)
            messages.Add($"IBAK: Nicht zugeordnete Fotos fuer Haltung {holdingKey}: {queue.Count}");
    }

    private static bool EntryWantsPhoto(ProtocolEntry entry)
    {
        var text = entry.Beschreibung?.ToLowerInvariant() ?? "";
        if (text.Contains("foto") || text.Contains("fotobeispiel"))
            return true;
        return false;
    }

    private static int ExtractPhotoCount(string? desc)
    {
        var text = desc ?? "";
        var m = Regex.Match(text, @"foto\s*(\d+)", RegexOptions.IgnoreCase);
        if (m.Success && int.TryParse(m.Groups[1].Value, out var count) && count > 0)
            return Math.Min(count, 5);
        return 1;
    }

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
                UserID = "SYSDBA",
                Password = "masterkey",
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

            var sql = holdingCol is null
                ? $"SELECT {fileCol} FROM {photoTable}"
                : $"SELECT {holdingCol}, {fileCol} FROM {photoTable}";

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
        var m = Regex.Match(fileName, @"^L_(.+?)_(\d+)\.(jpg|jpeg|png|bmp)$", RegexOptions.IgnoreCase);
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

    private static ProtocolEntry BuildEntry(string code, string desc, double? meter, string? mpeg, TimeSpan? time)
    {
        return new ProtocolEntry
        {
            Code = code,
            Beschreibung = desc,
            MeterStart = meter,
            MeterEnd = meter,
            Mpeg = mpeg,
            Zeit = time,
            Source = ProtocolEntrySource.Imported
        };
    }

    private static (bool isStart, bool isEnd, string index) ExtractRange(string desc)
    {
        var lower = desc.ToLowerInvariant();
        var isStart = lower.Contains("anfang") || lower.Contains("beginn");
        var isEnd = lower.Contains("ende");
        var index = "0";
        var m = RangeIndexRegex.Match(desc);
        if (m.Success)
            index = m.Groups[1].Value;
        return (isStart, isEnd, index);
    }

    private static double? ParseMeter(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        var normalized = text.Replace(',', '.');
        if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return value;
        return null;
    }

    private static TimeSpan? ParseTime(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        if (TimeSpan.TryParseExact(text, @"hh\:mm\:ss", CultureInfo.InvariantCulture, out var ts))
            return ts;
        if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out ts))
            return ts;
        return null;
    }

    private static string StripIbakMeta(string text)
    {
        var idx = text.IndexOf("@!$ibak$!", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
            return text[..idx].Trim();
        return text.Trim();
    }

    private static Dictionary<string, List<string>> BuildFileIndex(string root)
    {
        var dict = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories))
        {
            var ext = Path.GetExtension(file);
            if (!MediaExtensions.Contains(ext))
                continue;

            var name = Path.GetFileName(file);
            if (!dict.TryGetValue(name, out var list))
            {
                list = new List<string>();
                dict[name] = list;
            }
            list.Add(file);
        }
        return dict;
    }

    private static string? ResolveFile(Dictionary<string, List<string>> index, string fileName)
    {
        if (!index.TryGetValue(fileName, out var list) || list.Count == 0)
            return null;
        if (list.Count == 1)
            return list[0];
        return list[0];
    }

    private static string? FindDatenTxt(string root)
    {
        var candidates = Directory.EnumerateFiles(root, "Daten.txt", SearchOption.AllDirectories)
            .ToList();

        if (candidates.Count == 0)
            return null;

        var preferred = candidates.FirstOrDefault(p => p.IndexOf(Path.DirectorySeparatorChar + "Film" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0);
        return preferred ?? candidates[0];
    }

    private static string NormalizeHoldingKey(string? value)
    {
        var v = (value ?? "").Trim();
        if (string.IsNullOrWhiteSpace(v))
            return string.Empty;
        v = Regex.Replace(v, @"\s+", string.Empty);
        v = v.Replace('/', '-');
        v = v.Replace('–', '-');
        v = v.Replace('—', '-');
        if (v.StartsWith("L_", StringComparison.OrdinalIgnoreCase))
            v = v[2..];
        return v;
    }

    private sealed record IbakHolding(string Holding)
    {
        public List<ProtocolEntry> Entries { get; } = new();
        public Dictionary<string, ProtocolEntry> PendingRanges { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
