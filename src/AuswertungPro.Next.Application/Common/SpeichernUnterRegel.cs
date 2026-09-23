using System;
using System.IO;

namespace AuswertungPro.Next.Application.Common;

/// <summary>
/// «Speichern unter» nur im selben Projektordner (Audit A08/A02, Entscheid 23.09.2026). Videos, Fotos, PDFs,
/// Kosten (costs\) und Dossiers (Dossiers\) liegen im Projektordner und werden relativ dazu gefunden. Eine
/// Projektdatei in einem anderen Ordner fand keines davon mehr; Kosten und Dossiers erschienen leer, und wer
/// danach zurueckwechselte, verlor die inzwischen gemachte Arbeit. Eine Kopie des Projekts ist eine Kopie des
/// ganzen Ordners — das kann der Explorer vollstaendig, eine halbe Kopie waere schlimmer.
/// </summary>
public static class SpeichernUnterRegel
{
    /// <summary>Null, wenn erlaubt; sonst der Grund fuer den Bearbeiter.</summary>
    /// <param name="bisherigeProjektdatei">Die gespeicherte Projektdatei; null bei einem nie gespeicherten Projekt.</param>
    public static string? Sperrgrund(string? bisherigeProjektdatei, string neueProjektdatei)
    {
        if (string.IsNullOrWhiteSpace(bisherigeProjektdatei)) return null;
        var alt = Ordner(ProjectFileLocator.ProjectRootFromFile(bisherigeProjektdatei));
        var neu = Ordner(ProjectFileLocator.ProjectRootFromFile(neueProjektdatei));
        if (alt is null || neu is null || string.Equals(alt, neu, StringComparison.OrdinalIgnoreCase)) return null;

        return $"«Speichern unter» ist nur innerhalb des Projektordners «{alt}» möglich.\n\n"
               + "Videos, Fotos, PDFs, Kosten und Dossiers liegen in diesem Ordner. Am neuen Ort würden sie fehlen, "
               + "und Änderungen im neuen Projekt kämen im alten nicht an.\n\n"
               + "Für eine Kopie des Projekts den ganzen Projektordner im Explorer kopieren und danach die Kopie öffnen.";
    }

    private static string? Ordner(string? pfad)
    {
        if (string.IsNullOrWhiteSpace(pfad)) return null;
        try { return Path.TrimEndingDirectorySeparator(Path.GetFullPath(pfad)); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return pfad; }
    }
}
