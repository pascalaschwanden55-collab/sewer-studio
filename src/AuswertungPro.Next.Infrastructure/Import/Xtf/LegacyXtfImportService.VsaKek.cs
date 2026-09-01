using System.Xml.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Media;
using IVsaMediaPathResolver = AuswertungPro.Next.Application.Import.IVsaMediaPathResolver;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf;

/// <summary>
/// VSA-KEK-Teil des XTF-Imports: Untersuchungen, Kanalschaeden und die daraus
/// erzeugten Haltungen. Aus <c>LegacyXtfImportService.cs</c> herausgeloest, weil die
/// Datei sonst die 1000-Zeilen-Grenze reisst. Reiner Umzug, kein Verhaltenswechsel.
/// </summary>
public sealed partial class LegacyXtfImportService
{
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
        /// <summary>Rohwert aus der XTF: "in_Fliessrichtung" / "gegen_Fliessrichtung".</summary>
        public string Fliessrichtung { get; set; } = "";
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

    /// <summary>
    /// Liest den Modellnamen aus der HEADERSECTION (erstes MODEL-Element). Fehlt er,
    /// bleibt die Angabe leer — sie ist eine Zusatzinformation und darf keinen Import stoppen.
    /// </summary>
    private static string ReadModelName(XDocument doc)
    {
        var model = doc.Descendants()
            .FirstOrDefault(e => string.Equals(e.Name.LocalName, "MODEL", StringComparison.OrdinalIgnoreCase));
        return (string?)model?.Attribute("NAME") ?? "";
    }

    private static List<HaltungRecord> ParseVsaKek(XDocument doc, string sourcePath,
        IVsaMediaPathResolver mediaPaths,
        out Dictionary<string, List<VsaFinding>> findingsPerHaltung)
    {
        var modellName = ReadModelName(doc);
        var untersuchungen = new Dictionary<string, Untersuchung>(StringComparer.Ordinal);
        findingsPerHaltung = new Dictionary<string, List<VsaFinding>>(StringComparer.OrdinalIgnoreCase);
        var findingsByObjId = new Dictionary<string, VsaFinding>(StringComparer.OrdinalIgnoreCase);
        var findingsByTid = new Dictionary<string, VsaFinding>(StringComparer.OrdinalIgnoreCase);
        // Video-Pfad je Untersuchungs-TID (KEK.Datei mit Klasse=Untersuchung)
        var videoByUntersuchungTid = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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
                    case "Fliessrichtung": u.Fliessrichtung = child.Value; break;
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

            var schadenTid = (string?)node.Attribute("TID");
            var s = new Schaden();
            var finding = new VsaFinding
            {
                // Herkunft festhalten, solange sie bekannt ist: Nur damit laesst sich
                // spaeter genau dieses Element in der Originaldatei wiederfinden.
                KanalschadenTid = string.IsNullOrWhiteSpace(schadenTid) ? null : schadenTid,
                UntersuchungTid = refTid
            };
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
                    case "Videozaehlerstand":
                        // Sekunde ab Dateianfang (SN EN 13508-2, Kapitel 3.1.10).
                        // Wurde bis 2026-08-13 nie eingelesen, obwohl die ganze
                        // Weiterverarbeitung dahinter steht: finding.MPEG ->
                        // entry.Mpeg/entry.Zeit -> CodingBoundaryImportReferencePolicy.
                        // Ohne diesen Wert fiel die Videoreferenz von Rohranfang und
                        // Rohrende dort still auf TimeSpan.Zero zurueck.
                        finding.MPEG = child.Value;
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

            // SchadenlageAnfang/-Ende sind Uhrlagen im Rohrquerschnitt und keine
            // Laengenpositionen. Eine Streckenlaenge darf daraus nie entstehen.
            double ll = 0.0;
            if (string.Equals(s.Streckenschaden, "true", StringComparison.OrdinalIgnoreCase)
                && TryParseDouble(s.Quantifizierung1, out var q1))
            {
                ll = q1;
            }
            s.LL = ll;
            finding.LL = ll;

            u.Schaeden.Add(s);
            if (!string.IsNullOrWhiteSpace(s.ObjId))
                findingsByObjId[s.ObjId] = finding;
            // XTF-Variante nutzt Datei.Objekt = Kanalschaden-TID (kein OBJ_ID-Element vorhanden) — auch nach TID indizieren.
            if (!string.IsNullOrWhiteSpace(schadenTid))
                findingsByTid[schadenTid!] = finding;
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

            // --- Untersuchungs-Video (Klasse=Untersuchung, zentral bekanntes Video ODER relativpfad=Film) ---
            if (klasse.Contains("Untersuchung", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(objekt))
            {
                var istVideo = MediaFileTypes.HasVideoExtension(bezeichnung)
                               || relativpfad.Contains("Film", StringComparison.OrdinalIgnoreCase);
                if (istVideo)
                {
                    var videoPfad = mediaPaths.ResolveVideo(sourcePath, relativpfad, bezeichnung);
                    if (!string.IsNullOrWhiteSpace(videoPfad)
                        && !videoByUntersuchungTid.ContainsKey(objekt))
                    {
                        videoByUntersuchungTid[objekt] = videoPfad;
                    }
                }
                continue;
            }

            if (!art.Contains("Foto", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!klasse.Contains("Kanalschaden", StringComparison.OrdinalIgnoreCase))
                continue;
            // Datei.Objekt referenziert den Kanalschaden — je nach XTF-Variante via OBJ_ID ODER TID.
            if (string.IsNullOrWhiteSpace(objekt)
                || !(findingsByObjId.TryGetValue(objekt, out var finding)
                     || findingsByTid.TryGetValue(objekt, out finding)))
                continue;

            var fotoPath = mediaPaths.ResolvePhoto(sourcePath, relativpfad, bezeichnung);
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
                    var meter = f.MeterStart;
                    var key = $"{code}|{(meter.HasValue ? meter.Value.ToString("F2") : "")}";
                    if (!seen.Add(key)) continue;

                    var detail = XtfPrimaryDamageFormatter.FormatLine(f);
                    if (!string.IsNullOrWhiteSpace(detail))
                        primaere.Add(detail);
                }
            }

            var rec = new HaltungRecord
            {
                // Ankerangabe fuer eine spaetere Revision: aus welcher Datei und welcher
                // Untersuchung diese Haltung stammt.
                XtfHerkunft = new XtfHerkunft
                {
                    Datei = Path.GetFileName(sourcePath) ?? "",
                    Modell = modellName,
                    UntersuchungTid = u.Tid
                }
            };
            rec.SetFieldValue("Haltungsname", u.Bezeichnung, FieldSource.Xtf, userEdited: false);
            if (!string.IsNullOrWhiteSpace(u.InspizierteLaenge)) rec.SetFieldValue("Haltungslaenge_m", u.InspizierteLaenge, FieldSource.Xtf, userEdited: false);
            if (!string.IsNullOrWhiteSpace(zeitpunkt)) rec.SetFieldValue("Datum_Jahr", zeitpunkt, FieldSource.Xtf, userEdited: false);
            // Schacht oben/unten aus der Untersuchung (von-/bisPunktBezeichnung) — VSA_KEK ist Hauptquelle,
            // eine spaetere SIA405-Anreicherung fuellt nur, falls hier leer.
            if (!string.IsNullOrWhiteSpace(u.VonPunkt)) rec.SetFieldValue("Schacht_oben", u.VonPunkt, FieldSource.Xtf, userEdited: false);
            if (!string.IsNullOrWhiteSpace(u.BisPunkt)) rec.SetFieldValue("Schacht_unten", u.BisPunkt, FieldSource.Xtf, userEdited: false);
            if (findings is not null && findings.Count > 0)
                rec.VsaFindings = new List<VsaFinding>(findings);

            // Video-Link aus KEK.Datei (Klasse=Untersuchung) setzen, falls noch kein Link vorhanden
            if (videoByUntersuchungTid.TryGetValue(u.Tid, out var videoLink)
                && string.IsNullOrWhiteSpace(rec.GetFieldValue("Link")))
            {
                rec.SetFieldValue("Link", videoLink, FieldSource.Xtf, userEdited: false);
            }

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

            // Inspektionsrichtung aus der Untersuchung (IKAS liefert sie als <Fliessrichtung>).
            var richtung = XtfValueNormalizer.NormalizeInspectionDirection(u.Fliessrichtung);
            if (!string.IsNullOrWhiteSpace(richtung))
                rec.SetFieldValue("Inspektionsrichtung", richtung, FieldSource.Xtf, userEdited: false);

            // Bemerkungen mit Inspektionskontext anreichern. VSA_KEK ist Hauptquelle und darf Bemerkungen
            // setzen. Alle verfuegbaren Kontextangaben einbeziehen (nicht nur wenn Erfassungsart da ist),
            // damit Grund/Witterung/Ausfuehrender/Fahrzeug/Geraet nicht verloren gehen.
            var bemParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(u.Erfassungsart)) bemParts.Add($"Erfassung: {u.Erfassungsart}");
            if (!string.IsNullOrWhiteSpace(u.Grund)) bemParts.Add($"Grund: {u.Grund}");
            if (!string.IsNullOrWhiteSpace(u.Witterung)) bemParts.Add($"Witterung: {u.Witterung}");
            if (!string.IsNullOrWhiteSpace(u.Ausfuehrender)) bemParts.Add($"Ausfuehrender: {u.Ausfuehrender}");
            if (!string.IsNullOrWhiteSpace(u.Fahrzeug)) bemParts.Add($"Fahrzeug: {u.Fahrzeug}");
            if (!string.IsNullOrWhiteSpace(u.Geraet)) bemParts.Add($"Geraet: {u.Geraet}");
            if (bemParts.Count > 0)
            {
                rec.SetFieldValue("Bemerkungen", string.Join(", ", bemParts), FieldSource.Xtf, userEdited: false);
                rec.SetFieldValue("Pruefungsresultat", "", FieldSource.Xtf, userEdited: false);
            }

            records.Add(rec);
        }

        return records;
    }
}
