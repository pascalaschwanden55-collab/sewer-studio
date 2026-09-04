using System.Globalization;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Application.Export.Geonis;

/// <summary>
/// Liest Millimeter-Masse aus den Textfeldern des Programms.
///
/// Streng nach Absicht: Nur eindeutige Zahlen ergeben einen Wert. Alles andere ergibt "kein Wert".
/// Ein geratenes Mass darf nie in den Kataster zurueckgeschrieben werden — dort gibt es kein
/// Rueckgaengig.
/// </summary>
public static class Sia405MassParser
{
    /// <summary>Groesstes zulaessiges Mass in Millimetern (SIA405: 0..99999).</summary>
    public const int MaxMillimeter = 99999;

    private static readonly Regex ZahlMitEinheit = new(
        @"(?<n>\d{1,6}(?:[.,]\d{1,3})?)\s*(?<u>mm|cm|m)?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>Einzelnes Mass in Millimetern, z. B. "300", "300 mm", "0.30 m".</summary>
    public static int? LiesMillimeter(string? text)
    {
        var werte = LiesAlleMillimeter(text);
        return werte.Count == 1 ? werte[0] : null;
    }

    /// <summary>
    /// Schachtmass: "1000 mm" ergibt (1000, 1000) — rund oder quadratisch.
    /// "1100 x 900 mm" ergibt (1100, 900). Dimension1 ist im SIA405-Modell immer das
    /// groessere Innenmass, Dimension2 das kleinere.
    /// </summary>
    public static (int Dimension1, int Dimension2)? LiesSchachtmass(string? text)
    {
        var werte = LiesAlleMillimeter(text);
        if (werte.Count == 1)
            return (werte[0], werte[0]);
        if (werte.Count == 2)
            return werte[0] >= werte[1] ? (werte[0], werte[1]) : (werte[1], werte[0]);
        return null;
    }

    private static List<int> LiesAlleMillimeter(string? text)
    {
        var ergebnis = new List<int>();
        if (string.IsNullOrWhiteSpace(text))
            return ergebnis;

        var treffer = ZahlMitEinheit.Matches(text);
        if (treffer.Count == 0)
            return ergebnis;

        // Gemeinsame Einheit: "1100 x 900 mm" nennt die Einheit nur einmal am Schluss.
        var gemeinsameEinheit = string.Empty;
        foreach (Match match in treffer)
        {
            var einheit = match.Groups["u"].Value;
            if (!string.IsNullOrWhiteSpace(einheit))
            {
                gemeinsameEinheit = einheit;
                break;
            }
        }

        foreach (Match match in treffer)
        {
            var roh = match.Groups["n"].Value.Replace(',', '.');
            if (!decimal.TryParse(roh, NumberStyles.Number, CultureInfo.InvariantCulture, out var zahl))
            {
                ergebnis.Clear();
                return ergebnis;
            }

            var einheit = match.Groups["u"].Value;
            if (string.IsNullOrWhiteSpace(einheit))
                einheit = gemeinsameEinheit;

            var millimeter = InMillimeter(zahl, einheit);
            if (millimeter is null)
            {
                ergebnis.Clear();
                return ergebnis;
            }

            ergebnis.Add(millimeter.Value);
        }

        return ergebnis;
    }

    private static int? InMillimeter(decimal zahl, string einheit)
    {
        var faktor = einheit.ToLowerInvariant() switch
        {
            "cm" => 10m,
            "m" => 1000m,
            _ => 1m
        };

        var millimeter = decimal.Round(zahl * faktor, 0, MidpointRounding.AwayFromZero);
        if (millimeter <= 0m || millimeter > MaxMillimeter)
            return null;

        return (int)millimeter;
    }
}
