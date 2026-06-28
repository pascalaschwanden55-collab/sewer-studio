using System.Text;

namespace CadasterDbReader;

/// <summary>
/// Reine Hilfsmethoden fuer die Topologie-Normalisierung von Schacht- und Haltungsnamen.
/// Kein IO, keine Datenbankzugriffe.
/// </summary>
internal static class CadasterTopologyConventions
{
    /// <summary>
    /// Bereinigt eine Knoten-ID: Leerzeichen entfernen, numerisches ".0"-Suffix abschneiden.
    /// </summary>
    public static string CleanNodeId(string value)
    {
        var clean = (value ?? "").Trim().Replace(" ", "");
        if (clean.EndsWith(".0", StringComparison.Ordinal) &&
            clean[..^2].All(char.IsDigit))
        {
            return clean[..^2];
        }
        return clean;
    }

    /// <summary>
    /// Trennt einen Haltungsnamen "SchachtOben-SchachtUnten" in seine beiden Teile.
    /// Gibt false zurueck, wenn kein gueltiger Bindestrich gefunden wird.
    /// </summary>
    public static bool TrySplitHoldingPair(string value, out string start, out string end)
    {
        start = "";
        end = "";
        var clean = value.Trim();
        var dash = clean.IndexOf('-');
        if (dash <= 0 || dash >= clean.Length - 1)
            return false;

        start = CleanNodeId(clean[..dash]);
        end = CleanNodeId(clean[(dash + 1)..]);
        return !string.IsNullOrWhiteSpace(start) && !string.IsNullOrWhiteSpace(end);
    }

    /// <summary>
    /// Erzeugt einen reihenfolge-unabhaengigen Schluessel fuer ein Schachtpaar.
    /// </summary>
    public static string UnorderedPairKey(string a, string b)
    {
        var parts = new[] { NormalizePairComponent(a), NormalizePairComponent(b) }
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return $"{parts[0]}|{parts[1]}";
    }

    /// <summary>
    /// Normalisiert eine Schacht-ID fuer den Paarvergleich: nur alphanumerisch, Kleinbuchstaben.
    /// </summary>
    public static string NormalizePairComponent(string value)
    {
        var sb = new StringBuilder();
        foreach (var ch in CleanNodeId(value).ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
                sb.Append(ch);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Baut alle alternativen Haltungs-IDs (Richtungsvarianten, Punkt-freie Varianten, Schwanz-Varianten).
    /// </summary>
    public static List<string> BuildAlternativeHoldingIds(string schachtOben, string schachtUnten)
    {
        var variants = new List<string>();
        AddPairVariants(variants, schachtOben, schachtUnten);
        AddPairVariants(variants, schachtUnten, schachtOben);
        return variants
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Fuegt Paar-Varianten (mit/ohne Punkte, Schwanz-Segmente) in die Ziel-Liste ein.
    /// </summary>
    public static void AddPairVariants(List<string> variants, string a, string b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
            return;

        variants.Add($"{a}-{b}");

        var aNoDot = a.Replace(".", "");
        var bNoDot = b.Replace(".", "");
        if (!string.Equals(aNoDot, a, StringComparison.Ordinal) || !string.Equals(bNoDot, b, StringComparison.Ordinal))
            variants.Add($"{aNoDot}-{bNoDot}");

        var aTail = a.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? a;
        var bTail = b.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? b;
        if (!string.Equals(aTail, a, StringComparison.Ordinal) || !string.Equals(bTail, b, StringComparison.Ordinal))
            variants.Add($"{aTail}-{bTail}");
    }

    /// <summary>
    /// Wendet lokale Topologie-Konventionen auf eine Haltung an (in-place).
    /// Schaechte mit "10.&lt;ziffern&gt;"-Praefix gehoeren auf die Downstream-Seite.
    /// Mirror der Python-Implementierung.
    /// </summary>
    public static void ApplyTopologyConventions(CadasterTopologyHolding h)
    {
        if (h is null) return;
        var oben = (h.SchachtOben ?? "").Trim();
        var unten = (h.SchachtUnten ?? "").Trim();
        if (!System.Text.RegularExpressions.Regex.IsMatch(oben, @"^10\.\d+$")) return;
        h.SchachtOben = unten;
        h.SchachtUnten = oben;
        h.CanonicalFolderName = $"{unten}-{oben}";
        if (!h.FliessrichtungQuelle.Contains("+10dot_rule"))
            h.FliessrichtungQuelle += "+10dot_rule";
        const string msg = "schacht-Reihenfolge per 10.xxx-Konvention korrigiert";
        if (!h.Warnings.Contains(msg))
            h.Warnings.Add(msg);
    }

    /// <summary>
    /// Bereinigt einen Verzeichnisname-Bestandteil: ungueltige Zeichen durch '_' ersetzen.
    /// </summary>
    public static string SanitizePathSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var cleaned = new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "cadaster_project" : cleaned;
    }
}
