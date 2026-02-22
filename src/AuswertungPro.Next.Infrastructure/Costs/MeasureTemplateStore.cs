using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Costs;

public sealed class MeasureTemplateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public MeasureTemplateCatalog LoadMerged(string? projectPath)
    {
        var defaults = LoadDefault(projectPath);
        var overrides = LoadUserOverrides();
        return Merge(defaults, overrides);
    }

    public MeasureTemplateCatalog LoadDefault(string? projectPath)
    {
        var path = ResolvePath(projectPath, "measure_templates.json");
        return ReadCatalog(path);
    }

    public MeasureTemplateCatalog LoadUserOverrides()
    {
        var path = ResolveUserOverridePath();
        return ReadCatalog(path);
    }

    public bool SaveUserOverrides(MeasureTemplateCatalog catalog, out string error)
    {
        error = "";
        try
        {
            var path = ResolveUserOverridePath();
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(catalog, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(path, json);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool ResetUserOverrides(out string error)
    {
        error = "";
        try
        {
            var path = ResolveUserOverridePath();
            if (File.Exists(path))
                File.Delete(path);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public bool UpsertUserTemplate(MeasureTemplate template, out string error)
    {
        error = "";
        var overrides = LoadUserOverrides();
        var id = NormalizeId(template.Id, template.Name);
        if (string.IsNullOrWhiteSpace(id))
        {
            error = "Template Id oder Name fehlt.";
            return false;
        }

        template.Id = id;
        template.Name = string.IsNullOrWhiteSpace(template.Name) ? id : template.Name.Trim();

        var existing = FindTemplate(overrides, template.Id, template.Name);
        if (existing is not null)
        {
            existing.Name = template.Name;
            existing.Disabled = template.Disabled;
            var lines = template.Lines ?? new List<MeasureLineTemplate>();
            existing.Lines = lines.Select(CloneLine).ToList();
        }
        else
        {
            overrides.Measures.Add(CloneTemplate(template));
        }

        return SaveUserOverrides(overrides, out error);
    }

    public bool DisableUserTemplate(string idOrName, out string error)
    {
        error = "";
        var overrides = LoadUserOverrides();
        var existing = FindTemplate(overrides, idOrName, idOrName);
        if (existing is null)
        {
            error = "Template nicht gefunden.";
            return false;
        }

        existing.Disabled = true;
        return SaveUserOverrides(overrides, out error);
    }

    public bool DeleteUserTemplate(string idOrName, out string error)
    {
        error = "";
        var overrides = LoadUserOverrides();
        var existing = FindTemplate(overrides, idOrName, idOrName);
        if (existing is null)
        {
            error = "Template nicht gefunden.";
            return false;
        }

        overrides.Measures.Remove(existing);
        return SaveUserOverrides(overrides, out error);
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

    private static string ResolveUserOverridePath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "AuswertungPro", "measure_templates.user.json");
    }

    private static MeasureTemplateCatalog ReadCatalog(string path)
    {
        try
        {
            if (!File.Exists(path))
                return new MeasureTemplateCatalog();

            var json = File.ReadAllText(path);
            var model = JsonSerializer.Deserialize<MeasureTemplateCatalog>(json, JsonOptions) ?? new MeasureTemplateCatalog();
            return Normalize(model);
        }
        catch
        {
            return new MeasureTemplateCatalog();
        }
    }

    private static MeasureTemplateCatalog Normalize(MeasureTemplateCatalog model)
    {
        var normalized = new MeasureTemplateCatalog
        {
            Version = model.Version > 0 ? model.Version : 1,
            Measures = new List<MeasureTemplate>()
        };

        foreach (var template in model.Measures ?? new List<MeasureTemplate>())
        {
            template.Id ??= "";
            template.Name ??= "";
            template.Lines ??= new List<MeasureLineTemplate>();
            normalized.Measures.Add(template);
        }

        return normalized;
    }

    private static MeasureTemplateCatalog Merge(MeasureTemplateCatalog defaults, MeasureTemplateCatalog overrides)
    {
        var map = new Dictionary<string, MeasureTemplate>(StringComparer.OrdinalIgnoreCase);
        foreach (var template in defaults.Measures)
        {
            var id = NormalizeId(template.Id, template.Name);
            if (string.IsNullOrWhiteSpace(id))
                continue;
            map[id] = CloneTemplate(template with { Id = id });
        }

        foreach (var template in overrides.Measures)
        {
            var id = NormalizeId(template.Id, template.Name);
            if (string.IsNullOrWhiteSpace(id))
                continue;

            if (map.TryGetValue(id, out var existing))
            {
                if (template.Disabled)
                {
                    existing.Disabled = true;
                    if (template.Lines.Count > 0)
                        existing.Lines = template.Lines.Select(CloneLine).ToList();
                    if (!string.IsNullOrWhiteSpace(template.Name))
                        existing.Name = template.Name.Trim();
                }
                else
                {
                    map[id] = CloneTemplate(template with { Id = id, Name = ResolveName(template, existing) });
                }
            }
            else
            {
                map[id] = CloneTemplate(template with { Id = id, Name = ResolveName(template, null) });
            }
        }

        return new MeasureTemplateCatalog
        {
            Version = Math.Max(defaults.Version, overrides.Version),
            Measures = map.Values
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    private static MeasureTemplate? FindTemplate(MeasureTemplateCatalog catalog, string id, string name)
    {
        foreach (var template in catalog.Measures)
        {
            if (!string.IsNullOrWhiteSpace(id) &&
                string.Equals(template.Id, id, StringComparison.OrdinalIgnoreCase))
                return template;
            if (!string.IsNullOrWhiteSpace(name) &&
                string.Equals(template.Name, name, StringComparison.OrdinalIgnoreCase))
                return template;
        }

        return null;
    }

    private static string ResolveName(MeasureTemplate template, MeasureTemplate? fallback)
    {
        if (!string.IsNullOrWhiteSpace(template.Name))
            return template.Name.Trim();
        if (fallback is not null && !string.IsNullOrWhiteSpace(fallback.Name))
            return fallback.Name.Trim();
        return template.Id.Trim();
    }

    private static string NormalizeId(string? id, string? name)
    {
        var raw = string.IsNullOrWhiteSpace(id) ? name : id;
        if (string.IsNullOrWhiteSpace(raw))
            return "";

        var buffer = raw.Trim().ToUpperInvariant();
        var cleaned = new string(buffer
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
            .ToArray());

        return cleaned.Trim('_');
    }

    private static MeasureTemplate CloneTemplate(MeasureTemplate template)
        => new()
        {
            Id = template.Id,
            Name = template.Name,
            Disabled = template.Disabled,
            Lines = (template.Lines ?? new List<MeasureLineTemplate>()).Select(CloneLine).ToList()
        };

    private static MeasureLineTemplate CloneLine(MeasureLineTemplate line)
        => new()
        {
            Group = line.Group,
            ItemKey = line.ItemKey,
            Enabled = line.Enabled,
            DefaultQty = line.DefaultQty
        };
}
