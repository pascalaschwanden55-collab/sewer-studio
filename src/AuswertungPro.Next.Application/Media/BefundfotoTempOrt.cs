using System.IO;

namespace AuswertungPro.Next.Application.Media;

/// <summary>
/// Die eine Regel fuer «liegt im Windows-Temp-Ordner» (Deepscan 02.10.2026, R3).
/// Befundfotos gehoeren nie dorthin: Windows darf den Ordner jederzeit leeren
/// (Fall 12.09.2026, 16 verlorene Fotos). Aufnahmewege und Projektpruefung fragen
/// diese Stelle, statt den Temp-Pfad je selbst zu vergleichen. Reine Pfadrechnung,
/// ohne Dateizugriff.
/// </summary>
public static class BefundfotoTempOrt
{
    /// <summary>Temp-Ordner dieses Benutzers: <c>Path.GetTempPath()</c> und <c>%LOCALAPPDATA%\Temp</c>.</summary>
    public static IReadOnlyList<string> Standardwurzeln()
    {
        var wurzeln = new List<string> { Path.GetTempPath() };
        var lokal = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(lokal))
            wurzeln.Add(Path.Combine(lokal, "Temp"));
        return wurzeln;
    }

    public static bool LiegtImTemp(string? pfad)
        => LiegtImTemp(pfad, Standardwurzeln());

    /// <summary>
    /// Wahr, wenn <paramref name="pfad"/> ein vollstaendiger Pfad im oder unter einer der
    /// <paramref name="wurzeln"/> ist. Relative oder ungueltige Pfade gelten als nicht im Temp.
    /// </summary>
    public static bool LiegtImTemp(string? pfad, IEnumerable<string> wurzeln)
    {
        ArgumentNullException.ThrowIfNull(wurzeln);
        if (string.IsNullOrWhiteSpace(pfad))
            return false;

        try
        {
            var kandidat = pfad.Trim();
            if (!Path.IsPathFullyQualified(kandidat))
                return false;

            var voll = MitTrenner(Path.GetFullPath(kandidat));
            foreach (var wurzel in wurzeln)
            {
                if (string.IsNullOrWhiteSpace(wurzel) || !Path.IsPathFullyQualified(wurzel))
                    continue;

                if (voll.StartsWith(MitTrenner(Path.GetFullPath(wurzel)), StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // Ein unlesbarer Pfad ist ein Fall fuer die Dateipruefung, nicht fuer diese Regel.
            return false;
        }
    }

    private static string MitTrenner(string pfad)
        => Path.TrimEndingDirectorySeparator(pfad) + Path.DirectorySeparatorChar;
}
