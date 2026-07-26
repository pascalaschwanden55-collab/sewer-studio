using System;
using System.IO;
using System.Text.Json;
using AuswertungPro.Next.Application.Costs;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Costs;

public sealed class PositionTemplateStore : IPositionTemplateStore
{
    private readonly string? _userOverridePath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public PositionTemplateStore(string? userOverridePath = null)
    {
        _userOverridePath = userOverridePath;
    }

    public string? LastUserOverrideLoadError { get; private set; }
    private string? _lastMergedLoadError;

    public PositionTemplateCatalog Load(string? projectPath)
        => Load(projectPath, out _);

    public PositionTemplateCatalog Load(string? projectPath, out string? loadError)
    {
        var path = ResolvePath(projectPath, "position_templates.json");
        return ReadCatalog(path, out loadError);
    }

    public PositionTemplateCatalog LoadMerged(string? projectPath)
        => LoadMerged(projectPath, out _);

    public PositionTemplateCatalog LoadMerged(string? projectPath, out string? loadError)
    {
        var defaultCatalog = Load(projectPath, out var defaultLoadError);
        LastUserOverrideLoadError = null;
        var userCatalogPath = GetUserOverridePath();
        var userCatalog = ReadCatalog(
            userCatalogPath,
            out var userLoadError,
            rememberUserOverrideError: true);
        loadError = CombineLoadErrors(defaultLoadError, userLoadError);
        _lastMergedLoadError = loadError;
        return MergeCatalogs(defaultCatalog, userCatalog);
    }

    public bool SaveUserOverride(PositionTemplateCatalog catalog, out string? error)
    {
        error = null;
        if (catalog is null)
        {
            error = "Positionsvorlagen fehlen; Speichern ist gesperrt.";
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
            var userPath = GetUserOverridePath();
            ValidateCatalogStructure(catalog);
            _ = ReadCatalog(
                userPath,
                out var existingLoadError,
                rememberUserOverrideError: true);
            if (!string.IsNullOrWhiteSpace(existingLoadError))
            {
                error = $"Vorhandener User-Override konnte nicht geladen werden; Speichern ist gesperrt: {existingLoadError}";
                return false;
            }

            var dir = Path.GetDirectoryName(userPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(catalog, Application.Common.JsonDefaults.IndentedCamel);
            
            AtomicJsonFileWriter.WriteAllText(userPath, json);
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
                if (CostStoreFileProbe.ShouldUseProjectCandidate(projectPathCandidate))
                    return projectPathCandidate;
            }
        }

        return Path.Combine(AppContext.BaseDirectory, "Config", fileName);
    }

    private string GetUserOverridePath()
    {
        if (!string.IsNullOrWhiteSpace(_userOverridePath))
            return _userOverridePath;

        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appDataPath, "AuswertungPro", "position_templates.user.json");
    }

    private PositionTemplateCatalog ReadCatalog(
        string path,
        out string? loadError,
        bool rememberUserOverrideError = false)
    {
        try
        {
            loadError = null;
            var probe = CostStoreFileProbe.Probe(path);
            if (probe.State == CostStorePathState.Missing)
                return new PositionTemplateCatalog();
            if (probe.State != CostStorePathState.File)
                throw new IOException(probe.Error ?? "Vorlagendatei ist nicht sicher lesbar.");

            var json = File.ReadAllText(path);
            var model = JsonSerializer.Deserialize<PositionTemplateCatalog>(json, JsonOptions)
                        ?? throw new JsonException("Der Positionsvorlagen-Katalog darf nicht null sein.");
            return Normalize(model);
        }
        catch (Exception ex)
        {
            loadError = $"{Path.GetFileName(path)} ist beschaedigt oder nicht lesbar: {ex.Message}";
            if (rememberUserOverrideError)
                LastUserOverrideLoadError = ex.Message;
            return new PositionTemplateCatalog();
        }
    }

    private static string? CombineLoadErrors(string? defaultError, string? userOverrideError)
    {
        var userText = string.IsNullOrWhiteSpace(userOverrideError)
            ? null
            : $"Benutzer-Positionsvorlagen (Overrides): {userOverrideError}";
        if (string.IsNullOrWhiteSpace(defaultError))
            return userText;
        return userText is null ? defaultError : $"{defaultError}\n{userText}";
    }

    private static PositionTemplateCatalog Normalize(PositionTemplateCatalog model)
    {
        ValidateCatalogStructure(model);
        foreach (var group in model.Groups)
        {
            group.Name = group.Name.Trim();
            foreach (var position in group.Positions)
            {
                position.ItemKey ??= "";
                position.Name ??= "";
                position.Unit ??= "";
            }
        }

        return model;
    }

    private static void ValidateCatalogStructure(PositionTemplateCatalog model)
    {
        if (model.Groups is null)
            throw new InvalidDataException("Das Feld 'groups' darf nicht null sein.");

        var groupNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var groupIndex = 0; groupIndex < model.Groups.Count; groupIndex++)
        {
            var group = model.Groups[groupIndex]
                        ?? throw new InvalidDataException(
                            $"Positionsgruppe {groupIndex + 1} darf nicht null sein.");
            var name = group.Name?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(name))
                throw new InvalidDataException($"Positionsgruppe {groupIndex + 1} hat keinen Namen.");
            if (!groupNames.Add(name))
                throw new InvalidDataException($"Der Positionsgruppen-Name '{name}' ist doppelt.");
            if (group.Positions is null)
                throw new InvalidDataException($"Positionen der Gruppe '{name}' duerfen nicht null sein.");

            for (var positionIndex = 0; positionIndex < group.Positions.Count; positionIndex++)
            {
                var position = group.Positions[positionIndex]
                               ?? throw new InvalidDataException(
                                   $"Position {positionIndex + 1} der Gruppe '{name}' darf nicht null sein.");
                if (position.DefaultQty < 0)
                    throw new InvalidDataException(
                        $"Position {positionIndex + 1} der Gruppe '{name}' hat eine negative Menge.");
                if (!position.IsCustom && string.IsNullOrWhiteSpace(position.ItemKey))
                    throw new InvalidDataException(
                        $"Katalogposition {positionIndex + 1} der Gruppe '{name}' hat keinen Schluessel.");
                if (position.IsCustom
                    && string.IsNullOrWhiteSpace(position.ItemKey)
                    && string.IsNullOrWhiteSpace(position.Name))
                {
                    throw new InvalidDataException(
                        $"Freie Position {positionIndex + 1} der Gruppe '{name}' hat weder Schluessel noch Name.");
                }
            }
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
