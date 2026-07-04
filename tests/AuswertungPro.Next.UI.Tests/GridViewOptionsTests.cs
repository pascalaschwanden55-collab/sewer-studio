using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Behaviors;
using AuswertungPro.Next.UI.Services;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// P2 Zoom/Dichte: der GridViewOptions-Halter klammert Werte auf die gueltigen Bereiche
/// und Seed/Persist gehen in denselben Store-Slot wie die Spalten (ohne diese zu ueberschreiben).
/// </summary>
public sealed class GridViewOptionsTests
{
    [Theory]
    [InlineData(5.0, 2.0)]     // ueber Max -> geklammert
    [InlineData(0.1, 0.5)]     // unter Min -> geklammert
    [InlineData(1.25, 1.25)]   // im Bereich -> unveraendert
    public void GridZoom_is_clamped(double input, double expected)
        => Assert.Equal(expected, new GridViewOptions { GridZoom = input }.GridZoom);

    [Theory]
    [InlineData(9999, 240)]
    [InlineData(1, 24)]
    [InlineData(120, 120)]
    public void GridMinRowHeight_is_clamped(double input, double expected)
        => Assert.Equal(expected, new GridViewOptions { GridMinRowHeight = input }.GridMinRowHeight);

    [Fact]
    public void PropertyChanged_fires_only_on_real_change()
    {
        var options = new GridViewOptions();
        var count = 0;
        options.PropertyChanged += (_, _) => count++;

        options.GridZoom = 1.5;
        options.GridZoom = 1.5; // gleicher (bereits geklammerter) Wert -> kein Event

        Assert.Equal(1, count);
    }

    [Fact]
    public void Persist_and_seed_roundtrip_keeps_columns_untouched()
    {
        var settings = new AppSettings();
        ViewCustomizationStore.Configure(settings);

        // Spalten vorbelegen (wie nach P1), dann Zoom/Hoehe persistieren.
        var slot = ViewCustomizationStore.GetOrCreateGrid("BuilderPage", "Grid");
        slot.Columns.Add(new DataPageColumnLayout { FieldName = "Holding", IsVisible = false });

        var options = new GridViewOptions { GridMinRowHeight = 64, GridZoom = 1.4 };
        GridViewOptionsCore.Persist(options, "BuilderPage", "Grid");

        Assert.Equal(64, slot.GridMinRowHeight);
        Assert.Equal(1.4, slot.GridZoom);
        Assert.Single(slot.Columns); // Spalten unveraendert
        Assert.False(slot.Columns[0].IsVisible);

        var reloaded = new GridViewOptions();
        GridViewOptionsCore.Seed(reloaded, "BuilderPage", "Grid");
        Assert.Equal(64, reloaded.GridMinRowHeight);
        Assert.Equal(1.4, reloaded.GridZoom);
    }
}
