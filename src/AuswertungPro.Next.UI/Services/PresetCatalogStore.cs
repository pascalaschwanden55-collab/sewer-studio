using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace AuswertungPro.Next.UI.Services;

public sealed class PresetCatalog
{
    public int Version { get; set; } = 1;
    public string Updated { get; set; } = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    public Dictionary<string, PresetItem> Items { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<string>> FieldLists { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class PresetItem
{
    public decimal? PriceCHF { get; set; }
    public List<string> Measures { get; set; } = new();
}

public static class PresetCatalogStore
{
    private static string PresetPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppIdentity.ProductName, "presets.json");

    private static string LegacyPresetPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppIdentity.LegacyRoamingDataFolder, "presets.json");

    public static PresetCatalog LoadOrDefault()
    {
        MigrateLegacyIfNeeded();
        try
        {
            if (!File.Exists(PresetPath))
                return DefaultCatalog();

            var json = File.ReadAllText(PresetPath);
            var model = JsonSerializer.Deserialize<PresetCatalog>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return Normalize(model ?? DefaultCatalog());
        }
        catch
        {
            return DefaultCatalog();
        }
    }

    public static void Save(PresetCatalog catalog)
    {
        var dir = Path.GetDirectoryName(PresetPath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(catalog, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(PresetPath, json);
    }

    private static void MigrateLegacyIfNeeded()
    {
        try
        {
            if (File.Exists(PresetPath))
                return;
            if (!File.Exists(LegacyPresetPath))
                return;

            var dir = Path.GetDirectoryName(PresetPath);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            File.Copy(LegacyPresetPath, PresetPath, overwrite: false);
        }
        catch
        {
            // ignore migration errors
        }
    }

    private static PresetCatalog Normalize(PresetCatalog model)
    {
        var normalized = new PresetCatalog
        {
            Version = model.Version > 0 ? model.Version : 1,
            Updated = string.IsNullOrWhiteSpace(model.Updated)
                ? DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : model.Updated,
            Items = new Dictionary<string, PresetItem>(StringComparer.OrdinalIgnoreCase),
            FieldLists = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        };

        foreach (var kvp in model.Items ?? new Dictionary<string, PresetItem>())
        {
            var item = kvp.Value ?? new PresetItem();
            item.Measures ??= new List<string>();
            normalized.Items[kvp.Key] = item;
        }

        foreach (var kvp in model.FieldLists ?? new Dictionary<string, List<string>>())
        {
            normalized.FieldLists[kvp.Key] = kvp.Value ?? new List<string>();
        }

        var defaults = DefaultCatalog();
        foreach (var kvp in defaults.FieldLists)
            if (!normalized.FieldLists.ContainsKey(kvp.Key))
                normalized.FieldLists[kvp.Key] = kvp.Value;

        return normalized;
    }

    private static PresetCatalog DefaultCatalog()
        => new()
        {
            Version = 1,
            Updated = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Items = new Dictionary<string, PresetItem>(StringComparer.OrdinalIgnoreCase)
            {
                ["Reinigung / Spuelen"] = new PresetItem
                {
                    PriceCHF = 180,
                    Measures = new List<string> { "Spuelen", "TV-Nachkontrolle" }
                },
                ["Wurzeleinwuchs"] = new PresetItem
                {
                    PriceCHF = 450,
                    Measures = new List<string> { "Fraesen", "Spuelen", "TV-Nachkontrolle" }
                },
                ["Riss"] = new PresetItem
                {
                    PriceCHF = 650,
                    Measures = new List<string> { "Sanierungsplanung", "Kurzliner pruefen", "Dichtheit pruefen" }
                }
            },
            FieldLists = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Zustandsklasse"] = new List<string> { "I", "II", "III", "IV", "V" },
                ["Prioritaet"] = new List<string> { "hoch", "mittel", "tief" }
            }
        };
}
