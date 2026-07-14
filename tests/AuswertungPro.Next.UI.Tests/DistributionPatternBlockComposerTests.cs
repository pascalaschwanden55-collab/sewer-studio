using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Tests;

public sealed class DistributionPatternBlockComposerTests
{
    [Fact]
    public void Append_setzt_geklickte_bausteine_in_reihenfolge_zusammen()
    {
        var blocks = DistributionPatternBlockComposer.AvailableExcelBlocks;
        var pattern = string.Empty;

        pattern = DistributionPatternBlockComposer.Append(pattern, blocks.Single(x => x.Label == "Haltungen"));
        pattern = DistributionPatternBlockComposer.Append(pattern, blocks.Single(x => x.Label == "_"));
        pattern = DistributionPatternBlockComposer.Append(pattern, blocks.Single(x => x.Label == "Datum"));

        Assert.Equal("Haltungen_{Datum}", pattern);
    }

    [Fact]
    public void Parse_zeigt_platzhalter_text_und_trennzeichen_als_eigene_bausteine()
    {
        var parts = DistributionPatternBlockComposer.Parse("Haltungen_{Datum}-{Jahr}");

        Assert.Equal(["Haltungen", "_", "Datum", "-", "Jahr"], parts.Select(x => x.Text));
        Assert.Equal([false, false, true, false, true], parts.Select(x => x.IsPlaceholder));
    }

    [Fact]
    public void RemoveLast_entfernt_einen_ganzen_baustein_statt_einzelner_klammern()
    {
        var pattern = DistributionPatternBlockComposer.RemoveLast("Haltungen_{Datum}");

        Assert.Equal("Haltungen_", pattern);
        Assert.Equal("Haltungen", DistributionPatternBlockComposer.RemoveLast(pattern));
        Assert.Equal(string.Empty, DistributionPatternBlockComposer.RemoveLast("Haltungen"));
    }

    [Fact]
    public void RemoveLast_entfernt_bei_freiem_text_nur_das_letzte_zeichen()
        => Assert.Equal("Projek", DistributionPatternBlockComposer.RemoveLast("Projekt"));
}
