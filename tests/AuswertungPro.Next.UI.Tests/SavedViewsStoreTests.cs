using System.Text.Json;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Behaviors;
using AuswertungPro.Next.UI.Services;
using AuswertungPro.Next.UI.ViewModels.Pages;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// P6 gespeicherte Ansichten: Store-Roundtrip (Upsert/Get/Delete, Ueberschreiben gleichnamiger)
/// und Filter-JSON-Roundtrip (dasselbe Serialisierungs-Schema wie die BuilderPage-VM).
/// </summary>
public sealed class SavedViewsStoreTests
{
    [Fact]
    public void Upsert_get_and_delete_roundtrip()
    {
        var settings = new AppSettings();
        ViewCustomizationStore.Configure(settings);

        SavedViewsStore.Upsert("BuilderPage", new SavedView { Name = "Nur Sanierung", FilterJson = "{}" });
        SavedViewsStore.Upsert("BuilderPage", new SavedView { Name = "Für Offerte", FilterJson = "{}" });

        Assert.Equal(new[] { "Nur Sanierung", "Für Offerte" }, SavedViewsStore.Names("BuilderPage"));
        Assert.NotNull(SavedViewsStore.Get("BuilderPage", "für offerte")); // case-insensitiv

        SavedViewsStore.Delete("BuilderPage", "Nur Sanierung");
        Assert.Equal(new[] { "Für Offerte" }, SavedViewsStore.Names("BuilderPage"));
    }

    [Fact]
    public void Upsert_same_name_overwrites_instead_of_duplicating()
    {
        var settings = new AppSettings();
        ViewCustomizationStore.Configure(settings);

        SavedViewsStore.Upsert("BuilderPage", new SavedView { Name = "A", SortFieldName = "Holding" });
        SavedViewsStore.Upsert("BuilderPage", new SavedView { Name = "A", SortFieldName = "NetCost" });

        Assert.Single(SavedViewsStore.Names("BuilderPage"));
        Assert.Equal("NetCost", SavedViewsStore.Get("BuilderPage", "A")!.SortFieldName);
    }

    [Fact]
    public void Filter_criteria_survives_json_roundtrip()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var original = new BuilderPageFilterCriteria(
            Owner: "Privat", ExecutedBy: "Kanalsanierer", Sanieren: "Ja",
            Material: "Beton", Status: "offen", Year: "2026",
            Search: "Moosmatt", OnlyWithCost: true, OnlyWithMeasures: false);

        var json = JsonSerializer.Serialize(original, options);
        var restored = JsonSerializer.Deserialize<BuilderPageFilterCriteria>(json, options);

        Assert.Equal(original, restored);
    }
}
