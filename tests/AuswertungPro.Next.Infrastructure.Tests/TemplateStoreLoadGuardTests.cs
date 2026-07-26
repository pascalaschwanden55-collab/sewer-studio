using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class TemplateStoreLoadGuardTests
{
    [Fact]
    public void CostCatalogLoadMerged_Meldet_Defaultpfad_der_als_Ordner_belegt_ist()
    {
        using var temp = new TempDir();
        var projectPath = Path.Combine(temp.Path, "projekt.json");
        var invalidPath = Path.Combine(temp.Path, "Config", "cost_catalog.json");
        Directory.CreateDirectory(invalidPath);
        var store = new CostCatalogStore(Path.Combine(temp.Path, "cost_catalog.user.json"));

        _ = store.LoadMerged(projectPath, out var loadError);

        Assert.Contains("Ordner", loadError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CostCatalogLoadMerged_Meldet_doppelte_normalisierte_Schluessel_und_sperrt_Save()
    {
        using var temp = new TempDir();
        var projectPath = Path.Combine(temp.Path, "projekt.json");
        var defaultPath = Path.Combine(temp.Path, "Config", "cost_catalog.json");
        var overridePath = Path.Combine(temp.Path, "cost_catalog.user.json");
        Directory.CreateDirectory(Path.GetDirectoryName(defaultPath)!);
        File.WriteAllText(
            defaultPath,
            """
            {
              "items": [
                { "key": "ROBOTER-A", "name": "Erste Position" },
                { "key": " roboter a ", "name": "Zweite Position" }
              ]
            }
            """);
        var store = new CostCatalogStore(overridePath);

        var catalog = store.LoadMerged(projectPath, out var loadError);
        var saved = store.SaveUserOverrides(catalog, out var saveError);

        Assert.Contains("doppelt", loadError, StringComparison.OrdinalIgnoreCase);
        Assert.False(saved);
        Assert.Contains("gesperrt", saveError, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(overridePath));
    }

    [Fact]
    public void CostCatalogSave_ueberschreibt_keine_beschaedigte_Override_Datei_ohne_vorheriges_Load()
    {
        using var temp = new TempDir();
        var overridePath = Path.Combine(temp.Path, "cost_catalog.user.json");
        const string corruptContent = "{ kaputt";
        File.WriteAllText(overridePath, corruptContent);
        var store = new CostCatalogStore(overridePath);

        var saved = store.SaveUserOverrides(new CostCatalog(), out var saveError);

        Assert.False(saved);
        Assert.Contains("geladen", saveError, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(corruptContent, File.ReadAllText(overridePath));
    }

    [Fact]
    public void CostCatalogSave_lehnt_doppelte_normalisierte_Schluessel_ab()
    {
        using var temp = new TempDir();
        var overridePath = Path.Combine(temp.Path, "cost_catalog.user.json");
        var store = new CostCatalogStore(overridePath);
        var catalog = new CostCatalog
        {
            Items =
            [
                new CostCatalogItem { Key = "ROBOTER-A", Name = "Erste Position" },
                new CostCatalogItem { Key = " roboter a ", Name = "Zweite Position" }
            ]
        };

        var saved = store.SaveUserOverrides(catalog, out var saveError);

        Assert.False(saved);
        Assert.Contains("doppelt", saveError, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(overridePath));
    }

    [Fact]
    public void MeasureTemplateSave_IsBlocked_WhenExistingUserOverrideCouldNotBeLoaded()
    {
        using var temp = new TempDir();
        var overridePath = Path.Combine(temp.Path, "measure_templates.user.json");
        File.WriteAllText(overridePath, "{ kaputt");
        var store = new MeasureTemplateStore(overridePath);

        _ = store.LoadUserOverrides();
        var ok = store.SaveUserOverrides(new MeasureTemplateCatalog(), out var error);

        Assert.False(ok);
        Assert.Contains("konnte nicht geladen werden", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MeasureTemplateLoadMerged_Meldet_beschaedigte_Defaultdatei()
    {
        using var temp = new TempDir();
        var projectPath = Path.Combine(temp.Path, "Projektdateien", "projekt.json");
        var configPath = Path.Combine(temp.Path, "Projektdateien", "Config", "measure_templates.json");
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        File.WriteAllText(configPath, "{ kaputt");
        var store = new MeasureTemplateStore(Path.Combine(temp.Path, "measure_templates.user.json"));

        var catalog = store.LoadMerged(projectPath, out var loadError);

        Assert.Empty(catalog.Measures);
        Assert.NotNull(loadError);
        Assert.Contains("measure_templates.json", loadError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MeasureTemplateLoadMerged_Meldet_Defaultpfad_der_als_Ordner_belegt_ist()
    {
        using var temp = new TempDir();
        var projectPath = Path.Combine(temp.Path, "projekt.json");
        var invalidPath = Path.Combine(temp.Path, "Config", "measure_templates.json");
        Directory.CreateDirectory(invalidPath);
        var store = new MeasureTemplateStore(Path.Combine(temp.Path, "measure_templates.user.json"));

        _ = store.LoadMerged(projectPath, out var loadError);

        Assert.Contains("Ordner", loadError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MeasureTemplateLoadMerged_Meldet_doppelte_normalisierte_Ids_und_sperrt_Save()
    {
        using var temp = new TempDir();
        var projectPath = Path.Combine(temp.Path, "projekt.json");
        var defaultPath = Path.Combine(temp.Path, "Config", "measure_templates.json");
        var overridePath = Path.Combine(temp.Path, "measure_templates.user.json");
        Directory.CreateDirectory(Path.GetDirectoryName(defaultPath)!);
        File.WriteAllText(
            defaultPath,
            """
            {
              "measures": [
                { "id": "LINER-A", "name": "Erste Vorlage", "lines": [] },
                { "id": " liner a ", "name": "Zweite Vorlage", "lines": [] }
              ]
            }
            """);
        var store = new MeasureTemplateStore(overridePath);

        var catalog = store.LoadMerged(projectPath, out var loadError);
        var saved = store.SaveUserOverrides(catalog, out var saveError);

        Assert.Contains("doppelt", loadError, StringComparison.OrdinalIgnoreCase);
        Assert.False(saved);
        Assert.Contains("gesperrt", saveError, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(overridePath));
    }

    [Fact]
    public void MeasureTemplateSave_ueberschreibt_keine_beschaedigte_Override_Datei_ohne_vorheriges_Load()
    {
        using var temp = new TempDir();
        var overridePath = Path.Combine(temp.Path, "measure_templates.user.json");
        const string corruptContent = "{ kaputt";
        File.WriteAllText(overridePath, corruptContent);
        var store = new MeasureTemplateStore(overridePath);

        var saved = store.SaveUserOverrides(new MeasureTemplateCatalog(), out var saveError);

        Assert.False(saved);
        Assert.Contains("geladen", saveError, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(corruptContent, File.ReadAllText(overridePath));
    }

    [Fact]
    public void MeasureTemplateSave_lehnt_doppelte_normalisierte_Ids_ab()
    {
        using var temp = new TempDir();
        var overridePath = Path.Combine(temp.Path, "measure_templates.user.json");
        var store = new MeasureTemplateStore(overridePath);
        var catalog = new MeasureTemplateCatalog
        {
            Measures =
            [
                new MeasureTemplate { Id = "LINER-A", Name = "Erste Vorlage" },
                new MeasureTemplate { Id = " liner a ", Name = "Zweite Vorlage" }
            ]
        };

        var saved = store.SaveUserOverrides(catalog, out var saveError);

        Assert.False(saved);
        Assert.Contains("doppelt", saveError, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(overridePath));
    }

    [Fact]
    public void MeasureTemplate_Upsert_RoundTripsPositionen()
    {
        // Spiegelt "Als Vorlage speichern" pro Massnahme (Sanierungs-Matrix / Kosten-Fenster):
        // die aktuellen Positionen der Massnahme muessen als User-Vorlage persistiert werden.
        using var temp = new TempDir();
        var overridePath = Path.Combine(temp.Path, "measure_templates.user.json");
        var store = new MeasureTemplateStore(overridePath);

        var template = new MeasureTemplate
        {
            Id = "SCHLAUCHLINER_GFK",
            Name = "Schlauchliner GFK",
            Lines = new List<MeasureLineTemplate>
            {
                new() { Group = "Hauptarbeit", ItemKey = "SCHLAUCHLINER_GFK", Enabled = true, DefaultQty = 30m },
                new() { Group = "Nebenarbeiten", ItemKey = "VERKEHRSDIENST", Enabled = false, DefaultQty = 1m },
            }
        };

        var ok = store.UpsertUserTemplate(template, out var error);
        Assert.True(ok, error);

        // Frisch laden -> Vorlage inkl. Positionen muss persistiert sein.
        var reloaded = new MeasureTemplateStore(overridePath).LoadUserOverrides();
        var saved = reloaded.Measures.FirstOrDefault(m =>
            string.Equals(m.Id, "SCHLAUCHLINER_GFK", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(saved);
        Assert.Equal("Schlauchliner GFK", saved!.Name);
        Assert.Equal(2, saved.Lines.Count);
        Assert.Contains(saved.Lines, l => l.ItemKey == "SCHLAUCHLINER_GFK" && l.Enabled && l.DefaultQty == 30m);
    }

    [Fact]
    public void PositionTemplateSave_IsBlocked_WhenExistingUserOverrideCouldNotBeLoaded()
    {
        using var temp = new TempDir();
        var overridePath = Path.Combine(temp.Path, "position_templates.user.json");
        File.WriteAllText(overridePath, "{ kaputt");
        var store = new PositionTemplateStore(overridePath);

        _ = store.LoadMerged(null);
        var ok = store.SaveUserOverride(new PositionTemplateCatalog(), out var error);

        Assert.False(ok);
        Assert.Contains("konnte nicht geladen werden", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PositionTemplateLoadMerged_Meldet_beschaedigte_Defaultdatei_und_sperrt_Save()
    {
        using var temp = new TempDir();
        var projectPath = Path.Combine(temp.Path, "projekt.json");
        var defaultPath = Path.Combine(temp.Path, "Config", "position_templates.json");
        var overridePath = Path.Combine(temp.Path, "position_templates.user.json");
        Directory.CreateDirectory(Path.GetDirectoryName(defaultPath)!);
        File.WriteAllText(defaultPath, "{ kaputt");
        var store = new PositionTemplateStore(overridePath);

        var catalog = store.LoadMerged(projectPath, out var loadError);
        var saved = store.SaveUserOverride(catalog, out var saveError);

        Assert.Empty(catalog.Groups);
        Assert.Contains("position_templates.json", loadError, StringComparison.OrdinalIgnoreCase);
        Assert.False(saved);
        Assert.Contains("konnte nicht geladen werden", saveError, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(overridePath));
    }

    [Fact]
    public void PositionTemplateLoadMerged_Meldet_Defaultpfad_der_als_Ordner_belegt_ist()
    {
        using var temp = new TempDir();
        var projectPath = Path.Combine(temp.Path, "projekt.json");
        var invalidPath = Path.Combine(temp.Path, "Config", "position_templates.json");
        Directory.CreateDirectory(invalidPath);
        var store = new PositionTemplateStore(Path.Combine(temp.Path, "position_templates.user.json"));

        _ = store.LoadMerged(projectPath, out var loadError);

        Assert.Contains("Ordner", loadError, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PositionTemplateLoadMerged_Meldet_doppelte_Gruppennamen_und_sperrt_Save()
    {
        using var temp = new TempDir();
        var projectPath = Path.Combine(temp.Path, "projekt.json");
        var defaultPath = Path.Combine(temp.Path, "Config", "position_templates.json");
        var overridePath = Path.Combine(temp.Path, "position_templates.user.json");
        Directory.CreateDirectory(Path.GetDirectoryName(defaultPath)!);
        File.WriteAllText(
            defaultPath,
            """
            {
              "groups": [
                { "name": "Robotersanierung", "positions": [] },
                { "name": " robotersanierung ", "positions": [] }
              ]
            }
            """);
        var store = new PositionTemplateStore(overridePath);

        var catalog = store.LoadMerged(projectPath, out var loadError);
        var saved = store.SaveUserOverride(catalog, out var saveError);

        Assert.Contains("doppelt", loadError, StringComparison.OrdinalIgnoreCase);
        Assert.False(saved);
        Assert.Contains("gesperrt", saveError, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(overridePath));
    }

    [Fact]
    public void PositionTemplateSave_ueberschreibt_keine_beschaedigte_Override_Datei_ohne_vorheriges_Load()
    {
        using var temp = new TempDir();
        var overridePath = Path.Combine(temp.Path, "position_templates.user.json");
        const string corruptContent = "{ kaputt";
        File.WriteAllText(overridePath, corruptContent);
        var store = new PositionTemplateStore(overridePath);

        var saved = store.SaveUserOverride(new PositionTemplateCatalog(), out var saveError);

        Assert.False(saved);
        Assert.Contains("geladen", saveError, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(corruptContent, File.ReadAllText(overridePath));
    }

    [Fact]
    public void PositionTemplateSave_lehnt_doppelte_Gruppennamen_ab()
    {
        using var temp = new TempDir();
        var overridePath = Path.Combine(temp.Path, "position_templates.user.json");
        var store = new PositionTemplateStore(overridePath);
        var catalog = new PositionTemplateCatalog
        {
            Groups =
            [
                new PositionGroup { Name = "Robotersanierung" },
                new PositionGroup { Name = " robotersanierung " }
            ]
        };

        var saved = store.SaveUserOverride(catalog, out var saveError);

        Assert.False(saved);
        Assert.Contains("doppelt", saveError, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(overridePath));
    }

    [Fact]
    public void PositionTemplateLoadMerged_Meldet_explizit_null_Gruppen_statt_abzustuerzen()
    {
        using var temp = new TempDir();
        var projectPath = Path.Combine(temp.Path, "projekt.json");
        var defaultPath = Path.Combine(temp.Path, "Config", "position_templates.json");
        Directory.CreateDirectory(Path.GetDirectoryName(defaultPath)!);
        File.WriteAllText(defaultPath, """{ "groups": null }""");
        var store = new PositionTemplateStore(Path.Combine(temp.Path, "position_templates.user.json"));

        var catalog = store.LoadMerged(projectPath, out var loadError);

        Assert.Empty(catalog.Groups);
        Assert.Contains("groups", loadError, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; }

        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "template_store_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // ignore cleanup failures
            }
        }
    }
}
