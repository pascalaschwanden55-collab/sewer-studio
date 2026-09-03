using System.Text;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using System.Text.Json.Nodes;
using ImportRunContext = AuswertungPro.Next.Application.Import.ImportRunContext;
using ImportLogStatus = AuswertungPro.Next.Application.Import.ImportLogStatus;
using ImportProgress = AuswertungPro.Next.Application.Import.ImportProgress;
using IVsaMediaPathResolver = AuswertungPro.Next.Application.Import.IVsaMediaPathResolver;
using System.Globalization;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Xtf;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Common;

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
                        Message = $"Nicht unterstuetzte Datei uebersprungen: {Path.GetFileName(path)}"
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
                Message = $"Rohdatenkopie fehlgeschlagen, Import laeuft weiter: {ex.Message}"
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
                Message = $"Altes XTF-Rohdatenarchiv konnte nicht vollstaendig verschoben werden: {ex.Message}"
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

            var records = ParseSia405(doc);
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
            }
            else if (isVsa)
            {
                // SIA405-Header vorhanden aber keine SIA405-Daten → VSA_KEK als primaeres Modell verwenden
                stats.Messages.Add(new ImportMessage { Level = "Info", Context = "XTF", Message = $"SIA405-Header erkannt, aber keine SIA405-Daten. Fallback auf VSA_KEK." });
            }
        }

        // VSA_KEK verarbeiten, wenn NICHT bereits erfolgreich als SIA405 importiert
        if (!sia405Imported && isVsa)
        {
            var records = ParseVsaKek(doc, path, mediaPaths, out _);
            stats.Found += records.Count;

            foreach (var rec in records)
                MergeRecordIntoProject(project, rec, FieldSource.Xtf, stats, ctx);

            project.ImportHistory.Add(new JsonObject
            {
                ["type"] = "xtf",
                ["file"] = Path.GetFileName(path),
                ["timestampUtc"] = DateTime.UtcNow.ToString("o"),
                ["count"] = records.Count
            });

            stats.Messages.Add(new ImportMessage { Level = "Info", Context = "XTF", Message = $"Importiert {records.Count} Untersuchungen aus {Path.GetFileName(path)}" });
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
            Message = $"M150-Details: HG erkannt={hgCount}, HI erkannt={hiCount}, uebernommen={records.Count}, neu={Math.Max(0, createdDelta)}, aktualisiert={Math.Max(0, updatedDelta)}"
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
    private sealed class KanalData
    {
        public string Tid { get; init; } = "";
        public string Bezeichnung { get; set; } = "";
        public string Standortname { get; set; } = "";
        public string Status { get; set; } = "";
        public string Nutzungsart { get; set; } = "";
        public string Bemerkung { get; set; } = "";
        public string Zugaenglichkeit { get; set; } = "";

        /// <summary>
        /// Die Zustandsklasse aus der Datei (Z0 bis Z4). Sie wurde frueher nicht gelesen,
        /// wodurch jeder Import den Zustand verlor: Die nachlaufende VSA-Bewertung fand in
        /// einer Stammdaten-XTF keine Befunde und setzte "Leitung i.O." (Klasse 4). Aus
        /// einem exportierten Z0 wurde so beim Zurueckimportieren eine 4.
        /// </summary>
        public string BaulicherZustand { get; set; } = "";
        public string Eigentuemer { get; set; } = "";

        /// <summary>Die Kennung der verwiesenen Organisation.</summary>
        public string EigentuemerRef { get; set; } = "";
        public string DatenherrRef { get; set; } = "";
        public string DatenlieferantRef { get; set; } = "";
        public string Baujahr { get; set; } = "";
        public string Rohrlaenge { get; set; } = "";
        public string Funktion { get; set; } = "";
        // Die uebrigen Kanalfelder, die SewerStudio selbst hinausschreibt. Bis 2026-09-03
        // wurden sie nicht gelesen; die Rundreise Export, Import, Vergleich verlor sie.
        public string FunktionHydraulisch { get; set; } = "";
        public string Sanierungsbedarf { get; set; } = "";
        public string Verbindungsart { get; set; } = "";
        public string BettungUmhuellung { get; set; } = "";
        public string Bruttokosten { get; set; } = "";
    }

    private sealed class HaltungData
    {
        public string Tid { get; init; } = "";
        public string Bezeichnung { get; set; } = "";
        public string Laenge { get; set; } = "";
        public string LichteHoehe { get; set; } = "";
        public string LichteBreite { get; set; } = "";
        public string Material { get; set; } = "";
        public string KanalRef { get; set; } = "";
        public string VonRef { get; set; } = "";
        public string NachRef { get; set; } = "";
        public string LetzteAenderung { get; set; } = "";
        /// <summary>Die Kennung des Rohrprofils, das Profiltyp und Hoehen-Breiten-Verhaeltnis traegt.</summary>
        public string RohrprofilRef { get; set; } = "";
        public string Lagebestimmung { get; set; } = "";
    }

    private static List<HaltungRecord> ParseSia405(XDocument doc)
    {
        var kanaele = new Dictionary<string, KanalData>(StringComparer.OrdinalIgnoreCase);
        var kanaeleByBez = new Dictionary<string, KanalData>(StringComparer.OrdinalIgnoreCase);
        var haltungen = new Dictionary<string, HaltungData>(StringComparer.OrdinalIgnoreCase);
        var haltungspunkte = new Dictionary<string, (string Bezeichnung, string? AbwassernetzelementRef)>(StringComparer.OrdinalIgnoreCase);
        var abwasserknoten = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var rohrprofile = new Dictionary<string, (string Profiltyp, string Verhaeltnis)>(StringComparer.OrdinalIgnoreCase);

        // Die Organisationen der Datei, damit EigentuemerRef aufgeloest werden kann.
        // Sie stehen im Topic "Administration", also ausserhalb der Fachdaten-Baskets —
        // deshalb ueber das ganze Dokument gelesen.
        var organisationen = LiesOrganisationen(doc);

        var baskets = doc.Descendants()
            .Where(e => e.Name.LocalName.EndsWith("SIA405_Abwasser.SIA405_Abwasser", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var scope = baskets.Count > 0 ? baskets.SelectMany(b => b.Descendants()) : doc.Descendants();

        foreach (var node in scope)
        {
            var local = node.Name.LocalName;

            // Kanal
            if (local.Equals("Kanal", StringComparison.OrdinalIgnoreCase) || local.EndsWith(".Kanal", StringComparison.OrdinalIgnoreCase))
            {
                var tid = (string?)node.Attribute("TID");
                if (string.IsNullOrWhiteSpace(tid)) continue;
                var kd = new KanalData { Tid = tid! };
                foreach (var child in node.Elements())
                {
                    switch (child.Name.LocalName)
                    {
                        case "Bezeichnung": kd.Bezeichnung = child.Value; break;
                        case "Standortname": kd.Standortname = child.Value; break;
                        case "Status": kd.Status = child.Value; break;
                        case "Nutzungsart_Ist": kd.Nutzungsart = child.Value; break;
                        case "Bemerkung": kd.Bemerkung = child.Value; break;
                        case "Zugaenglichkeit": kd.Zugaenglichkeit = child.Value; break;
                        case "BaulicherZustand": kd.BaulicherZustand = child.Value; break;
                        case "Eigentuemer": kd.Eigentuemer = child.Value; break;
                        // In SIA405 ist der Eigentuemer ein Verweis, kein Text. Die
                        // Kennung wird gemerkt und spaeter gegen die Organisationen der
                        // Datei aufgeloest — ohne das ging der Eigentuemer bei jedem
                        // Import verloren, und beim naechsten Export fehlte genau er.
                        case "EigentuemerRef":
                            kd.EigentuemerRef = (string?)child.Attribute("REF") ?? "";
                            break;
                        case "DatenherrRef":
                            kd.DatenherrRef = (string?)child.Attribute("REF") ?? "";
                            break;
                        case "DatenlieferantRef":
                            kd.DatenlieferantRef = (string?)child.Attribute("REF") ?? "";
                            break;
                        case "Baujahr": kd.Baujahr = child.Value; break;
                        case "Rohrlaenge": kd.Rohrlaenge = child.Value; break;
                        // Das Modell schreibt "FunktionHierarchisch" mit grossem H. Die zwei
                        // anderen Schreibweisen stammen aus aelteren Lieferungen; bis
                        // 2026-09-03 fehlte ausgerechnet die des Modells.
                        case "FunktionHierarchisch": kd.Funktion = child.Value; break;
                        case "Funktionhierarchisch": kd.Funktion = child.Value; break;
                        case "Funktion_hierarchisch": kd.Funktion = child.Value; break;
                        case "FunktionHydraulisch": kd.FunktionHydraulisch = child.Value; break;
                        case "Sanierungsbedarf": kd.Sanierungsbedarf = child.Value; break;
                        case "Verbindungsart": kd.Verbindungsart = child.Value; break;
                        case "Bettung_Umhuellung": kd.BettungUmhuellung = child.Value; break;
                        case "Bruttokosten": kd.Bruttokosten = child.Value; break;
                    }
                }
                kanaele[tid!] = kd;
                if (!string.IsNullOrWhiteSpace(kd.Bezeichnung))
                    kanaeleByBez[kd.Bezeichnung] = kd;
            }

            // Haltung
            if (local.Equals("Haltung", StringComparison.OrdinalIgnoreCase) || local.EndsWith(".Haltung", StringComparison.OrdinalIgnoreCase))
            {
                var tid = (string?)node.Attribute("TID");
                if (string.IsNullOrWhiteSpace(tid)) continue;
                var hd = new HaltungData { Tid = tid! };
                foreach (var child in node.Elements())
                {
                    switch (child.Name.LocalName)
                    {
                        case "Bezeichnung": hd.Bezeichnung = child.Value; break;
                        case "LaengeEffektiv": hd.Laenge = child.Value; break;
                        case "Lichte_Hoehe": hd.LichteHoehe = child.Value; break;
                        case "Lichte_Breite": hd.LichteBreite = child.Value; break;
                        case "Material": hd.Material = child.Value; break;
                        case "Letzte_Aenderung": hd.LetzteAenderung = child.Value; break;
                        case "AbwasserbauwerkRef": hd.KanalRef = (string?)child.Attribute("REF") ?? ""; break;
                        case "vonHaltungspunktRef": hd.VonRef = (string?)child.Attribute("REF") ?? ""; break;
                        case "nachHaltungspunktRef": hd.NachRef = (string?)child.Attribute("REF") ?? ""; break;
                        case "RohrprofilRef": hd.RohrprofilRef = (string?)child.Attribute("REF") ?? ""; break;
                        case "Lagebestimmung": hd.Lagebestimmung = child.Value; break;
                    }
                }
                haltungen[tid!] = hd;
            }

            // Rohrprofil: Profiltyp und Hoehen-Breiten-Verhaeltnis haengen nicht an der
            // Haltung, sondern an diesem Objekt. Ohne es kaeme die Breite eines Rechteck-
            // oder Eiprofils nie im Programm an.
            if (local.Equals("Rohrprofil", StringComparison.OrdinalIgnoreCase) || local.EndsWith(".Rohrprofil", StringComparison.OrdinalIgnoreCase))
            {
                var tid = (string?)node.Attribute("TID");
                if (string.IsNullOrWhiteSpace(tid)) continue;
                string profiltyp = "", verhaeltnis = "";
                foreach (var child in node.Elements())
                {
                    switch (child.Name.LocalName)
                    {
                        case "Profiltyp": profiltyp = child.Value; break;
                        case "HoehenBreitenverhaeltnis": verhaeltnis = child.Value; break;
                    }
                }
                rohrprofile[tid!] = (profiltyp, verhaeltnis);
            }

            // Haltungspunkt
            if (local.Equals("Haltungspunkt", StringComparison.OrdinalIgnoreCase) || local.EndsWith(".Haltungspunkt", StringComparison.OrdinalIgnoreCase))
            {
                var tid = (string?)node.Attribute("TID");
                if (string.IsNullOrWhiteSpace(tid)) continue;
                string bezeichnung = "";
                string? abwRef = null;
                foreach (var child in node.Elements())
                {
                    switch (child.Name.LocalName)
                    {
                        case "Bezeichnung": bezeichnung = child.Value; break;
                        case "AbwassernetzelementRef": abwRef = (string?)child.Attribute("REF"); break;
                    }
                }
                haltungspunkte[tid!] = (bezeichnung, abwRef);
            }

            // Abwasserknoten
            if (local.Equals("Abwasserknoten", StringComparison.OrdinalIgnoreCase) || local.EndsWith(".Abwasserknoten", StringComparison.OrdinalIgnoreCase))
            {
                var tid = (string?)node.Attribute("TID");
                if (string.IsNullOrWhiteSpace(tid)) continue;
                string bezeichnung = "";
                foreach (var child in node.Elements())
                {
                    if (child.Name.LocalName == "Bezeichnung")
                        bezeichnung = child.Value;
                }
                abwasserknoten[tid!] = bezeichnung;
            }
        }

        // Hilfsfunktion für Schacht-Label
        string? ResolveSchachtLabel(string? refTid, string haltungsname, bool oben)
        {
            if (string.IsNullOrWhiteSpace(refTid)) return null;
            if (haltungspunkte.TryGetValue(refTid, out var hp))
            {
                // Der Abwasserknoten IST der Schacht; die Bezeichnung des Haltungspunkts
                // ist nur ein technischer Name. Im Kantonsexport heisst er "u-80401_von",
                // im SewerStudio-Export "<Haltung>_von" — beides sind keine Schachtnummern.
                // Die umgekehrte Reihenfolge schrieb genau diese Namen in "Schacht_oben".
                if (!string.IsNullOrWhiteSpace(hp.AbwassernetzelementRef) && abwasserknoten.TryGetValue(hp.AbwassernetzelementRef, out var knBez))
                    return knBez;

                // Ohne Knoten: Heisst der Punkt "<Haltung>_von" / "<Haltung>_nach" und der
                // Haltungsname ist "oben-unten", dann steckt der Schacht im Haltungsnamen.
                // So kam beim Rueckimport eines SewerStudio-Exports "78998-79002_nach" in
                // "Schacht_unten" an, obwohl "79002" gemeint war.
                var ausHaltung = SchachtAusHaltungsname(hp.Bezeichnung, haltungsname, oben);
                if (ausHaltung is not null) return ausHaltung;

                if (!string.IsNullOrWhiteSpace(hp.Bezeichnung)) return hp.Bezeichnung;
            }
            return null;
        }

        static string? SchachtAusHaltungsname(string? punktname, string haltungsname, bool oben)
        {
            var punkt = (punktname ?? "").Trim();
            var name = (haltungsname ?? "").Trim();
            var erwartet = name + (oben ? "_von" : "_nach");
            if (name.Length == 0 || !string.Equals(punkt, erwartet, StringComparison.OrdinalIgnoreCase))
                return null;

            var trenner = name.IndexOf('-');
            if (trenner <= 0 || trenner >= name.Length - 1 || name.IndexOf('-', trenner + 1) >= 0)
                return null;

            return oben ? name[..trenner] : name[(trenner + 1)..];
        }

        string? ResolveKnotenName(string? refTid)
        {
            if (string.IsNullOrWhiteSpace(refTid)) return null;
            if (!haltungspunkte.TryGetValue(refTid, out var hp)) return null;
            if (!string.IsNullOrWhiteSpace(hp.AbwassernetzelementRef) && abwasserknoten.TryGetValue(hp.AbwassernetzelementRef, out var knBez))
                return knBez;
            return string.IsNullOrWhiteSpace(hp.Bezeichnung) ? null : hp.Bezeichnung;
        }

        var records = new List<HaltungRecord>();
        foreach (var hd in haltungen.Values)
        {
            KanalData? kanal = null;
            if (!string.IsNullOrWhiteSpace(hd.KanalRef) && kanaele.TryGetValue(hd.KanalRef, out var kdByRef))
                kanal = kdByRef;
            else if (!string.IsNullOrWhiteSpace(hd.Bezeichnung) && kanaeleByBez.TryGetValue(hd.Bezeichnung, out var kdByBez))
                kanal = kdByBez;

            var haltungsname = !string.IsNullOrWhiteSpace(hd.Bezeichnung) ? hd.Bezeichnung : (kanal?.Bezeichnung ?? "");
            if (string.IsNullOrWhiteSpace(haltungsname))
                continue;

            var material = NormalizeSiaMaterial(hd.Material);
            var nutzungsart = kanal is null ? "" : NormalizeNutzungsart(kanal.Nutzungsart);

            var rec = new HaltungRecord();
            rec.SetFieldValue("Haltungsname", haltungsname, FieldSource.Xtf405, userEdited: false);
            rec.SetFieldValue(FieldKeys.CadastreObjectId, hd.Tid, FieldSource.Xtf405, userEdited: false);

            // Ein Normwert aus der Datei, so wie er dort steht. "unbekannt" ist keine
            // Angabe und wuerde nur einen besseren Wert aus einer anderen Quelle blockieren.
            void Uebernimm(string feld, string? wert, bool unbekanntIstLeer = true)
            {
                var text = (wert ?? "").Trim();
                if (text.Length == 0
                    || (unbekanntIstLeer
                        && string.Equals(text, "unbekannt", StringComparison.OrdinalIgnoreCase)))
                    return;
                rec.SetFieldValue(feld, text, FieldSource.Xtf405, userEdited: false);
            }
            if (!string.IsNullOrWhiteSpace(hd.Laenge)) rec.SetFieldValue("Haltungslaenge_m", hd.Laenge, FieldSource.Xtf405, userEdited: false);
            if (!string.IsNullOrWhiteSpace(material)) rec.SetFieldValue("Rohrmaterial", material, FieldSource.Xtf405, userEdited: false);

            var dn = !string.IsNullOrWhiteSpace(hd.LichteHoehe) ? hd.LichteHoehe : hd.LichteBreite;
            if (!string.IsNullOrWhiteSpace(dn)) rec.SetFieldValue("DN_mm", dn, FieldSource.Xtf405, userEdited: false);

            // Die zweite Dimension: Profiltyp und Hoehen-Breiten-Verhaeltnis stehen am
            // verwiesenen Rohrprofil. Aus Hoehe und Verhaeltnis entsteht die Breite;
            // beim Kreisprofil ist sie gleich der Hoehe (rund = beide gleich).
            if (!string.IsNullOrWhiteSpace(hd.RohrprofilRef) && rohrprofile.TryGetValue(hd.RohrprofilRef, out var profil))
            {
                var profiltyp = (profil.Profiltyp ?? "").Trim();
                if (profiltyp.Length > 0)
                {
                    rec.SetFieldValue(
                        FieldKeys.ProfileType,
                        ProfiltypVokabular.Normalisieren(profiltyp),
                        FieldSource.Xtf405,
                        userEdited: false);
                }

                var breite = XtfRohrprofilVerhaeltnis.Breite(dn, profil.Verhaeltnis)
                             ?? (string.Equals(profiltyp, "Kreisprofil", StringComparison.OrdinalIgnoreCase)
                                 ? SiaAbmessung.NachMillimeter(dn)
                                 : null);
                if (breite is > 0)
                    rec.SetFieldValue(FieldKeys.ClearWidthMm, breite.Value.ToString(CultureInfo.InvariantCulture), FieldSource.Xtf405, userEdited: false);
            }
            else if (!string.IsNullOrWhiteSpace(hd.LichteHoehe) && !string.IsNullOrWhiteSpace(hd.LichteBreite))
            {
                // Aeltere Modellfassungen fuehren die Breite direkt an der Haltung.
                rec.SetFieldValue(FieldKeys.ClearWidthMm, hd.LichteBreite.Trim(), FieldSource.Xtf405, userEdited: false);
            }

            var vonKnoten = ResolveKnotenName(hd.VonRef);
            var nachKnoten = ResolveKnotenName(hd.NachRef);
            // Keine Inspektionsrichtung: SIA405 ist der Kataster-Bestand und kennt keine Untersuchung.
            // Sie kommt aus der VSA-KEK-Untersuchung (<Fliessrichtung>), siehe ParseVsaKek.

            // Letzte_Aenderung ist das Aenderungsdatum des Datensatzes im Kataster, kein
            // Inspektionsdatum. Bis 2026-09-03 landete es in "Datum_Jahr" und ueberschrieb
            // dort das echte Inspektionsdatum aus WinCan: Aus 06.10.2025 wurde 03.09.2026.
            // Es gehoert in das Herkunftsfeld, das auch das QGIS-Nachfuellen verwendet.
            var letzteAenderung = NormalizeDate_yyyymmdd(hd.LetzteAenderung);
            if (!string.IsNullOrWhiteSpace(letzteAenderung))
                rec.SetFieldValue(FieldKeys.CadastreLastChange, letzteAenderung, FieldSource.Xtf405, userEdited: false);

            if (!string.IsNullOrWhiteSpace(hd.Lagebestimmung) && !string.Equals(hd.Lagebestimmung.Trim(), "unbekannt", StringComparison.OrdinalIgnoreCase))
                rec.SetFieldValue(FieldKeys.PositionAccuracy, hd.Lagebestimmung.Trim(), FieldSource.Xtf405, userEdited: false);

            if (kanal is not null)
            {
                if (!string.IsNullOrWhiteSpace(kanal.Standortname)) rec.SetFieldValue("Strasse", kanal.Standortname, FieldSource.Xtf405, userEdited: false);
                if (!string.IsNullOrWhiteSpace(nutzungsart)) rec.SetFieldValue("Nutzungsart", nutzungsart, FieldSource.Xtf405, userEdited: false);
                if (!string.IsNullOrWhiteSpace(kanal.Bemerkung)) rec.SetFieldValue("Bemerkungen", kanal.Bemerkung, FieldSource.Xtf405, userEdited: false);
                // Der Rohwert der XTF ("Abwasser Uri") faerbt die Eigentuemerspalte der
                // Excel-Vorlage nicht - sie vergleicht exakt gegen "AWU".
                var ausVerweis = organisationen.TryGetValue(kanal.EigentuemerRef ?? "", out var orgName)
                    ? orgName
                    : null;
                var eigentuemer = EigentumVokabular.Normalisieren(
                    string.IsNullOrWhiteSpace(kanal.Eigentuemer) ? ausVerweis : kanal.Eigentuemer);
                if (!string.IsNullOrWhiteSpace(eigentuemer)) rec.SetFieldValue("Eigentuemer", eigentuemer, FieldSource.Xtf405, userEdited: false);

                if (organisationen.TryGetValue(kanal.DatenherrRef, out var datenHerr))
                    Uebernimm(FieldKeys.DataOwner, datenHerr, unbekanntIstLeer: false);
                if (organisationen.TryGetValue(kanal.DatenlieferantRef, out var datenLieferant))
                    Uebernimm(FieldKeys.DataSupplier, datenLieferant, unbekanntIstLeer: false);

                // FunktionHierarchisch -> Katalog-Combo "PAA.<Suffix>" / "SAA.<Suffix>" (speist u.a. VSA-Zustandsnote B4)
                var funktion = NormalizeFunktionHierarchisch(kanal.Funktion);
                if (!string.IsNullOrWhiteSpace(funktion)) rec.SetFieldValue("FunktionHierarchisch", funktion, FieldSource.Xtf405, userEdited: false);

                // Baujahr ist das Baujahr, kein Inspektionsdatum. Bis 2026-09-03 fuellte es
                // ersatzweise "Datum_Jahr"; jetzt geht es in das Feld, das der Export liest.
                Uebernimm(FieldKeys.ConstructionYear, kanal.Baujahr);

                // Die uebrigen Kanalfelder, die der Export selbst hinausschreibt. Ohne sie
                // verlor die Rundreise Export, Import, Vergleich genau diese Werte.
                Uebernimm(FieldKeys.OperatingStatus, kanal.Status);
                Uebernimm(FieldKeys.RehabilitationNeed, kanal.Sanierungsbedarf);
                Uebernimm(FieldKeys.HydraulicFunction, kanal.FunktionHydraulisch);
                Uebernimm(FieldKeys.ConnectionType, kanal.Verbindungsart);
                Uebernimm(FieldKeys.BeddingEncasement, kanal.BettungUmhuellung);
                Uebernimm(FieldKeys.GrossCost, kanal.Bruttokosten);

                // Status -> offen/abgeschlossen (wie PS)
                var status = kanal.Status ?? "";
                if (!string.IsNullOrWhiteSpace(status))
                {
                    if (Regex.IsMatch(status, "(?i)in_Betrieb|aktiv"))
                        rec.SetFieldValue("Offen_abgeschlossen", "abgeschlossen", FieldSource.Xtf405, userEdited: false);
                    else if (Regex.IsMatch(status, "(?i)ausser_Betrieb|stillgelegt"))
                        rec.SetFieldValue("Offen_abgeschlossen", "offen", FieldSource.Xtf405, userEdited: false);
                }

                // Zustandsklasse aus der Datei uebernehmen. Sie gewinnt beim Import
                // bewusst gegen jede eigene Rechnung: Nur so sind die Daten in GEONIS
                // und in SewerStudio nach einem Austausch identisch.
                var zustand = ZustandsklasseAusXtf(kanal.BaulicherZustand);
                if (zustand is not null)
                    rec.SetFieldValue("Zustandsklasse", zustand, FieldSource.Xtf405, userEdited: false);

                // Zugaenglichkeit als Bemerkung ergänzen
                if (!string.IsNullOrWhiteSpace(kanal.Zugaenglichkeit) && !string.Equals(kanal.Zugaenglichkeit, "unbekannt", StringComparison.OrdinalIgnoreCase))
                {
                    var existing = rec.GetFieldValue("Bemerkungen") ?? "";
                    var add = $"Zugaenglichkeit: {kanal.Zugaenglichkeit}";
                    rec.SetFieldValue("Bemerkungen", string.IsNullOrWhiteSpace(existing) ? add : (existing + "\n" + add), FieldSource.Xtf405, userEdited: false);
                }
            }

            // Schacht-Labels (optional, für Debug/Logging)
            var schachtOben = ResolveSchachtLabel(hd.VonRef, haltungsname, oben: true);
            var schachtUnten = ResolveSchachtLabel(hd.NachRef, haltungsname, oben: false);
            if (!string.IsNullOrWhiteSpace(schachtOben)) rec.SetFieldValue("Schacht_oben", schachtOben, FieldSource.Xtf405, userEdited: false);
            if (!string.IsNullOrWhiteSpace(schachtUnten)) rec.SetFieldValue("Schacht_unten", schachtUnten, FieldSource.Xtf405, userEdited: false);

            records.Add(rec);
        }

        return records;
    }

    /// <summary>
    /// "Z0" bis "Z4" aus der XTF zur Ziffer, die das Programm fuehrt. Alles andere —
    /// "unbekannt", leer, ein unerwarteter Text — liefert <c>null</c> und setzt nichts.
    /// </summary>
    private static string? ZustandsklasseAusXtf(string? roh)
    {
        var wert = (roh ?? "").Trim();
        if (wert.Length != 2 || (wert[0] != 'Z' && wert[0] != 'z'))
            return null;

        return wert[1] is >= '0' and <= '4' ? wert[1].ToString() : null;
    }

    // Bekannte FunktionHierarchisch-Suffixe (ohne "PAA."-Praefix), passend zu FieldCatalog.ComboItems.
    private static readonly string[] FunktionHierarchischSuffixe =
    {
        "Sammelkanal", "Hauptsammelkanal", "Hauptsammelkanal_regional",
        "Liegenschaftsentwaesserung", "Sanierungsleitung",
        "Strassenentwaesserung", "Gewaesser"
    };

    /// <summary>
    /// Normalisiert die SIA405-Funktion (Funktionhierarchisch) auf einen GUELTIGEN Katalog-Combo-Wert
    /// "PAA.&lt;Suffix&gt;". Verarbeitet gaengige Rohformen (mit/ohne "PAA."-Praefix, Sub-Level-Trenner "."
    /// wie "Hauptsammelkanal.regional", Umlaute). Liefert leer, wenn der Rohwert keinem bekannten Suffix
    /// entspricht — dann wird das Feld NICHT gesetzt (kein ungueltiger Combo-Wert im Datagrid).
    /// </summary>
    private static string NormalizeFunktionHierarchisch(string? raw)
    {
        var v = (raw ?? "").Trim();
        if (string.IsNullOrWhiteSpace(v))
            return "";

        // Ein Normwert wie "SAA.Liegenschaftsentwaesserung" geht unveraendert durch.
        // Frueher wurde jeder Wert auf "PAA." umgeschrieben; die sekundaere Anlage (SAA)
        // fiel dabei heraus, obwohl der Kataster sie fuehrt und der Export sie schreibt.
        var norm = SiaKanalVokabular.FunktionHierarchisch.NachNorm(v);
        if (norm is not null && !norm.EndsWith(".unbekannt", StringComparison.OrdinalIgnoreCase))
            return norm;

        if (v.StartsWith("PAA.", StringComparison.OrdinalIgnoreCase))
            v = v.Substring(4);

        // Sub-Level-Trenner "." -> "_" (Hauptsammelkanal.regional -> Hauptsammelkanal_regional)
        v = v.Replace('.', '_');
        // Umlaute -> ASCII (Katalog nutzt ...entwaesserung)
        v = v.Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue")
             .Replace("Ä", "Ae").Replace("Ö", "Oe").Replace("Ü", "Ue");

        foreach (var known in FunktionHierarchischSuffixe)
            if (string.Equals(v, known, StringComparison.OrdinalIgnoreCase))
                return "PAA." + known;

        return "";
    }

    // Delegation: Logik liegt jetzt in XtfValueNormalizer
    private static string NormalizeSiaMaterial(string material)
        => XtfValueNormalizer.NormalizeSiaMaterial(material);

    // Delegation: Logik liegt jetzt in XtfValueNormalizer
    private static string NormalizeNutzungsart(string v)
        => XtfValueNormalizer.NormalizeNutzungsart(v);


    // Delegation: Logik liegt jetzt in XtfValueNormalizer
    private static bool TryParseDouble(string? s, out double value)
        => XtfValueNormalizer.TryParseDouble(s, out value);

    // Delegation: Logik liegt jetzt in XtfValueNormalizer
    private static string NormalizeDate_yyyymmdd(string? yyyymmdd)
        => XtfValueNormalizer.NormalizeDate_yyyymmdd(yyyymmdd);
}
