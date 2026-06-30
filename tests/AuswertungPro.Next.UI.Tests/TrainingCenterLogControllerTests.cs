using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingCenterLogControllerTests
{
    [Fact]
    public void CreateLine_formatiert_echtzeit_und_hauptlog_identisch_mit_viewmodel_verhalten()
    {
        var now = new DateTime(2026, 6, 30, 12, 3, 4);

        var line = TrainingCenterLogController.CreateLine(now, "Import gestartet");

        Assert.Equal("[12:03:04] Import gestartet", line.EntryText);
        Assert.Equal("[12:03:04] Import gestartet\n", line.LogTextAppend);
    }

    [Fact]
    public void AppendCapped_entfernt_aelteste_eintraege_ueber_dem_limit()
    {
        var entries = Enumerable.Range(0, 100)
            .Select(i => $"alt-{i}")
            .ToList();

        TrainingCenterLogController.AppendCapped(entries, "neu", maxEntries: 100);

        Assert.True(entries.Count == 100);
        Assert.DoesNotContain("alt-0", entries);
        Assert.Equal("alt-1", entries[0]);
        Assert.Equal("neu", entries[^1]);
    }

    [Fact]
    public void AppendCapped_erlaubt_mehrere_entfernungen_wenn_liste_schon_zu_lang_ist()
    {
        var entries = Enumerable.Range(0, 103)
            .Select(i => $"alt-{i}")
            .ToList();

        TrainingCenterLogController.AppendCapped(entries, "neu", maxEntries: 100);

        Assert.True(entries.Count == 100);
        Assert.Equal("alt-4", entries[0]);
        Assert.Equal("neu", entries[^1]);
    }
}
