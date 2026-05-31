using System.Globalization;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Import;

/// <summary>
/// Fachliche Plausibilitaetspruefung nach einem Import. Erzeugt Warnungen
/// (kein harter Fehler), damit offensichtlich falsche Werte nicht unbemerkt
/// bis in den Export durchlaufen. Reine Logik, ohne I/O.
/// </summary>
public static class ImportPlausibilityValidator
{
    public const int MinDn = 50;
    public const int MaxDn = 4000;

    /// <summary>Toleranz, um die eine Beobachtung hinter der Haltungslaenge liegen darf (m).</summary>
    public const double MeterTolerance = 1.0;

    public static IReadOnlyList<string> Validate(Project project)
    {
        var warnings = new List<string>();
        foreach (var record in project.Data)
            warnings.AddRange(Validate(record));
        return warnings;
    }

    public static IReadOnlyList<string> Validate(HaltungRecord record)
    {
        var warnings = new List<string>();
        var name = record.GetFieldValue("Haltungsname");
        var label = string.IsNullOrWhiteSpace(name) ? "(ohne Name)" : name;

        // DN-Bereich
        if (TryParseInt(record.GetFieldValue("DN_mm"), out var dn) && dn > 0
            && (dn < MinDn || dn > MaxDn))
        {
            warnings.Add($"{label}: DN {dn} mm ausserhalb plausibler Spanne ({MinDn}-{MaxDn} mm).");
        }

        // Meterstand darf nicht (deutlich) hinter der Haltungslaenge liegen
        if (TryParseDouble(record.GetFieldValue("Haltungslaenge_m"), out var length) && length > 0)
        {
            var entries = record.Protocol?.Current?.Entries;
            if (entries is not null)
            {
                foreach (var e in entries)
                {
                    if (e.IsDeleted || e.MeterStart is null)
                        continue;
                    var start = e.MeterStart.Value;
                    var maxMeter = Math.Max(start, e.MeterEnd ?? start);
                    if (maxMeter > length + MeterTolerance)
                        warnings.Add(
                            $"{label}: Beobachtung bei {maxMeter.ToString("0.##", CultureInfo.InvariantCulture)} m " +
                            $"liegt hinter der Haltungslaenge {length.ToString("0.##", CultureInfo.InvariantCulture)} m" +
                            (string.IsNullOrWhiteSpace(e.Code) ? "." : $" (Code {e.Code})."));
                }
            }
        }

        return warnings;
    }

    private static bool TryParseInt(string? value, out int result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(value))
            return false;
        var cleaned = value.Trim();
        return int.TryParse(cleaned, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);
    }

    private static bool TryParseDouble(string? value, out double result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(value))
            return false;
        // Komma- und Punkt-Dezimaltrenner akzeptieren (de-CH vs invariant).
        var cleaned = value.Trim().Replace(',', '.');
        return double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }
}
