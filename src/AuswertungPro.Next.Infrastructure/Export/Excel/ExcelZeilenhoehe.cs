using System;

namespace AuswertungPro.Next.Infrastructure.Export.Excel;

/// <summary>
/// Bestimmt die Hoehe einer Datenzeile aus den empfohlenen Massnahmen.
///
/// Warum ausgerechnet diese Spalte: Die Massnahmen muessen vollstaendig lesbar
/// sein, sie sagen dem Unternehmer, was zu tun ist. Die primaeren Schaeden waeren
/// dafuer untauglich - sie brauchen in echten Projekten bis zu 65 Zeilen und
/// wuerden die Tabelle unbrauchbar aufblaehen.
/// </summary>
public static class ExcelZeilenhoehe
{
    /// <summary>Mindesthoehe, damit auch leere Zeilen sauber aussehen.</summary>
    public const double Mindesthoehe = 22d;

    /// <summary>Hoehe einer Textzeile bei Schriftgroesse 9.</summary>
    public const double ProTextzeile = 12.5d;

    /// <summary>Zuschlag, damit die letzte Zeile nicht am Rand klebt.</summary>
    public const double Innenabstand = 4d;

    /// <summary>
    /// Obergrenze. Ein einzelner ueberlanger Eintrag darf keine Zeile erzeugen,
    /// die eine ganze Druckseite fuellt.
    /// </summary>
    public const double Hoechsthoehe = 200d;

    /// <param name="text">Inhalt der Massnahmen-Zelle, Zeilenumbrueche erlaubt.</param>
    /// <param name="spaltenbreite">Spaltenbreite in Excel-Einheiten (Zeichen).</param>
    public static double Berechne(string? text, double spaltenbreite)
    {
        if (spaltenbreite <= 0)
            spaltenbreite = 30d;

        if (string.IsNullOrWhiteSpace(text))
            return Mindesthoehe;

        var zeilen = 0;
        foreach (var teil in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var noetig = (int)Math.Ceiling(teil.Length / spaltenbreite);
            zeilen += Math.Max(1, noetig);
        }

        var hoehe = zeilen * ProTextzeile + Innenabstand;
        return Math.Min(Hoechsthoehe, Math.Max(Mindesthoehe, hoehe));
    }
}
