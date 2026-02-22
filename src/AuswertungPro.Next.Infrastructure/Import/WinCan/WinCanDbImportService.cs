using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Import.WinCan;

public sealed class WinCanDbImportService : IWinCanDbImportService
{
    private static readonly HashSet<string> MediaExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mpg", ".mpeg", ".mp4", ".avi", ".mov",
        ".jpg", ".jpeg", ".png", ".bmp",
        ".pdf"
    };

    public Result<ImportStats> ImportWinCanExport(string exportRoot, Project project)
    {
        if (string.IsNullOrWhiteSpace(exportRoot) || !Directory.Exists(exportRoot))
            return Result<ImportStats>.Fail("WINCAN_ROOT_MISSING", "Export-Ordner nicht gefunden.");

        var dbPath = FindDb3(exportRoot);
        if (string.IsNullOrWhiteSpace(dbPath))
            return ImportWithoutDb3(exportRoot, project, "WinCan DB3 nicht gefunden. Fallback auf MDB.");

        var messages = new List<string>();
        messages.Add($"Importquelle: WinCan DB3 ({Path.GetFileName(dbPath)})");
        var found = 0;
        var updated = 0;
        var errors = 0;
        var uncertain = 0;
        var created = 0;

        var fileIndex = BuildFileIndex(exportRoot);
        var protocolService = new ProtocolService();

        var db3ImportFailed = false;
        try
        {
            using var conn = new SqliteConnection($"Data Source={dbPath};");
            conn.Open();

            var sections = LoadSections(conn);
            var inspections = LoadInspections(conn);
            var obsByInspection = LoadObservations(conn);
            var mediaByObs = LoadObservationMedia(conn);
            var nodes = LoadNodes(conn);

            foreach (var section in sections)
            {
                if (string.IsNullOrWhiteSpace(section.Key))
                    continue;

                var key = NormalizeHoldingKey(section.Key);
                var record = project.Data.FirstOrDefault(r =>
                    string.Equals(NormalizeHoldingKey(r.GetFieldValue("Haltungsname")), key, StringComparison.OrdinalIgnoreCase));
                if (record is null)
                {
                    record = project.CreateNewRecord();
                    record.SetFieldValue("Haltungsname", section.Key, FieldSource.Legacy, userEdited: false);
                    project.AddRecord(record);
                    created++;
                    messages.Add($"Haltung neu angelegt: {section.Key}");
                }

                found++;

                var inspection = inspections
                    .Where(i => i.SectionFk == section.Pk)
                    .OrderByDescending(i => i.SortKey)
                    .FirstOrDefault();

                ApplySectionFields(record, section, inspection);

                if (inspection is null)
                {
                    uncertain++;
                    messages.Add($"Keine Inspektion in DB fuer Haltung {section.Key}");
                    continue;
                }

                if (!obsByInspection.TryGetValue(inspection.Pk, out var obsList) || obsList.Count == 0)
                {
                    uncertain++;
                    messages.Add($"Keine Beobachtungen in DB fuer Haltung {section.Key}");
                    continue;
                }

                var entries = new List<ProtocolEntry>();
                foreach (var obs in obsList.OrderBy(o => o.SortOrder))
                {
                    var entry = new ProtocolEntry
                    {
                        Code = obs.OpCode ?? "",
                        Beschreibung = obs.Observation ?? "",
                        MeterStart = obs.Distance,
                        MeterEnd = obs.Distance.HasValue && obs.ContDefectLength.HasValue && obs.ContDefectLength.Value > 0
                            ? obs.Distance.Value + obs.ContDefectLength.Value
                            : obs.Distance,
                        IsStreckenschaden = obs.ContDefectLength.HasValue && obs.ContDefectLength.Value > 0,
                        Mpeg = obs.TimeCtr,
                        Zeit = ParseTimeSpan(obs.TimeCtr),
                        Source = ProtocolEntrySource.Imported
                    };

                    var parameters = BuildObsParameters(obs);
                    if (parameters.Count > 0)
                    {
                        entry.CodeMeta = new ProtocolEntryCodeMeta
                        {
                            Code = entry.Code,
                            Parameters = parameters,
                            UpdatedAt = DateTimeOffset.UtcNow
                        };
                    }

                    if (mediaByObs.TryGetValue(obs.Pk, out var mediaList))
                    {
                        foreach (var media in mediaList)
                        {
                            if (string.IsNullOrWhiteSpace(media.FileName))
                                continue;

                            if (IsVideo(media.FileType))
                            {
                                var videoPath = ResolveFile(fileIndex, media.FileName);
                                if (!string.IsNullOrWhiteSpace(videoPath))
                                    record.SetFieldValue("Link", videoPath, FieldSource.Legacy, userEdited: false);
                            }
                            else if (IsImage(media.FileType))
                            {
                                var photoPath = ResolveFile(fileIndex, media.FileName);
                                if (!string.IsNullOrWhiteSpace(photoPath))
                                    entry.FotoPaths.Add(photoPath);
                            }
                        }
                    }

                    entries.Add(entry);
                }

                ApplyProtocol(record, entries, protocolService);
                UpdateFindings(record, entries);
                LinkSectionPdf(record, section.Key, fileIndex);

                updated++;
            }

            ImportNodes(project, nodes, fileIndex, messages, ref found, ref created, ref updated, ref uncertain);
        }
        catch (Exception ex)
        {
            db3ImportFailed = true;
            errors++;
            messages.Add($"Fehler beim WinCan-DB Import: {ex.Message}");
        }

        if (db3ImportFailed || found == 0)
        {
            var fallbackReason = db3ImportFailed
                ? "WinCan DB3 Import fehlgeschlagen. Versuche MDB-Fallback."
                : "WinCan DB3 ohne auswertbare Haltungsdaten. Versuche MDB-Fallback.";

            var fallback = ImportWithoutDb3(exportRoot, project, fallbackReason, failWhenNoMdb: false);
            if (fallback.Ok && fallback.Value is not null && fallback.Value.Found > 0)
            {
                found += fallback.Value.Found;
                created += fallback.Value.Created;
                updated += fallback.Value.Updated;
                errors += fallback.Value.Errors;
                uncertain += fallback.Value.Uncertain;
                messages.AddRange(fallback.Value.Messages);
            }
            else if (fallback.Ok && fallback.Value is not null)
            {
                messages.AddRange(fallback.Value.Messages);
            }
            else if (!string.IsNullOrWhiteSpace(fallback.ErrorMessage))
            {
                messages.Add($"MDB-Fallback fehlgeschlagen: {fallback.ErrorMessage}");
            }
        }

        project.ModifiedAtUtc = DateTime.UtcNow;
        project.Dirty = true;

        var stats = new ImportStats(found, created, updated, errors, uncertain, messages);
        return Result<ImportStats>.Success(stats);
    }

    private Result<ImportStats> ImportWithoutDb3(
        string exportRoot,
        Project project,
        string reasonMessage,
        bool failWhenNoMdb = true)
    {
        var mdbPaths = FindMdbCandidates(exportRoot);
        if (mdbPaths.Count == 0)
        {
            if (failWhenNoMdb)
                return Result<ImportStats>.Fail("WINCAN_DB_MISSING", "Keine WinCan DB3- oder MDB-Datei im Export gefunden.");

            return Result<ImportStats>.Success(new ImportStats(0, 0, 0, 0, 0, new[] { "Keine MDB-Datei fuer Fallback gefunden." }));
        }

        var messages = new List<string>
        {
            $"Importquelle: WinCan MDB-Fallback ({mdbPaths.Count} Datei(en) geprueft)",
            reasonMessage
        };

        var fileIndex = BuildFileIndex(exportRoot);
        var importedByHolding = new Dictionary<string, HaltungRecord>(StringComparer.OrdinalIgnoreCase);
        var found = 0;
        var created = 0;
        var updated = 0;
        var errors = 0;
        var uncertain = 0;

        var parsedFiles = 0;
        foreach (var mdbPath in mdbPaths)
        {
            if (!M150MdbImportHelper.TryParseMdbFile(mdbPath, out var records, out var parseError, out var warnings))
            {
                errors++;
                messages.Add($"MDB konnte nicht gelesen werden: {Path.GetFileName(mdbPath)} ({parseError ?? "unbekannter Fehler"})");
                continue;
            }

            parsedFiles++;
            messages.Add($"MDB gelesen: {Path.GetFileName(mdbPath)} ({records.Count} Haltungen)");
            messages.AddRange(warnings.Take(5).Select(w => $"{Path.GetFileName(mdbPath)}: {w}"));

            foreach (var imported in records)
            {
                var key = NormalizeHoldingKey(imported.GetFieldValue("Haltungsname"));
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                if (!importedByHolding.TryGetValue(key, out var existing))
                {
                    importedByHolding[key] = imported;
                    continue;
                }

                MergeImportedCandidate(existing, imported);
            }
        }

        if (importedByHolding.Count == 0)
        {
            if (failWhenNoMdb)
                return Result<ImportStats>.Fail("WINCAN_MDB_IMPORT_FAILED", "MDB vorhanden, aber keine verwertbaren Haltungsdaten gefunden.");

            messages.Add("MDB-Fallback: keine verwertbaren Haltungsdaten gefunden.");
            return Result<ImportStats>.Success(new ImportStats(0, 0, 0, errors, 0, messages));
        }

        foreach (var imported in importedByHolding.Values)
        {
            var key = NormalizeHoldingKey(imported.GetFieldValue("Haltungsname"));
            var target = project.Data.FirstOrDefault(r =>
                string.Equals(NormalizeHoldingKey(r.GetFieldValue("Haltungsname")), key, StringComparison.OrdinalIgnoreCase));

            var isNew = target is null;
            if (target is null)
            {
                target = project.CreateNewRecord();
                target.SetFieldValue("Haltungsname", key, FieldSource.Legacy, userEdited: false);
                project.AddRecord(target);
                created++;
            }

            found++;
            var changed = false;
            changed |= ApplyImportedField(target, "Datum_Jahr", imported.GetFieldValue("Datum_Jahr"));
            changed |= ApplyImportedField(target, "Haltungslaenge_m", imported.GetFieldValue("Haltungslaenge_m"));
            changed |= ApplyImportedField(target, "DN_mm", imported.GetFieldValue("DN_mm"));
            changed |= ApplyImportedField(target, "Rohrmaterial", imported.GetFieldValue("Rohrmaterial"));
            changed |= ApplyImportedField(target, "Inspektionsrichtung", imported.GetFieldValue("Inspektionsrichtung"));
            changed |= ApplyImportedField(target, "Bemerkungen", imported.GetFieldValue("Bemerkungen"));

            var rawLink = imported.GetFieldValue("Link");
            if (!string.IsNullOrWhiteSpace(rawLink))
            {
                var resolvedLink = ResolveFile(fileIndex, Path.GetFileName(rawLink)) ?? ResolveFile(fileIndex, rawLink);
                changed |= ApplyImportedField(target, "Link", resolvedLink ?? rawLink);
            }
            else
            {
                uncertain++;
            }

            if (!isNew && changed)
                updated++;
        }

        project.ModifiedAtUtc = DateTime.UtcNow;
        project.Dirty = true;

        messages.Add($"WinCan MDB-Fallback: {found} Haltungen verarbeitet, {created} neu, {updated} aktualisiert (MDB-Dateien: {parsedFiles}/{mdbPaths.Count}).");
        var stats = new ImportStats(found, created, updated, errors, uncertain, messages);
        return Result<ImportStats>.Success(stats);
    }

    private static Dictionary<string, List<string>> BuildFileIndex(string root)
    {
        var dict = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in GetMediaRoots(root))
        {
            foreach (var file in Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories))
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
        }
        return dict;
    }

    private static IEnumerable<string> GetMediaRoots(string root)
    {
        var candidates = new[]
        {
            root,
            Path.Combine(root, "Video"),
            Path.Combine(root, "Picture"),
            Path.Combine(root, "Pictures"),
            Path.Combine(root, "Foto"),
            Path.Combine(root, "Fotos"),
            Path.Combine(root, "Film"),
            Path.Combine(root, "Report"),
            Path.Combine(root, "Reports"),
            Path.Combine(root, "PDF"),
            Path.Combine(root, "Dokumente")
        };

        foreach (var dir in candidates)
            if (Directory.Exists(dir))
                yield return dir;
    }

    private static string? ResolveFile(Dictionary<string, List<string>> index, string fileName)
    {
        if (!index.TryGetValue(fileName, out var list) || list.Count == 0)
            return null;

        if (list.Count == 1)
            return list[0];

        // Prefer Video or Picture folders when ambiguous.
        var best = list.FirstOrDefault(p => p.IndexOf(Path.DirectorySeparatorChar + "Video" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0)
                   ?? list.FirstOrDefault(p => p.IndexOf(Path.DirectorySeparatorChar + "Picture" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0)
                   ?? list[0];
        return best;
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
            Comment = "Import (WinCan DB)",
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

    private static void LinkSectionPdf(HaltungRecord record, string sectionKey, Dictionary<string, List<string>> index)
    {
        var key = sectionKey;
        var matches = index.Keys
            .Where(k => k.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            .Where(k => k.Contains(sectionKey, StringComparison.OrdinalIgnoreCase))
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

    private static void ApplySectionFields(HaltungRecord record, DbSection section, DbInspection? inspection)
    {
        ApplyField(record, "Strasse", section.Street);
        ApplyField(record, "Rohrmaterial", NormalizeMaterial(section.Material));
        ApplyField(record, "DN_mm", NormalizeNumber(section.Size1) ?? NormalizeNumber(section.PipeHeightOrDia));
        ApplyField(record, "Haltungslaenge_m", NormalizeNumber(section.Length) ?? NormalizeNumber(section.RealLength) ?? NormalizeNumber(section.PipeLength));
        ApplyField(record, "Nutzungsart", NormalizeUsage(section.Usage));
        ApplyField(record, "Eigentuemer", section.Ownership);
        ApplyField(record, "Bemerkungen", section.Memo);
        ApplyField(record, "Datum_Jahr", NormalizeDate(section.ConstructionYearText, section.ConstructionDate));
        ApplyField(record, "Inspektionsrichtung", NormalizeInspectionDir(inspection?.InspectionDir));
    }

    private static void ImportNodes(
        Project project,
        List<DbNode> nodes,
        Dictionary<string, List<string>> index,
        List<string> messages,
        ref int found,
        ref int created,
        ref int updated,
        ref int uncertain)
    {
        if (nodes.Count == 0)
            return;

        foreach (var node in nodes)
        {
            var rawKey = node.Key ?? node.Number ?? string.Empty;
            var key = NormalizeHoldingKey(rawKey);
            if (string.IsNullOrWhiteSpace(key))
            {
                uncertain++;
                messages.Add("Schacht ohne Nummer in DB gefunden (ignoriert).");
                continue;
            }

            var record = FindSchachtRecord(project.SchaechteData, key);
            if (record is null)
            {
                record = new SchachtRecord();
                project.SchaechteData.Add(record);
                created++;
                messages.Add($"Schacht neu angelegt: {rawKey}");
            }

            found++;
            ApplyNodeFields(record, node);
            LinkNodePdf(record, rawKey, index);
            updated++;
        }
    }

    private static SchachtRecord? FindSchachtRecord(IEnumerable<SchachtRecord> records, string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        foreach (var record in records)
        {
            foreach (var field in SchachtKeyFields)
            {
                var value = record.GetFieldValue(field);
                if (string.IsNullOrWhiteSpace(value))
                    continue;
                if (string.Equals(NormalizeHoldingKey(value), key, StringComparison.OrdinalIgnoreCase))
                    return record;
            }
        }

        return null;
    }

    private static void ApplyNodeFields(SchachtRecord record, DbNode node)
    {
        SetSchachtField(record, "Schachtnummer", node.Key ?? node.Number);
        SetSchachtField(record, "Funktion", node.Type ?? node.NodeType ?? node.Usage);
        SetSchachtField(record, "Strasse", node.Street ?? node.Locality);
        SetSchachtField(record, "Eigentümer", node.Ownership ?? node.LandOwner);
        SetSchachtField(record, "Bemerkungen", node.Memo);
        SetSchachtField(record, "Zustandsklasse", NormalizeNumber(node.Condition));
        SetSchachtField(record, "Abdeckung Stk.", NormalizeNumber(node.CoversCount));
        SetSchachtField(record, "Status", node.State);
        SetSchachtField(record, "offen/abgeschlossen", NormalizeAccessible(node.Accessible));
        SetSchachtField(record, "Ausführung", node.ConstructionStyle);
        SetSchachtField(record, "Datum/Jahr", NormalizeDate(node.ConstructionYearText, node.ConstructionDate));
    }

    private static void LinkNodePdf(SchachtRecord record, string nodeKey, Dictionary<string, List<string>> index)
    {
        if (string.IsNullOrWhiteSpace(nodeKey))
            return;

        var matches = index.Keys
            .Where(k => k.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            .Where(k => k.Contains(nodeKey, StringComparison.OrdinalIgnoreCase))
            .Select(k => ResolveFile(index, k))
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        if (matches.Count == 0)
            return;

        var first = matches[0]!;
        SetSchachtField(record, "Link", first);
    }

    private static Dictionary<string, string> BuildObsParameters(DbObservation obs)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        AddIfValue(dict, "Q1", obs.Q1);
        AddIfValue(dict, "Q2", obs.Q2);
        AddIfValue(dict, "Q3", obs.Q3);
        AddIfValue(dict, "U1", obs.U1);
        AddIfValue(dict, "U2", obs.U2);
        AddIfValue(dict, "U3", obs.U3);
        AddIfValue(dict, "Char1", obs.Char1);
        AddIfValue(dict, "Char2", obs.Char2);
        AddIfValue(dict, "C1", obs.C1);
        AddIfValue(dict, "C2", obs.C2);
        AddIfValue(dict, "ClockPos1", obs.ClockPos1);
        AddIfValue(dict, "ClockPos2", obs.ClockPos2);
        return dict;
    }

    private static void AddIfValue(Dictionary<string, string> dict, string key, object? value)
    {
        if (value is null)
            return;
        var text = value.ToString();
        if (string.IsNullOrWhiteSpace(text))
            return;
        dict[key] = text.Trim();
    }

    private static bool IsImage(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
            return false;
        var t = type.Trim().ToUpperInvariant();
        return t is "JPG" or "JPEG" or "PNG" or "BMP";
    }

    private static bool IsVideo(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
            return false;
        var t = type.Trim().ToUpperInvariant();
        return t is "MPG" or "MPEG" or "MP4" or "AVI" or "MOV";
    }

    private static TimeSpan? ParseTimeSpan(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var text = value.Trim();
        var formats = new[] { @"hh\:mm\:ss", @"mm\:ss", @"hh\:mm\:ss\.ff", @"hh\:mm\:ss\.fff", @"mm\:ss\.ff", @"mm\:ss\.fff" };
        if (TimeSpan.TryParseExact(text, formats, CultureInfo.InvariantCulture, out var parsed))
            return parsed;
        if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out parsed))
            return parsed;
        return null;
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
        return v;
    }

    private static string? FindDb3(string exportRoot)
    {
        var candidates = Directory.EnumerateFiles(exportRoot, "*.db3", SearchOption.AllDirectories)
            .Where(p => p.IndexOf(Path.DirectorySeparatorChar + "DB" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0)
            .ToList();

        if (candidates.Count == 0)
            return null;

        return candidates
            .Select(p => new FileInfo(p))
            .OrderByDescending(fi => fi.Length)
            .First().FullName;
    }

    private static IReadOnlyList<string> FindMdbCandidates(string exportRoot)
    {
        static int Rank(string path)
        {
            var score = 0;
            if (path.IndexOf($"{Path.DirectorySeparatorChar}Projects{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) >= 0)
                score += 100;
            if (path.IndexOf($"{Path.DirectorySeparatorChar}DB{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) >= 0)
                score += 50;
            if (path.IndexOf("viewer", StringComparison.OrdinalIgnoreCase) >= 0)
                score += 25;
            return score;
        }

        var candidates = Directory.EnumerateFiles(exportRoot, "*.mdb", SearchOption.AllDirectories).ToList();
        if (candidates.Count == 0)
            return Array.Empty<string>();

        var ordered = candidates
            .Select(p => new FileInfo(p))
            .OrderByDescending(fi => Rank(fi.FullName))
            .ThenByDescending(fi => fi.Length)
            .ToList();

        var unique = new List<string>();
        var seenSignatures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in ordered)
        {
            var signature = $"{file.Name}|{file.Length}";
            if (!seenSignatures.Add(signature))
                continue;

            unique.Add(file.FullName);
        }

        return unique;
    }

    private static List<DbSection> LoadSections(SqliteConnection conn)
    {
        var list = new List<DbSection>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT OBJ_PK, OBJ_Key, OBJ_Street, OBJ_Material, OBJ_Size1, OBJ_PipeHeightOrDia, OBJ_Length,
                                   OBJ_RealLength, OBJ_PipeLength, OBJ_Usage, OBJ_Ownership, OBJ_ConstructionYearText,
                                   OBJ_ConstructionDate, OBJ_Memo, OBJ_FromNode_REF, OBJ_ToNode_REF
                            FROM SECTION WHERE OBJ_Key IS NOT NULL AND OBJ_Key <> ''";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new DbSection(
                Pk: r.GetString(0),
                Key: r.IsDBNull(1) ? "" : r.GetString(1),
                Street: r.IsDBNull(2) ? null : r.GetValue(2)?.ToString(),
                Material: r.IsDBNull(3) ? null : r.GetValue(3)?.ToString(),
                Size1: r.IsDBNull(4) ? null : r.GetValue(4)?.ToString(),
                PipeHeightOrDia: r.IsDBNull(5) ? null : r.GetValue(5)?.ToString(),
                Length: r.IsDBNull(6) ? null : r.GetValue(6)?.ToString(),
                RealLength: r.IsDBNull(7) ? null : r.GetValue(7)?.ToString(),
                PipeLength: r.IsDBNull(8) ? null : r.GetValue(8)?.ToString(),
                Usage: r.IsDBNull(9) ? null : r.GetValue(9)?.ToString(),
                Ownership: r.IsDBNull(10) ? null : r.GetValue(10)?.ToString(),
                ConstructionYearText: r.IsDBNull(11) ? null : r.GetValue(11)?.ToString(),
                ConstructionDate: r.IsDBNull(12) ? null : r.GetValue(12)?.ToString(),
                Memo: r.IsDBNull(13) ? null : r.GetValue(13)?.ToString(),
                FromNodeFk: r.IsDBNull(14) ? null : r.GetValue(14)?.ToString(),
                ToNodeFk: r.IsDBNull(15) ? null : r.GetValue(15)?.ToString()));
        }
        return list;
    }

    private static List<DbInspection> LoadInspections(SqliteConnection conn)
    {
        var list = new List<DbInspection>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT INS_PK, INS_Section_FK, INS_StartDate, INS_StartTime, INS_TimeStamp, INS_InspectionDir FROM SECINSP";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var pk = r.GetString(0);
            var sectionFk = r.GetString(1);
            var sortKey = ParseSqliteDate(r[2]) ?? ParseSqliteDate(r[3]) ?? ParseSqliteDate(r[4]) ?? DateTime.MinValue;
            var dir = r.IsDBNull(5) ? null : r.GetValue(5)?.ToString();
            list.Add(new DbInspection(pk, sectionFk, sortKey, dir));
        }
        return list;
    }

    private static Dictionary<string, List<DbObservation>> LoadObservations(SqliteConnection conn)
    {
        var dict = new Dictionary<string, List<DbObservation>>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT OBS_PK, OBS_Inspection_FK, OBS_OpCode, OBS_Observation, OBS_Distance, OBS_ContDefectLength, OBS_TimeCtr, OBS_Q1_Value, OBS_Q2_Value, OBS_Q3_Value, OBS_U1_Value, OBS_U2_Value, OBS_U3_Value, OBS_Char1, OBS_Char2, OBS_C1_Value, OBS_C2_Value, OBS_ClockPos1, OBS_ClockPos2, OBS_SortOrder FROM SECOBS WHERE OBS_Deleted IS NULL";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var obs = new DbObservation(
                Pk: r.GetString(0),
                InspectionFk: r.GetString(1),
                OpCode: r.IsDBNull(2) ? "" : r.GetString(2),
                Observation: r.IsDBNull(3) ? "" : r.GetString(3),
                Distance: r.IsDBNull(4) ? null : (double?)Convert.ToDouble(r[4], CultureInfo.InvariantCulture),
                ContDefectLength: r.IsDBNull(5) ? null : (double?)Convert.ToDouble(r[5], CultureInfo.InvariantCulture),
                TimeCtr: r.IsDBNull(6) ? null : r.GetString(6),
                Q1: r.IsDBNull(7) ? null : r.GetString(7),
                Q2: r.IsDBNull(8) ? null : r.GetString(8),
                Q3: r.IsDBNull(9) ? null : r.GetString(9),
                U1: r.IsDBNull(10) ? null : r.GetString(10),
                U2: r.IsDBNull(11) ? null : r.GetString(11),
                U3: r.IsDBNull(12) ? null : r.GetString(12),
                Char1: r.IsDBNull(13) ? null : r.GetString(13),
                Char2: r.IsDBNull(14) ? null : r.GetString(14),
                C1: r.IsDBNull(15) ? null : r.GetString(15),
                C2: r.IsDBNull(16) ? null : r.GetString(16),
                ClockPos1: r.IsDBNull(17) ? null : r.GetValue(17),
                ClockPos2: r.IsDBNull(18) ? null : r.GetValue(18),
                SortOrder: r.IsDBNull(19) ? 0 : r.GetInt32(19));

            if (!dict.TryGetValue(obs.InspectionFk, out var list))
            {
                list = new List<DbObservation>();
                dict[obs.InspectionFk] = list;
            }
            list.Add(obs);
        }
        return dict;
    }

    private static Dictionary<string, List<DbMedia>> LoadObservationMedia(SqliteConnection conn)
    {
        var dict = new Dictionary<string, List<DbMedia>>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT OMM_Observation_FK, OMM_FileName, OMM_FileType FROM SECOBSMM WHERE OMM_Deleted IS NULL";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var obsFk = r.IsDBNull(0) ? "" : r.GetString(0);
            if (string.IsNullOrWhiteSpace(obsFk))
                continue;

            var media = new DbMedia(
                ObservationFk: obsFk,
                FileName: r.IsDBNull(1) ? "" : r.GetString(1),
                FileType: r.IsDBNull(2) ? "" : r.GetString(2));

            if (!dict.TryGetValue(obsFk, out var list))
            {
                list = new List<DbMedia>();
                dict[obsFk] = list;
            }
            list.Add(media);
        }
        return dict;
    }

    private static List<DbNode> LoadNodes(SqliteConnection conn)
    {
        var list = new List<DbNode>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT OBJ_PK, OBJ_Key, OBJ_Number, OBJ_Street, OBJ_Type, OBJ_NodeType, OBJ_Usage, OBJ_Material,
                                   OBJ_Shape, OBJ_Size1, OBJ_Size2, OBJ_DepthToInvert, OBJ_RimToInvert, OBJ_Condition,
                                   OBJ_Ownership, OBJ_LandOwner, OBJ_ConstructionYearText, OBJ_ConstructionDate, OBJ_Memo,
                                   OBJ_State, OBJ_CoversCount, OBJ_Accessible, OBJ_ConstructionStyle, OBJ_Locality
                            FROM NODE
                            WHERE (OBJ_Key IS NOT NULL AND OBJ_Key <> '') OR (OBJ_Number IS NOT NULL AND OBJ_Number <> '')";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new DbNode(
                Pk: r.GetString(0),
                Key: r.IsDBNull(1) ? null : r.GetValue(1)?.ToString(),
                Number: r.IsDBNull(2) ? null : r.GetValue(2)?.ToString(),
                Street: r.IsDBNull(3) ? null : r.GetValue(3)?.ToString(),
                Type: r.IsDBNull(4) ? null : r.GetValue(4)?.ToString(),
                NodeType: r.IsDBNull(5) ? null : r.GetValue(5)?.ToString(),
                Usage: r.IsDBNull(6) ? null : r.GetValue(6)?.ToString(),
                Material: r.IsDBNull(7) ? null : r.GetValue(7)?.ToString(),
                Shape: r.IsDBNull(8) ? null : r.GetValue(8)?.ToString(),
                Size1: r.IsDBNull(9) ? null : r.GetValue(9)?.ToString(),
                Size2: r.IsDBNull(10) ? null : r.GetValue(10)?.ToString(),
                DepthToInvert: r.IsDBNull(11) ? null : r.GetValue(11)?.ToString(),
                RimToInvert: r.IsDBNull(12) ? null : r.GetValue(12)?.ToString(),
                Condition: r.IsDBNull(13) ? null : r.GetValue(13)?.ToString(),
                Ownership: r.IsDBNull(14) ? null : r.GetValue(14)?.ToString(),
                LandOwner: r.IsDBNull(15) ? null : r.GetValue(15)?.ToString(),
                ConstructionYearText: r.IsDBNull(16) ? null : r.GetValue(16)?.ToString(),
                ConstructionDate: r.IsDBNull(17) ? null : r.GetValue(17)?.ToString(),
                Memo: r.IsDBNull(18) ? null : r.GetValue(18)?.ToString(),
                State: r.IsDBNull(19) ? null : r.GetValue(19)?.ToString(),
                CoversCount: r.IsDBNull(20) ? null : r.GetValue(20)?.ToString(),
                Accessible: r.IsDBNull(21) ? null : r.GetValue(21)?.ToString(),
                ConstructionStyle: r.IsDBNull(22) ? null : r.GetValue(22)?.ToString(),
                Locality: r.IsDBNull(23) ? null : r.GetValue(23)?.ToString()));
        }
        return list;
    }

    private static DateTime? ParseSqliteDate(object? raw)
    {
        if (raw is null)
            return null;
        var text = raw.ToString();
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var m = Regex.Match(text, @"Date\((\d+)\)");
        if (m.Success && long.TryParse(m.Groups[1].Value, out var ms))
            return DateTimeOffset.FromUnixTimeMilliseconds(ms).DateTime;

        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dt))
            return dt;

        return null;
    }

    private sealed record DbSection(
        string Pk,
        string Key,
        string? Street,
        string? Material,
        string? Size1,
        string? PipeHeightOrDia,
        string? Length,
        string? RealLength,
        string? PipeLength,
        string? Usage,
        string? Ownership,
        string? ConstructionYearText,
        string? ConstructionDate,
        string? Memo,
        string? FromNodeFk,
        string? ToNodeFk);
    private sealed record DbInspection(string Pk, string SectionFk, DateTime SortKey, string? InspectionDir);
    private sealed record DbObservation(
        string Pk,
        string InspectionFk,
        string OpCode,
        string Observation,
        double? Distance,
        double? ContDefectLength,
        string? TimeCtr,
        string? Q1,
        string? Q2,
        string? Q3,
        string? U1,
        string? U2,
        string? U3,
        string? Char1,
        string? Char2,
        string? C1,
        string? C2,
        object? ClockPos1,
        object? ClockPos2,
        int SortOrder);

    private sealed record DbMedia(string ObservationFk, string FileName, string FileType);

    private sealed record DbNode(
        string Pk,
        string? Key,
        string? Number,
        string? Street,
        string? Type,
        string? NodeType,
        string? Usage,
        string? Material,
        string? Shape,
        string? Size1,
        string? Size2,
        string? DepthToInvert,
        string? RimToInvert,
        string? Condition,
        string? Ownership,
        string? LandOwner,
        string? ConstructionYearText,
        string? ConstructionDate,
        string? Memo,
        string? State,
        string? CoversCount,
        string? Accessible,
        string? ConstructionStyle,
        string? Locality);

    private static readonly string[] SchachtKeyFields =
    {
        "Schachtnummer",
        "SchachtNr",
        "Schacht",
        "Schacht-Nr",
        "Schacht Nummer",
        "Schacht ID",
        "Schacht-ID"
    };

    private static void ApplyField(HaltungRecord record, string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        record.SetFieldValue(field, value.Trim(), FieldSource.Legacy, userEdited: false);
    }

    private static bool ApplyImportedField(HaltungRecord record, string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var before = record.GetFieldValue(field);
        record.SetFieldValue(field, value.Trim(), FieldSource.Legacy, userEdited: false);
        var after = record.GetFieldValue(field);
        return !string.Equals(before, after, StringComparison.OrdinalIgnoreCase);
    }

    private static void MergeImportedCandidate(HaltungRecord target, HaltungRecord source)
    {
        var mergeFields = new[]
        {
            "Datum_Jahr",
            "Haltungslaenge_m",
            "DN_mm",
            "Rohrmaterial",
            "Inspektionsrichtung",
            "Bemerkungen",
            "Link"
        };

        foreach (var field in mergeFields)
        {
            var current = target.GetFieldValue(field);
            if (!string.IsNullOrWhiteSpace(current))
                continue;

            var incoming = source.GetFieldValue(field);
            if (string.IsNullOrWhiteSpace(incoming))
                continue;

            target.SetFieldValue(field, incoming, FieldSource.Legacy, userEdited: false);
        }
    }

    private static void SetSchachtField(SchachtRecord record, string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        if (record.Fields.TryGetValue(field, out var existing) && !string.IsNullOrWhiteSpace(existing))
            return;

        record.SetFieldValue(field, value.Trim());
    }

    private static string? NormalizeNumber(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var text = raw.Trim();
        if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var val) ||
            double.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out val))
        {
            if (Math.Abs(val - Math.Round(val)) < 0.01)
                return ((int)Math.Round(val)).ToString(CultureInfo.InvariantCulture);
            return val.ToString("0.##", CultureInfo.InvariantCulture);
        }

        return text;
    }

    private static string? NormalizeDate(string? yearText, string? rawDate)
    {
        if (!string.IsNullOrWhiteSpace(yearText))
            return yearText.Trim();

        var dt = ParseSqliteDate(rawDate);
        if (dt.HasValue)
            return dt.Value.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

        return null;
    }

    private static string? NormalizeMaterial(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var t = raw.Trim();
        var lower = t.ToLowerInvariant();
        if (lower.Contains("polyvinylchlorid") || lower.Contains("pvc"))
            return "PVC";
        if (lower.Contains("polyethylen") || lower.Contains("pe"))
            return "PE";
        if (lower.Contains("pp"))
            return "PP";
        if (lower.Contains("gfk") || lower.Contains("glasfaser"))
            return "GFK";
        if (lower.Contains("beton"))
            return "Beton";
        if (lower.Contains("steinzeug"))
            return "Steinzeug";
        if (lower.Contains("guss"))
            return "Guss";

        return t;
    }

    private static string? NormalizeUsage(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var t = raw.Trim();
        var lower = t.ToLowerInvariant();
        if (lower.Contains("regen"))
            return "Regenwasser";
        if (lower.Contains("schmutz"))
            return "Schmutzwasser";
        if (lower.Contains("misch"))
            return "Mischabwasser";

        return t;
    }

    private static string? NormalizeInspectionDir(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var t = raw.Trim();
        if (t == "1")
            return "In Fliessrichtung";
        if (t == "2")
            return "Gegen Fliessrichtung";

        return t;
    }

    private static string? NormalizeAccessible(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var t = raw.Trim().ToLowerInvariant();
        if (t is "1" or "true" or "ja" or "yes")
            return "offen";
        if (t is "0" or "false" or "nein" or "no")
            return "abgeschlossen";

        return raw.Trim();
    }
}
