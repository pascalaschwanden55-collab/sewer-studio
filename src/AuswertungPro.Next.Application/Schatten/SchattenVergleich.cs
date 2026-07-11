using System;
using System.Globalization;
using System.Linq;

namespace AuswertungPro.Next.Application.Schatten;

/// <summary>Gesamt-Ampel des Vergleichs Mensch vs. Schatten fuer eine Haltung.</summary>
public enum SchattenAbweichung
{
    KeinVergleich,   // Mensch hat (noch) keine Auswertung -> grau
    Gleich,          // gruen
    LeichtAbweichend,// gelb: Massnahme anders ODER Kosten > +-25 %
    StarkAbweichend  // rot: Zustandsklasse anders
}

/// <summary>
/// Reine Vergleichslogik (testbar, kein I/O): legt die menschliche Auswertung
/// (Feldwerte als Text) neben das Schatten-Ergebnis und liefert die Ampel.
/// </summary>
public static class SchattenVergleich
{
    public const decimal KostenToleranz = 0.25m; // +-25 %

    public static SchattenAbweichung Bewerte(
        string? menschKlasse,
        string? menschMassnahme,
        string? menschKostenText,
        string? schattenKlasse,
        string? schattenMassnahme,
        decimal? schattenKosten)
    {
        var klasseMensch = Normalize(menschKlasse);
        var massnahmeMensch = Normalize(menschMassnahme);
        var kostenMensch = TryParseKosten(menschKostenText);

        var hatMenschWerte = klasseMensch.Length > 0 || massnahmeMensch.Length > 0 || kostenMensch.HasValue;
        if (!hatMenschWerte)
            return SchattenAbweichung.KeinVergleich;

        // Rot: Zustandsklasse widerspricht (nur wenn beide Seiten eine Klasse haben).
        var klasseSchatten = Normalize(schattenKlasse);
        if (klasseMensch.Length > 0 && klasseSchatten.Length > 0 &&
            !string.Equals(klasseMensch, klasseSchatten, StringComparison.OrdinalIgnoreCase))
            return SchattenAbweichung.StarkAbweichend;

        // Gelb: Massnahme passt nicht zusammen (nur wenn beide vorhanden).
        var massnahmeSchatten = Normalize(schattenMassnahme);
        if (massnahmeMensch.Length > 0 && massnahmeSchatten.Length > 0 &&
            !MassnahmeStimmtUeberein(massnahmeMensch, massnahmeSchatten))
            return SchattenAbweichung.LeichtAbweichend;

        // Gelb: Kosten weichen um mehr als die Toleranz ab (nur wenn beide vorhanden).
        if (kostenMensch is > 0m && schattenKosten is > 0m)
        {
            var abweichung = Math.Abs(kostenMensch.Value - schattenKosten.Value) / kostenMensch.Value;
            if (abweichung > KostenToleranz)
                return SchattenAbweichung.LeichtAbweichend;
        }

        return SchattenAbweichung.Gleich;
    }

    /// <summary>
    /// Mensch-Massnahmenfeld ist Freitext (oft mehrere, mit Trennzeichen). Uebereinstimmung =
    /// die Schatten-Massnahme kommt normalisiert darin vor (oder umgekehrt bei Kurzformen).
    /// </summary>
    public static bool MassnahmeStimmtUeberein(string menschText, string schattenMassnahme)
    {
        var m = NormalizeVergleichsText(menschText);
        var s = NormalizeVergleichsText(schattenMassnahme);
        if (m.Length == 0 || s.Length == 0) return false;
        return m.Contains(s, StringComparison.OrdinalIgnoreCase)
            || s.Contains(m, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Toleranter Kosten-Parser fuer das Mensch-Feld: "1'200.50", "1200,50 CHF",
    /// "CHF 1 200", leer. Liefert null, wenn keine Zahl erkennbar ist.
    /// </summary>
    public static decimal? TryParseKosten(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var bereinigt = new string(text
            .Replace("CHF", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Fr.", "", StringComparison.OrdinalIgnoreCase)
            .Where(ch => char.IsDigit(ch) || ch is '.' or ',')
            .ToArray());
        if (bereinigt.Length == 0) return null;

        // Schweizer Apostroph-Tausender sind oben schon entfernt; Komma als Dezimaltrenner
        // nur, wenn kein Punkt vorhanden ist (sonst 1,234.50-Falle).
        if (bereinigt.Contains(',') && !bereinigt.Contains('.'))
            bereinigt = bereinigt.Replace(',', '.');
        else
            bereinigt = bereinigt.Replace(",", "");

        return decimal.TryParse(bereinigt, NumberStyles.Number, CultureInfo.InvariantCulture, out var wert)
            ? wert
            : null;
    }

    private static string Normalize(string? s) => (s ?? "").Trim();

    private static string NormalizeVergleichsText(string s)
        => new(s.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
}
