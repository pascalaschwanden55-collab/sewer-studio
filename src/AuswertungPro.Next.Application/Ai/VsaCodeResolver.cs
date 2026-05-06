using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Zentraler VSA-Code-Resolver fuer KI-Findings.
/// Einzige Quelle fuer Code-Normalisierung, Label-Lookup und Clock-Normalisierung.
/// Wird von PlayerWindow (Codiermodus) und CodingModeWindow gemeinsam genutzt.
/// </summary>
public static class VsaCodeResolver
{
    /// <summary>
    /// Normalisiert einen rohen VSA-Code-Hint von der KI.
    /// Akzeptiert nur Codes die fachlich plausibel sind:
    /// - Exakt im Katalog (BCD, BCAEB, BABBA)
    /// - Hauptcode (3Z) im Katalog UND Gesamtlaenge 2-6 Grossbuchstaben
    /// Gibt normalisierten Code oder null zurueck.
    /// </summary>
    public static string? NormalizeFindingCode(string? rawCode)
    {
        if (string.IsNullOrWhiteSpace(rawCode))
            return null;

        var normalized = rawCode.Trim().Replace(".", "").ToUpperInvariant();

        // VSA-Codes sind 2-6 Grossbuchstaben
        if (normalized.Length < 2 || normalized.Length > 6)
            return null;
        if (!Regex.IsMatch(normalized, @"^[A-Z]{2,6}$"))
            return null;

        // 1. Exakter Katalog-Lookup
        if (AuswertungPro.Next.Application.CodeCatalog.VsaCodeTree.LookupLabel(normalized) != null)
            return normalized;

        // 2. Hauptcode (3 Zeichen) validieren — Untercodes akzeptieren
        if (normalized.Length >= 3)
        {
            var main = normalized[..3];
            if (AuswertungPro.Next.Application.CodeCatalog.VsaCodeTree.LookupLabel(main) != null)
                return normalized;
        }

        return null;
    }

    /// <summary>
    /// Klartext-Lookup mit Fallback-Kette: voller Code → 3Z → 2Z → null.
    /// </summary>
    public static string? LookupLabel(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var label = AuswertungPro.Next.Application.CodeCatalog.VsaCodeTree.LookupLabel(code);
        if (label != null) return label;
        if (code.Length >= 3)
        {
            label = AuswertungPro.Next.Application.CodeCatalog.VsaCodeTree.LookupLabel(code[..3]);
            if (label != null) return label;
        }
        if (code.Length >= 2)
            return AuswertungPro.Next.Application.CodeCatalog.VsaCodeTree.LookupLabel(code[..2]);
        return null;
    }

    /// <summary>
    /// Leitet groben VSA-Code aus KI-Label ab (Keyword-Heuristik).
    /// Spezifische Begriffe per Substring, generische per Wortgrenze.
    /// </summary>
    public static string? InferCodeFromLabel(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return null;

        var text = label.Trim().ToLowerInvariant()
            .Replace("ä", "ae").Replace("ö", "oe")
            .Replace("ü", "ue").Replace("ß", "ss");

        // ══════════════════════════════════════════════════════════════
        // VSA/EN 13508-2 Keyword → Code Mapping (vollstaendig)
        // Reihenfolge: Spezifischste zuerst, generische zuletzt
        // ══════════════════════════════════════════════════════════════

        // ── BC: Bestandsaufnahme ──
        if (Has(text, "anschluss") || Has(text, "abzweig") || Has(text, "stutzen")
            || Has(text, "zulauf") || Has(text, "lateral connection") || HasWord(text, "lateral"))
            return "BCA";
        if (Has(text, "reparatur sichtbar") || Has(text, "rohr ausgetauscht") || Has(text, "innenauskleidung sichtbar"))
            return "BCB";
        if (Has(text, "bogen") || Has(text, "kruemm") || Has(text, "kurve") || HasWord(text, "bend")
            || Has(text, "richtungsaenderung"))
            return "BCC";
        if (Has(text, "rohranfang") || Has(text, "pipe start") || Has(text, "anfangsknoten"))
            return "BCD";
        if (Has(text, "rohrende") || Has(text, "pipe end") || Has(text, "endknoten"))
            return "BCE";

        // ── BA: Bauliche Schaeden ──
        // BAA: Verformung/Deformation
        if (Has(text, "verformung") || Has(text, "deformation") || Has(text, "deformiert")
            || HasWord(text, "oval") || HasWord(text, "dent") || HasWord(text, "deformed"))
            return "BAA";
        // BAB: Risse
        if (Has(text, "riss") || HasWord(text, "crack") || Has(text, "fracture") || Has(text, "fissure")
            || Has(text, "haarriss"))
            return "BAB";
        // BAC: Bruch/Einsturz/Loch
        if (Has(text, "bruch") || Has(text, "einsturz") || Has(text, "collapse")
            || HasWord(text, "hole") || Has(text, "loch") || Has(text, "scherbe") || Has(text, "missing wall"))
            return "BAC";
        // BAD: Defektes Mauerwerk
        if (Has(text, "mauerwerk") || Has(text, "backstein") || Has(text, "sohle abgesackt")
            || Has(text, "brickwork"))
            return "BAD";
        // BAE: Fehlender Moertel
        if (Has(text, "moertel") || Has(text, "mortar"))
            return "BAE";
        // BAF: Oberflaechenschaden (Korrosion, Abplatzung, Armierung, Beule etc.)
        if (Has(text, "oberflaechenschaden") || Has(text, "surface damage") || Has(text, "abplatzung")
            || Has(text, "zuschlag") || Has(text, "armierung") || Has(text, "korrodiert")
            || Has(text, "korrosion") || Has(text, "corrosion") || HasWord(text, "rost")
            || Has(text, "erosion") || Has(text, "beule") || Has(text, "druckstelle"))
            return "BAF";
        // BAG: Einragender Anschluss
        if (Has(text, "einragender anschluss") || Has(text, "anschluss einragend")
            || Has(text, "protruding connection"))
            return "BAG";
        // BAH: Schadhafter Anschluss
        if (Has(text, "schadhafter anschluss") || Has(text, "anschluss beschaedigt")
            || Has(text, "anschluss verstopft") || Has(text, "connection defect"))
            return "BAH";
        // BAI: Einragendes Dichtungsmaterial
        if (Has(text, "dichtung") || Has(text, "dichtring") || HasWord(text, "seal")
            || Has(text, "seal defect") || Has(text, "seal displaced"))
            return "BAI";
        // BAJ: Verschobene Rohrverbindung (Versatz, Knick)
        if (Has(text, "versatz") || Has(text, "verschobene rohrverbindung") || HasWord(text, "offset")
            || Has(text, "displaced") || HasWord(text, "joint") || Has(text, "knick"))
            return "BAJ";
        // BAK: Feststellung der Innenauskleidung
        if (Has(text, "innenauskleidung") || Has(text, "liner") || Has(text, "auskleidung schadhaft"))
            return "BAK";
        // BAL: Schadhafte Reparatur
        if (Has(text, "reparatur mangelhaft") || Has(text, "reparatur schadhaft")
            || Has(text, "reparaturwerkstoff"))
            return "BAL";
        // BAM: Schadhafte Schweissnaht
        if (Has(text, "schweissnaht") || Has(text, "weld"))
            return "BAM";
        // BAN: Poroese Leitung
        if (Has(text, "poroes") || Has(text, "porous"))
            return "BAN";
        // BAO: Boden sichtbar
        if (Has(text, "boden sichtbar") || Has(text, "visible soil"))
            return "BAO";
        // BAP: Hohlraum sichtbar
        if (Has(text, "hohlraum") || Has(text, "void") || Has(text, "cavity"))
            return "BAP";

        // ── BB: Betriebliche Stoerungen ──
        // BBA: Wurzeln
        if (Has(text, "wurzel") || Has(text, "root") || Has(text, "bewuchs"))
            return "BBA";
        // BBB: Anhaftende Stoffe (Inkrustation, Fett, Faeulnis)
        if (Has(text, "inkrustation") || Has(text, "encrustation") || Has(text, "kalk")
            || Has(text, "anhaftung") || Has(text, "sinter") || Has(text, "calcite")
            || HasWord(text, "fett") || Has(text, "faeulnis") || Has(text, "grease"))
            return "BBB";
        // BBC: Ablagerungen
        if (Has(text, "ablagerung") || HasWord(text, "sediment") || Has(text, "schlamm")
            || HasWord(text, "silt") || HasWord(text, "debris") || HasWord(text, "deposit")
            || HasWord(text, "buildup"))
            return "BBC";
        // BBD: Eindringender Boden
        if (Has(text, "bodeneindringung") || Has(text, "eindringender boden")
            || Has(text, "sand dringt") || Has(text, "humus dringt") || Has(text, "soil intrusion"))
            return "BBD";
        // BBE: Andere Hindernisse
        if (Has(text, "hindernis") || Has(text, "obstacle") || Has(text, "blockage")
            || Has(text, "gegenstand") || Has(text, "werkleitung"))
            return "BBE";
        // BBF: Infiltration
        if (Has(text, "infiltration") || Has(text, "water ingress") || HasWord(text, "ingress")
            || Has(text, "undicht") || Has(text, "fremdwasser") || HasWord(text, "leak")
            || Has(text, "schwitzen") || Has(text, "tropft") || Has(text, "wasseraustritt"))
            return "BBF";
        // BBG: Exfiltration
        if (Has(text, "exfiltration"))
            return "BBG";
        // BBH: Ungeziefer
        if (Has(text, "ungeziefer") || Has(text, "ratte") || Has(text, "kakerlake")
            || Has(text, "pest") || Has(text, "rodent"))
            return "BBH";

        // ── BD: Weitere Codes ──
        if (Has(text, "allgemeinzustand") || Has(text, "fotobeispiel"))
            return "BDA";
        // BDD ist Basiscode mit Kindern (BDDA-BDDE). Wird als Hint zurueckgegeben —
        // der Code-Picker muss den Untertyp (klar/trueb/gefaerbt) ergaenzen.
        if (Has(text, "wasserspiegel") || Has(text, "wasserstand") || Has(text, "wasserlinie")
            || Has(text, "water level") || Has(text, "rueckstau") || Has(text, "standing water"))
            return "BDD";
        if (Has(text, "fehlanschluss") || Has(text, "zufluss") || Has(text, "misconnection"))
            return "BDE";
        if (Has(text, "gefaehrliche atmosphaere") || Has(text, "sauerstoffmangel")
            || Has(text, "schwefelwasserstoff") || Has(text, "methan"))
            return "BDF";
        if (Has(text, "keine sicht") || Has(text, "unter wasser") || Has(text, "verschlammung")
            || HasWord(text, "dampf") || Has(text, "no visibility"))
            return "BDG";

        return null;
    }

    /// <summary>
    /// Normalisiert PositionClock auf "N:00" Format.
    /// "3:00" → "3:00", "oben" → "12:00", "12 Uhr" → "12:00", null → null.
    /// </summary>
    public static string? NormalizeClock(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var text = raw.Trim().ToLowerInvariant();

        // H4: Englische Kurzwoerter mit Word-Boundary, damit "brightness" oder "righter"
        // nicht faelschlich matchen. Deutsche Begriffe sind spezifisch genug fuer Contains.
        if (text.Contains("oben") || text.Contains("scheitel") || text.Contains("krone")
            || Regex.IsMatch(text, @"\b(top|crown)\b"))
            return "12:00";
        if (text.Contains("unten") || text.Contains("sohle")
            || Regex.IsMatch(text, @"\b(bottom|invert)\b"))
            return "6:00";
        if (text.Contains("rechts") || Regex.IsMatch(text, @"\bright\b")) return "3:00";
        if (text.Contains("links") || Regex.IsMatch(text, @"\bleft\b")) return "9:00";

        var m = Regex.Match(raw, @"\b(1[0-2]|0?[1-9])\b");
        if (m.Success && int.TryParse(m.Groups[1].Value, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var hour) && hour >= 1 && hour <= 12)
            return $"{hour}:00";

        return raw.Trim();
    }

    // Phase 5.3 vorbereitend: ResolveFromClassifier (Sensor-Fusion mit
    // YoloClassifyPrediction) wurde nirgends aufgerufen — totes Feature.
    // Fuer Wiedereinfuehrung muesste YoloClassifyPrediction (UI.Ai.Pipeline.Dtos)
    // entweder mit nach Application gezogen werden oder als Extension-Method
    // in UI gehalten werden.

    private static bool Has(string text, string term) => text.Contains(term);

    private static bool HasWord(string text, string word)
    {
        int idx = text.IndexOf(word, StringComparison.Ordinal);
        while (idx >= 0)
        {
            bool leftOk = idx == 0 || !char.IsLetterOrDigit(text[idx - 1]);
            bool rightOk = idx + word.Length >= text.Length || !char.IsLetterOrDigit(text[idx + word.Length]);
            if (leftOk && rightOk) return true;
            idx = text.IndexOf(word, idx + 1, StringComparison.Ordinal);
        }
        return false;
    }
}
