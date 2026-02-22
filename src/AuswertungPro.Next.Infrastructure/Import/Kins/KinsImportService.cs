using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Infrastructure.Import.Kins;

/// <summary>
/// KINS kann je nach Exportformat IBAK- oder WinCan-aehnliche Strukturen enthalten.
/// Dieser Service erkennt die Struktur heuristisch und delegiert an bestehende Importer.
/// </summary>
public sealed class KinsImportService : IKinsImportService
{
    private static readonly Regex ObservationLineRegex = new(
        @"^\s*(?<meter>\d+(?:[.,]\d+)?)m\s+(?<text>.*?)(?:\s+@Pos=(?<pos>.*))?\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IWinCanDbImportService _winCanImport;
    private readonly IIbakImportService _ibakImport;

    public KinsImportService(IWinCanDbImportService winCanImport, IIbakImportService ibakImport)
    {
        _winCanImport = winCanImport ?? throw new ArgumentNullException(nameof(winCanImport));
        _ibakImport = ibakImport ?? throw new ArgumentNullException(nameof(ibakImport));
    }

    public Result<ImportStats> ImportKinsExport(string exportRoot, Project project)
    {
        if (project is null)
            return Result<ImportStats>.Fail("KINS_PROJECT_NULL", "Projekt ist null.");

        if (string.IsNullOrWhiteSpace(exportRoot) || !Directory.Exists(exportRoot))
            return Result<ImportStats>.Fail("KINS_ROOT_MISSING", "KINS Export-Ordner nicht gefunden.");

        var hasDb3 = HasAnyFile(exportRoot, "*.db3");
        var hasMdb = HasAnyFile(exportRoot, "*.mdb");
        var hasFdb = HasAnyFile(exportRoot, "*.fdb");
        var hasDatenTxt = HasFileNamed(exportRoot, "Daten.txt");
        var hasKiDvDataTxt = HasFileNamed(exportRoot, "kiDVDaten.txt");

        // Heuristik:
        // - KINS-TXT: kiDVDaten.txt vorhanden.
        // - WinCan: DB3 eindeutig, oder MDB ohne Daten.txt/FDB.
        // - IBAK: Daten.txt/FDB eindeutig.
        // - Bei gemischten Bestaenden werden beide Importer ausgefuehrt.
        var runKinsTxt = hasKiDvDataTxt;
        var runWinCan = hasDb3 || (hasMdb && !hasDatenTxt && !hasFdb && !hasKiDvDataTxt);
        var runIbak = hasDatenTxt || hasFdb;

        if (!runKinsTxt && !runWinCan && !runIbak)
        {
            // Unbekannte/uneindeutige Struktur -> beide als Fallback versuchen.
            runWinCan = true;
            runIbak = true;
        }

        var messages = new List<string>
        {
            "Importquelle: KINS",
            $"Erkennung: kiDVDaten.txt={hasKiDvDataTxt}, DB3={hasDb3}, MDB={hasMdb}, FDB={hasFdb}, Daten.txt={hasDatenTxt}",
            $"Strategie: KINS-TXT={(runKinsTxt ? "ja" : "nein")}, WinCan={(runWinCan ? "ja" : "nein")}, IBAK={(runIbak ? "ja" : "nein")}"
        };

        var found = 0;
        var created = 0;
        var updated = 0;
        var errors = 0;
        var uncertain = 0;

        var executed = 0;
        var successCount = 0;

        if (runKinsTxt)
        {
            executed++;
            MergeResult("KINS-TXT", ImportKinsDvdText(exportRoot, project), messages,
                ref found, ref created, ref updated, ref errors, ref uncertain, ref successCount);
        }

        if (runWinCan)
        {
            executed++;
            MergeResult("WinCan", _winCanImport.ImportWinCanExport(exportRoot, project), messages,
                ref found, ref created, ref updated, ref errors, ref uncertain, ref successCount);
        }

        if (runIbak)
        {
            executed++;
            MergeResult("IBAK", _ibakImport.ImportIbakExport(exportRoot, project), messages,
                ref found, ref created, ref updated, ref errors, ref uncertain, ref successCount);
        }

        if (executed == 0)
            return Result<ImportStats>.Fail("KINS_NO_STRATEGY", "Keine KINS-Importstrategie ausgewaehlt.");

        if (successCount == 0)
        {
            var last = messages.LastOrDefault(m => m.StartsWith("Fehler:", StringComparison.OrdinalIgnoreCase))
                       ?? "Kein kompatibles KINS-Format erkannt.";
            return Result<ImportStats>.Fail("KINS_IMPORT_FAILED", last);
        }

        var dedupedMessages = Deduplicate(messages);
        var stats = new ImportStats(found, created, updated, errors, uncertain, dedupedMessages);
        return Result<ImportStats>.Success(stats);
    }

    private static Result<ImportStats> ImportKinsDvdText(string exportRoot, Project project)
    {
        var dataFiles = EnumerateFilesSafe(exportRoot, "kiDVDaten.txt");
        if (dataFiles.Count == 0)
            return Result<ImportStats>.Fail("KINS_TXT_MISSING", "KINS kiDVDaten.txt nicht gefunden.");

        var messages = new List<string>
        {
            $"KINS-TXT Dateien gefunden: {dataFiles.Count}"
        };

        var videoIndex = BuildVideoIndex(exportRoot);
        var protocolService = new ProtocolService();
        var recordingDate = TryReadRecordingDate(exportRoot);

        var found = 0;
        var created = 0;
        var updated = 0;
        var errors = 0;
        var uncertain = 0;

        foreach (var dataFile in dataFiles)
        {
            try
            {
                var lines = ReadTextLines(dataFile);
                KinsHoldingHeader? currentHeader = null;
                var currentEntries = new List<ProtocolEntry>();

                void FlushCurrent()
                {
                    if (currentHeader is null)
                        return;

                    var header = currentHeader.Value;
                    var holdingName = $"{header.From}-{header.To}";
                    var record = FindRecord(project, holdingName);
                    if (record is null)
                    {
                        record = project.CreateNewRecord();
                        project.AddRecord(record);
                        created++;
                    }

                    found++;
                    ApplyImportedField(record, "Haltungsname", holdingName);
                    ApplyImportedField(record, "Nutzungsart", header.Usage);
                    ApplyImportedField(record, "Rohrmaterial", header.Material);
                    if (!string.IsNullOrWhiteSpace(header.Diameter))
                        ApplyImportedField(record, "DN_mm", header.Diameter);
                    if (recordingDate.HasValue)
                        ApplyImportedField(record, "Datum_Jahr", recordingDate.Value.ToString("yyyy", CultureInfo.InvariantCulture));

                    var maxMeter = currentEntries
                        .Select(e => e.MeterEnd ?? e.MeterStart)
                        .Where(v => v.HasValue)
                        .Select(v => v!.Value)
                        .DefaultIfEmpty()
                        .Max();
                    if (maxMeter > 0)
                        ApplyImportedField(record, "Haltungslaenge_m", maxMeter.ToString("0.0", CultureInfo.InvariantCulture));

                    var videoPath = ResolveVideoPath(videoIndex, header.VideoFile);
                    if (!string.IsNullOrWhiteSpace(videoPath))
                    {
                        ApplyImportedField(record, "Link", videoPath);
                    }
                    else
                    {
                        uncertain++;
                        messages.Add($"KINS-TXT: Video nicht gefunden fuer {holdingName}: {header.VideoFile}");
                    }

                    if (currentEntries.Count == 0)
                    {
                        uncertain++;
                        messages.Add($"KINS-TXT: Keine Beobachtungen fuer {holdingName} in {Path.GetFileName(dataFile)}");
                    }

                    ApplyProtocol(record, currentEntries, protocolService, $"Import (KINS kiDVDaten.txt: {Path.GetFileName(dataFile)})");
                    updated++;

                    currentHeader = null;
                    currentEntries = new List<ProtocolEntry>();
                }

                foreach (var rawLine in lines)
                {
                    var line = rawLine?.TrimEnd() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    if (TryParseHeaderLine(line, out var header))
                    {
                        FlushCurrent();
                        currentHeader = header;
                        continue;
                    }

                    if (currentHeader is null)
                        continue;

                    if (TryParseObservationLine(line, out var entry))
                        currentEntries.Add(entry);
                }

                FlushCurrent();
            }
            catch (Exception ex)
            {
                errors++;
                messages.Add($"KINS-TXT Fehler in {Path.GetFileName(dataFile)}: {ex.Message}");
            }
        }

        if (found == 0)
            return Result<ImportStats>.Fail("KINS_TXT_NO_HOLDINGS", "kiDVDaten.txt gefunden, aber keine Haltungen erkannt.");

        project.ModifiedAtUtc = DateTime.UtcNow;
        project.Dirty = true;

        var deduped = Deduplicate(messages);
        return Result<ImportStats>.Success(new ImportStats(found, created, updated, errors, uncertain, deduped));
    }

    private static void MergeResult(
        string source,
        Result<ImportStats> result,
        List<string> messages,
        ref int found,
        ref int created,
        ref int updated,
        ref int errors,
        ref int uncertain,
        ref int successCount)
    {
        if (!result.Ok || result.Value is null)
        {
            var reason = string.IsNullOrWhiteSpace(result.ErrorMessage)
                ? "unbekannter Fehler"
                : result.ErrorMessage;
            messages.Add($"Fehler: {source} Import fehlgeschlagen: {reason}");
            return;
        }

        successCount++;
        var s = result.Value;
        found += s.Found;
        created += s.Created;
        updated += s.Updated;
        errors += s.Errors;
        uncertain += s.Uncertain;

        messages.Add($"{source}: Gefunden {s.Found}, Neu {s.Created}, Aktualisiert {s.Updated}, Unklar {s.Uncertain}, Fehler {s.Errors}");
        foreach (var m in s.Messages)
            messages.Add($"{source}: {m}");
    }

    private static bool HasAnyFile(string root, string pattern)
    {
        try
        {
            return Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories).Any();
        }
        catch
        {
            return false;
        }
    }

    private static bool HasFileNamed(string root, string fileName)
    {
        try
        {
            return Directory.EnumerateFiles(root, fileName, SearchOption.AllDirectories).Any();
        }
        catch
        {
            return false;
        }
    }

    private static IReadOnlyList<string> Deduplicate(IEnumerable<string> messages)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ordered = new List<string>();
        foreach (var message in messages)
        {
            var text = (message ?? string.Empty).Trim();
            if (text.Length == 0)
                continue;
            if (set.Add(text))
                ordered.Add(text);
        }
        return ordered;
    }

    private static List<string> EnumerateFilesSafe(string root, string pattern)
    {
        try
        {
            return Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories).ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static List<string> ReadTextLines(string path)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        try
        {
            return File.ReadAllLines(path, Encoding.GetEncoding(1252)).ToList();
        }
        catch
        {
            return File.ReadAllLines(path, Encoding.UTF8).ToList();
        }
    }

    private static bool TryParseHeaderLine(string line, out KinsHoldingHeader header)
    {
        header = default;
        if (string.IsNullOrWhiteSpace(line))
            return false;

        var marker = line.IndexOf("@Datei=", StringComparison.OrdinalIgnoreCase);
        if (marker < 0)
            return false;

        var prefix = line[..marker].Trim();
        var videoFile = line[(marker + "@Datei=".Length)..].Trim();
        if (string.IsNullOrWhiteSpace(videoFile))
            return false;

        var arrowIndex = prefix.IndexOf("->", StringComparison.Ordinal);
        if (arrowIndex < 0)
            return false;

        var left = prefix[..arrowIndex].Trim();
        var right = prefix[(arrowIndex + 2)..].Trim();

        var leftTokens = Tokenize(left);
        if (leftTokens.Length < 2)
            return false;

        var rightTokens = Tokenize(right);
        if (rightTokens.Length < 1)
            return false;

        var usage = leftTokens[0];
        var from = leftTokens[1];
        var to = rightTokens[0];

        string material = string.Empty;
        string? diameter = null;

        if (rightTokens.Length > 1)
        {
            var tail = rightTokens.Skip(1).ToList();
            if (tail.Count > 0 && int.TryParse(tail[^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                diameter = tail[^1];
                tail.RemoveAt(tail.Count - 1);
            }

            material = string.Join(" ", tail);
        }

        header = new KinsHoldingHeader(usage, from, to, material, diameter, videoFile);
        return true;
    }

    private static bool TryParseObservationLine(string line, out ProtocolEntry entry)
    {
        entry = new ProtocolEntry
        {
            Source = ProtocolEntrySource.Imported
        };

        var match = ObservationLineRegex.Match(line ?? string.Empty);
        if (!match.Success)
            return false;

        var meterText = match.Groups["meter"].Value.Trim().Replace(',', '.');
        if (!double.TryParse(meterText, NumberStyles.Float, CultureInfo.InvariantCulture, out var meter))
            return false;

        var description = match.Groups["text"].Value.Trim();
        var pos = match.Groups["pos"].Success ? match.Groups["pos"].Value.Trim() : string.Empty;

        entry.Code = string.Empty;
        entry.Beschreibung = description;
        entry.MeterStart = meter;
        entry.MeterEnd = meter;
        entry.IsStreckenschaden = false;
        entry.Mpeg = string.IsNullOrWhiteSpace(pos) ? null : pos;
        entry.Zeit = ParseKinsTime(pos);

        return true;
    }

    private static TimeSpan? ParseKinsTime(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var value = text.Trim();
        var formats = new[] { @"h\:mm\:ss", @"hh\:mm\:ss", @"m\:ss", @"mm\:ss" };
        if (TimeSpan.TryParseExact(value, formats, CultureInfo.InvariantCulture, out var ts))
            return ts;

        if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out ts))
            return ts;

        return null;
    }

    private static string[] Tokenize(string value)
        => Regex.Split(value?.Trim() ?? string.Empty, @"\s+")
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToArray();

    private static Dictionary<string, List<string>> BuildVideoIndex(string root)
    {
        var index = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mpg", ".mpeg", ".mp4", ".avi", ".mov"
        };

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories);
        }
        catch
        {
            return index;
        }

        foreach (var file in files)
        {
            var ext = Path.GetExtension(file);
            if (!extensions.Contains(ext))
                continue;

            var name = Path.GetFileName(file);
            if (!index.TryGetValue(name, out var list))
            {
                list = new List<string>();
                index[name] = list;
            }
            list.Add(file);
        }

        return index;
    }

    private static string? ResolveVideoPath(Dictionary<string, List<string>> index, string fileName)
    {
        var key = Path.GetFileName(fileName.Trim());
        if (index.TryGetValue(key, out var list) && list.Count > 0)
            return list[0];

        return null;
    }

    private static DateTime? TryReadRecordingDate(string exportRoot)
    {
        var infoFile = EnumerateFilesSafe(exportRoot, "kiDVinfo.txt").FirstOrDefault();
        if (string.IsNullOrWhiteSpace(infoFile))
            return null;

        foreach (var line in ReadTextLines(infoFile))
        {
            if (line.IndexOf("Aufnahmen", StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            var m = Regex.Match(line, @"(?<d>\d{2}\.\d{2}\.\d{2,4})");
            if (!m.Success)
                continue;

            var dateText = m.Groups["d"].Value;
            if (DateTime.TryParseExact(dateText, new[] { "dd.MM.yy", "dd.MM.yyyy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                return dt;
        }

        return null;
    }

    private static HaltungRecord? FindRecord(Project project, string holdingName)
    {
        var key = NormalizeHoldingKey(holdingName);
        var exact = project.Data.FirstOrDefault(r =>
            string.Equals(NormalizeHoldingKey(r.GetFieldValue("Haltungsname")), key, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
            return exact;

        foreach (var record in project.Data)
        {
            var candidate = NormalizeHoldingKey(record.GetFieldValue("Haltungsname"));
            if (string.IsNullOrWhiteSpace(candidate))
                continue;
            if (candidate.Contains(key, StringComparison.OrdinalIgnoreCase) || key.Contains(candidate, StringComparison.OrdinalIgnoreCase))
                return record;
        }

        return null;
    }

    private static string NormalizeHoldingKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Trim().Replace(" ", string.Empty);
        return normalized.ToUpperInvariant();
    }

    private static bool ApplyImportedField(HaltungRecord record, string fieldName, string? value)
    {
        var normalized = (value ?? string.Empty).Trim();
        var before = (record.GetFieldValue(fieldName) ?? string.Empty).Trim();
        if (string.Equals(before, normalized, StringComparison.Ordinal))
            return false;

        record.SetFieldValue(fieldName, normalized, FieldSource.Legacy, userEdited: false);
        return true;
    }

    private static void ApplyProtocol(HaltungRecord record, List<ProtocolEntry> entries, ProtocolService protocolService, string comment)
    {
        var cloned = entries.Select(CloneEntry).ToList();

        if (record.Protocol is null)
        {
            record.Protocol = protocolService.EnsureProtocol(record.GetFieldValue("Haltungsname") ?? string.Empty, cloned, null);
            return;
        }

        if (record.Protocol.Current.Entries.Count == 0 && record.Protocol.Original.Entries.Count == 0)
        {
            record.Protocol = protocolService.EnsureProtocol(record.GetFieldValue("Haltungsname") ?? string.Empty, cloned, null);
            return;
        }

        record.Protocol.History.Add(record.Protocol.Current);
        record.Protocol.Current = new ProtocolRevision
        {
            Comment = comment,
            CreatedAt = DateTimeOffset.UtcNow,
            Entries = cloned
        };
    }

    private static ProtocolEntry CloneEntry(ProtocolEntry e)
    {
        return new ProtocolEntry
        {
            EntryId = e.EntryId,
            Code = e.Code,
            Beschreibung = e.Beschreibung,
            MeterStart = e.MeterStart,
            MeterEnd = e.MeterEnd,
            IsStreckenschaden = e.IsStreckenschaden,
            Mpeg = e.Mpeg,
            Zeit = e.Zeit,
            FotoPaths = new List<string>(e.FotoPaths),
            Source = e.Source,
            IsDeleted = e.IsDeleted
        };
    }

    private readonly record struct KinsHoldingHeader(
        string Usage,
        string From,
        string To,
        string Material,
        string? Diameter,
        string VideoFile);
}
