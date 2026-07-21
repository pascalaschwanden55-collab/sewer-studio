using System.IO;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Services;

internal sealed record ProjectOperationContext(
    Project Project,
    string? ProjectPath);

[Flags]
internal enum ProjectOperationImpact
{
    None = 0,
    ProjectFilesWritten = 1,
    ProjectDataChanged = 2
}

internal delegate bool ProjectOperationCheck(
    ProjectOperationContext projectContext,
    string dialogTitle,
    ProjectOperationImpact impact);

/// <summary>
/// Prueft, ob ein laenger laufender UI-Vorgang noch zum gleichen Projekt gehoert.
/// Ein gleicher Ordner allein reicht nicht: Ein neu geladenes Projekt kann am
/// selben Ort liegen, besitzt aber andere Datensatz-Instanzen.
/// </summary>
internal static class ActiveProjectGuard
{
    internal static bool IsCurrent(
        ProjectOperationContext expected,
        Project currentProject,
        string? currentProjectPath)
        => ReferenceEquals(expected.Project, currentProject)
           && string.Equals(
               NormalizeLocation(expected.ProjectPath),
               NormalizeLocation(currentProjectPath),
               StringComparison.OrdinalIgnoreCase);

    private static string? NormalizeLocation(string? location)
    {
        if (string.IsNullOrWhiteSpace(location))
            return null;

        var trimmed = location.Trim();
        try
        {
            return Path.GetFullPath(trimmed)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return trimmed.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }
}
