using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf;

// LegacyXtfImportService SIA405-Format-Parser: Liest SIA-405 / VSA-DSS XTFs
// (Schweizer Datenstandard), normalisiert Material/Nutzungsart/Videozaehler-
// stand auf VSA-KEK-konforme Werte. Aus dem Hauptdatei extrahiert (Slice 29a).
public sealed partial class LegacyXtfImportService
{
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
        public string Videozaehlerstand { get; set; } = "";
    }

    /// <summary>
    /// Parst den IBAK-Videozaehlerstand im Format "HH:MM:SS:FF" (Frames).
    /// Toleriert auch "HH:MM:SS" und "H:MM:SS" ohne Frames.
    /// </summary>
    private static bool TryParseVideozaehlerstand(string? raw, out TimeSpan ts)
    {
        ts = default;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var parts = raw.Split(':');
        if (parts.Length < 3 || parts.Length > 4) return false;
        if (!int.TryParse(parts[0], out var hh)) return false;
        if (!int.TryParse(parts[1], out var mm)) return false;
        if (!int.TryParse(parts[2], out var ss)) return false;
        if (hh < 0 || mm < 0 || mm >= 60 || ss < 0 || ss >= 60) return false;
        ts = new TimeSpan(hh, mm, ss);
        return true;
    }
}
