using System.Linq;
using AuswertungPro.Next.Application.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class ClassifierDatasetPlanTests
{
    [Theory]
    [InlineData("BDDC", "BDD")]
    [InlineData("BAIZ", "BAI")]
    [InlineData("BAJB", "BAJ")]
    [InlineData("BABBA", "BAB")]
    [InlineData("BBAA", "BBA")]
    [InlineData("BCD", "BCD")]
    [InlineData("BDA", "BDA")]
    [InlineData("BCAAA", "BCA")] // Paket 5: Anschluss
    [InlineData("BCCBY", "BCC")] // Paket 5: Bogen
    [InlineData("BBCC", "BBC")]  // Paket 5: Anhaftende Stoffe
    [InlineData("BAAB", "BAA")]  // Paket 5: Verformung
    [InlineData("kein_schaden", "LEER")]
    public void MapCode_bekannte_Codes_werden_auf_Klasse_abgebildet(string code, string expected)
        => Assert.Equal(expected, ClassifierDatasetPlan.MapCodeToClass(code));

    [Theory]
    [InlineData("axial")]
    [InlineData("schacht")]
    [InlineData("AECXC")]
    [InlineData("BDBA")]    // BDB nicht in v2-Whitelist
    public void MapCode_ausgeschlossene_Codes_geben_null(string code)
        => Assert.Null(ClassifierDatasetPlan.MapCodeToClass(code));

    [Theory]
    [InlineData("81030-80945_8.8s_BCD_t+0.png", "81030-80945")]
    [InlineData("06.24341-35625_100.8s_BDA_t+0.png", "06.24341-35625")]
    [InlineData("80671-80658_1048.7s_BCE_t+0.png", "80671-80658")]
    public void ParseHaltung_extrahiert_Haltungs_Key(string file, string expected)
    {
        Assert.True(ClassifierDatasetPlan.TryParseFrame(file, out var info));
        Assert.Equal(expected, info.Haltung);
    }

    [Fact]
    public void ParseFrame_liefert_Code_und_Klasse()
    {
        Assert.True(ClassifierDatasetPlan.TryParseFrame("287425-81162_319.1s_BDDC_t+0.png", out var info));
        Assert.Equal("BDDC", info.Code);
        Assert.Equal("BDD", info.TrainingClass);
    }

    [Fact]
    public void ParseFrame_kein_schaden_ohne_t_suffix_wird_LEER()
    {
        Assert.True(ClassifierDatasetPlan.TryParseFrame("80628-80622_75.2s_kein_schaden.png", out var info));
        Assert.Equal("LEER", info.TrainingClass);
    }

    [Fact]
    public void ParseFrame_gold_suffix_vom_VideoLabelTool_liefert_sauberen_Code()
    {
        // VideoLabelTool speichert <haltung>_<zeit>s_<CODE>_gold.png
        Assert.True(ClassifierDatasetPlan.TryParseFrame("07.1028055-10285_45.3s_BCCAY_gold.png", out var info));
        Assert.Equal("BCCAY", info.Code);
        Assert.Equal("BCC", info.TrainingClass);
        Assert.Equal("07.1028055-10285", info.Haltung);
    }

    [Fact]
    public void ParseFrame_gold_Negativbeispiel_LEER_wird_LEER()
    {
        Assert.True(ClassifierDatasetPlan.TryParseFrame("80628-80622_75.2s_LEER_gold.png", out var info));
        Assert.Equal("LEER", info.TrainingClass);
    }

    [Fact]
    public void Split_haelt_eine_Haltung_komplett_in_einem_Split()
    {
        var frames = new[]
        {
            new FrameInfo("H1", 0, "BCD", "BCD"), new FrameInfo("H1", 5, "BCD", "BCD"),
            new FrameInfo("H2", 0, "BDA", "BDA"), new FrameInfo("H3", 0, "BCE", "BCE"),
            new FrameInfo("H4", 0, "BDA", "BDA"), new FrameInfo("H5", 0, "BCE", "BCE"),
        };
        var split = ClassifierDatasetPlan.SplitByHaltung(frames, valFraction: 0.4, seed: 42);

        foreach (var h in frames.Select(f => f.Haltung).Distinct())
            Assert.False(
                split.Train.Any(f => f.Haltung == h) && split.Val.Any(f => f.Haltung == h),
                $"Haltung {h} ist in train UND val (Leakage)");

        Assert.Equal(frames.Length, split.Train.Count + split.Val.Count);
    }

    [Fact]
    public void Split_ist_deterministisch_bei_gleichem_Seed()
    {
        var frames = Enumerable.Range(0, 20).Select(i => new FrameInfo($"H{i}", 0, "BCD", "BCD")).ToArray();
        var a = ClassifierDatasetPlan.SplitByHaltung(frames, 0.2, 7);
        var b = ClassifierDatasetPlan.SplitByHaltung(frames, 0.2, 7);
        Assert.Equal(a.Val.Select(f => f.Haltung), b.Val.Select(f => f.Haltung));
        Assert.NotEmpty(a.Val);
    }
}
