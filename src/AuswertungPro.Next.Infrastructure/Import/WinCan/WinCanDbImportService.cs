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
using AuswertungPro.Next.Infrastructure.Media;

namespace AuswertungPro.Next.Infrastructure.Import.WinCan;

public sealed partial class WinCanDbImportService : IWinCanDbImportService
{
    private static readonly HashSet<string> MediaExtensions = new(
        MediaFileTypes.VideoExtensions
            .Concat(new[] { ".jpg", ".jpeg", ".png", ".bmp", ".pdf" }),
        StringComparer.OrdinalIgnoreCase);

    public Result<ImportStats> ImportWinCanExport(string exportRoot, Project project, ImportRunContext? ctx = null)
    {
        if (string.IsNullOrWhiteSpace(exportRoot) || !Directory.Exists(exportRoot))
            return Result<ImportStats>.Fail("WINCAN_ROOT_MISSING", "Export-Ordner nicht gefunden.");

        ctx?.Log.AddEntry("WinCan", "Start", ImportLogStatus.Info, sourceFile: exportRoot);

        // WinCan VX speichert in .sdf (SQL Server Compact) — dafuer gibt es keinen .NET 8/10 Treiber.
        // Wenn .sdf vorhanden aber kein .db3:
        //   1. Versuche SDF -> SQLite-Konvertierung (SsceDLL + PowerShell + Python)
        //   2. Wenn das scheitert: XTF-Fallback aus Misc/Exchange
        //   3. Wenn auch das scheitert: User-Hinweis fuer manuellen XTF-Export
        var dbPath = FindDb3(exportRoot);
        if (string.IsNullOrWhiteSpace(dbPath))
        {
            var sdfPath = FindSdf(exportRoot);
            if (!string.IsNullOrWhiteSpace(sdfPath))
            {
                ctx?.Log.AddEntry("WinCan", "SDF_Detected", ImportLogStatus.Info,
                    sourceFile: sdfPath,
                    detail: "WinCan VX SDF erkannt - versuche automatische Konvertierung nach SQLite.");

                // Schritt 1: Auto-Konvertierung SDF -> .db3 (nur wenn SSCE verfuegbar)
                if (SdfToSqliteConverter.IsSsceAvailable())
                {
                    try
                    {
                        // Ziel-.db3 direkt im selben Ordner wie die SDF -> beim naechsten
                        // Import wird FindDb3 sie automatisch finden (idempotent).
                        var targetDb3 = Path.ChangeExtension(sdfPath, ".db3");
                        ctx?.Log.AddEntry("WinCan", "SDF_Convert_Start", ImportLogStatus.Info,
                            sourceFile: sdfPath,
                            detail: $"Konvertierung SDF -> SQLite gestartet (Ziel: {Path.GetFileName(targetDb3)}).");

                        // CancellationToken durchreichen - Import-Cancel beendet jetzt auch lange PowerShell/Python-Prozesse
                        var convertedPath = SdfToSqliteConverter.Convert(sdfPath, targetDb3, ctx?.CancellationToken ?? default);
                        if (!string.IsNullOrWhiteSpace(convertedPath) && File.Exists(convertedPath))
                        {
                            ctx?.Log.AddEntry("WinCan", "SDF_Convert_Ok", ImportLogStatus.Info,
                                sourceFile: convertedPath,
                                detail: $"SDF erfolgreich nach SQLite konvertiert ({new FileInfo(convertedPath).Length / 1024} KB).");
                            // Mit der konvertierten DB weitermachen
                            dbPath = convertedPath;
                            // -> faellt durch zum normalen DB3-Importpfad weiter unten
                        }
                        else
                        {
                            ctx?.Log.AddEntry("WinCan", "SDF_Convert_NoOutput", ImportLogStatus.Info,
                                sourceFile: sdfPath,
                                detail: "Konvertierung lieferte keine Ausgabedatei - faellt zurueck auf XTF-Suche.");
                        }
                    }
                    catch (Exception convEx)
                    {
                        ctx?.Log.AddEntry("WinCan", "SDF_Convert_Failed", ImportLogStatus.Info,
                            sourceFile: sdfPath,
                            detail: $"SDF-Konvertierung fehlgeschlagen: {convEx.Message} - faellt zurueck auf XTF-Suche.");
                    }
                }
                else
                {
                    ctx?.Log.AddEntry("WinCan", "SDF_NoSsce", ImportLogStatus.Info,
                        sourceFile: sdfPath,
                        detail: "SQL Server Compact 4.0 Runtime nicht installiert - keine Auto-Konvertierung moeglich. Faellt zurueck auf XTF.");
                }

                // Schritt 2: Falls Konvertierung nicht geklappt hat -> XTF-Fallback
                if (string.IsNullOrWhiteSpace(dbPath))
                {
                    try
                    {
                        var xtfFallback = TryImportViaXtfFallback(exportRoot, project, sdfPath, ctx);
                        if (xtfFallback is not null)
                            return xtfFallback;
                    }
                    catch (Exception ex)
                    {
                        ctx?.Log.AddEntry("WinCan", "XTF_Fallback_Exception", ImportLogStatus.Error,
                            sourceFile: sdfPath, detail: ex.Message);

                        return Result<ImportStats>.Success(new ImportStats(0, 0, 0, 1, 0,
                            new[]
                            {
                                $"WinCan VX SDF erkannt ({Path.GetFileName(sdfPath)})",
                                "Auto-Konvertierung SDF -> SQLite scheiterte oder SSCE 4.0 fehlt.",
                                $"XTF-Fallback fehlgeschlagen mit Fehler: {ex.Message}"
                            }));
                    }

                    // Schritt 3: Weder Konvertierung noch XTF -> User-Hinweis
                    return Result<ImportStats>.Success(new ImportStats(0, 0, 0, 0, 0,
                        new[]
                        {
                            $"WinCan VX SDF erkannt ({Path.GetFileName(sdfPath)}), aber Import nicht moeglich:",
                            SdfToSqliteConverter.IsSsceAvailable()
                                ? "  - Auto-Konvertierung SDF -> SQLite scheiterte (siehe Log)."
                                : "  - SQL Server Compact 4.0 nicht installiert (SSCERuntime_x64-ENU.exe).",
                            "  - Kein XTF-Export im Projekt gefunden (Misc/, Exchange/, Export/, XTF/, Root).",
                            "Loesungen: SSCE 4.0 installieren ODER in WinCan VX 'Export -> INTERLIS 2' erstellen."
                        }));
                }
                // Wenn dbPath jetzt gesetzt ist (SDF erfolgreich konvertiert) -> faellt zum DB3-Pfad durch
            }

            if (string.IsNullOrWhiteSpace(dbPath))
                return ImportWithoutDb3(exportRoot, project, "WinCan DB3 nicht gefunden. Fallback auf MDB.", ctx: ctx);
        }

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

            var sectionIndex = 0;
            foreach (var section in sections)
            {
                ctx?.CancellationToken.ThrowIfCancellationRequested();
                sectionIndex++;
                ctx?.Progress?.Report(new ImportProgress(
                    "Haltungen importieren", sectionIndex, sections.Count,
                    $"WinCan {sectionIndex}/{sections.Count}", section.Key));
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
                BuildPrimaryDamagesText(record, entries);

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

            var fallback = ImportWithoutDb3(exportRoot, project, fallbackReason, failWhenNoMdb: false, ctx: ctx);
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
        bool failWhenNoMdb = true,
        ImportRunContext? ctx = null)
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
            ctx?.CancellationToken.ThrowIfCancellationRequested();
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
            ctx?.CancellationToken.ThrowIfCancellationRequested();
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
            changed |= ApplyImportedField(target, "Rohrmaterial", NormalizeMaterial(imported.GetFieldValue("Rohrmaterial")));
            changed |= ApplyImportedField(target, "Inspektionsrichtung", imported.GetFieldValue("Inspektionsrichtung"));
            changed |= ApplyImportedField(target, "Bemerkungen", imported.GetFieldValue("Bemerkungen"));
            changed |= ApplyImportedField(target, "Nutzungsart", NormalizeUsage(imported.GetFieldValue("Nutzungsart")));
            changed |= ApplyImportedField(target, "Primaere_Schaeden",
                XtfPrimaryDamageFormatter.DeduplicateText(imported.GetFieldValue("Primaere_Schaeden")));

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

            // Transfer protocol from MDB import (SO_T observations)
            if (imported.Protocol is not null && target.Protocol is null)
            {
                target.Protocol = imported.Protocol;
                changed = true;
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
        var findings = new List<VsaFinding>(entries.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            var rawCode = (entry.Code ?? "").Trim().ToUpperInvariant();
            // Streckenschaden-Marker zum echten VSA-Code aufloesen
            var effectiveCode = ResolveEffectiveCode(rawCode, entry.Beschreibung, out _);

            var meterStart = entry.MeterStart;
            var meterEnd = entry.MeterEnd;
            var meterKey = meterStart.HasValue
                ? meterStart.Value.ToString("F2", CultureInfo.InvariantCulture)
                : meterEnd.HasValue
                    ? meterEnd.Value.ToString("F2", CultureInfo.InvariantCulture)
                    : string.Empty;
            var dedupeKey = $"{effectiveCode}|{meterKey}";
            if (!seen.Add(dedupeKey))
                continue;

            var finding = new VsaFinding
            {
                KanalSchadencode = effectiveCode,
                Raw = entry.Beschreibung,
                MeterStart = meterStart,
                MeterEnd = meterEnd,
                SchadenlageAnfang = meterStart,
                SchadenlageEnde = meterEnd,
                MPEG = entry.Mpeg,
                FotoPath = entry.FotoPaths.Count > 0 ? entry.FotoPaths[0] : null
            };

            // Quantifizierung1 aus Beschreibung extrahieren (WinCan liefert kein Q1-Feld)
            // Nur fuer Codes mit QuantRules: BAA, BAB, BAC, BAF, BBA, BDD
            if (string.IsNullOrEmpty(finding.Quantifizierung1) && !string.IsNullOrEmpty(entry.Beschreibung))
            {
                var normCode = effectiveCode.Length >= 3 ? effectiveCode[..3].ToUpperInvariant() : "";
                if (normCode is "BAA" or "BAB" or "BAC" or "BAF" or "BBA" or "BDD")
                {
                    finding.Quantifizierung1 = ExtractQuantValue(entry.Beschreibung);
                }
            }

            findings.Add(finding);
        }

        // DB3 gilt als Quelle der Wahrheit: vorhandene VsaFindings durch den aktuellen Importstand ersetzen.
        record.VsaFindings = findings;
    }

    /// <summary>
    /// Extrahiert einen numerischen Quantifizierungswert aus dem WinCan-Beschreibungstext.
    /// Sucht nach Prozent (%), Grad (°) oder Millimeter (mm) Angaben.
    /// </summary>
    private static string? ExtractQuantValue(string beschreibung)
    {
        // Prozent: "5%", "25 %", "10.5%"
        var m = Regex.Match(beschreibung, @"(\d+(?:[.,]\d+)?)\s*%");
        if (m.Success) return m.Groups[1].Value.Replace(',', '.');

        // Grad: "15°", "45 °"
        m = Regex.Match(beschreibung, @"(\d+(?:[.,]\d+)?)°");
        if (m.Success) return m.Groups[1].Value.Replace(',', '.');

        // Millimeter: "2mm", "0.5 mm"
        m = Regex.Match(beschreibung, @"(\d+(?:[.,]\d+)?)\s*mm");
        if (m.Success) return m.Groups[1].Value.Replace(',', '.');

        return null;
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
        var enumOpts = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            MatchCasing = MatchCasing.CaseInsensitive
        };
        var candidates = Directory.EnumerateFiles(exportRoot, "*.db3", enumOpts)
            .Where(p => p.IndexOf(Path.DirectorySeparatorChar + "DB" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0)
            .ToList();

        if (candidates.Count == 0)
            return null;

        return candidates
            .Select(p => new FileInfo(p))
            .OrderByDescending(fi => fi.Length)
            .FirstOrDefault()?.FullName;
    }

    /// <summary>
    /// Sucht nach WinCan VX .sdf (SQL Server Compact) Datenbanken.
    /// SDF kann unter .NET 8 nicht direkt gelesen werden (kein Treiber).
    /// </summary>
    private static string? FindSdf(string exportRoot)
    {
        try
        {
            var enumOpts = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                MatchCasing = MatchCasing.CaseInsensitive
            };

            var candidates = Directory.EnumerateFiles(exportRoot, "*.sdf", enumOpts)
                .Where(p =>
                {
                    var dir = Path.GetDirectoryName(p) ?? "";
                    var dirName = Path.GetFileName(dir);
                    return string.Equals(dirName, "DB", StringComparison.OrdinalIgnoreCase);
                })
                .Where(p => !Path.GetFileName(p).Contains("_Meta", StringComparison.OrdinalIgnoreCase))
                .ToList();

            return candidates
                .Select(p => new FileInfo(p))
                .OrderByDescending(fi => fi.Length)
                .FirstOrDefault()?.FullName;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// WinCan VX SDF-Fallback: suche XTF-Export in Misc/Exchange und importiere diesen.
    /// WinCan VX legt den INTERLIS-Export standardmaessig dort ab.
    /// </summary>
    private Result<ImportStats>? TryImportViaXtfFallback(
        string exportRoot, Project project, string sdfPath, ImportRunContext? ctx)
    {
        // Suche XTF-Dateien im gesamten Projektordner (robust, ignoriert gesperrte Ordner)
        var xtfFiles = new List<string>();
        var enumOpts = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            MatchCasing = MatchCasing.CaseInsensitive
        };

        try
        {
            xtfFiles.AddRange(Directory.EnumerateFiles(exportRoot, "*.xtf", enumOpts));
        }
        catch (Exception ex)
        {
            ctx?.Log.AddEntry("WinCan", "XTF_Search_Error", ImportLogStatus.Info,
                sourceFile: exportRoot,
                detail: $"Fehler bei XTF-Suche: {ex.Message}");
        }

        if (xtfFiles.Count == 0)
        {
            ctx?.Log.AddEntry("WinCan", "XTF_NotFound", ImportLogStatus.Info,
                sourceFile: exportRoot,
                detail: $"Keine *.xtf Dateien im Projektordner gefunden (Suche in: {exportRoot})");
            return null;
        }

        ctx?.Log.AddEntry("WinCan", "XTF_Fallback", ImportLogStatus.Info,
            detail: $"SDF nicht lesbar, verwende {xtfFiles.Count} XTF-Datei(en) als Fallback: {string.Join(", ", xtfFiles.Select(Path.GetFileName))}");

        // XTF-Import via LegacyXtfImportService
        var xtfService = new XtfImportServiceAdapter();
        var xtfResult = xtfService.ImportXtfFiles(xtfFiles, project, ctx);

        if (!xtfResult.Ok || xtfResult.Value is null)
        {
            // XTF-Dateien gefunden, aber Import fehlgeschlagen — Fehler nicht verschlucken
            ctx?.Log.AddEntry("WinCan", "XTF_Fallback_Failed", ImportLogStatus.Error,
                detail: $"XTF-Fallback fehlgeschlagen: {xtfResult.ErrorMessage ?? "unbekannter Fehler"}");

            var errMessages = new List<string>
            {
                $"Importquelle: WinCan VX SDF-Fallback via XTF ({Path.GetFileName(sdfPath)})",
                $"SDF-Datenbank erkannt, aber nicht direkt lesbar (SQL Server Compact, kein .NET 8 Treiber).",
                $"{xtfFiles.Count} XTF-Datei(en) gefunden, aber Import fehlgeschlagen: {xtfResult.ErrorMessage ?? "unbekannter Fehler"}"
            };
            return Result<ImportStats>.Success(new ImportStats(0, 0, 0, 1, 0, errMessages));
        }

        // Ergebnis anreichern mit Hinweis auf SDF-Herkunft
        var messages = new List<string>
        {
            $"Importquelle: WinCan VX SDF-Fallback via XTF ({Path.GetFileName(sdfPath)})",
            $"SDF-Datenbank erkannt, aber nicht direkt lesbar (SQL Server Compact, kein .NET 8 Treiber).",
            $"Stattdessen {xtfFiles.Count} XTF-Export(e) aus Misc/Exchange importiert."
        };
        messages.AddRange(xtfResult.Value.Messages);

        // Medien aus dem WinCan VX Projektordner verknuepfen
        var fileIndex = BuildFileIndex(exportRoot);
        LinkMediaFromFileIndex(project, fileIndex, messages);

        return Result<ImportStats>.Success(new ImportStats(
            xtfResult.Value.Found,
            xtfResult.Value.Created,
            xtfResult.Value.Updated,
            xtfResult.Value.Errors,
            xtfResult.Value.Uncertain,
            messages));
    }

    /// <summary>
    /// Verknuepft Video- und Foto-Dateien aus dem WinCan VX Projektordner mit importierten Haltungen.
    /// </summary>
    private static void LinkMediaFromFileIndex(
        Project project, Dictionary<string, List<string>> fileIndex, List<string> messages)
    {
        var linked = 0;
        foreach (var record in project.Data)
        {
            var haltungsname = record.GetFieldValue("Haltungsname");
            if (string.IsNullOrWhiteSpace(haltungsname)) continue;

            // Bereits ein Video verlinkt?
            var existingLink = record.GetFieldValue("Link");
            if (!string.IsNullOrWhiteSpace(existingLink)) continue;

            // Suche Video-Datei die den Haltungsnamen enthaelt
            foreach (var kv in fileIndex)
            {
                if (!kv.Key.Contains(haltungsname, StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (var filePath in kv.Value)
                {
                    var ext = Path.GetExtension(filePath);
                    if (MediaExtensions.Contains(ext) && MediaFileTypes.VideoExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
                    {
                        record.SetFieldValue("Link", filePath, Domain.Models.FieldSource.Legacy, userEdited: false);
                        linked++;
                        break;
                    }
                }
                break;
            }
        }

        if (linked > 0)
            messages.Add($"Medien verknuepft: {linked} Videos aus dem WinCan VX Projektordner zugeordnet.");
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

    // Streckenschaden-Marker: A01, A02, B01, B02, ... (DIN EN 13508-2 Anfang/Ende Streckenschaden)
    private static readonly Regex ContinuousDefectMarkerRegex = new(@"^[AB]\d{2}$", RegexOptions.Compiled);

    // VSA-Code am Anfang der Beschreibung extrahieren (z.B. "BBCC (Harte Ablagerungen...)")
    private static readonly Regex EmbeddedVsaCodeRegex = new(@"^([A-Z]{3,5})\b", RegexOptions.Compiled);

    /// <summary>
    /// Prueft ob der Code ein Streckenschaden-Marker (A01, B02 etc.) ist.
    /// Falls ja, wird der echte VSA-Code aus der Beschreibung extrahiert.
    /// </summary>
    private static string ResolveEffectiveCode(string code, string? description, out string? resolvedDescription)
    {
        resolvedDescription = description;
        if (!ContinuousDefectMarkerRegex.IsMatch(code) || string.IsNullOrWhiteSpace(description))
            return code;

        var match = EmbeddedVsaCodeRegex.Match(description.Trim());
        if (match.Success)
        {
            var vsaCode = match.Groups[1].Value;
            // Beschreibung bereinigen: VSA-Code am Anfang entfernen
            var rest = description.Trim().Substring(vsaCode.Length).TrimStart(' ', '(');
            if (rest.EndsWith(")"))
                rest = rest.Substring(0, rest.Length - 1);
            resolvedDescription = rest.Trim();
            if (string.IsNullOrWhiteSpace(resolvedDescription))
                resolvedDescription = description;
            return vsaCode;
        }

        return code;
    }

    /// <summary>
    /// Erzeugt den "Primaere_Schaeden" Text aus den Protokoll-Eintraegen.
    /// Format: "0.00m CODE Beschreibung\n..."
    /// Streckenschaden-Marker (A01, B02) werden zum echten VSA-Code aufgeloest.
    /// </summary>
    private static void BuildPrimaryDamagesText(HaltungRecord record, List<ProtocolEntry> entries)
    {
        if (entries.Count == 0)
            return;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lines = new List<string>();
        foreach (var entry in entries)
        {
            var rawCode = (entry.Code ?? "").Trim().ToUpperInvariant();
            var desc = entry.Beschreibung?.Trim();
            if (string.IsNullOrWhiteSpace(desc) && string.IsNullOrWhiteSpace(rawCode))
                continue;

            // Streckenschaden-Marker zum echten VSA-Code aufloesen
            var code = ResolveEffectiveCode(rawCode, desc, out var resolvedDesc);

            // Deduplicate by effective code + meter position
            if (code.Length > 0)
            {
                var meterKey = entry.MeterStart.HasValue ? entry.MeterStart.Value.ToString("F2") : "";
                var key = $"{code}|{meterKey}";
                if (!seen.Add(key))
                    continue;
            }

            var line = "";
            if (entry.MeterStart.HasValue)
                line = $"{entry.MeterStart.Value:0.00}m ";

            if (!string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(resolvedDesc))
                line += $"{code} {resolvedDesc}";
            else if (!string.IsNullOrWhiteSpace(code))
                line += code;
            else
                line += resolvedDesc;

            lines.Add(line.TrimEnd());
        }

        if (lines.Count == 0)
            return;

        var text = XtfPrimaryDamageFormatter.DeduplicateText(string.Join("\n", lines));
        record.SetFieldValue("Primaere_Schaeden", text, FieldSource.Legacy, userEdited: false);
    }

}
