using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using AuswertungPro.Next.UI.Ai.Pipeline;

namespace AuswertungPro.Next.UI.Ai;

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
        if (Services.CodeCatalog.VsaCodeTree.LookupLabel(normalized) != null)
            return normalized;

        // 2. Hauptcode (3 Zeichen) validieren — Untercodes akzeptieren
        if (normalized.Length >= 3)
        {
            var main = normalized[..3];
            if (Services.CodeCatalog.VsaCodeTree.LookupLabel(main) != null)
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
        var label = Services.CodeCatalog.VsaCodeTree.LookupLabel(code);
        if (label != null) return label;
        if (code.Length >= 3)
        {
            label = Services.CodeCatalog.VsaCodeTree.LookupLabel(code[..3]);
            if (label != null) return label;
        }
        if (code.Length >= 2)
            return Services.CodeCatalog.VsaCodeTree.LookupLabel(code[..2]);
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
        // BAK: Schadhafte Innenauskleidung
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

        if (text.Contains("oben") || text.Contains("scheitel") || text.Contains("krone"))
            return "12:00";
        if (text.Contains("unten") || text.Contains("sohle"))
            return "6:00";
        if (text.Contains("rechts")) return "3:00";
        if (text.Contains("links")) return "9:00";

        var m = Regex.Match(raw, @"\b(1[0-2]|0?[1-9])\b");
        if (m.Success && int.TryParse(m.Groups[1].Value, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var hour) && hour >= 1 && hour <= 12)
            return $"{hour}:00";

        return raw.Trim();
    }

    // ── Sensor-Fusion: YOLO-cls + Meterstand + Import-Kontext ──

    /// <summary>
    /// Ergebnis der Code-Aufloesung: Bester VSA-Code mit Konfidenz und Quelle.
    /// </summary>
    public sealed record ResolvedCode(string Code, double Confidence, string Source);

    /// <summary>
    /// Kombiniert YOLO-cls Wahrscheinlichkeiten mit Meterstand und Import-Kontext
    /// fuer zuverlaessige VSA-Code-Zuweisung (Sensor-Fusion).
    /// Regellogik in C# (Business Logic), Sidecar liefert nur rohe Predictions.
    /// </summary>
    public static ResolvedCode? ResolveFromClassifier(
        IReadOnlyList<YoloClassifyPrediction>? predictions,
        double currentMeter,
        double totalLength,
        IReadOnlyList<(string Code, string Description, double Meter)>? importContext = null)
    {
        if (predictions == null || predictions.Count == 0)
            return null;

        var top1 = predictions[0];
        var top1Code = top1.ClassName.ToUpperInvariant();
        var top1Conf = top1.Confidence;

        // BCD/BCE-Konfidenz aus Top-K extrahieren (auch wenn nicht Top-1)
        var bcdConf = predictions
            .FirstOrDefault(p => string.Equals(p.ClassName, "BCD", StringComparison.OrdinalIgnoreCase))
            ?.Confidence ?? 0;
        var bceConf = predictions
            .FirstOrDefault(p => string.Equals(p.ClassName, "BCE", StringComparison.OrdinalIgnoreCase))
            ?.Confidence ?? 0;

        // ── Meterstand-basierte Regeln (Sensor-Fusion) ──

        // Rohranfang: Meter < 0.5m UND (BCD Top-1 ODER BCD-Konfidenz > 20%)
        if (currentMeter < 0.5 && (top1Code == "BCD" || bcdConf > 0.20))
        {
            return new ResolvedCode("BCD", Math.Max(bcdConf, 0.80),
                $"Meter {currentMeter:F2}m + YOLO BCD {bcdConf:P0}");
        }

        // Rohrende: Meter > 90% Gesamtlaenge UND (BCE Top-1 ODER BCE-Konfidenz > 20%)
        if (totalLength > 1 && currentMeter > totalLength * 0.90
            && (top1Code == "BCE" || bceConf > 0.20))
        {
            return new ResolvedCode("BCE", Math.Max(bceConf, 0.80),
                $"Meter {currentMeter:F2}/{totalLength:F1}m + YOLO BCE {bceConf:P0}");
        }

        // ── Import-Kontext Boost ──
        // Import-Befund in der Naehe (+/- 1.5m) + YOLO erkennt gleiche Familie
        if (importContext != null && importContext.Count > 0 && top1Conf > 0.30)
        {
            var family = top1Code.Length >= 3 ? top1Code[..3] : top1Code;
            var nearbyImport = importContext
                .Where(ic => !string.IsNullOrWhiteSpace(ic.Code)
                    && ic.Code.StartsWith(family, StringComparison.OrdinalIgnoreCase)
                    && Math.Abs(ic.Meter - currentMeter) < 1.5)
                .OrderBy(ic => Math.Abs(ic.Meter - currentMeter))
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(nearbyImport.Code))
            {
                return new ResolvedCode(nearbyImport.Code, top1Conf,
                    $"YOLO {top1Code} {top1Conf:P0} + Import {nearbyImport.Code} @ {nearbyImport.Meter:F1}m");
            }
        }

        // ── Reine YOLO-Klassifikation (ohne Boost) ──
        if (top1Code != "OTHER" && top1Conf > 0.40)
        {
            return new ResolvedCode(top1Code, top1Conf, $"YOLO {top1Code} {top1Conf:P0}");
        }

        // ── Fallback: Top-2 wenn Top-1 = OTHER ──
        if (top1Code == "OTHER" && predictions.Count > 1)
        {
            var top2 = predictions[1];
            if (top2.Confidence > 0.15
                && !string.Equals(top2.ClassName, "OTHER", StringComparison.OrdinalIgnoreCase))
            {
                return new ResolvedCode(top2.ClassName.ToUpperInvariant(), top2.Confidence,
                    $"YOLO Fallback {top2.ClassName} {top2.Confidence:P0} (Top-1 OTHER)");
            }
        }

        return null;
    }

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
