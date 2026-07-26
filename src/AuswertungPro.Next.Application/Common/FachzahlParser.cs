using System.Globalization;

namespace AuswertungPro.Next.Application.Common;

/// <summary>
/// Kulturunabhaengiger Parser fuer fachliche Zahlen (Laengen, Mengen, Preise) aus
/// Nutzereingaben und Katalogtexten. Dezimaltrennzeichen ist "." oder ",",
/// Tausendertrennzeichen das gerade (') oder typografische (U+2019) Apostroph.
/// Mehrdeutige Eingaben (z.B. "1.300" — 1300 oder 1.3?) werden ABGELEHNT statt
/// geraten; das Ergebnis ist damit auf jeder Windows-Kultur (de-DE/de-CH/en-US)
/// identisch. Hintergrund: decimal.TryParse mit CurrentCulture liest "45.30"
/// unter de-DE still als 4530 (Faktor-100-Falle im Kostenbereich).
/// </summary>
public static class FachzahlParser
{
    /// <summary>Parst eine Fachzahl; false bei leerer, ungueltiger oder mehrdeutiger Eingabe.</summary>
    public static bool TryParseDecimal(string? raw, out decimal value)
    {
        value = 0m;
        if (!TryNormalizeToInvariant(raw, allowThreeDecimalPlaces: false, out var normalized))
            return false;

        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// Parst Laengen und Mengen. In diesem fachlich bekannten Messwert-Kontext sind
    /// drei Dezimalstellen erlaubt; Tausendergruppen bleiben nur in eindeutig
    /// gruppierter Form (Apostroph/Leerzeichen oder vollstaendige Gruppen) zulaessig.
    /// </summary>
    public static bool TryParseMeasurement(string? raw, out decimal value)
    {
        value = 0m;
        if (!TryNormalizeToInvariant(raw, allowThreeDecimalPlaces: true, out var normalized))
            return false;

        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    // Bringt die Eingabe in die Invariant-Form (optionales '-', Ziffern, hoechstens ein '.').
    private static bool TryNormalizeToInvariant(
        string? raw,
        bool allowThreeDecimalPlaces,
        out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var text = raw.Trim();

        var negative = text[0] == '-';
        if (negative || text[0] == '+')
            text = text[1..].TrimStart();

        // Schweizer Tausendergruppen zuerst pruefen. Blindes Entfernen wuerde
        // Tippfehler wie "45'30" still zu 4530 machen.
        text = text
            .Replace('\u2019', '\'')
            .Replace('\u00A0', ' ');
        if (!TryStripSwissGrouping(text, out text))
            return false;
        if (text.Length == 0)
            return false;

        var dotCount = text.Count(c => c == '.');
        var commaCount = text.Count(c => c == ',');

        string integerDigits;
        string? fractionDigits;

        if (dotCount + commaCount == 0)
        {
            integerDigits = text;
            fractionDigits = null;
        }
        else if (dotCount + commaCount == 1)
        {
            // Genau ein Trennzeichen: Dezimalpunkt — ausser bei exakt drei
            // Nachkommastellen; das waere ebenso ein Tausenderpunkt ("1.300")
            // und damit mehrdeutig.
            var sepIndex = text.IndexOfAny('.', ',');
            var integerPart = text[..sepIndex];
            var fraction = text[(sepIndex + 1)..];
            // "1.300" bleibt mehrdeutig (Tausender oder 1,3). Bei einem
            // Ganzzahlteil nur aus Nullen ist die Tausenderdeutung dagegen
            // unmoeglich: "0,155" ist eindeutig der Bruch 0,155.
            if (!allowThreeDecimalPlaces
                && fraction.Length == 3
                && integerPart.Any(c => c != '0'))
                return false;
            integerDigits = integerPart;
            fractionDigits = fraction;
        }
        else if (dotCount > 1 && commaCount > 1)
        {
            return false;   // mehrere Trennzeichen beider Arten: keine sichere Deutung
        }
        else if (dotCount == 0 || commaCount == 0)
        {
            // Mehrere gleiche Trennzeichen: nur als Tausendergruppen deutbar ("1.300.500").
            var groupSep = dotCount > 1 ? '.' : ',';
            if (!TryStripGrouping(text, groupSep, out integerDigits))
                return false;
            fractionDigits = null;
        }
        else
        {
            // Genau ein Trennzeichen der einen Art, eines oder mehrere der anderen:
            // das letzte Trennzeichen ist der Dezimalpunkt, die anderen Tausenderpunkte.
            var lastDot = text.LastIndexOf('.');
            var lastComma = text.LastIndexOf(',');
            var sepIndex = Math.Max(lastDot, lastComma);
            var decimalSep = text[sepIndex];
            var groupSep = decimalSep == '.' ? ',' : '.';
            if (text.IndexOf(decimalSep) != sepIndex)
                return false;   // Dezimaltrennzeichen kommt mehrfach vor
            var fraction = text[(sepIndex + 1)..];
            if (fraction.Contains(groupSep))
                return false;   // Tausenderpunkt hinter dem Dezimalpunkt
            if (!TryStripGrouping(text[..sepIndex], groupSep, out integerDigits))
                return false;
            fractionDigits = fraction;
        }

        if (!IsDigits(integerDigits) || (fractionDigits is not null && !IsDigits(fractionDigits)))
            return false;

        normalized = (negative ? "-" : "") + integerDigits +
                     (fractionDigits is null ? "" : "." + fractionDigits);
        return true;
    }

    private static bool TryStripSwissGrouping(string text, out string normalized)
    {
        normalized = string.Empty;
        var decimalIndex = text.IndexOfAny('.', ',');
        var integerPart = decimalIndex < 0 ? text : text[..decimalIndex];
        var suffix = decimalIndex < 0 ? string.Empty : text[decimalIndex..];

        // Gruppierung ist nur im Ganzzahlteil erlaubt.
        if (suffix.Contains('\'') || suffix.Contains(' '))
            return false;

        var hasApostrophe = integerPart.Contains('\'');
        var hasSpace = integerPart.Contains(' ');
        if (hasApostrophe && hasSpace)
            return false;

        if (!hasApostrophe && !hasSpace)
        {
            normalized = text;
            return true;
        }

        var separator = hasApostrophe ? '\'' : ' ';
        if (!TryStripGrouping(integerPart, separator, out var digits))
            return false;

        normalized = digits + suffix;
        return true;
    }

    // Prueft Tausendergruppen (erste Gruppe 1-3 Ziffern, danach exakt 3) und entfernt sie.
    private static bool TryStripGrouping(string text, char groupSep, out string digits)
    {
        digits = string.Empty;
        var groups = text.Split(groupSep);
        if (groups[0].Length is < 1 or > 3 || !IsDigits(groups[0]))
            return false;

        for (var i = 1; i < groups.Length; i++)
        {
            if (groups[i].Length != 3 || !IsDigits(groups[i]))
                return false;
        }

        digits = string.Concat(groups);
        return true;
    }

    private static bool IsDigits(string value)
    {
        if (value.Length == 0)
            return false;
        foreach (var c in value)
        {
            if (c is < '0' or > '9')
                return false;
        }
        return true;
    }
}
