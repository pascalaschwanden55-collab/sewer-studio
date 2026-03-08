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

        /// <summary>Parameter-Schlüssel: "Q1" oder "Q2".</summary>
        public string Parameter { get; set; } = "";

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
            return JsonSerializer.Deserialize<VsaClassificationTable>(json,
                Application.Common.JsonDefaults.CaseInsensitive) ?? new VsaClassificationTable();
        }
        catch (Exception)
        {
            // Korrupte oder fehlende JSON-Datei: leere Tabelle verwenden
            return new VsaClassificationTable();
        }
    }

    /// <summary>
    /// Findet eine Regel anhand des Codes (exakt, dann 3-Char-Fallback).
    /// </summary>
    public VsaRule? Find(string code)
    {
        var norm = VsaEvaluationService.NormalizeCode(code);
        var exact = Rules.FirstOrDefault(r => string.Equals(VsaEvaluationService.NormalizeCode(r.Code), norm, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
            return exact;

        if (norm.Length > 3)
        {
            var shortCode = norm.Substring(0, 3);
            return Rules.FirstOrDefault(r => string.Equals(VsaEvaluationService.NormalizeCode(r.Code), shortCode, StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }

    /// <summary>
    /// Klassifiziert einen Schadenscode unter Berücksichtigung der Quantifizierung (Q1/Q2).
    /// Gibt null zurück, wenn kein passender Code gefunden wird.
    /// Fällt auf statische EZ-Werte zurück, wenn keine Quantifizierung vorhanden ist.
    /// </summary>
    public VsaClassificationResult? Classify(string code, string? q1, string? q2)
    {
        var rule = Find(code);
        if (rule is null)
            return null;

        var ezd = rule.EZD;
        var ezs = rule.EZS;
        var ezb = rule.EZB;

        if (rule.QuantRules is { Count: > 0 })
        {
            foreach (var qr in rule.QuantRules)
            {
                var paramValue = qr.Parameter.Equals("Q1", StringComparison.OrdinalIgnoreCase)
                    ? TryParseQuantValue(q1)
                    : qr.Parameter.Equals("Q2", StringComparison.OrdinalIgnoreCase)
                        ? TryParseQuantValue(q2)
                        : null;

                if (paramValue is null)
                    continue; // Kein Wert → statischen Default beibehalten

                var resolved = ResolveFromRanges(qr.Ranges, paramValue.Value);
                if (resolved is null)
                    continue; // Kein passender Bereich → statischen Default beibehalten

                switch (qr.Requirement.ToLowerInvariant())
                {
                    case "ezd": ezd = resolved; break;
                    case "ezs": ezs = resolved; break;
                    case "ezb": ezb = resolved; break;
                }
            }
        }

        return new VsaClassificationResult(ezd, ezs, ezb);
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
