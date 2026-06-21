using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;

namespace AuswertungPro.Next.Infrastructure.Ai;

/// <summary>
/// Zentraler VSA-Code-Resolver fuer KI-Findings.
/// Einzige Quelle fuer Code-Normalisierung, Label-Lookup und Clock-Normalisierung.
/// Wird vom PlayerWindow-Codiermodus genutzt.
/// </summary>
public static class VsaCodeResolver
{
    private static ICodeCatalogProvider? _catalogProvider;

    /// <summary>
    /// Liest den aktuell konfigurierten Katalog-Provider (zum Sichern/Wiederherstellen in Tests).
    /// </summary>
    public static ICodeCatalogProvider? CurrentCatalog => _catalogProvider;

    public static void ConfigureCatalog(ICodeCatalogProvider? catalogProvider)
    {
        _catalogProvider = catalogProvider;
    }

    /// <summary>
    /// Normalisiert einen rohen VSA-Code-Hint von der KI.
    /// Akzeptiert nur Codes die fachlich plausibel sind:
    /// - Exakt im Katalog (BCD, BCAEB, BABBA)
    /// - Hauptcode (3Z) im Katalog UND Gesamtlaenge 2-8 Grossbuchstaben
    /// Gibt normalisierten Code oder null zurueck.
    /// </summary>
    public static string? NormalizeFindingCode(string? rawCode)
    {
        if (string.IsNullOrWhiteSpace(rawCode))
            return null;

        var normalized = rawCode.Trim().Replace(".", "").ToUpperInvariant();

        // VSA-KEK-Codes sind meistens 3-5 Zeichen, alte Daten koennen laenger sein.
        if (normalized.Length < 2 || normalized.Length > 8)
            return null;
        if (!Regex.IsMatch(normalized, @"^[A-Z]{2,8}$"))
            return null;

        // 1. Exakter Katalog-Lookup
        if (TryGetCatalogDefinition(normalized, out _))
            return normalized;

        // 2. Hauptcode (3 Zeichen) validieren — Untercodes akzeptieren
        if (normalized.Length >= 3)
        {
            var main = normalized[..3];
            if (TryGetCatalogDefinition(main, out _))
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
        var catalogLabel = LookupCatalogLabel(code);
        if (catalogLabel != null) return catalogLabel;
        if (code.Length >= 3)
        {
            catalogLabel = LookupCatalogLabel(code[..3]);
            if (catalogLabel != null) return catalogLabel;
        }
        if (code.Length >= 2)
        {
            catalogLabel = LookupCatalogLabel(code[..2]);
            if (catalogLabel != null) return catalogLabel;
        }
        return null;
    }

    public static bool IsStreckenschadenCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;

        var normalized = code.Trim().Replace(".", "").ToUpperInvariant();
        if (TryGetCatalogDefinition(normalized, out var exact) && exact.RequiresRange)
            return true;

        for (var len = normalized.Length - 1; len >= 3; len--)
        {
            var prefix = normalized[..len];
            if (TryGetCatalogDefinition(prefix, out var def) && def.RequiresRange)
                return true;
        }

        if (StreckenschadenCodes.Contains(normalized))
            return true;

        for (var len = normalized.Length - 1; len >= 3; len--)
        {
            if (StreckenschadenCodes.Contains(normalized[..len]))
                return true;
        }

        return false;
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
            .Replace("\u00e4", "ae").Replace("\u00f6", "oe")
            .Replace("\u00fc", "ue").Replace("\u00df", "ss")
            .Replace("ä", "ae").Replace("ö", "oe")
            .Replace("ü", "ue").Replace("ß", "ss");

        // Grundstruktur
        if (Has(text, "anschluss") || Has(text, "abzweig") || Has(text, "stutzen")
            || Has(text, "zulauf") || Has(text, "lateral connection") || HasWord(text, "lateral"))
            return CatalogValidated("BCA");
        if (Has(text, "bogen") || Has(text, "kruemm") || Has(text, "kurve") || HasWord(text, "bend"))
            return CatalogValidated("BCC");
        if (Has(text, "rohranfang") || Has(text, "pipe start") || Has(text, "anfangsknoten")
            || Has(text, "einstieg") || HasWord(text, "manhole"))
            return CatalogValidated("BCD");
        if (Has(text, "rohrende") || Has(text, "pipe end") || Has(text, "endknoten")
            || Has(text, "ausstieg"))
            return CatalogValidated("BCE");

        // Strukturelle Schaeden
        if (Has(text, "riss") || HasWord(text, "crack") || Has(text, "fracture") || Has(text, "fissure"))
            return CatalogValidated("BAB");
        if (Has(text, "bruch") || Has(text, "einsturz") || Has(text, "collapse"))
            return CatalogValidated("BAC");
        if (Has(text, "deformation") || Has(text, "verformung") || HasWord(text, "oval"))
            return CatalogValidated("BAA");
        if (Has(text, "rohrverbindung") || Has(text, "muffe") || Has(text, "versatz")
            || HasWord(text, "offset") || Has(text, "displaced"))
            return CatalogValidated("BAJ");
        if (Has(text, "wurzel") || Has(text, "root intrusion") || Has(text, "bewuchs"))
            return CatalogValidated("BBA");
        if (Has(text, "einragung") || Has(text, "intrusion") || Has(text, "protruding"))
            return CatalogValidated("BAI");

        // Oberflaechenschaeden
        if (Has(text, "korrosion") || Has(text, "corrosion") || HasWord(text, "rost")
            || Has(text, "erosion"))
            return CatalogValidated("BAF");
        if (Has(text, "inkrustation") || Has(text, "encrustation") || Has(text, "kalk")
            || Has(text, "anhaftung") || Has(text, "sinter") || Has(text, "attached deposit"))
            return CatalogValidated("BBB");

        // Betrieblich
        if (Has(text, "ablagerung") || HasWord(text, "sediment") || Has(text, "schlamm")
            || HasWord(text, "silt") || HasWord(text, "debris"))
            return CatalogValidated("BBC");
        if (DescribesNormalWastewaterFlow(text) || MentionsTurbidWastewaterWithoutBackwater(text))
            return null;
        if (Has(text, "wasserspiegel")
            || Has(text, "wasserstand")
            || Has(text, "wasserlinie")
            || Has(text, "water level")
            || Has(text, "waterline")
            || Has(text, "standing water")
            || HasWord(text, "puddle")
            || Has(text, "rueckstau")
            || (HasWord(text, "water") &&
                (HasWord(text, "level") || HasWord(text, "standing")
                 || Has(text, "sohle") || Has(text, "invert"))))
            return CatalogValidated("BDDC");

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
        IReadOnlyList<(string Code, string Description, double Meter)>? importContext = null,
        bool isBend = false)
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

        // Bogen-Veto: Der cls-Klassifikator hat keine stabile Bogen-Klasse und meldet
        // Boegen oft als BCE/Rohrende. Klare andere Schadencodes bleiben unangetastet.
        if (isBend && (top1Code is "BCE" or "LEER" or "OTHER" or "NORMAL"
                       || (top1Conf <= 0.40 && bceConf > 0.20)))
        {
            return new ResolvedCode(
                "BCC",
                Math.Max(Math.Max(top1Code == "BCE" ? top1Conf : bceConf, 0.75), 0.01),
                top1Code == "BCE"
                    ? $"Bogen-Geometrie statt YOLO BCE {top1Conf:P0}"
                    : "Bogen-Geometrie");
        }

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

        // ── Negativ-Gate: Grundgeruest-Codes sind ortsgebunden ──
        // BCD existiert nur am Rohranfang, BCE nur am Rohrende. Ein "BCE" mitten
        // im Rohr ist meist ein offener Anschluss (vor v8 kannte das Modell BCA nicht) —
        // Pilot 2026-06-10: 7x BCE @ 1.7m von 12.5m. Dann lieber KEINE
        // Klassifikator-Entscheidung (Qwen/Label-Heuristik uebernimmt).
        if (top1Code == "BCD" && currentMeter > 1.5)
            return null;
        if (top1Code == "BCE" && totalLength > 1 && currentMeter < totalLength * 0.85)
            return null;

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

    private static bool DescribesNormalWastewaterFlow(string text)
        => !DescribesBackwaterOrStandingWater(text)
           && (Has(text, "normalfluss")
               || Has(text, "normaler durchfluss")
               || Has(text, "normaler abfluss")
               || Has(text, "normaler abwasserfluss")
               || Has(text, "normal fliess")
               || Has(text, "normal flies")
               || Has(text, "normal flow")
               || Has(text, "flowing normally"));

    private static bool MentionsTurbidWastewaterWithoutBackwater(string text)
        => !DescribesBackwaterOrStandingWater(text)
           && (Has(text, "trueb") || Has(text, "turbid") || Has(text, "murky"))
           && (Has(text, "abwasser") || Has(text, "wastewater") || Has(text, "sewage"));

    private static bool DescribesBackwaterOrStandingWater(string text)
        => Has(text, "rueckstau")
           || Has(text, "backwater")
           || Has(text, "stauwasser")
           || Has(text, "angestaut")
           || Has(text, "aufgestaut")
           || Has(text, "standing water")
           || Has(text, "stehendes wasser")
           || Has(text, "stehendes abwasser");

    private static string? CatalogValidated(string code)
    {
        if (_catalogProvider is null)
            return code;

        return NormalizeFindingCode(code) is null ? null : code;
    }

    private static string? LookupCatalogLabel(string code)
    {
        if (!TryGetCatalogDefinition(code, out var def))
            return null;

        return string.IsNullOrWhiteSpace(def.Title) ? def.Code : def.Title;
    }

    private static bool TryGetCatalogDefinition(string code, out CodeDefinition def)
    {
        def = new CodeDefinition();
        var catalog = _catalogProvider;
        if (catalog is null || string.IsNullOrWhiteSpace(code))
            return false;

        var normalized = code.Trim().Replace(".", "").ToUpperInvariant();
        if (catalog.TryGet(normalized, out def))
            return true;

        if (normalized.Length >= 3 && catalog.TryGet(normalized[..3], out def))
            return true;

        if (normalized.Length >= 2 && catalog.TryGet(normalized[..2], out def))
            return true;

        return false;
    }

    // Bedeutungen der BB-Codes kommen aus VsaCodeTree.Groups["BB"]; dieses Set ist nur
    // eine Streckenschaden-Heuristik und darf keine umetikettierten Fachbedeutungen tragen.
    private static readonly HashSet<string> StreckenschadenCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "BABA", "BABAB", "BABAC", "BABB", "BABBA", "BABBB", "BABBC",
        "BABC", "BABCA", "BAFA", "BAFAE", "BAFB", "BAFC", "BAFD",
        "BAG", "BAGA", "BBA", "BBAA", "BBAB", "BBB", "BBBA",
        "BBC", "BBCA", "BBCB", "BBCC", "BBD", "BBDA", "BBDB"
    };

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
