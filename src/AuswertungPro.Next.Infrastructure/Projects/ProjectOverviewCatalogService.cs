using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Projects;

namespace AuswertungPro.Next.Infrastructure.Projects;

/// <summary>
/// Liest die Kopfdaten gespeicherter Projekte. Eine defekte Datei wird als
/// fehlerhafter Eintrag gemeldet und blockiert die restliche Liste nicht.
/// </summary>
public sealed class ProjectOverviewCatalogService : IProjectOverviewCatalog
{
    private readonly IProjectFileDiscovery _projectFileDiscovery;

    public ProjectOverviewCatalogService(IProjectFileDiscovery projectFileDiscovery)
    {
        _projectFileDiscovery = projectFileDiscovery
            ?? throw new ArgumentNullException(nameof(projectFileDiscovery));
    }

    public IReadOnlyList<ProjectOverviewDescriptor> Load(ProjectOverviewCatalogRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entries = new List<ProjectOverviewDescriptor>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hidden = new HashSet<string>(
            request.HiddenProjectPaths ?? Array.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);

        void AddEntry(string? file, bool isLastProject)
        {
            if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
                return;
            if (hidden.Contains(file) || !seen.Add(file))
                return;

            try
            {
                entries.Add(ReadEntry(file, isLastProject));
            }
            catch
            {
                entries.Add(BuildCorruptEntry(file, isLastProject));
            }
        }

        AddEntry(request.LastProjectPath, isLastProject: true);

        foreach (var recentPath in request.RecentProjectPaths ?? Array.Empty<string>())
        {
            AddEntry(
                recentPath,
                string.Equals(recentPath, request.LastProjectPath, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var file in _projectFileDiscovery.FindProjectFiles(
                     request.ScanRoots ?? Array.Empty<string>()))
        {
            AddEntry(file, isLastProject: false);
        }

        return entries
            .OrderByDescending(entry => entry.IsLastProject)
            .ThenByDescending(entry => entry.ModifiedAtUtc ?? DateTime.MinValue)
            .ThenBy(entry => entry.Name)
            .ToList();
    }

    private static ProjectOverviewDescriptor ReadEntry(string file, bool isLastProject)
    {
        using var stream = File.OpenRead(file);
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;
        var fallbackName = ResolveFallbackName(file);

        var name = root.TryGetProperty("Name", out var nameElement)
                   && !string.IsNullOrWhiteSpace(nameElement.GetString())
            ? nameElement.GetString()
            : fallbackName;
        var description = root.TryGetProperty("Description", out var descriptionElement)
            ? descriptionElement.GetString()
            : string.Empty;
        var modifiedAtUtc = TryReadModifiedAt(root) ?? File.GetLastWriteTimeUtc(file);
        var holdingCount = ReadArrayLength(root, "Data");
        var schachtCount = ReadArrayLength(root, "SchaechteData");

        return new ProjectOverviewDescriptor(
            name ?? fallbackName,
            description ?? string.Empty,
            file,
            modifiedAtUtc,
            isLastProject,
            holdingCount,
            schachtCount,
            IsCorrupt: false);
    }

    private static ProjectOverviewDescriptor BuildCorruptEntry(string file, bool isLastProject)
        => new(
            ResolveFallbackName(file),
            "Projektdatei konnte nicht gelesen werden.",
            file,
            File.Exists(file) ? File.GetLastWriteTimeUtc(file) : null,
            isLastProject,
            HoldingCount: 0,
            SchachtCount: 0,
            IsCorrupt: true);

    private static int ReadArrayLength(JsonElement root, string propertyName)
        => root.TryGetProperty(propertyName, out var element)
           && element.ValueKind == JsonValueKind.Array
            ? element.GetArrayLength()
            : 0;

    private static string ResolveFallbackName(string file)
    {
        var name = Path.GetFileNameWithoutExtension(file);
        if (string.Equals(name, "projekt", StringComparison.OrdinalIgnoreCase))
        {
            var projectRoot = ProjectFileLocator.ProjectRootFromFile(file);
            if (!string.IsNullOrWhiteSpace(projectRoot))
                name = Path.GetFileName(projectRoot);
        }

        return string.IsNullOrWhiteSpace(name) ? Path.GetFileName(file) : name;
    }

    private static DateTime? TryReadModifiedAt(JsonElement root)
    {
        if (!root.TryGetProperty("ModifiedAtUtc", out var element)
            || element.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var raw = element.GetString();
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        if (DateTime.TryParse(
                raw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            return parsed;
        }

        return DateTime.TryParse(raw, out parsed)
            ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc)
            : null;
    }
}
