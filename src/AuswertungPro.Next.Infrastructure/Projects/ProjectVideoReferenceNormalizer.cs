using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Media;

namespace AuswertungPro.Next.Infrastructure.Projects;

/// <summary>
/// Haelt Videoverknuepfungen portabel. Absolute Altpfade innerhalb des
/// Projektordners werden relativ gespeichert; externe Quellen bleiben erhalten.
/// </summary>
internal static class ProjectVideoReferenceNormalizer
{
    private static readonly string[] VideoFields = [FieldKeys.Link, "Link_G"];

    public static int Normalize(Project? project, string? projectFilePath)
    {
        if (project is null || string.IsNullOrWhiteSpace(projectFilePath))
            return 0;

        var projectRoot = ProjectFileLocator.ProjectRootFromFile(projectFilePath)
                          ?? Path.GetDirectoryName(projectFilePath);
        if (string.IsNullOrWhiteSpace(projectRoot))
            return 0;

        var changed = 0;
        foreach (var record in project.Data)
            changed += NormalizeRecord(record, projectRoot);

        if (changed > 0)
            project.Dirty = true;

        return changed;
    }

    private static int NormalizeRecord(HaltungRecord record, string projectRoot)
    {
        var changed = 0;
        foreach (var field in VideoFields)
        {
            var raw = record.GetFieldValue(field)?.Trim();
            if (string.IsNullOrWhiteSpace(raw)
                || !Path.IsPathRooted(raw)
                || !MediaFileTypes.HasVideoExtension(raw))
                continue;

            var normalized = ProjectPathResolver.MakeRelativeIfInsideProject(raw, projectRoot);
            if (Path.IsPathRooted(normalized)
                || string.Equals(raw, normalized, StringComparison.OrdinalIgnoreCase))
                continue;

            // Nur die Darstellung des Pfades aendern. Herkunft und UserEdited-Schutz bleiben erhalten.
            record.Fields[field] = normalized;
            changed++;
        }

        return changed;
    }
}
