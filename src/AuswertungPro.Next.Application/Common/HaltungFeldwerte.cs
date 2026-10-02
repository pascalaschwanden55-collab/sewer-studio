using System.Globalization;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Common;

/// <summary>Gelesenes Inspektionsdatum. <see cref="NurJahr"/>: Im Text stand nur ein Jahr (Datum = 1. Januar).</summary>
public readonly record struct Inspektionsdatum(DateTime Datum, bool NurJahr);

/// <summary>
/// Gemeinsame Leseregel fuer Projektfelder, die bisher jeder Leser selbst deutete
/// (Deepscan 02.10.2026, A4): das Inspektionsdatum (<see cref="FieldKeys.InspectionYear"/>,
/// "Datum_Jahr") und die Haltungslaenge (<see cref="FieldKeys.HoldingLengthMeters"/>).
/// Suche, Training, Dateistempel, Bewertung, Dossier und Dashboard lesen hierueber, damit
/// ein Feldwert ueberall dasselbe bedeutet. Gespeicherte Werte bleiben unveraendert;
/// die Importparser legen ihre Rohform weiter selbst fest.
/// </summary>
public static class HaltungFeldwerte
{
    // Ganze Feldwerte; Tag vor Monat (Schweizer Schreibweise), ISO und JJJJMMTT.
    private static readonly string[] ExakteFormate =
    {
        "dd.MM.yyyy", "d.M.yyyy", "dd.MM.yy", "d.M.yy",
        "dd/MM/yyyy", "d/M/yyyy", "dd/MM/yy", "d/M/yy",
        "dd-MM-yyyy", "d-M-yyyy", "dd-MM-yy", "d-M-yy",
        "yyyy-MM-dd", "yyyy/MM/dd", "yyyy.MM.dd", "yyyyMMdd"
    };

    // Datum irgendwo im Text, z.B. "Aufnahmen: 04.12.14 - 05.12.14" oder "05.03.2024 14:30".
    private static readonly Regex TagMonatJahr = new(
        @"\b(?<d>\d{1,2})[./-](?<m>\d{1,2})[./-](?<y>\d{4}|\d{2})(?!\d)", RegexOptions.CultureInvariant);

    // ISO-Datum im Text, auch mit angehaengter Uhrzeit ("2024-03-05T10:00:00").
    private static readonly Regex JahrMonatTag = new(
        @"\b(?<y>\d{4})[-/.](?<m>\d{1,2})[-/.](?<d>\d{1,2})(?!\d)", RegexOptions.CultureInvariant);

    // Eingebettetes JJJJMMTT, z.B. im Dateinamen "20251110_9866-9327.pdf"; nie Teil einer laengeren Ziffernfolge.
    private static readonly Regex KompaktesDatum = new(
        @"(?<!\d)(?<y>\d{4})(?<m>\d{2})(?<d>\d{2})(?!\d)", RegexOptions.CultureInvariant);

    private static readonly Regex Jahreszahl = new(@"\b(?<y>19\d{2}|20\d{2})\b", RegexOptions.CultureInvariant);

    /// <summary>Liest das Inspektionsdatum einer Haltung aus <see cref="FieldKeys.InspectionYear"/>.</summary>
    public static DateTime? LiesInspektionsdatum(HaltungRecord record)
        => LiesInspektionsdatum(record?.GetFieldValue(FieldKeys.InspectionYear));

    /// <summary>Liest ein Inspektionsdatum aus Text; ein reines Jahr ergibt den 1. Januar.</summary>
    public static DateTime? LiesInspektionsdatum(string? roh)
        => LiesInspektionsdatumGenau(roh)?.Datum;

    /// <summary>
    /// Liest ein Inspektionsdatum und meldet, ob nur ein Jahr bekannt war.
    /// Reihenfolge: ganzer Wert in einem festen Format, Tag.Monat.Jahr im Text,
    /// Jahr-Monat-Tag im Text, eingebettetes JJJJMMTT (Jahr 1990-2099), zuletzt eine
    /// alleinstehende Jahreszahl. Zweistellige Jahre folgen der festen Kalenderregel
    /// (bis 49 = 20xx, ab 50 = 19xx), unabhaengig von der Windows-Kultur.
    /// </summary>
    public static Inspektionsdatum? LiesInspektionsdatumGenau(string? roh)
    {
        if (string.IsNullOrWhiteSpace(roh))
            return null;

        var text = roh.Trim();
        if (DateTime.TryParseExact(text, ExakteFormate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exakt))
            return new Inspektionsdatum(exakt.Date, NurJahr: false);

        var tagZuerst = TagMonatJahr.Match(text);
        if (tagZuerst.Success && TryBaue(tagZuerst, out var datum))
            return new Inspektionsdatum(datum, NurJahr: false);

        var iso = JahrMonatTag.Match(text);
        if (iso.Success && TryBaue(iso, out datum))
            return new Inspektionsdatum(datum, NurJahr: false);

        foreach (Match kompakt in KompaktesDatum.Matches(text))
        {
            var jahr = int.Parse(kompakt.Groups["y"].Value, CultureInfo.InvariantCulture);
            if (jahr is >= 1990 and <= 2099 && TryBaue(kompakt, out datum))
                return new Inspektionsdatum(datum, NurJahr: false);
        }

        var nurJahr = Jahreszahl.Match(text);
        return nurJahr.Success
            ? new Inspektionsdatum(new DateTime(int.Parse(nurJahr.Groups["y"].Value, CultureInfo.InvariantCulture), 1, 1), NurJahr: true)
            : null;
    }

    /// <summary>Liest die Haltungslaenge in Metern aus <see cref="FieldKeys.HoldingLengthMeters"/>.</summary>
    public static double? LiesLaenge(HaltungRecord record)
        => LiesLaenge(record?.GetFieldValue(FieldKeys.HoldingLengthMeters));

    /// <summary>
    /// Liest eine Laenge in Metern nach der Regel von <see cref="FachzahlParser.TryParseMeasurement"/>:
    /// Punkt oder Komma als Dezimalzeichen, Apostroph-Tausender erlaubt, mehrdeutige Werte und
    /// Einheiten ("45 m") abgelehnt. Null, wenn nichts Lesbares dasteht. Ob 0 oder negative
    /// Werte gelten, entscheidet der Aufrufer.
    /// </summary>
    public static double? LiesLaenge(string? roh)
        => FachzahlParser.TryParseMeasurement(roh, out var laenge) ? (double)laenge : null;

    private static bool TryBaue(Match treffer, out DateTime datum)
    {
        datum = default;
        var jahr = int.Parse(treffer.Groups["y"].Value, CultureInfo.InvariantCulture);
        if (treffer.Groups["y"].Value.Length == 2)
            jahr = CultureInfo.InvariantCulture.Calendar.ToFourDigitYear(jahr);
        var monat = int.Parse(treffer.Groups["m"].Value, CultureInfo.InvariantCulture);
        var tag = int.Parse(treffer.Groups["d"].Value, CultureInfo.InvariantCulture);
        if (jahr is < 1 or > 9999 || monat is < 1 or > 12 || tag < 1 || tag > DateTime.DaysInMonth(jahr, monat))
            return false;

        datum = new DateTime(jahr, monat, tag);
        return true;
    }
}
