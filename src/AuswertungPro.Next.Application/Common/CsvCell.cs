using System;

namespace AuswertungPro.Next.Application.Common;

/// <summary>
/// Zentrale, sichere Aufbereitung einer CSV-Zelle (Gesamtaudit 2026-08-14, Prio 2).
///
/// Zwei getrennte Aufgaben:
///
/// 1. <b>Trennzeichen maskieren</b> — Semikolon/Komma, Anfuehrungszeichen und Umbrueche
///    duerfen die Spaltenstruktur nicht zerreissen. Das taten die bisherigen
///    Escape-Helfer schon.
///
/// 2. <b>Formeln entschaerfen</b> — das fehlte. Excel und LibreOffice fuehren eine Zelle
///    aus, die mit <c>=</c>, <c>+</c>, <c>-</c>, <c>@</c>, Tabulator oder Wagenruecklauf
///    beginnt. Unsere Exporte enthalten Freitext aus Kundenprotokollen (Befundtexte,
///    Bemerkungen, Standortnamen). Ein Text wie <c>=HYPERLINK(...)</c> aus einer
///    importierten Fremddatei wuerde beim Oeffnen der Tabelle wirken.
///    Deshalb wird ein einfaches Anfuehrungszeichen vorangestellt: Der Wert bleibt
///    sichtbar derselbe, wird aber als Text behandelt.
///
/// Negative Zahlen sind ausgenommen: <c>-12,5</c> soll eine Zahl bleiben und ist
/// keine Formel.
/// </summary>
public static class CsvCell
{
    /// <summary>Zeichen, mit denen eine Tabellenkalkulation eine Formel beginnt.</summary>
    private static readonly char[] FormulaStartChars = { '=', '+', '-', '@', '\t', '\r' };

    /// <summary>
    /// Bereitet einen Wert als CSV-Zelle auf: erst Formel entschaerfen, dann maskieren.
    /// <paramref name="separator"/> ist das verwendete Trennzeichen (Standard Semikolon).
    /// </summary>
    public static string Escape(string? value, char separator = ';')
    {
        var text = Neutralize(value);

        if (text.IndexOf(separator) >= 0
            || text.Contains('"')
            || text.Contains('\n')
            || text.Contains('\r'))
        {
            return $"\"{text.Replace("\"", "\"\"")}\"";
        }

        return text;
    }

    /// <summary>
    /// Entschaerft nur den Formelanfang, ohne zu maskieren. Fuer Aufrufer, die ihre
    /// eigene Maskierung schon besitzen.
    /// </summary>
    public static string Neutralize(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Length == 0)
            return text;

        if (Array.IndexOf(FormulaStartChars, text[0]) < 0)
            return text;

        // Eine negative Zahl ist keine Formel und soll als Zahl ankommen.
        if (text[0] == '-' && IstZahl(text))
            return text;

        return "'" + text;
    }

    private static bool IstZahl(string text)
    {
        // Bewusst kulturunabhaengig und eng: Vorzeichen, Ziffern, ein Trennzeichen.
        var trennerGesehen = false;
        for (var i = 1; i < text.Length; i++)
        {
            var c = text[i];
            if (char.IsAsciiDigit(c))
                continue;
            if ((c == ',' || c == '.') && !trennerGesehen)
            {
                trennerGesehen = true;
                continue;
            }

            return false;
        }

        return text.Length > 1;
    }
}
