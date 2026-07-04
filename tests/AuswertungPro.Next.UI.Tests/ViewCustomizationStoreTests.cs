using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Services;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// P0 Persistenz-Fundament: der ViewCustomizationStore keyt korrekt je ViewKey/GridKey
/// und ist gegen null-Dictionaries und fehlende Konfiguration robust.
/// </summary>
public sealed class ViewCustomizationStoreTests
{
    [Fact]
    public void GetOrCreate_creates_and_reuses_per_view_key()
    {
        var settings = new AppSettings();
        ViewCustomizationStore.Configure(settings);

        var first = ViewCustomizationStore.GetOrCreate("BuilderPage");
        var again = ViewCustomizationStore.GetOrCreate("BuilderPage");
        var other = ViewCustomizationStore.GetOrCreate("DataPage");

        Assert.Same(first, again);
        Assert.NotSame(first, other);
        Assert.True(settings.ViewCustomizations.ContainsKey("BuilderPage"));
        Assert.True(settings.ViewCustomizations.ContainsKey("DataPage"));
    }

    [Fact]
    public void GetOrCreateGrid_keys_grids_within_a_view()
    {
        var settings = new AppSettings();
        ViewCustomizationStore.Configure(settings);

        var grid = ViewCustomizationStore.GetOrCreateGrid("BuilderPage", "Grid");
        var same = ViewCustomizationStore.GetOrCreateGrid("BuilderPage", "Grid");

        Assert.Same(grid, same);
        Assert.NotNull(grid.Columns);
        Assert.True(settings.ViewCustomizations["BuilderPage"].Grids.ContainsKey("Grid"));
    }

    [Fact]
    public void GetOrCreate_guards_null_dictionary()
    {
        var settings = new AppSettings { ViewCustomizations = null! };
        ViewCustomizationStore.Configure(settings);

        var view = ViewCustomizationStore.GetOrCreate("X");

        Assert.NotNull(view);
        Assert.NotNull(settings.ViewCustomizations);
    }

    [Fact]
    public void GetOrCreate_without_configure_returns_detached_container()
    {
        ViewCustomizationStore.ResetForTests();

        var view = ViewCustomizationStore.GetOrCreate("X");

        Assert.NotNull(view);
        Assert.NotNull(view.Grids);
    }

    [Fact]
    public void Fresh_settings_has_initialized_view_customizations()
        => Assert.NotNull(new AppSettings().ViewCustomizations);
}
