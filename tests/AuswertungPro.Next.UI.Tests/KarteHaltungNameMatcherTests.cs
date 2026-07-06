using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Tests;

public sealed class KarteHaltungNameMatcherTests
{
    [Fact]
    public void Exakter_Name_passt()
        => Assert.True(KarteHaltungNameMatcher.Matches("21731-21730", "21731-21730"));

    [Fact]
    public void Umgekehrte_Schacht_Reihenfolge_passt()
        => Assert.True(KarteHaltungNameMatcher.Matches("21731-21730", "21730-21731"));

    [Fact]
    public void Teilstrecken_Suffix_passt_auf_basis()
        => Assert.True(KarteHaltungNameMatcher.Matches("21731-21730.1", "21731-21730"));

    [Fact]
    public void Umgekehrt_und_suffix_kombiniert_passt()
        => Assert.True(KarteHaltungNameMatcher.Matches("21731-21730.1", "21730-21731"));

    [Fact]
    public void Verschiedene_haltungen_passen_nicht()
        => Assert.False(KarteHaltungNameMatcher.Matches("21731-21730", "99999-88888"));

    [Fact]
    public void Lange_katasternummer_mit_punkt_wird_nicht_als_suffix_gestrippt()
        => Assert.False(KarteHaltungNameMatcher.Matches("7.32154", "7"));

    [Fact]
    public void Leer_oder_null_passt_nie()
    {
        Assert.False(KarteHaltungNameMatcher.Matches(null, "21731-21730"));
        Assert.False(KarteHaltungNameMatcher.Matches("21731-21730", ""));
        Assert.False(KarteHaltungNameMatcher.Matches("  ", "  "));
    }
}
