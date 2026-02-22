using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace AuswertungPro.Next.UI.Services;

public sealed class DropdownOptionsModel
{
    public List<string> SanierenOptions { get; set; } = new();
    public List<string> EigentuemerOptions { get; set; } = new();
    public List<string> PruefungsresultatOptions { get; set; } = new() { "" };
    public List<string> ReferenzpruefungOptions { get; set; } = new() { "" };
    public List<string> EmpfohleneSanierungsmassnahmenOptions { get; set; } = new() { "" };
}

public static class DropdownOptionsStore
{
    private static readonly string[] FixedEigentuemerOptions = { "Kanton", "Bund", "AWU", "Gemeinde", "Privat" };

    private static string OptionsDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppIdentity.ProductName, "dropdowns");

    private static string LegacyOptionsDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppIdentity.LegacyRoamingDataFolder, "dropdowns");

    private static string LegacyOptionsPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppIdentity.LegacyRoamingDataFolder, "dropdowns.json");

    private static readonly object MigrationLock = new();
    private static bool _migrated;

    public static DropdownOptionsModel LoadOrDefault()
    {
        return new DropdownOptionsModel
        {
            SanierenOptions = LoadSanierenOptions(),
            EigentuemerOptions = LoadEigentuemerOptions(),
            PruefungsresultatOptions = LoadPruefungsresultatOptions(),
            ReferenzpruefungOptions = LoadReferenzpruefungOptions(),
            EmpfohleneSanierungsmassnahmenOptions = LoadEmpfohleneSanierungsmassnahmenOptions()
        };
    }

    public static void Save(DropdownOptionsModel model)
    {
        SaveSanierenOptions(model.SanierenOptions);
        SaveEigentuemerOptions(model.EigentuemerOptions);
        SavePruefungsresultatOptions(model.PruefungsresultatOptions);
        SaveReferenzpruefungOptions(model.ReferenzpruefungOptions);
        SaveEmpfohleneSanierungsmassnahmenOptions(model.EmpfohleneSanierungsmassnahmenOptions);
    }

    public static List<string> LoadSanierenOptions()
        => LoadList("sanieren", DefaultModel().SanierenOptions);

    public static void SaveSanierenOptions(IEnumerable<string> options)
        => SaveList("sanieren", options);

    public static List<string> LoadEigentuemerOptions()
        => new(FixedEigentuemerOptions);

    public static void SaveEigentuemerOptions(IEnumerable<string> options)
        => SaveList("eigentuemer", FixedEigentuemerOptions);

    public static List<string> LoadPruefungsresultatOptions()
        => LoadList("pruefungsresultat", DefaultModel().PruefungsresultatOptions);

    public static void SavePruefungsresultatOptions(IEnumerable<string> options)
        => SaveList("pruefungsresultat", options);

    public static List<string> LoadReferenzpruefungOptions()
        => LoadList("referenzpruefung", DefaultModel().ReferenzpruefungOptions);

    public static void SaveReferenzpruefungOptions(IEnumerable<string> options)
        => SaveList("referenzpruefung", options);

    public static List<string> LoadEmpfohleneSanierungsmassnahmenOptions()
        => LoadList("sanierungsmassnahmen", DefaultModel().EmpfohleneSanierungsmassnahmenOptions);

    public static void SaveEmpfohleneSanierungsmassnahmenOptions(IEnumerable<string> options)
        => SaveList("sanierungsmassnahmen", options);

    private static List<string> LoadList(string key, List<string> defaults)
    {
        EnsureMigrated();
        try
        {
            Directory.CreateDirectory(OptionsDir);
            var path = Path.Combine(OptionsDir, $"{key}.json");
            if (!File.Exists(path))
                return new List<string>(defaults);

            var json = File.ReadAllText(path);
            var list = JsonSerializer.Deserialize<List<string>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (list is null || list.Count == 0)
                return new List<string>(defaults);

            // If the saved list only contains empty strings, it's a broken state — use defaults
            if (list.All(x => string.IsNullOrWhiteSpace(x)) && defaults.Any(x => !string.IsNullOrWhiteSpace(x)))
                return new List<string>(defaults);

            return list;
        }
        catch
        {
            return new List<string>(defaults);
        }
    }

    private static void SaveList(string key, IEnumerable<string> options)
    {
        Directory.CreateDirectory(OptionsDir);
        var path = Path.Combine(OptionsDir, $"{key}.json");
        var json = JsonSerializer.Serialize(options, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private static void EnsureMigrated()
    {
        if (_migrated)
            return;
        lock (MigrationLock)
        {
            if (_migrated)
                return;
            _migrated = true;

            // Migration 1: copy legacy per-key files (Roaming/AuswertungPro/dropdowns/*.json)
            try
            {
                if (Directory.Exists(LegacyOptionsDir) && !Directory.Exists(OptionsDir))
                {
                    Directory.CreateDirectory(OptionsDir);
                    foreach (var legacyFile in Directory.EnumerateFiles(LegacyOptionsDir, "*.json", SearchOption.TopDirectoryOnly))
                    {
                        var dest = Path.Combine(OptionsDir, Path.GetFileName(legacyFile));
                        if (!File.Exists(dest))
                            File.Copy(legacyFile, dest, overwrite: false);
                    }
                }
            }
            catch
            {
                // ignore migration errors
            }

            if (!File.Exists(LegacyOptionsPath))
                return;

            try
            {
                var json = File.ReadAllText(LegacyOptionsPath);
                var model = JsonSerializer.Deserialize<DropdownOptionsModel>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (model is null)
                    return;

                if (model.SanierenOptions.Count > 0)
                    SaveSanierenOptions(model.SanierenOptions);
                if (model.EigentuemerOptions.Count > 0)
                    SaveEigentuemerOptions(model.EigentuemerOptions);
                if (model.PruefungsresultatOptions.Count > 0)
                    SavePruefungsresultatOptions(model.PruefungsresultatOptions);
                if (model.ReferenzpruefungOptions.Count > 0)
                    SaveReferenzpruefungOptions(model.ReferenzpruefungOptions);
                if (model.EmpfohleneSanierungsmassnahmenOptions.Count > 0)
                    SaveEmpfohleneSanierungsmassnahmenOptions(model.EmpfohleneSanierungsmassnahmenOptions);
            }
            catch
            {
                // ignore migration errors
            }
        }
    }

    private static DropdownOptionsModel DefaultModel() => new()
    {
        SanierenOptions = new List<string> { "Ja", "Nein" },
        EigentuemerOptions = new List<string>(FixedEigentuemerOptions),
        PruefungsresultatOptions = new List<string>
        {
            "Pruefung bestanden",
            "Pruefung knapp nicht bestanden",
            "Pruefung nicht bestanden (grob undicht)",
            "Keine"
        },
        ReferenzpruefungOptions = new List<string> { "Ja", "Nein" },
        EmpfohleneSanierungsmassnahmenOptions = new List<string>
        {
            "",
            "Schlauchliner (Nadelfilz) DN 100-200",
            "Schlauchliner (GFK) DN 200-300",
            "Kurzliner / Partliner",
            "Manschette (Quick Lock)",
            "Manschette (Quick Lock EPDM)",
            "Pointliner",
            "Anschluss verpressen",
            "Reinigung + TV-Inspektion",
            "Erneuerung / Neubau"
        }
    };
}
