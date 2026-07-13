using System;
using System.IO;
using System.Text.Json;
using AuswertungPro.Next.Domain.Models.Costs;

namespace AuswertungPro.Next.Infrastructure.Costs;

public sealed class CostCalculationService
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly string _seedDataDir;
    private readonly string _userDataDir;
    private readonly string _seedCatalogPath;
    private readonly string _legacyUserCatalogPath;
    private readonly string _userCatalogPath;
    private readonly string _seedTemplatesPath;
    private readonly string _userTemplatesPath;

    public CostCalculationService(string projectRoot)
        : this(projectRoot, userDataDir: null)
    {
    }

    /// <summary>
    /// Erlaubt Werkzeugen und Tests einen isolierten Benutzerordner. Ohne Übergabe bleibt
    /// der bisherige Roaming-AppData-Pfad unverändert.
    /// </summary>
    public CostCalculationService(string projectRoot, string? userDataDir)
    {
        _seedDataDir = Path.Combine(projectRoot, "Data");
        _userDataDir = string.IsNullOrWhiteSpace(userDataDir)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AuswertungPro",
                "legacy_costs")
            : Path.GetFullPath(userDataDir);
        _seedCatalogPath = Path.Combine(_seedDataDir, "seed_price_catalog.json");
        _legacyUserCatalogPath = Path.Combine(_seedDataDir, "user_catalog.json");
        _userCatalogPath = Path.Combine(_userDataDir, "user_catalog.json");
        _seedTemplatesPath = Path.Combine(_seedDataDir, "measure_templates.json");
        _userTemplatesPath = Path.Combine(_userDataDir, "measure_templates.json");

        EnsureUserDataDirectory();
        EnsureSeedCatalog();
        EnsureSeedTemplates();
    }

    private void EnsureUserDataDirectory()
    {
        if (!Directory.Exists(_userDataDir))
            Directory.CreateDirectory(_userDataDir);
    }

    private void EnsureSeedCatalog()
    {
        if (File.Exists(_userCatalogPath))
            return;

        if (File.Exists(_legacyUserCatalogPath))
        {
            File.Copy(_legacyUserCatalogPath, _userCatalogPath, false);
            return;
        }

        if (File.Exists(_seedCatalogPath))
        {
            File.Copy(_seedCatalogPath, _userCatalogPath, false);
        }
    }
    
    private void EnsureSeedTemplates()
    {
        if (File.Exists(_userTemplatesPath))
            return;
        if (File.Exists(_seedTemplatesPath))
            File.Copy(_seedTemplatesPath, _userTemplatesPath, false);
    }

    public PriceCatalog LoadCatalog()
    {

        PriceCatalog? TryLoad(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return null;
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<PriceCatalog>(json, _jsonOptions);
            }
            catch
            {
                return null;
            }
        }

        var user = TryLoad(_userCatalogPath);
        if (user is { Items.Count: > 0 })
            return user;

        var seed = TryLoad(_seedCatalogPath);
        if (seed is { Items.Count: > 0 })
        {
            // Self-heal: if an old/invalid user_catalog.json exists, replace it with the valid seed.
            try
            {
                File.Copy(_seedCatalogPath, _userCatalogPath, overwrite: true);
            }
            catch
            {
                // ignore; we'll still return seed
            }

            return seed;
        }

        return new PriceCatalog();
    }

    public void SaveCatalog(PriceCatalog catalog)
    {
        EnsureUserDataDirectory();
        var json = JsonSerializer.Serialize(catalog, Application.Common.JsonDefaults.Indented);
        AtomicJsonFileWriter.WriteAllText(_userCatalogPath, json);
    }

    public MeasureTemplates LoadTemplates()
    {
        MeasureTemplates? TryLoad(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return null;
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<MeasureTemplates>(json, _jsonOptions);
            }
            catch
            {
                return null;
            }
        }

        var user = TryLoad(_userTemplatesPath);
        if (user is { Templates.Count: > 0 })
            return user;

        var seed = TryLoad(_seedTemplatesPath);
        if (seed is { Templates.Count: > 0 })
        {
            try
            {
                File.Copy(_seedTemplatesPath, _userTemplatesPath, overwrite: true);
            }
            catch
            {
                // ignore; we'll still return seed
            }

            return seed;
        }

        return new MeasureTemplates();
    }

    public void SaveTemplates(MeasureTemplates templates)
    {
        EnsureUserDataDirectory();
        var json = JsonSerializer.Serialize(templates, Application.Common.JsonDefaults.Indented);
        AtomicJsonFileWriter.WriteAllText(_userTemplatesPath, json);
    }

    public string GetCatalogPath() => _userCatalogPath;

}
