using System;
using System.IO;

namespace AuswertungPro.Next.Application.Common;

/// <summary>Ergebnis der Zielordner-Planung fuer ein neues Projekt.</summary>
public sealed record NewProjectFolderPlan(string FolderPath, string ProjectFilePath);

/// <summary>
/// Berechnet aus Basisverzeichnis + Projektname den Projektordner und den
/// projekt.json-Pfad. Pure (kein Dateisystem-Zugriff): die Existenzpruefung
/// kommt als Delegate, damit die Logik unit-testbar bleibt.
/// </summary>
public static class NewProjectFolderPlanner
{
    public const string ProjectFileName = "projekt.json";

    public static NewProjectFolderPlan Plan(
        string baseDirectory,
        string projectName,
        Func<string, bool> directoryExists)
    {
        ArgumentNullException.ThrowIfNull(directoryExists);

        var safeName = ProjectPathResolver.SanitizePathSegment(projectName);
        var candidate = Path.Combine(baseDirectory, safeName);

        // Kollision: -2, -3, ... bis ein freier Ordnername gefunden ist.
        var counter = 2;
        while (directoryExists(candidate))
        {
            candidate = Path.Combine(baseDirectory, $"{safeName}-{counter}");
            counter++;
        }

        return new NewProjectFolderPlan(candidate, Path.Combine(candidate, ProjectFileName));
    }
}
