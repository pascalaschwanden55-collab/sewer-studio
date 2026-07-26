using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using AuswertungPro.Next.Application.Costs;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Costs;

public sealed class MeasureTemplateStore : IMeasureTemplateStore
{
    private readonly string? _userOverridePath;
    private string? _lastMergedLoadError;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public MeasureTemplateStore(string? userOverridePath = null)
    {
        _userOverridePath = userOverridePath;
    }

    public string? LastUserOverrideLoadError { get; private set; }

    public MeasureTemplateCatalog LoadMerged(string? projectPath)
        => LoadMerged(projectPath, out _);

    public MeasureTemplateCatalog LoadMerged(string? projectPath, out string? loadError)
    {
        var defaults = ReadCatalog(
            ResolvePath(projectPath, "measure_templates.json"),
            out var defaultError);
        var overrides = LoadUserOverrides();
        loadError = CombineLoadErrors(defaultError, LastUserOverrideLoadError);
        _lastMergedLoadError = loadError;
        return Merge(defaults, overrides);
    }

    private static string? CombineLoadErrors(string? defaultError, string? userOverrideError)
    {
        var userText = string.IsNullOrWhiteSpace(userOverrideError)
            ? null
            : $"Benutzer-Vorlagen (Overrides): {userOverrideError}";
        if (string.IsNullOrWhiteSpace(defaultError))
            return userText;
        return userText is null ? defaultError : $"{defaultError}\n{userText}";
    }

    public MeasureTemplateCatalog LoadDefault(string? projectPath)
    {
        var path = ResolvePath(projectPath, "measure_templates.json");
        return ReadCatalog(path);
    }

    public MeasureTemplateCatalog LoadUserOverrides()
    {
        LastUserOverrideLoadError = null;
        var path = ResolveUserOverridePath();
        return ReadCatalog(path, rememberUserOverrideError: true);
    }

    public bool SaveUserOverrides(MeasureTemplateCatalog catalog, out string error)
    {
        error = "";
        if (catalog is null)
        {
            error = "Massnahmenvorlagen fehlen; Speichern ist gesperrt.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(_lastMergedLoadError))
        {
            error = $"Vorlagendatei konnte nicht geladen werden; Speichern ist gesperrt: {_lastMergedLoadError}";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(LastUserOverrideLoadError))
        {
            error = $"User-Override konnte nicht geladen werden; Speichern ist gesperrt: {LastUserOverrideLoadError}";
            return false;
        }

        try
        {
            var path = ResolveUserOverridePath();
            ValidateCatalogStructure(catalog);
            _ = ReadCatalog(path, out var existingLoadError, rememberUserOverrideError: true);
            if (!string.IsNullOrWhiteSpace(existingLoadError))
            {
                error = $"Vorhandener User-Override konnte nicht geladen werden; Speichern ist gesperrt: {existingLoadError}";
                return false;
            }

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(catalog, Application.Common.JsonDefaults.Indented);
            AtomicJsonFileWriter.WriteAllText(path, json);
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
            var probe = CostStoreFileProbe.Probe(path);
            if (probe.State == CostStorePathState.Invalid)
            {
                error = probe.Error ?? "User-Override ist nicht sicher zugreifbar.";
                return false;
            }

            if (probe.State == CostStorePathState.File)
                File.Delete(path);

            if (CostStoreFileProbe.Probe(path).State != CostStorePathState.Missing)
            {
                error = "User-Override konnte nicht sicher entfernt werden.";
                return false;
            }

            LastUserOverrideLoadError = null;
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
                if (CostStoreFileProbe.ShouldUseProjectCandidate(projectPathCandidate))
                    return projectPathCandidate;
            }
        }

        return Path.Combine(AppContext.BaseDirectory, "Config", fileName);
    }

    private string ResolveUserOverridePath()
    {
        if (!string.IsNullOrWhiteSpace(_userOverridePath))
            return _userOverridePath;

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "AuswertungPro", "measure_templates.user.json");
    }

    private MeasureTemplateCatalog ReadCatalog(string path, bool rememberUserOverrideError = false)
        => ReadCatalog(path, out _, rememberUserOverrideError);

    private MeasureTemplateCatalog ReadCatalog(
        string path,
        out string? loadError,
        bool rememberUserOverrideError = false)
    {
        try
        {
            loadError = null;
            var probe = CostStoreFileProbe.Probe(path);
            if (probe.State == CostStorePathState.Missing)
                return new MeasureTemplateCatalog();
            if (probe.State != CostStorePathState.File)
                throw new IOException(probe.Error ?? "Vorlagendatei ist nicht sicher lesbar.");

            var json = File.ReadAllText(path);
            var model = JsonSerializer.Deserialize<MeasureTemplateCatalog>(json, JsonOptions)
                        ?? throw new JsonException("Der Vorlagenkatalog darf nicht null sein.");
            return Normalize(model);
        }
        catch (Exception ex)
        {
            loadError = $"{Path.GetFileName(path)} ist beschaedigt oder nicht lesbar: {ex.Message}";
            if (rememberUserOverrideError)
                LastUserOverrideLoadError = ex.Message;
            return new MeasureTemplateCatalog();
        }
    }

    private static MeasureTemplateCatalog Normalize(MeasureTemplateCatalog model)
    {
        ValidateCatalogStructure(model);
        var normalized = new MeasureTemplateCatalog
        {
            Version = model.Version > 0 ? model.Version : 1,
            Measures = new List<MeasureTemplate>()
        };

        foreach (var template in model.Measures)
        {
            template.Id ??= "";
            template.Name ??= "";
            foreach (var line in template.Lines)
            {
                line.Group ??= "";
                line.ItemKey ??= "";
            }
            normalized.Measures.Add(template);
        }

        return normalized;
    }

    private static void ValidateCatalogStructure(MeasureTemplateCatalog model)
    {
        if (model.Measures is null)
            throw new InvalidDataException("Das Feld 'measures' darf nicht null sein.");

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var templateIndex = 0; templateIndex < model.Measures.Count; templateIndex++)
        {
            var template = model.Measures[templateIndex]
                           ?? throw new InvalidDataException(
                               $"Massnahmenvorlage {templateIndex + 1} darf nicht null sein.");
            var id = NormalizeId(template.Id, template.Name);
            if (string.IsNullOrWhiteSpace(id))
                throw new InvalidDataException(
                    $"Massnahmenvorlage {templateIndex + 1} hat weder ID noch Name.");
            if (!ids.Add(id))
                throw new InvalidDataException($"Die normalisierte Vorlagen-ID '{id}' ist doppelt.");
            if (template.Lines is null)
                throw new InvalidDataException($"Positionen der Vorlage '{id}' duerfen nicht null sein.");

            for (var lineIndex = 0; lineIndex < template.Lines.Count; lineIndex++)
            {
                var line = template.Lines[lineIndex]
                           ?? throw new InvalidDataException(
                               $"Position {lineIndex + 1} der Vorlage '{id}' darf nicht null sein.");
                if (string.IsNullOrWhiteSpace(line.ItemKey))
                    throw new InvalidDataException(
                        $"Position {lineIndex + 1} der Vorlage '{id}' hat keinen Katalog-Schluessel.");
                if (line.DefaultQty < 0)
                    throw new InvalidDataException(
                        $"Position {lineIndex + 1} der Vorlage '{id}' hat eine negative Menge.");
            }
        }
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
