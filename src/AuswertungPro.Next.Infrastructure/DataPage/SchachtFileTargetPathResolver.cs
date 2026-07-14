using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.DataPage;

public sealed class SchachtFileTargetPathResolver : ISchachtFileTargetResolver
{
    public string? ResolvePdfPath(SchachtRecord record, string? projectFilePath)
    {
        ArgumentNullException.ThrowIfNull(record);

        var pdfPath = ResolvePdfCandidate(record.GetFieldValue(FieldKeys.PdfPath), projectFilePath);
        if (!string.IsNullOrWhiteSpace(pdfPath))
            return pdfPath;

        var link = record.GetFieldValue(FieldKeys.Link);
        if (string.IsNullOrWhiteSpace(link)
            || !link.Trim().EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return ResolvePdfCandidate(link, projectFilePath);
    }

    public string? ResolveExplorerTarget(SchachtRecord record, string? projectFilePath)
    {
        ArgumentNullException.ThrowIfNull(record);

        var pdfPath = ResolvePdfPath(record, projectFilePath);
        if (!string.IsNullOrWhiteSpace(pdfPath))
            return pdfPath;

        foreach (var raw in EnumerateExplorerPathCandidates(record))
        {
            var resolved = ResolveExistingPath(raw, projectFilePath);
            if (!string.IsNullOrWhiteSpace(resolved))
                return resolved;
        }

        return null;
    }

    private static string? ResolvePdfCandidate(string? raw, string? projectFilePath)
    {
        var resolved = ResolveExistingPath(raw, projectFilePath);
        if (string.IsNullOrWhiteSpace(resolved))
            return null;

        return resolved.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? resolved : null;
    }

    private static IEnumerable<string> EnumerateExplorerPathCandidates(SchachtRecord record)
    {
        foreach (var field in new[] { FieldKeys.PdfPath, FieldKeys.PdfAll, FieldKeys.PdfEigen, FieldKeys.Link })
        {
            var raw = record.GetFieldValue(field);
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            foreach (var part in raw.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = part.Trim();
                if (!string.IsNullOrWhiteSpace(trimmed))
                    yield return trimmed;
            }
        }
    }

    private static string? ResolveExistingPath(string? raw, string? projectFilePath)
    {
        var path = raw?.Trim();
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (File.Exists(path))
            return Path.GetFullPath(path);
        if (Directory.Exists(path))
            return Path.GetFullPath(path);

        if (Path.IsPathRooted(path) || string.IsNullOrWhiteSpace(projectFilePath))
            return null;

        var baseDirectory = ProjectFileLocator.ProjectRootFromFile(projectFilePath);
        if (string.IsNullOrWhiteSpace(baseDirectory))
            return null;

        var combined = Path.GetFullPath(Path.Combine(baseDirectory, path));
        if (File.Exists(combined))
            return combined;
        if (Directory.Exists(combined))
            return combined;

        return null;
    }
}
