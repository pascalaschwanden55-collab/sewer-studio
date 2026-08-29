using System.Globalization;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Protocol;

using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf;

/// <summary>
/// Reine Normalisierungs-Hilfsmethoden fuer den XTF/SIA405-Import.
/// Alle Methoden sind zustandslos und haben keine IO- oder Dokument-Abhaengigkeiten.
/// Extrahiert aus LegacyXtfImportService.
/// </summary>
internal static class XtfValueNormalizer
{
    /// <summary>
    /// Uebersetzt die Fliessrichtung der VSA-KEK-Untersuchung auf die Katalogwerte des Feldes
    /// "Inspektionsrichtung" ("In Fliessrichtung" / "Gegen Fliessrichtung").
    ///
    /// Unbekannte Schreibweisen ergeben bewusst einen leeren Wert: Ein Wert, den die Auswahlliste
    /// nicht kennt, waere im Programm unsichtbar — dann lieber ehrlich leer.
    /// Wichtig: "gegen" zuerst pruefen, sonst schluckt die "in"-Regel auch "gegen_Fliessrichtung".
    /// </summary>
    public static string NormalizeInspectionDirection(string? richtung)
    {
        if (string.IsNullOrWhiteSpace(richtung))
            return "";

        if (Regex.IsMatch(richtung, "(?i)gegen[_ ]?flie(?:ss|ß|s)richtung"))
            return "Gegen Fliessrichtung";
        if (Regex.IsMatch(richtung, "(?i)(^|[_ ])in[_ ]?flie(?:ss|ß|s)richtung"))
            return "In Fliessrichtung";

        return "";
    }

    /// <summary>
    /// Bildet SIA405-Materialbezeichnungen auf die Auswahlwerte des Feldes "Rohrmaterial" ab
    /// (<see cref="Domain.Models.FieldCatalog"/>).
    ///
    /// Warum so streng: Rohrmaterial ist ein Auswahlfeld. Ein Wert, der nicht in der Liste steht,
    /// wird im Programm als LEER angezeigt — die Daten sind da, man sieht sie nur nicht. Genau das
    /// passierte mit den frueheren Ausgaben "Kunststoff PVC"/"Kunststoff PE", die es in der
    /// Auswahlliste nie gab.
    ///
    /// Zwei Fallen stecken hier drin:
    /// 1. Das Kataster schreibt "Polyvin*i*lchlorid" (mit i). Die Regex muss beide Schreibweisen
    ///    treffen, sonst greift sie bei echten IKAS-Dateien nie.
    /// 2. Reihenfolge: "Hartpolyethylen" enthaelt "polyethylen" — der speziellere Fall zuerst.
    ///
    /// Unbekanntes wird lesbar durchgereicht statt verworfen: nicht waehlbar, aber nicht verloren.
    /// </summary>
    public static string NormalizeSiaMaterial(string material)
    {
        material ??= "";
        if (string.IsNullOrWhiteSpace(material)) return "";

        if (Regex.IsMatch(material, "(?i)hartpolyethylen|PE[-_ ]?HD")) return "Hartpolyethylen";
        if (Regex.IsMatch(material, "(?i)polyethylen")) return "Polyethylen";
        if (Regex.IsMatch(material, "(?i)polyvin[iy]lchlorid|(?:^|[_ ])PVC(?:[_ ]|$)")) return "Polyvinylchlorid";
        if (Regex.IsMatch(material, "(?i)polypropylen")) return "Polypropylen";
        if (Regex.IsMatch(material, "(?i)epoxydharz")) return "Epoxydharz";
        if (Regex.IsMatch(material, "(?i)glasfaser|(?:^|[_ ])GFK(?:[_ ]|$)")) return "GFK";
        // Asbestzement und Faserzement vor Zement: sonst schluckt die Zement-Regel
        // beide Sonderfaelle. Asbestzement ist ein eigener Werkstoff - im
        // AWU-Kantonsexport 247 Haltungen -, mit anderer Sanierung und anderem
        // Arbeitsschutz. "Zement" waere dort schlicht falsch.
        if (Regex.IsMatch(material, "(?i)asbestzement|(?:^|[_ ])AZ(?:[_ ]|$)")) return "Asbestzement";
        if (Regex.IsMatch(material, "(?i)faserzement")) return "Faserzement";
        if (Regex.IsMatch(material, "(?i)zement")) return "Zement";
        if (Regex.IsMatch(material, "(?i)beton")) return "Beton";
        if (Regex.IsMatch(material, "(?i)steinzeug")) return "Steinzeug";
        if (Regex.IsMatch(material, "(?i)guss")) return "Guss";
        if (Regex.IsMatch(material, "(?i)^ton$|(?:^|[_ ])Ton(?:[_ ]|$)")) return "Ton";

        material = material.Replace("_", " ").Trim();
        if (material.Length == 0) return "";
        return char.ToUpperInvariant(material[0]) + material[1..];
    }

    /// <summary>
    /// Normalisiert den SIA405-Nutzungsart-Wert auf den deutschen Bezeichner.
    /// </summary>
    public static string NormalizeNutzungsart(string v)
        => NutzungsartVokabular.Normalisieren(v);

    /// <summary>
    /// Wandelt ein Datum im Format yyyymmdd in dd.MM.yyyy um.
    /// Unbekannte Formate werden unveraendert zurueckgegeben (getrimmt).
    /// </summary>
    public static string NormalizeDate_yyyymmdd(string? yyyymmdd)
    {
        yyyymmdd ??= "";
        var m = Regex.Match(yyyymmdd.Trim(), @"^(\d{4})(\d{2})(\d{2})$");
        if (!m.Success) return yyyymmdd.Trim();
        return $"{m.Groups[3].Value}.{m.Groups[2].Value}.{m.Groups[1].Value}";
    }

    /// <summary>
    /// Parst einen Double-Wert aus einem String; unterstuetzt Komma und Punkt als Dezimaltrennzeichen.
    /// Faellt bei einfachem Parse-Fehler auf einen Regex-Extrakt-Versuch zurueck.
    /// </summary>
    public static bool TryParseDouble(string? s, out double value)
    {
        value = 0.0;
        if (string.IsNullOrWhiteSpace(s))
            return false;
        s = s.Trim().Replace(",", ".");
        if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
            return true;

        var match = Regex.Match(s, @"-?\d+(?:[.,]\d+)?");
        if (!match.Success)
            return false;

        var number = match.Value.Replace(",", ".");
        return double.TryParse(number, NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// Normalisiert einen VSA/SIA-Schadencode: Whitespace trimmen, Grossbuchstaben, Sonderzeichen entfernen.
    /// </summary>
    public static string NormalizeCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return string.Empty;
        return Regex.Replace(code.Trim().ToUpperInvariant(), @"[^A-Z0-9]", string.Empty);
    }

    /// <summary>
    /// Berechnet einen Aehnlichkeitsrang fuer zwei normalisierten Codes.
    /// 0 = exakt gleich, 1 = Praefix-Match, 2 = unterschiedlich.
    /// </summary>
    public static int GetCodeSimilarityRank(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return 2;
        if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
            return 0;
        if (left.StartsWith(right, StringComparison.OrdinalIgnoreCase)
            || right.StartsWith(left, StringComparison.OrdinalIgnoreCase))
            return 1;
        return 2;
    }

    /// <summary>
    /// Parst eine MPEG-Zeitangabe (z.B. "01:23:45" oder "23:45") in einen TimeSpan.
    /// Gibt null zurueck wenn das Format nicht erkannt wird.
    /// </summary>
    public static TimeSpan? ParseMpegTime(string? raw)
        => ProtocolTimeParser.ParseMpegTime(raw);
}
