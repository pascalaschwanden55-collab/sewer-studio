using System;
using System.IO;
using AuswertungPro.Next.Infrastructure.Import;

namespace AuswertungPro.Next.Infrastructure.WebGis;

/// <summary>
/// Ablage der WebGIS-Berichte und des Logs fuer Senden UND Holen: &lt;Projektordner&gt;\__WebGIS_Export. Berichte und
/// Log sind Beilage — ein Schreibfehler bricht keinen Lauf ab, er liefert null bzw. schreibt nichts.
/// Jedes Schreibziel geht durch die Schreibgrenze des Projekts (Pruefung 22.09.2026, C3): Ein Ordner oder eine
/// Datei, die eine Verknuepfung ist, wird nicht beschrieben — sonst landeten Berichte ausserhalb des Projekts.
/// </summary>
public static class WebGisBerichtAblage
{
    public const string Ordnername = "__WebGIS_Export";
    public const string LogDatei = "WebGIS_Log.txt";

    /// <summary>
    /// Der Ablageordner zum Projektpfad (Datei oder Ordner; «Projektdateien» zaehlt als Projektordner), angelegt.
    /// Null ohne Projektpfad oder wenn der Ordner nicht sicher im Projekt liegt.
    /// </summary>
    public static string? Ordner(string? projektPfad)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(projektPfad)) return null;
            var wurzel = Directory.Exists(projektPfad) ? projektPfad : Path.GetDirectoryName(projektPfad);
            if (string.IsNullOrWhiteSpace(wurzel)) return null;
            if (string.Equals(Path.GetFileName(wurzel), "Projektdateien", StringComparison.OrdinalIgnoreCase))
                wurzel = Path.GetDirectoryName(wurzel) ?? wurzel;
            var ordner = new ProjectWritePathGuard(wurzel).EnsureSafeDirectoryTarget(Path.Combine(wurzel, Ordnername));
            Directory.CreateDirectory(ordner);
            return ordner;
        }
        catch (Exception)
        {
            return null; // Bericht ist Beilage; der Lauf haengt nicht daran.
        }
    }

    /// <summary>Schreibt WebGIS_&lt;Art&gt;_&lt;Zeit&gt;.txt; liefert den Pfad oder null.</summary>
    public static string? SchreibeBericht(string? ordner, string art, string text)
    {
        try
        {
            if (ordner is null) return null;
            var datei = Sicher(ordner, $"WebGIS_{art}_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            File.WriteAllText(datei, text);
            return datei;
        }
        catch (Exception)
        {
            return null; // Bericht ist Beilage; der Lauf haengt nicht daran.
        }
    }

    /// <summary>Haengt eine Zeile an WebGIS_Log.txt an.</summary>
    public static void HaengeAnLog(string? ordner, string zeile)
    {
        if (ordner is null) return;
        try
        {
            File.AppendAllText(Sicher(ordner, LogDatei), zeile + Environment.NewLine);
        }
        catch (Exception)
        {
            // Das Log ist Beilage; ein Schreibfehler darf den laufenden Katasterlauf nicht abbrechen.
        }
    }

    /// <summary>Die Datei im Ablageordner, geprueft gegen die Schreibgrenze des Projekts (Ordner eingeschlossen).</summary>
    private static string Sicher(string ordner, string dateiname)
    {
        var projekt = Path.GetDirectoryName(Path.GetFullPath(ordner))
            ?? throw new InvalidOperationException("Ablageordner ohne Projektordner.");
        return new ProjectWritePathGuard(projekt).EnsureSafeFileTarget(Path.Combine(ordner, dateiname));
    }
}
