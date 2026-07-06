using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Costs;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class TemplateStoreLoadGuardTests
{
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
