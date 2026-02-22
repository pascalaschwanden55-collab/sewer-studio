using System;
using System.IO;
using System.Text.Json;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Costs;

public sealed class PositionTemplateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public PositionTemplateCatalog Load(string? projectPath)
    {
        var path = ResolvePath(projectPath, "position_templates.json");
        return ReadCatalog(path);
    }

    public PositionTemplateCatalog LoadMerged(string? projectPath)
    {
        var defaultCatalog = Load(projectPath);
        var userCatalogPath = GetUserOverridePath();
        
        if (!File.Exists(userCatalogPath))
            return defaultCatalog;

        var userCatalog = ReadCatalog(userCatalogPath);
        return MergeCatalogs(defaultCatalog, userCatalog);
    }

    public bool SaveUserOverride(PositionTemplateCatalog catalog, out string? error)
    {
        error = null;
        try
        {
            var userPath = GetUserOverridePath();
            var dir = Path.GetDirectoryName(userPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(catalog, new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            
            File.WriteAllText(userPath, json);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static string ResolvePath(string? projectPath, string fileName)
    {
        if (!string.IsNullOrWhiteSpace(projectPath))
        {
            var dir = Path.GetDirectoryName(projectPath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                var projectPathCandidate = Path.Combine(dir, "Config", fileName);
                if (File.Exists(projectPathCandidate))
                    return projectPathCandidate;
            }
        }

        return Path.Combine(AppContext.BaseDirectory, "Config", fileName);
    }

    private static string GetUserOverridePath()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appDataPath, "AuswertungPro", "position_templates.user.json");
    }

    private static PositionTemplateCatalog ReadCatalog(string path)
    {
        try
        {
            if (!File.Exists(path))
                return new PositionTemplateCatalog();

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<PositionTemplateCatalog>(json, JsonOptions) ?? new PositionTemplateCatalog();
        }
        catch
        {
            return new PositionTemplateCatalog();
        }
    }

    private static PositionTemplateCatalog MergeCatalogs(PositionTemplateCatalog defaultCatalog, PositionTemplateCatalog userCatalog)
    {
        var merged = new PositionTemplateCatalog
        {
            Version = Math.Max(defaultCatalog.Version, userCatalog.Version),
            Groups = new List<PositionGroup>(defaultCatalog.Groups)
        };

        // Override with user groups if they exist
        foreach (var userGroup in userCatalog.Groups)
        {
            var existingGroupIndex = merged.Groups.FindIndex(g => 
                string.Equals(g.Name, userGroup.Name, StringComparison.OrdinalIgnoreCase));
            
            if (existingGroupIndex >= 0)
            {
                merged.Groups[existingGroupIndex] = userGroup;
            }
            else
            {
                merged.Groups.Add(userGroup);
            }
        }

        return merged;
    }
}