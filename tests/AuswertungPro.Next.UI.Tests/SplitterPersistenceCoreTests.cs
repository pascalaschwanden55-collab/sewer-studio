using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Behaviors;
using AuswertungPro.Next.UI.Services;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// P3 Layout/Panels: die Splitter-Persistenz klammert die Groesse und legt sie
/// pro Ansicht unter dem SplitterKey ab.
/// </summary>
public sealed class SplitterPersistenceCoreTests
{
    [Fact]
    public void Persist_clamps_and_stores_by_key()
    {
        var settings = new AppSettings();
        ViewCustomizationStore.Configure(settings);

        SplitterPersistenceCore.Persist("BuilderPage", "Stats", actualSize: 5000, min: 280, max: 900);

        Assert.Equal(900, settings.ViewCustomizations["BuilderPage"].SplitterSizes["Stats"]);
    }

    [Fact]
    public void TryGetStored_roundtrips_a_persisted_size()
    {
        var settings = new AppSettings();
        ViewCustomizationStore.Configure(settings);

        SplitterPersistenceCore.Persist("BuilderPage", "Stats", 460, 280, 900);

        Assert.True(SplitterPersistenceCore.TryGetStored("BuilderPage", "Stats", out var size));
        Assert.Equal(460, size);
    }

    [Fact]
    public void TryGetStored_returns_false_when_missing()
    {
        var settings = new AppSettings();
        ViewCustomizationStore.Configure(settings);

        Assert.False(SplitterPersistenceCore.TryGetStored("BuilderPage", "Unknown", out _));
    }
}
