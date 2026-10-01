using System.Text;
using System.Xml.Linq;
using System.Text.Json.Nodes;
using ImportRunContext = AuswertungPro.Next.Application.Import.ImportRunContext;
using ImportLogStatus = AuswertungPro.Next.Application.Import.ImportLogStatus;
using ImportProgress = AuswertungPro.Next.Application.Import.ImportProgress;
using IVsaMediaPathResolver = AuswertungPro.Next.Application.Import.IVsaMediaPathResolver;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Common;
using AuswertungPro.Next.Infrastructure.Import.Xtf.Sia405;
using AuswertungPro.Next.Infrastructure.Import.Xtf.VsaKek;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf;

public sealed partial class LegacyXtfImportService
{
    public ImportStats ImportXtfFiles(IEnumerable<string> xtfPaths, Project project, ImportRunContext? ctx = null)
    {
        var stats = new ImportStats();
        var archiveSources = ctx?.DryRun != true;

        if (archiveSources)
            TryMigrateLegacyArchive(stats);

        var pathList = xtfPaths.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        var fileIndex = 0;

        foreach (var path in pathList)
        {
            ctx?.CancellationToken.ThrowIfCancellationRequested();
            fileIndex++;
            ctx?.Progress?.Report(new ImportProgress(
                "Dateien lesen", fileIndex, pathList.Count,
                $"XTF {fileIndex}/{pathList.Count}", Path.GetFileName(path)));
            ctx?.Log.AddEntry("XTF", "StartFile", ImportLogStatus.Info,
                sourceFile: path, detail: Path.GetFileName(path));

            try
            {
                if (!File.Exists(path))
                    throw new FileNotFoundException($"Datei nicht gefunden: {path}");

                var ext = Path.GetExtension(path).ToLowerInvariant();

                // Rohdaten-Archiv ist nur ein Sicherheitsnetz. Ein Kopierfehler darf den
                // fachlichen Import der Originaldatei nicht verhindern.
                if (archiveSources)
                    TryArchiveSource(path, stats);

                if (ext == ".mdb")
                {
                    ImportMdb(path, project, stats, ctx);
                    continue;
                }

                if (ext is ".m150" or ".xml")
                {
                    ImportM150(path, project, stats, ctx);
                    continue;
                }

                if (ext != ".xtf")
                {
                    stats.Messages.Add(new ImportMessage
                    {
                        Level = "Warn",
                        Context = "IMPORT",
                        Message = $"Nicht unterstützte Datei übersprungen: {Path.GetFileName(path)}"
                    });
                    continue;
                }

                ImportXtf(path, project, stats, _mediaPaths, ctx);
            }
            catch (Exception ex)
            {
                stats.Errors++;
                stats.Messages.Add(new ImportMessage { Level = "Error", Context = "IMPORT", Message = $"{Path.GetFileName(path)}: {ex.Message}" });
            }
        }

        project.ModifiedAtUtc = DateTime.UtcNow;
        project.Dirty = true;

        return stats;
    }

    private void TryArchiveSource(string sourcePath, ImportStats stats)
    {
        if (string.IsNullOrWhiteSpace(_archiveRoot))
            return;

        try
        {
            Directory.CreateDirectory(_archiveRoot);
            var targetPath = Path.Combine(_archiveRoot, Path.GetFileName(sourcePath));
            if (!File.Exists(targetPath))
                File.Copy(sourcePath, targetPath, overwrite: false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PathTooLongException)
        {
            stats.Messages.Add(new ImportMessage
            {
                Level = "Warn",
                Context = "XTF-ARCHIV",
                Message = $"Rohdatenkopie fehlgeschlagen, Import läuft weiter: {ex.Message}"
            });
        }
    }

    private void TryMigrateLegacyArchive(ImportStats stats)
    {
        if (string.IsNullOrWhiteSpace(_archiveRoot)
            || string.IsNullOrWhiteSpace(_legacyArchiveRoot)
            || !Directory.Exists(_legacyArchiveRoot)
            || string.Equals(_archiveRoot, _legacyArchiveRoot, StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            Directory.CreateDirectory(_archiveRoot);
            foreach (var oldPath in Directory.EnumerateFiles(_legacyArchiveRoot))
            {
                var targetPath = BuildUniquePath(Path.Combine(_archiveRoot, Path.GetFileName(oldPath)));
                File.Move(oldPath, targetPath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PathTooLongException)
        {
            stats.Messages.Add(new ImportMessage
            {
                Level = "Warn",
                Context = "XTF-ARCHIV",
                Message = $"Altes XTF-Rohdatenarchiv konnte nicht vollständig verschoben werden: {ex.Message}"
            });
        }
    }

    private static string BuildUniquePath(string desiredPath)
    {
        if (!File.Exists(desiredPath))
            return desiredPath;

        var directory = Path.GetDirectoryName(desiredPath)!;
        var name = Path.GetFileNameWithoutExtension(desiredPath);
        var extension = Path.GetExtension(desiredPath);
        for (var number = 1; ; number++)
        {
            var candidate = Path.Combine(directory, $"{name}_{number}{extension}");
            if (!File.Exists(candidate))
                return candidate;
        }
    }

    private void ImportXtf(string path, Project project, ImportStats stats,
        IVsaMediaPathResolver mediaPaths, ImportRunContext? ctx = null)
    {
        var (doc, isSia405, isVsa) = _sourceReader.Read(path);

        // SIA405 und VSA_KEK koennen beide im Header stehen (VSA_KEK referenziert SIA405_Abwasser als Dependency).
        // Primaeres Modell bestimmen: wenn VSA_KEK-Daten vorhanden, diese bevorzugen.
        var sia405Imported = false;
        if (isSia405)
        {
            // Schaechte sind unabhaengig von den Haltungen: Eine Datei kann Normschaechte
            // ohne Kanaele enthalten, und umgekehrt (Goeschenen hat 17 Haltungen und
            // null Schaechte).
            var schaechte = ParseSia405Schaechte(doc);
            if (schaechte.Count > 0)
            {
                var beruehrt = MergeSchaechteIntoProject(project, schaechte, stats, ctx);
                if (beruehrt > 0)
                {
                    stats.Messages.Add(new ImportMessage
                    {
                        Level = "Info",
                        Context = "XTF405",
                        Message = $"Importiert {beruehrt} Schaechte aus {Path.GetFileName(path)}"
                    });
                }
            }

            var records = ParseSia405(doc, out var meldungen);
            stats.Messages.AddRange(meldungen);

            if (records.Count > 0)
            {
                sia405Imported = true;
                stats.Found += records.Count;

                foreach (var rec in records)
                    MergeRecordIntoProject(project, rec, FieldSource.Xtf405, stats, ctx);

                project.ImportHistory.Add(new JsonObject
                {
                    ["type"] = "xtf405",
                    ["file"] = Path.GetFileName(path),
                    ["timestampUtc"] = DateTime.UtcNow.ToString("o"),
                    ["count"] = records.Count
                });

                stats.Messages.Add(new ImportMessage { Level = "Info", Context = "XTF405", Message = $"Importiert {records.Count} Haltungen aus {Path.GetFileName(path)}" });

                // SIA405 hat Vorrang; die VSA-KEK-Untersuchungen derselben Datei bleiben
                // liegen. Bis 01.10.2026 geschah das ohne Meldung.
                var liegenGeblieben = isVsa ? VsaKekObjektLeser.Lies(doc).Untersuchungen.Count : 0;
                if (liegenGeblieben > 0)
                {
                    stats.Messages.Add(new ImportMessage
                    {
                        Level = "Warn",
                        Context = "XTF",
                        Message = $"{Path.GetFileName(path)}: Datei enthält zusätzlich {liegenGeblieben} VSA-KEK-Untersuchung(en), "
                                  + "die nicht übernommen wurden – die SIA405-Haltungen haben Vorrang."
                    });
                }
            }
            else if (isVsa)
            {
                // SIA405-Header vorhanden aber keine SIA405-Daten → VSA_KEK als primaeres Modell verwenden
                stats.Messages.Add(new ImportMessage { Level = "Info", Context = "XTF", Message = $"SIA405-Header erkannt, aber keine SIA405-Daten. Fallback auf VSA_KEK." });
            }
        }

        if (isSia405)
            XtfZusatzReader.Uebernehme(doc, project, stats);

        // VSA_KEK verarbeiten, wenn NICHT bereits erfolgreich als SIA405 importiert
        if (!sia405Imported && isVsa)
        {
            var ergebnis = ParseVsaKek(doc, path, mediaPaths, out var luecken);
            stats.Found += ergebnis.Haltungen.Count;

            foreach (var rec in ergebnis.Haltungen)
                MergeRecordIntoProject(project, rec, FieldSource.Xtf, stats, ctx);

            var beruehrteSchaechte = MergeVsaKekSchaechteIntoProject(project, ergebnis.Schaechte, stats, ctx);

            project.ImportHistory.Add(new JsonObject
            {
                ["type"] = "xtf",
                ["file"] = Path.GetFileName(path),
                ["timestampUtc"] = DateTime.UtcNow.ToString("o"),
                ["count"] = ergebnis.Haltungen.Count,
                ["schaechte"] = beruehrteSchaechte
            });

            // Bauwerke, Untersuchungen und ungeklaerte Faelle getrennt zaehlen. Eine
            // einzige Zahl "Untersuchungen" liess frueher offen, wie viele Bauwerke
            // daraus wurden — und verdeckte, dass Schaechte als Haltungen ankamen.
            stats.Messages.Add(new ImportMessage
            {
                Level = "Info",
                Context = "XTF",
                Message = $"{Path.GetFileName(path)}: {ergebnis.Untersuchungen} Untersuchungen gelesen — "
                          + $"{ergebnis.Haltungen.Count} Haltung(en), {beruehrteSchaechte} Schaecht(e), "
                          + $"{ergebnis.Offene.Count} ungeklärt."
            });

            foreach (var offen in ergebnis.Offene)
            {
                stats.Uncertain++;
                stats.Messages.Add(new ImportMessage
                {
                    Level = "Warn",
                    Context = "XTF",
                    Message = $"Untersuchung \"{offen.Bezeichnung}\" nicht zugeordnet: {offen.Grund}"
                });
            }

            stats.Messages.AddRange(luecken);

            // Weitere Untersuchungen derselben Haltung (z.B. Gegenbefahrung): als eigene
            // Protokollfassung ablegen, erst nach der Uebernahme der Haupt-Untersuchungen.
            VsaKekWeitereUntersuchungen.LegeAb(project, ergebnis.Weitere, stats);
        }

        if (!isSia405 && !isVsa)
        {
            stats.Messages.Add(new ImportMessage { Level = "Warn", Context = "XTF", Message = $"Unbekanntes Schema (kein SIA405/VSA_KEK erkannt): {Path.GetFileName(path)}" });
        }
    }

    private void ImportM150(string path, Project project, ImportStats stats, ImportRunContext? ctx = null)
    {
        var (hgCount, hiCount) = M150MdbImportHelper.GetM150XmlNodeCounts(path, _m150SourceFiles);
        var createdBefore = stats.CreatedRecords;
        var updatedBefore = stats.UpdatedRecords;

        var records = M150MdbImportHelper.ParseM150File(path, _m150SourceFiles, out var warnings);
        stats.Found += records.Count;

        foreach (var rec in records)
            MergeRecordIntoProject(project, rec, FieldSource.Xtf, stats, ctx);

        var createdDelta = stats.CreatedRecords - createdBefore;
        var updatedDelta = stats.UpdatedRecords - updatedBefore;

        foreach (var warning in warnings)
        {
            stats.Messages.Add(new ImportMessage
            {
                Level = "Warn",
                Context = "M150",
                Message = $"{Path.GetFileName(path)}: {warning}"
            });
        }

        project.ImportHistory.Add(new JsonObject
        {
            ["type"] = "m150",
            ["file"] = Path.GetFileName(path),
            ["timestampUtc"] = DateTime.UtcNow.ToString("o"),
            ["count"] = records.Count
        });

        stats.Messages.Add(new ImportMessage
        {
            Level = "Info",
            Context = "M150",
            Message = $"Importiert {records.Count} Haltungen aus {Path.GetFileName(path)}"
        });

        stats.Messages.Add(new ImportMessage
        {
            Level = "Info",
            Context = "M150",
            Message = $"M150-Details: HG erkannt={hgCount}, HI erkannt={hiCount}, übernommen={records.Count}, neu={Math.Max(0, createdDelta)}, aktualisiert={Math.Max(0, updatedDelta)}"
        });
    }

    private void ImportMdb(string path, Project project, ImportStats stats, ImportRunContext? ctx = null)
    {
        if (!M150MdbImportHelper.TryParseMdbFile(
                path,
                _m150MdbRows,
                out var records,
                out var error,
                out var warnings))
            throw new InvalidOperationException(error ?? $"MDB Import fehlgeschlagen: {Path.GetFileName(path)}");

        stats.Found += records.Count;
        foreach (var rec in records)
            MergeRecordIntoProject(project, rec, FieldSource.Xtf, stats, ctx);

        foreach (var warning in warnings)
        {
            stats.Messages.Add(new ImportMessage
            {
                Level = "Warn",
                Context = "MDB",
                Message = $"{Path.GetFileName(path)}: {warning}"
            });
        }

        project.ImportHistory.Add(new JsonObject
        {
            ["type"] = "mdb",
            ["file"] = Path.GetFileName(path),
            ["timestampUtc"] = DateTime.UtcNow.ToString("o"),
            ["count"] = records.Count
        });

        stats.Messages.Add(new ImportMessage
        {
            Level = "Info",
            Context = "MDB",
            Message = $"Importiert {records.Count} Haltungen aus {Path.GetFileName(path)}"
        });
    }

    private static void MergeRecordIntoProject(Project project, HaltungRecord source, FieldSource importSource, ImportStats stats, ImportRunContext? ctx = null)
    {
        var key = NormalizeHoldingKey(source.GetFieldValue("Haltungsname"));
        if (string.IsNullOrWhiteSpace(key))
        {
            stats.Errors++;
            stats.Messages.Add(new ImportMessage { Level = "Error", Context = "XTF", Message = "Record ohne Haltungsname übersprungen." });
            return;
        }

        var target = project.Data.FirstOrDefault(r =>
            string.Equals(NormalizeHoldingKey(r.GetFieldValue("Haltungsname")), key, StringComparison.OrdinalIgnoreCase));
        bool created = false;
        if (target is null)
        {
            target = new HaltungRecord();
            target.SetFieldValue("Haltungsname", key, importSource, userEdited: false);
            if (ctx is null)
                project.Data.Add(target);
            else
                ctx.WithCollectionLock(() => project.Data.Add(target));
            created = true;
            stats.CreatedRecords++;
        }

        var merge = MergeEngine.MergeRecord(target, source, importSource, ctx: ctx);
        stats.UpdatedFields += merge.Updated;
        if (!created && merge.Updated > 0) stats.UpdatedRecords++;
        stats.Conflicts += merge.Conflicts;
        stats.Errors += merge.Errors;

        if (source.VsaFindings is not null && source.VsaFindings.Count > 0)
        {
            target.VsaFindings = new List<VsaFinding>(source.VsaFindings);
            VsaFindingProtocolSynchronizer.Sync(target, target.VsaFindings);
        }

        // Ankerangabe der eingelesenen Datei uebernehmen. Sie gehoert zum aktuellen
        // Importstand; ohne diese Zeile ginge sie beim Zusammenfuehren mit einem
        // bestehenden Datensatz verloren. Ein vorhandener Anker bleibt erhalten,
        // wenn die Quelle keinen mitbringt.
        if (source.XtfHerkunft is not null)
            target.XtfHerkunft = source.XtfHerkunft;

        foreach (var c in merge.ConflictDetails)
        {
            stats.ConflictDetails.Add(c);
            project.Conflicts.Add(c);
        }
    }

    // Delegation: Logik liegt jetzt in Common.HoldingKeyNormalizer
    private static string NormalizeHoldingKey(string? value)
        => Common.HoldingKeyNormalizer.Normalize(value);

    // ===================== SIA405 =====================
    private static List<HaltungRecord> ParseSia405(XDocument doc, out List<ImportMessage> meldungen)
    {
        // Drei getrennte Schritte, damit eine neue Feldregel nur die Abbildung beruehrt:
        // 1. Objekte lesen — die Verweise bleiben Kennungen,
        // 2. Bezuege aufloesen — Kanal, Rohrprofil, Organisationen, Schachtnamen,
        //    danach je Bezeichnung nur die erste Haltung (keine Vermischung); was sich
        //    nicht aufloesen liess, meldet Sia405Bezugsmeldungen (seit 01.10.2026),
        // 3. fachlich abbilden — welche Angabe in welches Programmfeld geht.
        // Die Uebernahme ins Projekt (Handwertschutz, Konflikte) macht danach
        // MergeRecordIntoProject.
        var bestand = Sia405ObjektLeser.Lies(doc);
        var haltungen = Sia405DoppelteBezeichnungen.NurErste(Sia405Beziehungen.Loese(bestand, out var ohneNamen), out var doppelte);
        meldungen = doppelte.Select(m => new ImportMessage { Level = "Warn", Context = "XTF405", Message = m }).ToList();
        meldungen.AddRange(Sia405Bezugsmeldungen.Erzeuge(bestand, haltungen, ohneNamen));
        return haltungen.Select(Sia405HaltungAbbildung.BaueRecord).ToList();
    }

    // ===================== VSA_KEK =====================
    private static XtfVsaKekErgebnis ParseVsaKek(XDocument doc, string sourcePath,
        IVsaMediaPathResolver mediaPaths, out List<ImportMessage> luecken)
    {
        // Gleiche drei Schritte wie bei SIA405:
        // 1. Objekte lesen — Untersuchung, Kanal-/Normschachtschaden, Datei, Bauwerke,
        // 2. Bezuege aufloesen — Schaden und Datei zur Untersuchung ueber die TID, dann
        //    Haltung, Schacht oder ungeklaert; danach je Haltung die Haupt-Untersuchung
        //    waehlen (die vollstaendigste, die weiteren werden Protokollfassungen),
        // 3. fachlich abbilden — Haltungsfelder, Schachtprotokoll, Importbeleg.
        // Die Uebernahme ins Projekt machen danach MergeRecordIntoProject,
        // MergeVsaKekSchaechteIntoProject und VsaKekWeitereUntersuchungen.
        var bestand = VsaKekObjektLeser.Lies(doc);
        var bezuege = VsaKekBeziehungen.Loese(bestand, sourcePath, mediaPaths);
        // Nicht Zuordenbares wird nicht uebernommen, aber seit 01.10.2026 gemeldet.
        luecken = VsaKekLueckenmeldungen.Erzeuge(bezuege);
        var gruppen = VsaKekUntersuchungsWahl.Waehle(bezuege.Haltungsuntersuchungen, u => u.Bezeichnung, VsaKekAbbildung.Merkmale);
        return VsaKekAbbildung.Baue(bezuege, gruppen, sourcePath, bestand.ModellName);
    }
}
