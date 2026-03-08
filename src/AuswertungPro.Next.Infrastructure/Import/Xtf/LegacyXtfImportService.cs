using System.Text;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using System.Text.Json.Nodes;
using System.Globalization;
using ImportRunContext = AuswertungPro.Next.Application.Import.ImportRunContext;
using ImportLogStatus = AuswertungPro.Next.Application.Import.ImportLogStatus;
using ImportProgress = AuswertungPro.Next.Application.Import.ImportProgress;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Infrastructure.Import.Common;



namespace AuswertungPro.Next.Infrastructure.Import.Xtf;

public sealed class LegacyXtfImportService
{
    public ImportStats ImportXtfFiles(IEnumerable<string> xtfPaths, Project project, ImportRunContext? ctx = null)
    {
        var stats = new ImportStats();

        var xtfTargetDir = Path.Combine(AppContext.BaseDirectory, "Rohdaten", "xtf_imports");
        Directory.CreateDirectory(xtfTargetDir);

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

                // Kopiere Quelldatei ins Projektverzeichnis (Rohdaten/xtf_imports)
                var xtfTargetPath = Path.Combine(xtfTargetDir, Path.GetFileName(path));
                if (!File.Exists(xtfTargetPath))
                    File.Copy(path, xtfTargetPath, overwrite: false);

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

                ImportXtf(path, project, stats, ctx);
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

    private static void ImportXtf(string path, Project project, ImportStats stats, ImportRunContext? ctx = null)
    {
        // Format-Erkennung via kleinem Puffer statt ganzer Datei in den Speicher
        bool isSia405 = false, isVsa = false;
        using (var sr = new StreamReader(path, Encoding.UTF8))
        {
            var buf = new char[4096];
            var read = sr.Read(buf, 0, buf.Length);
            var header = new string(buf, 0, read);
            isSia405 = header.Contains("SIA405", StringComparison.OrdinalIgnoreCase);
            isVsa = header.Contains("VSA_KEK", StringComparison.OrdinalIgnoreCase);
        }
        var doc = XDocument.Load(path, LoadOptions.PreserveWhitespace);

        if (isSia405)
        {
            var records = ParseSia405(doc);
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

        if (isVsa)
        {
            var records = ParseVsaKek(doc, path, out _);
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

    private static void ImportM150(string path, Project project, ImportStats stats, ImportRunContext? ctx = null)
    {
        var (hgCount, hiCount) = M150MdbImportHelper.GetM150XmlNodeCounts(path);
        var createdBefore = stats.CreatedRecords;
        var updatedBefore = stats.UpdatedRecords;

        var records = M150MdbImportHelper.ParseM150File(path, out var warnings);
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

    private static void ImportMdb(string path, Project project, ImportStats stats, ImportRunContext? ctx = null)
    {
        if (!M150MdbImportHelper.TryParseMdbFile(path, out var records, out var error, out var warnings))
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
            project.Data.Add(target);
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
            SyncProtocolFromFindings(target, target.VsaFindings);
        }

        foreach (var c in merge.ConflictDetails)
        {
            stats.ConflictDetails.Add(c);
            project.Conflicts.Add(c);
        }
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
        public string Eigentuemer { get; set; } = "";
        public string Baujahr { get; set; } = "";
        public string Rohrlaenge { get; set; } = "";
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
    }

    private static List<HaltungRecord> ParseSia405(XDocument doc)
    {
        var kanaele = new Dictionary<string, KanalData>(StringComparer.OrdinalIgnoreCase);
        var kanaeleByBez = new Dictionary<string, KanalData>(StringComparer.OrdinalIgnoreCase);
        var haltungen = new Dictionary<string, HaltungData>(StringComparer.OrdinalIgnoreCase);
        var haltungspunkte = new Dictionary<string, (string Bezeichnung, string? AbwassernetzelementRef)>(StringComparer.OrdinalIgnoreCase);
        var abwasserknoten = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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
                        case "Eigentuemer": kd.Eigentuemer = child.Value; break;
                        case "Baujahr": kd.Baujahr = child.Value; break;
                        case "Rohrlaenge": kd.Rohrlaenge = child.Value; break;
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
                    }
                }
                haltungen[tid!] = hd;
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
        string? ResolveSchachtLabel(string? refTid)
        {
            if (string.IsNullOrWhiteSpace(refTid)) return null;
            if (haltungspunkte.TryGetValue(refTid, out var hp))
            {
                if (!string.IsNullOrWhiteSpace(hp.Bezeichnung)) return hp.Bezeichnung;
                if (!string.IsNullOrWhiteSpace(hp.AbwassernetzelementRef) && abwasserknoten.TryGetValue(hp.AbwassernetzelementRef, out var knBez))
                    return knBez;
            }
            return null;
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
            if (!string.IsNullOrWhiteSpace(hd.Laenge)) rec.SetFieldValue("Haltungslaenge_m", hd.Laenge, FieldSource.Xtf405, userEdited: false);
            if (!string.IsNullOrWhiteSpace(material)) rec.SetFieldValue("Rohrmaterial", material, FieldSource.Xtf405, userEdited: false);

            var dn = !string.IsNullOrWhiteSpace(hd.LichteHoehe) ? hd.LichteHoehe : hd.LichteBreite;
            if (!string.IsNullOrWhiteSpace(dn)) rec.SetFieldValue("DN_mm", dn, FieldSource.Xtf405, userEdited: false);

            var vonKnoten = ResolveKnotenName(hd.VonRef);
            var nachKnoten = ResolveKnotenName(hd.NachRef);
            // Inspektionsrichtung wird nicht beim XTF-Import gesetzt, sondern nur beim PDF-Import

            var datum = NormalizeDate_yyyymmdd(hd.LetzteAenderung);
            if (!string.IsNullOrWhiteSpace(datum))
                rec.SetFieldValue("Datum_Jahr", datum, FieldSource.Xtf405, userEdited: false);

            if (kanal is not null)
            {
                if (!string.IsNullOrWhiteSpace(kanal.Standortname)) rec.SetFieldValue("Strasse", kanal.Standortname, FieldSource.Xtf405, userEdited: false);
                if (!string.IsNullOrWhiteSpace(nutzungsart)) rec.SetFieldValue("Nutzungsart", nutzungsart, FieldSource.Xtf405, userEdited: false);
                if (!string.IsNullOrWhiteSpace(kanal.Bemerkung)) rec.SetFieldValue("Bemerkungen", kanal.Bemerkung, FieldSource.Xtf405, userEdited: false);
                if (!string.IsNullOrWhiteSpace(kanal.Eigentuemer)) rec.SetFieldValue("Eigentuemer", kanal.Eigentuemer, FieldSource.Xtf405, userEdited: false);

                // Baujahr -> Datum_Jahr (falls leer)
                if (!string.IsNullOrWhiteSpace(kanal.Baujahr) && string.IsNullOrWhiteSpace(rec.GetFieldValue("Datum_Jahr")))
                    rec.SetFieldValue("Datum_Jahr", kanal.Baujahr, FieldSource.Xtf405, userEdited: false);

                // Status -> offen/abgeschlossen (wie PS)
                var status = kanal.Status ?? "";
                if (!string.IsNullOrWhiteSpace(status))
                {
                    if (Regex.IsMatch(status, "(?i)in_Betrieb|aktiv"))
                        rec.SetFieldValue("Offen_abgeschlossen", "abgeschlossen", FieldSource.Xtf405, userEdited: false);
                    else if (Regex.IsMatch(status, "(?i)ausser_Betrieb|stillgelegt"))
                        rec.SetFieldValue("Offen_abgeschlossen", "offen", FieldSource.Xtf405, userEdited: false);
                }

                // Zugaenglichkeit als Bemerkung ergänzen
                if (!string.IsNullOrWhiteSpace(kanal.Zugaenglichkeit) && !string.Equals(kanal.Zugaenglichkeit, "unbekannt", StringComparison.OrdinalIgnoreCase))
                {
                    var existing = rec.GetFieldValue("Bemerkungen") ?? "";
                    var add = $"Zugaenglichkeit: {kanal.Zugaenglichkeit}";
                    rec.SetFieldValue("Bemerkungen", string.IsNullOrWhiteSpace(existing) ? add : (existing + "\n" + add), FieldSource.Xtf405, userEdited: false);
                }
            }

            // Schacht-Labels (optional, für Debug/Logging)
            var schachtOben = ResolveSchachtLabel(hd.VonRef);
            var schachtUnten = ResolveSchachtLabel(hd.NachRef);
            if (!string.IsNullOrWhiteSpace(schachtOben)) rec.SetFieldValue("Schacht_oben", schachtOben, FieldSource.Xtf405, userEdited: false);
            if (!string.IsNullOrWhiteSpace(schachtUnten)) rec.SetFieldValue("Schacht_unten", schachtUnten, FieldSource.Xtf405, userEdited: false);

            records.Add(rec);
        }

        return records;
    }

    private static string NormalizeSiaMaterial(string material)
    {
        material ??= "";
        if (string.IsNullOrWhiteSpace(material)) return "";

        if (Regex.IsMatch(material, "Kunststoff_Hartpolyethylen", RegexOptions.IgnoreCase)) return "Kunststoff PE-HD";
        if (Regex.IsMatch(material, "Kunststoff_Polyethylen", RegexOptions.IgnoreCase)) return "Kunststoff PE";
        if (Regex.IsMatch(material, "Kunststoff_Polyvinylchlorid", RegexOptions.IgnoreCase)) return "Kunststoff PVC";
        if (Regex.IsMatch(material, "Beton_Normalbeton", RegexOptions.IgnoreCase)) return "Beton";
        if (Regex.IsMatch(material, "Beton_", RegexOptions.IgnoreCase)) return "Beton";
        if (Regex.IsMatch(material, "Steinzeug", RegexOptions.IgnoreCase)) return "Steinzeug";

        material = material.Replace("_", " ").Trim();
        if (material.Length == 0) return "";
        return char.ToUpperInvariant(material[0]) + material[1..];
    }

    private static string NormalizeNutzungsart(string v)
    {
        v ??= "";
        if (Regex.IsMatch(v, "(?i)Schmutzabwasser")) return "Schmutzwasser";
        if (Regex.IsMatch(v, "(?i)Regenabwasser")) return "Regenwasser";
        if (Regex.IsMatch(v, "(?i)Mischabwasser")) return "Mischabwasser";
        return v.Trim();
    }

    // ===================== VSA_KEK =====================
    private sealed class Untersuchung
    {
        public string Tid { get; init; } = "";
        public string Bezeichnung { get; set; } = "";
        public string Ausfuehrender { get; set; } = "";
        public string Zeitpunkt { get; set; } = "";
        public string InspizierteLaenge { get; set; } = "";
        public string Erfassungsart { get; set; } = "";
        public string Fahrzeug { get; set; } = "";
        public string Geraet { get; set; } = "";
        public string Witterung { get; set; } = "";
        public string Grund { get; set; } = "";
        public string VonPunkt { get; set; } = "";
        public string BisPunkt { get; set; } = "";
        public List<Schaden> Schaeden { get; } = new();
    }

    private sealed class Schaden
    {
        public string ObjId { get; set; } = "";
        public string Schadencode { get; set; } = "";
        public string Distanz { get; set; } = "";
        public string Anmerkung { get; set; } = "";
        public string Einzelschadenklasse { get; set; } = "";
        public string Streckenschaden { get; set; } = "";
        public string Quantifizierung1 { get; set; } = "";
        public string Quantifizierung2 { get; set; } = "";
        public string SchadenlageAnfang { get; set; } = "";
        public string SchadenlageEnde { get; set; } = "";
        public double LL { get; set; }
    }

    private static List<HaltungRecord> ParseVsaKek(XDocument doc, string sourcePath, out Dictionary<string, List<VsaFinding>> findingsPerHaltung)
    {
        var untersuchungen = new Dictionary<string, Untersuchung>(StringComparer.Ordinal);
        findingsPerHaltung = new Dictionary<string, List<VsaFinding>>(StringComparer.OrdinalIgnoreCase);
        var findingsByObjId = new Dictionary<string, VsaFinding>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in doc.Descendants().Where(e => e.Name.LocalName.Contains("Untersuchung", StringComparison.OrdinalIgnoreCase)))
        {
            var tid = (string?)node.Attribute("TID");
            if (string.IsNullOrWhiteSpace(tid))
                continue;

            var u = new Untersuchung { Tid = tid! };

            foreach (var child in node.Elements())
            {
                switch (child.Name.LocalName)
                {
                    case "Bezeichnung": u.Bezeichnung = child.Value; break;
                    case "Ausfuehrender": u.Ausfuehrender = child.Value; break;
                    case "Zeitpunkt": u.Zeitpunkt = child.Value; break;
                    case "Inspizierte_Laenge": u.InspizierteLaenge = child.Value; break;
                    case "Erfassungsart": u.Erfassungsart = child.Value; break;
                    case "Fahrzeug": u.Fahrzeug = child.Value; break;
                    case "Geraet": u.Geraet = child.Value; break;
                    case "Witterung": u.Witterung = child.Value; break;
                    case "Grund": u.Grund = child.Value; break;
                    case "vonPunktBezeichnung": u.VonPunkt = child.Value; break;
                    case "bisPunktBezeichnung": u.BisPunkt = child.Value; break;
                }
            }

            untersuchungen[tid!] = u;
        }

        foreach (var node in doc.Descendants().Where(e => e.Name.LocalName.Contains("Kanalschaden", StringComparison.OrdinalIgnoreCase)))
        {
            // UntersuchungRef/@REF
            var refNode = node.Elements().FirstOrDefault(e => e.Name.LocalName == "UntersuchungRef");
            var refTid = (string?)refNode?.Attribute("REF");
            if (string.IsNullOrWhiteSpace(refTid) || !untersuchungen.TryGetValue(refTid!, out var u))
                continue;

            var s = new Schaden();
            var finding = new VsaFinding();
            foreach (var child in node.Elements())
            {
                switch (child.Name.LocalName)
                {
                    case "OBJ_ID":
                        s.ObjId = child.Value;
                        break;
                    case "KanalSchadencode":
                        s.Schadencode = child.Value;
                        finding.KanalSchadencode = child.Value;
                        break;
                    case "Distanz":
                        s.Distanz = child.Value;
                        if (TryParseDouble(child.Value, out var meter))
                            finding.MeterStart = meter;
                        break;
                    case "Anmerkung":
                        s.Anmerkung = child.Value;
                        finding.Raw = child.Value;
                        break;
                    case "Einzelschadenklasse":
                        s.Einzelschadenklasse = child.Value;
                        if (int.TryParse(child.Value, out var ez))
                        {
                            // Best-effort: wenn keine Regel vorhanden, nutze Einzelschadenklasse für alle Anforderungen
                            if (ez < 0) ez = 0;
                            if (ez > 4) ez = 4;
                            finding.EZD = ez;
                            finding.EZS = ez;
                            finding.EZB = ez;
                        }
                        break;
                    case "Streckenschaden":
                        s.Streckenschaden = child.Value;
                        break;
                    case "Quantifizierung1":
                        s.Quantifizierung1 = child.Value;
                        finding.Quantifizierung1 = child.Value;
                        break;
                    case "Quantifizierung2":
                        s.Quantifizierung2 = child.Value;
                        finding.Quantifizierung2 = child.Value;
                        break;
                    case "SchadenlageAnfang":
                        s.SchadenlageAnfang = child.Value;
                        if (double.TryParse(child.Value.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var anfang))
                            finding.SchadenlageAnfang = anfang;
                        break;
                    case "SchadenlageEnde":
                        s.SchadenlageEnde = child.Value;
                        if (double.TryParse(child.Value.Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var ende))
                            finding.SchadenlageEnde = ende;
                        break;
                }
            }

            // LL berechnen wie PS
            double ll = 0.0;
            if (string.Equals(s.Streckenschaden, "true", StringComparison.OrdinalIgnoreCase))
            {
                if (TryParseDouble(s.SchadenlageAnfang, out var anf) && TryParseDouble(s.SchadenlageEnde, out var end) && end > anf)
                    ll = end - anf;
                else if (TryParseDouble(s.Quantifizierung1, out var q1))
                    ll = q1;
            }
            s.LL = ll;
            finding.LL = ll;

            u.Schaeden.Add(s);
            if (!string.IsNullOrWhiteSpace(s.ObjId))
                findingsByObjId[s.ObjId] = finding;
            // Add finding to findingsPerHaltung (by Bezeichnung)
            if (!string.IsNullOrWhiteSpace(refTid) && untersuchungen.TryGetValue(refTid, out var untersuchung))
            {
                var haltungName = untersuchung.Bezeichnung;
                if (!string.IsNullOrWhiteSpace(haltungName))
                {
                    if (!findingsPerHaltung.TryGetValue(haltungName, out var list))
                    {
                        list = new List<VsaFinding>();
                        findingsPerHaltung[haltungName] = list;
                    }
                    list.Add(finding);
                }
            }
        }

        foreach (var node in doc.Descendants().Where(e => e.Name.LocalName.Contains("Datei", StringComparison.OrdinalIgnoreCase)))
        {
            string art = "";
            string klasse = "";
            string objekt = "";
            string bezeichnung = "";
            string relativpfad = "";

            foreach (var child in node.Elements())
            {
                switch (child.Name.LocalName)
                {
                    case "Art":
                        art = child.Value;
                        break;
                    case "Klasse":
                        klasse = child.Value;
                        break;
                    case "Objekt":
                        objekt = child.Value;
                        break;
                    case "Bezeichnung":
                        bezeichnung = child.Value;
                        break;
                    case "Relativpfad":
                        relativpfad = child.Value;
                        break;
                }
            }

            if (!art.Contains("Foto", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!klasse.Contains("Kanalschaden", StringComparison.OrdinalIgnoreCase))
                continue;
            if (string.IsNullOrWhiteSpace(objekt) || !findingsByObjId.TryGetValue(objekt, out var finding))
                continue;

            var fotoPath = ResolveVsaPhotoPath(sourcePath, relativpfad, bezeichnung);
            if (string.IsNullOrWhiteSpace(fotoPath))
                continue;

            if (string.IsNullOrWhiteSpace(finding.FotoPath))
                finding.FotoPath = fotoPath;
        }

        var records = new List<HaltungRecord>();

        foreach (var u in untersuchungen.Values)
        {
            if (string.IsNullOrWhiteSpace(u.Bezeichnung))
                continue;

            var zeitpunkt = NormalizeDate_yyyymmdd(u.Zeitpunkt);

            var primaere = new List<string>();

            if (findingsPerHaltung.TryGetValue(u.Bezeichnung, out var findings))
            {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var f in findings)
                {
                    var code = (f.KanalSchadencode ?? "").Trim().ToUpperInvariant();
                    if (code.Length == 0) continue;
                    var meter = f.MeterStart ?? f.SchadenlageAnfang;
                    var key = $"{code}|{(meter.HasValue ? meter.Value.ToString("F2") : "")}";
                    if (!seen.Add(key)) continue;

                    var detail = XtfPrimaryDamageFormatter.FormatLine(f);
                    if (!string.IsNullOrWhiteSpace(detail))
                        primaere.Add(detail);
                }
            }

            var rec = new HaltungRecord();
            rec.SetFieldValue("Haltungsname", u.Bezeichnung, FieldSource.Xtf, userEdited: false);
            if (!string.IsNullOrWhiteSpace(u.InspizierteLaenge)) rec.SetFieldValue("Haltungslaenge_m", u.InspizierteLaenge, FieldSource.Xtf, userEdited: false);
            if (!string.IsNullOrWhiteSpace(zeitpunkt)) rec.SetFieldValue("Datum_Jahr", zeitpunkt, FieldSource.Xtf, userEdited: false);
            if (findings is not null && findings.Count > 0)
                rec.VsaFindings = new List<VsaFinding>(findings);

            if (primaere.Count > 0)
            {
                var val = XtfPrimaryDamageFormatter.DeduplicateText(string.Join("\n", primaere));
                rec.SetFieldValue("Primaere_Schaeden", val, FieldSource.Xtf, userEdited: false);
            }

            // NOTE: VSA-Zustandsnote wird NICHT hier berechnet, sondern später durch VsaEvaluationService
            // Die korrekte Berechnung basiert auf VSA-Regeln und allen Schadenscodes pro Haltung
            
            // maxKlasse wird hier nicht korrekt berechnet - entfernt um falsche Werte zu vermeiden
            // if (maxKlasse > 0)
            // {
            //     rec.SetFieldValue("Zustandsklasse", maxKlasse.ToString(), FieldSource.Xtf, userEdited: false);
            //     rec.SetFieldValue("VSA_Zustandsnote_D", maxKlasse.ToString(), FieldSource.Xtf, userEdited: false);
            // }

            // Inspektionsrichtung ist in den XTF-Daten nicht enthalten (nur in PDF-Reports)

            if (!string.IsNullOrWhiteSpace(u.Erfassungsart))
            {
                var bem = $"Erfassung: {u.Erfassungsart}";
                if (!string.IsNullOrWhiteSpace(u.Fahrzeug)) bem += $", Fahrzeug: {u.Fahrzeug}";
                if (!string.IsNullOrWhiteSpace(u.Geraet)) bem += $", Geraet: {u.Geraet}";
                rec.SetFieldValue("Bemerkungen", bem, FieldSource.Xtf, userEdited: false);

                rec.SetFieldValue("Pruefungsresultat", "", FieldSource.Xtf, userEdited: false);
            }

            records.Add(rec);
        }

        return records;
    }

    private static string ResolveVsaPhotoPath(string xtfPath, string? relativeFolder, string? fileName)
    {
        fileName = (fileName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(fileName))
            return string.Empty;

        if (Path.IsPathRooted(fileName))
            return fileName;

        var baseDir = Path.GetDirectoryName(xtfPath) ?? "";
        var rel = (relativeFolder ?? "").Trim().Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        var candidates = new List<string>();

        if (!string.IsNullOrWhiteSpace(rel))
            candidates.Add(Path.GetFullPath(Path.Combine(baseDir, rel, fileName)));
        candidates.Add(Path.GetFullPath(Path.Combine(baseDir, fileName)));
        candidates.Add(Path.GetFullPath(Path.Combine(baseDir, "Foto", fileName)));
        candidates.Add(Path.GetFullPath(Path.Combine(baseDir, "Fotos", fileName)));
        candidates.Add(Path.GetFullPath(Path.Combine(baseDir, "Picture", fileName)));
        candidates.Add(Path.GetFullPath(Path.Combine(baseDir, "Pictures", fileName)));

        foreach (var c in candidates)
        {
            if (File.Exists(c))
                return c;
        }

        return candidates[0];
    }

    private static void SyncProtocolFromFindings(HaltungRecord record, IReadOnlyList<VsaFinding> findings)
    {
        if (findings.Count == 0)
            return;

        var hasProtocol = record.Protocol is not null;
        var hasEntries = hasProtocol
                         && (((record.Protocol?.Current?.Entries.Count ?? 0) > 0)
                             || ((record.Protocol?.Original?.Entries.Count ?? 0) > 0));

        if (!hasEntries)
        {
            var entries = BuildImportedProtocolEntries(findings);
            if (entries.Count > 0)
                record.Protocol = new ProtocolService().EnsureProtocol(record.GetFieldValue("Haltungsname") ?? "", entries, null);
            return;
        }

        if (record.Protocol is null)
            return;

        SyncRevisionImportedEntries(record.Protocol.Original, findings);
        SyncRevisionImportedEntries(record.Protocol.Current, findings);
        foreach (var rev in record.Protocol.History)
            SyncRevisionImportedEntries(rev, findings);
    }

    private static List<ProtocolEntry> BuildImportedProtocolEntries(IReadOnlyList<VsaFinding> findings)
    {
        var list = new List<ProtocolEntry>(findings.Count);
        foreach (var f in findings)
        {
            if (string.IsNullOrWhiteSpace(f.KanalSchadencode))
                continue;

            var mStart = GetFindingMeterStart(f);
            var mEnd = GetFindingMeterEnd(f);

            var entry = new ProtocolEntry
            {
                Code = f.KanalSchadencode.Trim(),
                Beschreibung = f.Raw?.Trim() ?? string.Empty,
                MeterStart = mStart,
                MeterEnd = mEnd,
                IsStreckenschaden = mStart.HasValue && mEnd.HasValue && mEnd >= mStart,
                Mpeg = f.MPEG,
                Zeit = ParseMpegTime(f.MPEG) ?? (f.Timestamp?.TimeOfDay),
                Source = ProtocolEntrySource.Imported
            };

            if (!string.IsNullOrWhiteSpace(f.Quantifizierung1) || !string.IsNullOrWhiteSpace(f.Quantifizierung2))
            {
                entry.CodeMeta = new ProtocolEntryCodeMeta
                {
                    Code = entry.Code,
                    Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Quantifizierung1"] = f.Quantifizierung1 ?? string.Empty,
                        ["Quantifizierung2"] = f.Quantifizierung2 ?? string.Empty
                    },
                    UpdatedAt = DateTimeOffset.UtcNow
                };
            }

            if (!string.IsNullOrWhiteSpace(f.FotoPath))
                entry.FotoPaths.Add(f.FotoPath);

            list.Add(entry);
        }

        return list;
    }

    private static void SyncRevisionImportedEntries(ProtocolRevision revision, IReadOnlyList<VsaFinding> findings)
    {
        if (revision?.Entries is null || revision.Entries.Count == 0)
            return;

        foreach (var entry in revision.Entries)
        {
            if (entry.IsDeleted || entry.Source != ProtocolEntrySource.Imported)
                continue;

            var match = FindBestFindingForEntry(entry, findings);
            if (match is null)
                continue;

            if (!entry.MeterStart.HasValue)
                entry.MeterStart = GetFindingMeterStart(match);
            if (!entry.MeterEnd.HasValue)
                entry.MeterEnd = GetFindingMeterEnd(match);
            if (string.IsNullOrWhiteSpace(entry.Beschreibung) && !string.IsNullOrWhiteSpace(match.Raw))
                entry.Beschreibung = match.Raw.Trim();
            if (string.IsNullOrWhiteSpace(entry.Mpeg) && !string.IsNullOrWhiteSpace(match.MPEG))
                entry.Mpeg = match.MPEG;
            if (!entry.Zeit.HasValue)
                entry.Zeit = ParseMpegTime(match.MPEG) ?? (match.Timestamp?.TimeOfDay);

            if (string.IsNullOrWhiteSpace(match.FotoPath))
                continue;

            entry.FotoPaths ??= new List<string>();
            if (!entry.FotoPaths.Any(p => string.Equals(p, match.FotoPath, StringComparison.OrdinalIgnoreCase)))
                entry.FotoPaths.Add(match.FotoPath);
        }
    }

    private static VsaFinding? FindBestFindingForEntry(ProtocolEntry entry, IReadOnlyList<VsaFinding> findings)
    {
        if (findings.Count == 0)
            return null;

        var entryMeter = entry.MeterStart ?? entry.MeterEnd;
        var entryCode = NormalizeCode(entry.Code);
        var scored = new List<(VsaFinding Finding, double Delta, int CodeRank, bool HasPhoto)>(findings.Count);

        foreach (var finding in findings)
        {
            var findingMeter = GetFindingMeterStart(finding) ?? GetFindingMeterEnd(finding);
            var delta = (entryMeter.HasValue && findingMeter.HasValue)
                ? Math.Abs(findingMeter.Value - entryMeter.Value)
                : double.MaxValue;
            var codeRank = GetCodeSimilarityRank(entryCode, NormalizeCode(finding.KanalSchadencode));
            var hasPhoto = !string.IsNullOrWhiteSpace(finding.FotoPath);
            scored.Add((finding, delta, codeRank, hasPhoto));
        }

        if (entryMeter.HasValue && scored.Count > 0)
        {
            // Primär nach Distanz matchen; Codes dienen als Tiebreaker.
            var byMeter = scored
                .Where(s => s.Delta <= 0.15)
                .OrderBy(s => s.Delta)
                .ThenBy(s => s.CodeRank)
                .ThenByDescending(s => s.HasPhoto)
                .ToList();
            if (byMeter.Count > 0)
                return byMeter[0].Finding;

            var byMeterLoose = scored
                .Where(s => s.Delta <= 0.50 && s.CodeRank <= 1)
                .OrderBy(s => s.Delta)
                .ThenBy(s => s.CodeRank)
                .ThenByDescending(s => s.HasPhoto)
                .ToList();
            if (byMeterLoose.Count > 0)
                return byMeterLoose[0].Finding;
        }

        var byCode = scored
            .Where(s => s.CodeRank == 0 || s.CodeRank == 1)
            .OrderBy(s => s.CodeRank)
            .ThenByDescending(s => s.HasPhoto)
            .ThenBy(s => s.Delta)
            .ToList();
        if (byCode.Count > 0)
            return byCode[0].Finding;

        return scored
            .OrderByDescending(s => s.HasPhoto)
            .ThenBy(s => s.Delta)
            .Select(s => s.Finding)
            .FirstOrDefault();
    }

    private static string NormalizeCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return string.Empty;
        return Regex.Replace(code.Trim().ToUpperInvariant(), @"[^A-Z0-9]", string.Empty);
    }

    private static int GetCodeSimilarityRank(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return 2;
        if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
            return 0;
        if (left.StartsWith(right, StringComparison.OrdinalIgnoreCase)
            || right.StartsWith(left, StringComparison.OrdinalIgnoreCase))
            return 1;
        return 2;
    }

    private static double? GetFindingMeterStart(VsaFinding finding)
        => finding.MeterStart ?? finding.SchadenlageAnfang;

    private static double? GetFindingMeterEnd(VsaFinding finding)
        => finding.MeterEnd ?? finding.SchadenlageEnde;

    private static TimeSpan? ParseMpegTime(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var text = raw.Trim();
        var formats = new[] { @"hh\:mm\:ss", @"mm\:ss", @"h\:mm\:ss", @"m\:ss", @"hh\:mm\:ss\.fff", @"mm\:ss\.fff" };
        if (TimeSpan.TryParseExact(text, formats, CultureInfo.InvariantCulture, out var parsed))
            return parsed;

        return TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out parsed) ? parsed : null;
    }

    private static bool TryParseDouble(string? s, out double value)
    {
        value = 0.0;
        if (string.IsNullOrWhiteSpace(s))
            return false;
        s = s.Trim().Replace(",", ".");
        if (double.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out value))
            return true;

        var match = Regex.Match(s, @"-?\d+(?:[.,]\d+)?");
        if (!match.Success)
            return false;

        var number = match.Value.Replace(",", ".");
        return double.TryParse(number, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    private static string NormalizeDate_yyyymmdd(string? yyyymmdd)
    {
        yyyymmdd ??= "";
        var m = Regex.Match(yyyymmdd.Trim(), @"^(\d{4})(\d{2})(\d{2})$");
        if (!m.Success) return yyyymmdd.Trim();
        return $"{m.Groups[3].Value}.{m.Groups[2].Value}.{m.Groups[1].Value}";
    }
}
