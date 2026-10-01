using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Setzt die Rohrmaterial-Auswahl aus den festen Katalogwerten und den eigenen
/// Ergaenzungen zusammen.
///
/// Die festen Werte stammen aus dem Feldkatalog und duerfen nie verloren gehen:
/// Der XTF-Import normalisiert auf genau diese Schreibweisen
/// (<c>XtfValueNormalizer.NormalizeSiaMaterial</c>). Fehlt einer davon in der Liste,
/// zeigt das Feld einen importierten Wert leer an. Deshalb sind sie gesperrt und
/// wandern auch nie in die Datei der eigenen Werte.
/// </summary>
public static class PipeMaterialOptionList
{
    /// <summary>Die im Programm eingebauten, nicht loeschbaren Materialwerte.</summary>
    public static IReadOnlyList<string> FixedOptions
        => FieldCatalog.GetComboItems(FieldKeys.PipeMaterial);

    /// <summary>
    /// Anzeigeliste: feste Katalogwerte zuerst, danach die eigenen Ergaenzungen.
    /// Leere und doppelte Eintraege (ohne Ruecksicht auf Gross-/Kleinschreibung)
    /// fallen weg.
    /// </summary>
    public static List<string> Compose(IEnumerable<string>? customOptions)
    {
        var result = new List<string>(FixedOptions);
        var known = new HashSet<string>(
            result.Select(x => x.Trim()),
            StringComparer.OrdinalIgnoreCase);

        foreach (var custom in EnumerateUsable(customOptions))
        {
            if (known.Add(custom))
                result.Add(custom);
        }

        return result;
    }

    /// <summary>
    /// Filtert aus einer Anzeigeliste die eigenen Werte heraus. Nur diese werden
    /// gespeichert — die festen Katalogwerte kommen beim naechsten Start ohnehin
    /// wieder aus dem Feldkatalog.
    /// </summary>
    public static List<string> ExtractCustom(IEnumerable<string>? allOptions)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var value in EnumerateUsable(allOptions))
        {
            if (IsFixed(value))
                continue;
            if (seen.Add(value))
                result.Add(value);
        }

        return result;
    }

    /// <summary>Meldet, ob der Wert ein gesperrter Katalogwert ist.</summary>
    public static bool IsFixed(string? value)
    {
        var text = (value ?? string.Empty).Trim();
        if (text.Length == 0)
            return false;

        return FixedOptions.Any(x => string.Equals(x.Trim(), text, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> EnumerateUsable(IEnumerable<string>? values)
    {
        if (values is null)
            yield break;

        foreach (var value in values)
        {
            var text = (value ?? string.Empty).Trim();
            if (text.Length > 0)
                yield return text;
        }
    }
}
