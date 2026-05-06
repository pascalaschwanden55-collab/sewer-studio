using System.Globalization;
using System.Text.Json;
using AuswertungPro.Next.Domain.Vsa;

namespace AuswertungPro.Next.Infrastructure.Vsa.Classification;

public sealed class VsaClassificationTable
{
    public double DefaultMinLength_m { get; set; } = 3.0;
    public List<VsaRule> Rules { get; set; } = new();

    public sealed class VsaRule
    {
        public string Code { get; set; } = "";
        public int? EZD { get; set; }
        public int? EZS { get; set; }
        public int? EZB { get; set; }

        /// <summary>
        /// Optionale quantifizierungsabhängige Regeln.
        /// Wenn vorhanden und Q1/Q2-Werte gesetzt sind, überschreiben diese die statischen EZ-Werte.
        /// </summary>
        public List<QuantRule>? QuantRules { get; set; }
    }

    /// <summary>
    /// Regel für quantifizierungsabhängige EZ-Auflösung.
    /// </summary>
    public sealed class QuantRule
    {
        /// <summary>Ziel-Anforderung: "ezd", "ezs" oder "ezb".</summary>
        public string Requirement { get; set; } = "";

        /// <summary>Parameter-Schlüssel: "Q1", "Q2", "Q1_PCT_DN" (Q1 in % von DN) oder "Q1_BIEGESTEIF"/"Q1_BIEGEWEICH" (kontextabhaengig).</summary>
        public string Parameter { get; set; } = "";

        /// <summary>
        /// Optionaler Materialfilter: nur anwenden wenn Material in dieser Liste ist.
        /// Werte: "biegesteif" (Beton, Steinzeug, Guss), "biegeweich" (PVC, PE, PP, GFK).
        /// Leer = alle Materialien.
        /// </summary>
        public List<string>? MaterialFilter { get; set; }

        /// <summary>Geordnete Bereiche mit Min (inklusiv) und Max (exklusiv).</summary>
        public List<QuantRange> Ranges { get; set; } = new();
    }

    /// <summary>
    /// Ein Wertebereich für die Quantifizierungsauflösung.
    /// </summary>
    public sealed class QuantRange
    {
        public double Min { get; set; }
        public double Max { get; set; }
        public int EZ { get; set; }
    }

    public static VsaClassificationTable LoadFromFile(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            var table = JsonSerializer.Deserialize<VsaClassificationTable>(json,
                Application.Common.JsonDefaults.Lenient);
            if (table is null || table.Rules.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[VsaClassificationTable] WARNUNG: '{path}' lieferte leere Tabelle (rules={table?.Rules.Count ?? 0}). " +
                    "Alle Findings werden als 'unbekannter Code' gewertet -> ZN bleibt 4.0/falsch.");
                return new VsaClassificationTable();
            }
            return table;
        }
        catch (Exception ex)
        {
            // Korrupte JSON: leere Tabelle, aber sichtbar im Log + Throw fuer Aufrufer-Auswahl.
            System.Diagnostics.Debug.WriteLine(
                $"[VsaClassificationTable] FEHLER beim Parsen von '{path}': {ex.GetType().Name}: {ex.Message}");
            // Aufrufer kann entscheiden, ob er die leere Tabelle akzeptiert oder die Methode-Variante
            // 'TryLoadFromFile' nutzt. Hier konservativ: leere Tabelle zurueckgeben mit Log.
            return new VsaClassificationTable();
        }
    }

    /// <summary>Versucht die Tabelle zu laden. Bei Fehler: false + ErrorMessage statt stiller Schluckung.</summary>
    public static bool TryLoadFromFile(string path, out VsaClassificationTable table, out string? errorMessage)
    {
        table = new VsaClassificationTable();
        errorMessage = null;
        try
        {
            if (!File.Exists(path))
            {
                errorMessage = $"Datei nicht gefunden: {path}";
                return false;
            }
            var json = File.ReadAllText(path);
            var parsed = JsonSerializer.Deserialize<VsaClassificationTable>(json,
                Application.Common.JsonDefaults.Lenient);
            if (parsed is null)
            {
                errorMessage = "Deserialisierung lieferte null";
                return false;
            }
            if (parsed.Rules.Count == 0)
            {
                errorMessage = "Tabelle hat keine Rules (leer oder Schema-Mismatch)";
                return false;
            }
            table = parsed;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"{ex.GetType().Name}: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Longest-prefix-match: BABBC -> BABBC -> BABB -> BAB.
    /// Erlaubt Char1- und Char2-spezifische Sonderregeln (z.B. BABB Querriss pauschal mild,
    /// BACC Einsturz mit Hohlraeumen pauschal kritisch) ohne dass jeder Vollcode in der Tabelle stehen muss.
    /// </summary>
    public VsaRule? Find(string code)
    {
        var norm = VsaEvaluationService.NormalizeCode(code);
        if (norm.Length < 3)
            return null;

        for (int len = norm.Length; len >= 3; len--)
        {
            var prefix = norm.Substring(0, len);
            var match = Rules.FirstOrDefault(r =>
                string.Equals(VsaEvaluationService.NormalizeCode(r.Code), prefix, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
                return match;
        }

        return null;
    }

    /// <summary>
    /// Klassifiziert einen Schadenscode unter Beruecksichtigung der Quantifizierung (Q1/Q2)
    /// und optional des Haltungs-Kontexts (DN, Rohrmaterial).
    /// Q1_PCT_DN: Q1 wird relativ zu DN bewertet (z.B. BAJ Versatz: ≥DN/2 = EZ 0).
    /// MaterialFilter: Regel nur anwenden wenn Material in der Liste ist (biegesteif vs. biegeweich).
    /// </summary>
    public VsaClassificationResult? Classify(string code, string? q1, string? q2, double? dn = null, string? material = null)
    {
        var rule = Find(code);
        if (rule is null)
            return null;

        var ezd = rule.EZD;
        var ezs = rule.EZS;
        var ezb = rule.EZB;

        var materialClass = ClassifyMaterial(material);

        if (rule.QuantRules is { Count: > 0 })
        {
            // Erste passende Regel pro Requirement gewinnt — verhindert dass ein
            // Fallback-Eintrag eine spezifische material-/DN-Regel ueberschreibt.
            // Material-/DN-spezifische Regeln zuerst auflisten, generischer Fallback zuletzt.
            var setRequirements = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var qr in rule.QuantRules)
            {
                var req = qr.Requirement.ToLowerInvariant();
                if (setRequirements.Contains(req))
                    continue; // schon gesetzt durch eine spezifischere Regel

                // Materialfilter: Regel ueberspringen wenn Material nicht passt
                if (qr.MaterialFilter is { Count: > 0 } &&
                    (materialClass is null ||
                     !qr.MaterialFilter.Any(m => string.Equals(m, materialClass, StringComparison.OrdinalIgnoreCase))))
                {
                    continue;
                }

                var raw = qr.Parameter.StartsWith("Q1", StringComparison.OrdinalIgnoreCase)
                    ? TryParseQuantValue(q1)
                    : qr.Parameter.StartsWith("Q2", StringComparison.OrdinalIgnoreCase)
                        ? TryParseQuantValue(q2)
                        : null;

                if (raw is null)
                    continue;

                // DN-relative Auswertung: Wert wird zu Prozent von DN umgerechnet.
                var paramValue = raw.Value;
                if (qr.Parameter.Equals("Q1_PCT_DN", StringComparison.OrdinalIgnoreCase) ||
                    qr.Parameter.Equals("Q2_PCT_DN", StringComparison.OrdinalIgnoreCase))
                {
                    if (dn is null or <= 0)
                        continue; // Ohne DN kann nicht relativiert werden
                    paramValue = raw.Value / dn.Value * 100.0;
                }

                var resolved = ResolveFromRanges(qr.Ranges, paramValue);
                if (resolved is null)
                    continue;

                switch (req)
                {
                    case "ezd": ezd = resolved; setRequirements.Add(req); break;
                    case "ezs": ezs = resolved; setRequirements.Add(req); break;
                    case "ezb": ezb = resolved; setRequirements.Add(req); break;
                }
            }
        }

        return new VsaClassificationResult(ezd, ezs, ezb);
    }

    /// <summary>
    /// Klassifiziert Rohrmaterial nach VSA-Verhalten:
    /// biegesteif: Beton, Stahlbeton, Steinzeug, Guss, Asbestzement, Mauerwerk.
    /// biegeweich: PVC, PE, PP, GFK, Stahl, alle Kunststoffe.
    /// </summary>
    private static string? ClassifyMaterial(string? material)
    {
        if (string.IsNullOrWhiteSpace(material))
            return null;
        var m = material.Trim().ToLowerInvariant();
        if (m.Contains("beton") || m.Contains("steinzeug") || m.Contains("guss")
            || m.Contains("asbest") || m.Contains("mauerwerk") || m.Contains("zement"))
            return "biegesteif";
        if (m.Contains("pvc") || m.Contains("pe") || m.Contains("pp")
            || m.Contains("gfk") || m.Contains("polyvinyl") || m.Contains("polyethylen")
            || m.Contains("polypropylen") || m.Contains("kunststoff") || m.Contains("stahl"))
            return "biegeweich";
        return null;
    }

    private static double? TryParseQuantValue(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        var normalized = raw.Trim().Replace(',', '.');
        return double.TryParse(normalized, NumberStyles.Float | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    private static int? ResolveFromRanges(List<QuantRange> ranges, double value)
    {
        foreach (var range in ranges)
        {
            if (value >= range.Min && value < range.Max)
                return range.EZ;
        }
        return null; // Kein Bereich passt
    }
}
