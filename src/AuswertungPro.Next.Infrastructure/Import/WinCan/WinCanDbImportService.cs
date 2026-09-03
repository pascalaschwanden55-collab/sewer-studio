using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Domain.VsaCatalog;
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

        // Ein gewaehlter Sammelordner kann mehrere vollstaendige WinCan-Projekte enthalten
        // (je Projekt ein eigener Ordner "DB"). Dann wird jedes Projekt einzeln eingelesen,
        // damit keines still liegen bleibt und die Medien-/PDF-Suche je Projekt getrennt
        // bleibt. Nur ein einzelnes Projekt behaelt den bisherigen Ablauf unveraendert.
        var projektWurzeln = FindWinCanProjektWurzeln(exportRoot);
        if (projektWurzeln.Count > 1)
            return ImportMehrereProjekte(projektWurzeln, project, ctx);

        return ImportEinzelnesProjekt(exportRoot, project, ctx, zonenName: null);
    }

    private Result<ImportStats> ImportEinzelnesProjekt(
        string exportRoot,
        Project project,
        ImportRunContext? ctx,
        string? zonenName)
    {
        // WinCan VX speichert in .sdf (SQL Server Compact) — dafuer gibt es keinen .NET 8 Treiber.
        // Wenn .sdf vorhanden aber kein .db3, versuche XTF aus Misc/Exchange als Fallback.
        //
        // Die Quellenwahl schaut in JEDE Kandidatendatei hinein und protokolliert das
        // Ergebnis. Das Protokoll wandert bis ins Plausibilitaetstor und in den Bericht.
        var quellen = WaehleDatenbank(exportRoot);
        var dbPath = quellen.Gewinner?.Pfad;
        if (string.IsNullOrWhiteSpace(dbPath))
        {
            var sdfPath = FindSdf(exportRoot);
            if (!string.IsNullOrWhiteSpace(sdfPath))
            {
                ctx?.Log.AddEntry("WinCan", "SDF_Detected", ImportLogStatus.Info,
                    sourceFile: sdfPath,
                    detail: "WinCan VX SDF erkannt — kein .NET 8 Treiber verfuegbar. Suche XTF-Export als Fallback.");

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
                            $"XTF-Fallback fehlgeschlagen mit Fehler: {ex.Message}"
                        }));
                }

                // Kein XTF gefunden → Benutzer informieren
                return Result<ImportStats>.Success(new ImportStats(0, 0, 0, 0, 0,
                    new[]
                    {
                        $"WinCan VX SDF erkannt ({Path.GetFileName(sdfPath)}), aber kein XTF-Export gefunden.",
                        "Gesuchte Ordner: Misc/, Exchange/, Export/, XTF/ und Projektroot.",
                        "Bitte im WinCan VX unter 'Export → INTERLIS 2' einen XTF-Export erstellen und dann per XTF-Import einlesen."
                    }));
            }

            // Kandidaten vorhanden, aber keiner brauchbar: ehrlich melden statt
            // "nicht gefunden". Eine defekte oder gesperrte Datenbank IST gefunden
            // worden — sie liess sich nur nicht lesen. Das Protokoll geht mit,
            // damit das Plausibilitaetstor hart abbrechen kann.
            var unbrauchbare = quellen.AlleVersuche
                .Where(v => v.Befund.ErkanntAlsQuelle)
                .ToList();

            if (unbrauchbare.Count > 0)
            {
                var meldungen = new List<string>();
                foreach (var versuch in unbrauchbare)
                {
                    var text = $"Fehler beim WinCan-DB Import: "
                               + versuch.Berichtszeile(Path.GetFileName);
                    meldungen.Add(text);
                    ctx?.Log.AddEntry("WinCan", "DB3", ImportLogStatus.Error,
                        sourceFile: versuch.Pfad, detail: versuch.Befund.Grund);
                }

                var rueckfall = ImportWithoutDb3(
                    exportRoot, project,
                    "Keine lesbare WinCan-Datenbank. Versuche MDB-Fallback.",
                    failWhenNoMdb: false, ctx: ctx);

                var rueckfallWerte = rueckfall.Ok ? rueckfall.Value : null;
                if (rueckfallWerte is not null)
                    meldungen.AddRange(rueckfallWerte.Messages);
                else if (!string.IsNullOrWhiteSpace(rueckfall.ErrorMessage))
                    meldungen.Add($"MDB-Fallback fehlgeschlagen: {rueckfall.ErrorMessage}");

                return Result<ImportStats>.Success(new ImportStats(
                    rueckfallWerte?.Found ?? 0,
                    rueckfallWerte?.Created ?? 0,
                    rueckfallWerte?.Updated ?? 0,
                    unbrauchbare.Count + (rueckfallWerte?.Errors ?? 0),
                    rueckfallWerte?.Uncertain ?? 0,
                    meldungen)
                {
                    ErwarteteHaltungen = quellen.ErwarteteMenge,
                    BearbeiteteHaltungen = rueckfallWerte?.BearbeiteteHaltungen ?? 0,
                    Quellenprotokoll = quellen
                });
            }

            return ImportWithoutDb3(exportRoot, project, "WinCan DB3 nicht gefunden. Fallback auf MDB.", ctx: ctx);
        }

        var messages = new List<string>();
        messages.Add($"Importquelle: WinCan DB3 ({Path.GetFileName(dbPath)})");
        // Getrennte Zaehlung: found/updated enthalten auch Schaechte und taugen nicht
        // als Pruefgroesse fuer das Plausibilitaetstor.
        var bearbeiteteHaltungen = 0;
        var found = 0;
        var updated = 0;
        var errors = 0;
        var uncertain = 0;
        var created = 0;

        var fileIndex = BuildFileIndex(exportRoot);
        var protocolService = _protocolService;

        var db3ImportFailed = false;
        try
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode = SqliteOpenMode.ReadOnly
            }.ToString();
            using var conn = new SqliteConnection(connectionString);
            conn.Open();

            var database = WinCanDbReader.Read(conn);
            var sections = database.Sections;
            var inspections = database.Inspections;
            var obsByInspection = database.ObservationsByInspection;
            var mediaByObs = database.MediaByObservation;
            var nodes = database.Nodes;

            // Knoten-Lookup (OBJ_PK -> Schachtnummer), um Schacht oben/unten an den Haltungen
            // aus OBJ_FromNode_REF/OBJ_ToNode_REF aufzuloesen.
            var nodeKeyByPk = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var n in nodes)
            {
                var nk = n.Key ?? n.Number;
                if (!string.IsNullOrWhiteSpace(n.Pk) && !string.IsNullOrWhiteSpace(nk))
                    nodeKeyByPk[n.Pk] = nk!;
            }

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

                // Audit I2: Fehler pro Haltung isolieren — eine kaputte Section darf
                // weder die restlichen blockieren noch den MDB-Fallback ausloesen.
                try
                {
                    // Haltungsnamen sind nur INNERHALB eines WinCan-Projekts eindeutig. Werden
                    // mehrere Projekte in dasselbe Programmprojekt eingelesen, muss eine
                    // gleichnamige, aber andere Haltung einen eigenen Datensatz bekommen.
                    var datensatzName = BestimmeHaltungsname(
                        project, section, nodeKeyByPk, zonenName, messages);

                    var record = FindRecord(project, datensatzName);
                    if (record is null)
                    {
                        record = project.CreateNewRecord();
                        record.SetFieldValue("Haltungsname", datensatzName, FieldSource.Legacy, userEdited: false);
                        AddRecord(project, record, ctx);
                        created++;
                        messages.Add($"Haltung neu angelegt: {datensatzName}");
                    }

                    found++;

                    var kandidaten = inspections
                        .Where(i => i.SectionFk == section.Pk)
                        .OrderByDescending(i => i.SortKey)
                        .ToList();
                    var inspection = kandidaten.FirstOrDefault();

                    // Mehr als eine Untersuchung je Haltung: Nur die neueste kommt ins
                    // Protokoll. Das darf nicht still passieren; in Seilergasse gingen so
                    // 12 Befunde, 9 Fotos und 1 Video verloren, bei "0 Fehler" im Bericht.
                    foreach (var uebersprungen in kandidaten.Skip(1))
                    {
                        var befunde = obsByInspection.TryGetValue(uebersprungen.Pk, out var liste) ? liste.Count : 0;
                        messages.Add(
                            $"Haltung {section.Key}: WinCan fuehrt {kandidaten.Count} Untersuchungen. " +
                            $"Uebernommen: {Datumstext(inspection!)}; uebersprungen: {Datumstext(uebersprungen)} " +
                            $"mit {befunde} Befunden.");
                    }

                    // Stammdaten + Schaechte ueber die zentrale MergeEngine (Leer-Schutz, Import-
                    // Prioritaet Legacy < Pdf < Xtf, Konfliktprotokoll) statt bedingungsloser
                    // Direktschreibung — schuetzt hoeherwertige XTF/PDF-Werte vor stillem Ueberschreiben.
                    var source = new HaltungRecord();
                    ApplySectionFields(source, section, inspection);

                    // Schacht oben/unten aus den Knoten-Referenzen der Section aufloesen.
                    // Schacht_oben = Anfangsschacht der BEFAHRUNG. Im Normalfall (in Fliessrichtung)
                    // ist das FromNode, ToNode = unten. Bei GEGENBEFAHRUNG (INS_InspectionDir U/UP/
                    // UPSTREAM/2) faehrt die Kamera von ToNode nach FromNode -> oben/unten tauschen
                    // (konsistent mit M150ValueExtractor.ShouldReverseWinCanDirection und VSA_KEK von=oben).
                    // Schacht oben/unten sind HYDRAULISCH und werden NICHT nach der
                    // Fahrtrichtung gedreht.
                    //
                    // Gemessen am Bestand Andermatt (2026-08-21): OBJ_FromNode_REF /
                    // OBJ_ToNode_REF stimmen in 16 von 16 Faellen mit "Schacht oben" /
                    // "Schacht unten" im Kundenprotokoll ueberein — einschliesslich aller
                    // drei Gegenbefahrungen. Die frueher hier eingebaute Umkehrung
                    // vertauschte genau diese drei Haltungen und erzeugte damit auch
                    // falsche Haltungsnummern.
                    //
                    // Die Fahrtrichtung geht nicht verloren: sie steht getrennt im Feld
                    // "Inspektionsrichtung"; die WinCan-XTF fuehrt sie zusaetzlich als
                    // vonPunktBezeichnung / bisPunktBezeichnung.
                    var obenRef  = section.FromNodeFk;
                    var untenRef = section.ToNodeFk;
                    if (!string.IsNullOrWhiteSpace(obenRef)
                        && nodeKeyByPk.TryGetValue(obenRef!, out var schachtOben))
                        ApplyField(source, "Schacht_oben", schachtOben);
                    if (!string.IsNullOrWhiteSpace(untenRef)
                        && nodeKeyByPk.TryGetValue(untenRef!, out var schachtUnten))
                        ApplyField(source, "Schacht_unten", schachtUnten);

                    Common.LegacyStammdatenMerger.MergeLegacy(project, record, source, ctx);

                    // Ab hier ist die Haltung mit ihren Stammdaten im Projekt angekommen.
                    // Bewusst SCHON HIER zaehlen, nicht erst nach dem Protokoll: Eine
                    // Haltung ohne Befunde (sauberes Rohr) ist vollstaendig importiert
                    // und darf keinen Fehlalarm im Plausibilitaetstor ausloesen.
                    bearbeiteteHaltungen++;

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
                        // Wurzel-Fix: rohen WinCan-OpCode normalisieren (Punkt-Trenner und Meter-Suffixe
                        // entfernen, Hauptcode + Laenge gegen Katalog pruefen), damit typischer Parsing-Muell
                        // nicht ins Protokoll und spaeter ins Training gelangt. CodeMeta.Code erbt entry.Code.
                        var rawCode = obs.OpCode ?? "";
                        var normalizedCode = VsaCodeValidator.TryNormalizeKnownCode(rawCode) ?? "";
                        if (normalizedCode.Length == 0 && !string.IsNullOrWhiteSpace(rawCode))
                            messages.Add($"WinCan: Code '{rawCode}' unbekannt/ungueltig - leer uebernommen (Haltung {section.Key}).");

                        var entry = new ProtocolEntry
                        {
                            Code = normalizedCode,
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

                                // Ein leerer Medientyp in der Datenbank darf eine vorhandene
                                // Datei nicht verwerfen — dann entscheidet die Dateiendung.
                                var medientyp = WinCanValueNormalizer.MedientypOderEndung(
                                    media.FileType, media.FileName);

                                if (IsVideo(medientyp))
                                {
                                    var videoPath = ResolveFile(fileIndex, media.FileName);
                                    if (!string.IsNullOrWhiteSpace(videoPath))
                                        record.SetFieldValue("Link", videoPath, FieldSource.Legacy, userEdited: false);
                                }
                                else if (IsImage(medientyp))
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

                    // Primaere_Schaeden (abgeleiteter Zusammenfassungstext) ebenfalls ueber die
                    // MergeEngine, damit ein hoeherwertiger Bestandswert nicht still ueberschrieben wird.
                    var damageSource = new HaltungRecord();
                    BuildPrimaryDamagesText(damageSource, entries);
                    Common.LegacyStammdatenMerger.MergeLegacy(project, record, damageSource, ctx);

                    updated++;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    errors++;
                    messages.Add($"Fehler bei Haltung {section.Key} (WinCan DB): {ex.Message}");
                    ctx?.Log.AddEntry("WinCan", "Haltung", ImportLogStatus.Error,
                        recordKey: section.Key, sourceFile: dbPath, detail: ex.Message);
                }
            }

            var haltungsnamen = sections
                .Select(x => x.Key)
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            ImportNodes(project, nodes, fileIndex, haltungsnamen, _pdfReferenzen,
                messages, ref found, ref created, ref updated, ref uncertain, ctx);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            db3ImportFailed = true;
            errors++;
            messages.Add($"Fehler beim WinCan-DB Import: {ex.Message}");
            ctx?.Log.AddEntry("WinCan", "DB3", ImportLogStatus.Error,
                sourceFile: dbPath, detail: ex.Message);
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

        var stats = new ImportStats(found, created, updated, errors, uncertain, messages)
        {
            ErwarteteHaltungen = quellen.ErwarteteMenge,
            BearbeiteteHaltungen = bearbeiteteHaltungen,
            Quellenprotokoll = quellen
        };
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
            if (!M150MdbImportHelper.TryParseMdbFile(
                    mdbPath,
                    _m150MdbRows,
                    out var records,
                    out var parseError,
                    out var warnings))
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
            var target = FindRecord(project, imported.GetFieldValue("Haltungsname"));

            var isNew = target is null;
            if (target is null)
            {
                target = project.CreateNewRecord();
                target.SetFieldValue("Haltungsname", key, FieldSource.Legacy, userEdited: false);
                AddRecord(project, target, ctx);
                created++;
            }

            found++;

            // Stammdaten des fertig aufgebauten MDB-Records ueber die MergeEngine mergen
            // (Material/Usage weiterhin normalisiert): hoeherwertige XTF/PDF-Werte bleiben
            // geschuetzt, Konflikte werden in project.Conflicts protokolliert.
            var mdbSource = new HaltungRecord();
            ApplyImportedField(mdbSource, "Datum_Jahr", imported.GetFieldValue("Datum_Jahr"));
            ApplyImportedField(mdbSource, "Haltungslaenge_m", imported.GetFieldValue("Haltungslaenge_m"));
            ApplyImportedField(mdbSource, "DN_mm", imported.GetFieldValue("DN_mm"));
            ApplyImportedField(mdbSource, "Rohrmaterial", NormalizeMaterial(imported.GetFieldValue("Rohrmaterial")));
            ApplyImportedField(mdbSource, "Inspektionsrichtung", imported.GetFieldValue("Inspektionsrichtung"));
            ApplyImportedField(mdbSource, "Bemerkungen", imported.GetFieldValue("Bemerkungen"));
            ApplyImportedField(mdbSource, "Nutzungsart", NormalizeUsage(imported.GetFieldValue("Nutzungsart")));
            ApplyImportedField(mdbSource, "Primaere_Schaeden",
                XtfPrimaryDamageFormatter.DeduplicateText(imported.GetFieldValue("Primaere_Schaeden")));

            var mdbMerge = Common.LegacyStammdatenMerger.MergeLegacy(project, target, mdbSource, ctx);
            var changed = mdbMerge.Updated > 0;

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
        // Genau die vom Benutzer gewaehlte Wurzel ist vertrauenswuerdig. Bekannte
        // Medien-Unterordner duerfen nicht als neue Wurzeln behandelt werden, weil
        // eine dort liegende Junction sonst den Schutz der rekursiven Suche umgeht.
        var files = SafeFileEnumeration.EnumerateFilesSafe(root, "*", recursive: true);
        return Common.MediaFileIndex.Build(files, MediaExtensions);
    }

    private static string? ResolveFile(Dictionary<string, List<string>> index, string fileName)
        => Common.MediaFileIndex.ResolveSingle(index, fileName);

    private static void ApplyProtocol(HaltungRecord record, List<ProtocolEntry> entries, IProtocolService protocolService)
        => Common.ImportProtocolApplier.Apply(record, entries, protocolService, "Import (WinCan DB)");

    private static void UpdateFindings(HaltungRecord record, List<ProtocolEntry> entries)
    {
        // DB3 gilt als Quelle der Wahrheit: vorhandene VsaFindings durch den aktuellen Importstand ersetzen.
        record.VsaFindings = WinCanFindingFactory.BuildFindings(entries);
    }

    private static Dictionary<string, string> BuildObsParameters(WinCanDbObservation obs)
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

    // Delegation: Logik liegt jetzt in WinCanValueNormalizer
    private static bool IsImage(string? type)
        => WinCanValueNormalizer.IsImage(type);

    // Delegation: Logik liegt jetzt in WinCanValueNormalizer
    private static bool IsVideo(string? type)
        => WinCanValueNormalizer.IsVideo(type);

    // Delegation: Logik liegt jetzt in WinCanValueNormalizer
    private static TimeSpan? ParseTimeSpan(string? value)
        => WinCanValueNormalizer.ParseTimeSpan(value);

    // Delegation: Logik liegt jetzt in Common.HoldingKeyNormalizer
    private static string NormalizeHoldingKey(string? value)
        => Common.HoldingKeyNormalizer.Normalize(value);

    // Haltungs-Matching einheitlich zu IBAK/KINS: exakt ODER Grenz-Praefix
    // (100-200 == 100-200-1, aber NICHT 100-2000). Frueher matchte WinCan nur exakt
    // und legte bei Segment-Suffix-Unterschieden ein Duplikat statt Zusammenfuehrung an.
    private static HaltungRecord? FindRecord(Project project, string? holdingName)
    {
        var key = NormalizeHoldingKey(holdingName);
        if (string.IsNullOrWhiteSpace(key))
            return null;

        var exact = project.Data.FirstOrDefault(r =>
            string.Equals(NormalizeHoldingKey(r.GetFieldValue("Haltungsname")), key, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
            return exact;

        var boundaryMatches = project.Data
            .Where(record =>
            {
                var candidate = NormalizeHoldingKey(record.GetFieldValue("Haltungsname"));
                return !string.IsNullOrWhiteSpace(candidate)
                       && Common.HoldingKeyMatch.IsBoundaryPrefixMatch(candidate, key);
            })
            .Take(2)
            .ToList();

        return boundaryMatches.Count == 1
            ? boundaryMatches[0]
            : null;
    }

    /// <summary>
    /// Waehlt die fachliche Datenbank ueber die gemeinsame Quellenwahl.
    /// Frueher entschied hier die Dateigroesse — und traf damit immer die groessere,
    /// aber leere "*_Meta.db3".
    /// </summary>
    private static string? FindDb3(string exportRoot)
        => WaehleDatenbank(exportRoot).Gewinner?.Pfad;

    /// <summary>
    /// Sucht nach WinCan VX .sdf (SQL Server Compact) Datenbanken.
    /// SDF kann unter .NET 8 nicht direkt gelesen werden (kein Treiber).
    /// </summary>
    private static string? FindSdf(string exportRoot)
    {
        var candidates = new List<(string Path, long Length)>();
        foreach (var path in SafeFileEnumeration.EnumerateFilesSafe(exportRoot, "*", recursive: true))
        {
            if (!Path.GetExtension(path).Equals(".sdf", StringComparison.OrdinalIgnoreCase))
                continue;

            var directory = Path.GetDirectoryName(path) ?? "";
            if (!Path.GetFileName(directory).Equals("DB", StringComparison.OrdinalIgnoreCase)
                || Path.GetFileName(path).Contains("_Meta", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                candidates.Add((path, new FileInfo(path).Length));
            }
            catch
            {
                // Eine unlesbare Datei verhindert den Import der restlichen Quellen nicht.
            }
        }

        return candidates
            .OrderByDescending(candidate => candidate.Length)
            .ThenBy(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase)
            .Select(candidate => candidate.Path)
            .FirstOrDefault();
    }

    /// <summary>
    /// WinCan VX SDF-Fallback: suche XTF-Export in Misc/Exchange und importiere diesen.
    /// WinCan VX legt den INTERLIS-Export standardmaessig dort ab.
    /// </summary>
    private Result<ImportStats>? TryImportViaXtfFallback(
        string exportRoot, Project project, string sdfPath, ImportRunContext? ctx)
    {
        // Unterordner werden einzeln gelesen. Verknuepfungen innerhalb der gewaehlten
        // Wurzel werden nicht betreten; die explizit gewaehlte Wurzel selbst bleibt lesbar.
        var xtfFiles = SafeFileEnumeration
            .EnumerateFilesSafe(exportRoot, "*", recursive: true)
            .Where(path => Path.GetExtension(path).Equals(".xtf", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (xtfFiles.Count == 0)
        {
            ctx?.Log.AddEntry("WinCan", "XTF_NotFound", ImportLogStatus.Info,
                sourceFile: exportRoot,
                detail: $"Keine *.xtf Dateien im Projektordner gefunden (Suche in: {exportRoot})");
            return null;
        }

        ctx?.Log.AddEntry("WinCan", "XTF_Fallback", ImportLogStatus.Info,
            detail: $"SDF nicht lesbar, verwende {xtfFiles.Count} XTF-Datei(en) als Fallback: {string.Join(", ", xtfFiles.Select(Path.GetFileName))}");

        var xtfResult = _xtfImport.ImportXtfFiles(xtfFiles, project, ctx);

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

            var candidates = fileIndex
                .Where(kv => HoldingTextNormalizer.ContainsKeyAtBoundary(kv.Key, haltungsname))
                .SelectMany(kv => kv.Value)
                .Where(filePath =>
                {
                    var ext = Path.GetExtension(filePath);
                    return MediaExtensions.Contains(ext)
                           && MediaFileTypes.VideoExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
                })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(2)
                .ToList();

            if (candidates.Count == 1)
            {
                record.SetFieldValue("Link", candidates[0], Domain.Models.FieldSource.Legacy, userEdited: false);
                linked++;
            }
            else if (candidates.Count > 1)
            {
                messages.Add($"Medien nicht verknuepft: mehrere Video-Kandidaten fuer {haltungsname}.");
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

        var candidates = AuswertungPro.Next.Infrastructure.Common.SafeFileEnumeration.EnumerateFilesSafe(exportRoot, "*.mdb", recursive: true).ToList();
        if (candidates.Count == 0)
            return Array.Empty<string>();

        var ordered = candidates
            .Select(p => new FileInfo(p))
            .OrderByDescending(fi => Rank(fi.FullName))
            .ThenByDescending(fi => fi.Length)
            // Finaler deterministischer Tiebreak: bei gleichem Rang UND gleicher Groesse
            // sonst dateisystem-abhaengige Auswahl.
            .ThenBy(fi => fi.FullName, StringComparer.OrdinalIgnoreCase)
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


    private static void SetSchachtField(SchachtRecord record, string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        if (record.Fields.TryGetValue(field, out var existing) && !string.IsNullOrWhiteSpace(existing))
            return;

        // Importwert: nie als Handeingabe kennzeichnen. Ein bereits von Hand gesetztes
        // Feld bleibt zusaetzlich unangetastet, auch wenn es leer geleert wurde.
        record.SetFieldValue(field, value.Trim(), FieldSource.Legacy, userEdited: false);
    }

    // Delegation: Logik liegt jetzt in WinCanValueNormalizer
    private static string? NormalizeNumber(string? raw)
        => WinCanValueNormalizer.NormalizeNumber(raw);

    // Delegation: Logik liegt jetzt in WinCanValueNormalizer
    private static string? NormalizeDate(string? yearText, string? rawDate)
        => WinCanValueNormalizer.NormalizeDate(yearText, rawDate);

    // Delegation: Logik liegt jetzt in Common.MaterialTextNormalizer
    private static string? NormalizeMaterial(string? raw)
        => Common.MaterialTextNormalizer.Normalize(raw);

    // Delegation: Logik liegt jetzt in WinCanValueNormalizer
    private static string? NormalizeUsage(string? raw)
        => WinCanValueNormalizer.NormalizeUsage(raw);

    // Delegation: Logik liegt jetzt in WinCanValueNormalizer
    private static string? NormalizeInspectionDir(string? raw)
        => WinCanValueNormalizer.NormalizeInspectionDir(raw);

    /// <summary>
    /// Die Untersuchung im Bericht. Nur ein glaubwuerdiges Startdatum ist ein Aufnahmetag.
    /// Der technische Sortierschluessel darf die richtige Untersuchung waehlen, wird aber
    /// nie als Untersuchungsdatum ausgegeben.
    /// </summary>
    private static string Datumstext(WinCanDbInspection inspection)
    {
        if (inspection.HatWinCanVorgabedatum)
            return "WinCan-Platzhalterdatum (kein glaubwuerdiges Untersuchungsdatum)";

        var startdatum = WinCanValueNormalizer.ParseSqliteDate(inspection.StartDate);
        return startdatum is null
            ? "ohne glaubwuerdiges Untersuchungsdatum"
            : startdatum.Value.ToString("dd.MM.yyyy", System.Globalization.CultureInfo.InvariantCulture);
    }

    // Delegation: Logik liegt jetzt in WinCanValueNormalizer
    private static string? NormalizeAccessible(string? raw)
        => WinCanValueNormalizer.NormalizeAccessible(raw);

}
