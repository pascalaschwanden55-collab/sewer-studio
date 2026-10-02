using System.Collections.Generic;
using System.IO;
using System.Security;

namespace AuswertungPro.Next.Application.Common;

/// <summary>
/// Macht Ordner sichtbar, die <see cref="SafeFileEnumeration"/> uebersprungen hat.
///
/// Anlass (Deepscan 02.10.2026, R1): Die sichere Dateisuche laesst unlesbare Ordner und
/// Verknuepfungen bewusst aus, meldete das aber nur, wenn der Aufrufer eine Liste mitgab.
/// Ein Import oder eine Verteilung konnte so «0 Fehler» melden, obwohl Protokolle oder
/// Videos in einem gesperrten Unterordner fehlten. Dieser Baustein liefert fuer jeden
/// uebersprungenen Ordner eine einheitliche Berichtszeile. Er betritt selbst keine
/// Verknuepfung; der Pfadschutz der Dateisuche bleibt unveraendert.
/// </summary>
public static class UebersprungeneOrdner
{
    /// <summary>
    /// Durchlaeuft den Ordnerbaum unter <paramref name="wurzel"/> mit derselben Grenze wie
    /// die Dateisuche und liefert je uebersprungenem Ordner eine Berichtszeile.
    /// Eine fehlende Wurzel liefert keine Zeile; das meldet der Aufrufer selbst.
    /// </summary>
    public static IReadOnlyList<string> PruefeBaum(string? wurzel)
        => Meldungen(SammleBaum(wurzel));

    /// <summary>Die uebersprungenen Ordner unter <paramref name="wurzel"/> als Pfade.</summary>
    public static IReadOnlyList<string> SammleBaum(string? wurzel)
    {
        if (string.IsNullOrWhiteSpace(wurzel) || !Directory.Exists(wurzel))
            return [];

        var uebersprungen = new List<string>();
        foreach (var _ in SafeFileEnumeration.EnumerateDirectoriesSafe(wurzel, uebersprungen))
        {
            // Nur durchlaufen: gesucht sind die uebersprungenen Ordner, nicht die gefundenen.
        }

        return uebersprungen;
    }

    /// <summary>Berichtszeilen fuer die von der Dateisuche gesammelten Pfade, ohne Doppel.</summary>
    public static IReadOnlyList<string> Meldungen(IEnumerable<string>? pfade)
        => pfade is null
            ? []
            : pfade
                .Where(pfad => !string.IsNullOrWhiteSpace(pfad))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(pfad => pfad, StringComparer.OrdinalIgnoreCase)
                .Select(Meldung)
                .ToList();

    /// <summary>Eine Berichtszeile: «Ordner «…» übersprungen: nicht lesbar» bzw. «…: Verknüpfung …».</summary>
    public static string Meldung(string pfad)
        => $"Ordner «{pfad}» übersprungen: {Grund(pfad)}";

    private static string Grund(string pfad)
    {
        try
        {
            // Die Attribute der Verknuepfung selbst; ihr Ziel wird nicht gelesen.
            if ((File.GetAttributes(pfad) & FileAttributes.ReparsePoint) != 0)
                return "Verknüpfung, wird nicht betreten";
        }
        catch (Exception ex) when (ex is IOException
                                       or UnauthorizedAccessException
                                       or SecurityException
                                       or ArgumentException
                                       or NotSupportedException)
        {
            // Nicht einmal die Attribute sind lesbar: der Grund bleibt «nicht lesbar».
        }

        return "nicht lesbar";
    }
}
