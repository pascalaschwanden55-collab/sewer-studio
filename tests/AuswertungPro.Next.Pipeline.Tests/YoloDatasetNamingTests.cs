using AuswertungPro.Next.Application.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Charakterisierungstests fuer YoloDatasetNaming.ChooseSplit.
/// </summary>
public sealed class YoloDatasetNamingTests
{
    [Fact]
    public void ChooseSplit_ValidierungsRatio0_LiefertImmerTrain()
    {
        Assert.Equal("train", YoloDatasetNaming.ChooseSplit("irgendein-pfad", 0));
        Assert.Equal("train", YoloDatasetNaming.ChooseSplit("anderer-pfad", 0));
    }

    [Fact]
    public void ChooseSplit_ValidierungsRatio1_LiefertImmerVal()
    {
        Assert.Equal("val", YoloDatasetNaming.ChooseSplit("irgendein-pfad", 1));
        Assert.Equal("val", YoloDatasetNaming.ChooseSplit("anderer-pfad", 1));
    }

    [Fact]
    public void ChooseSplit_GleicherKey_LiefertImmerGleichesSplit()
    {
        // Deterministisch: gleicher Key => gleiche Antwort
        var split1 = YoloDatasetNaming.ChooseSplit("C:/frames/foo_BAB.png", 0.2);
        var split2 = YoloDatasetNaming.ChooseSplit("C:/frames/foo_BAB.png", 0.2);
        Assert.Equal(split1, split2);
    }

    [Fact]
    public void ChooseSplit_GrossKleinschreibung_IstUnerheblich()
    {
        // Schluessel wird intern zu Grossbuchstaben normiert
        var lower = YoloDatasetNaming.ChooseSplit("c:/frames/bcd_001.png", 0.2);
        var upper = YoloDatasetNaming.ChooseSplit("C:/FRAMES/BCD_001.PNG", 0.2);
        Assert.Equal(lower, upper);
    }

    [Fact]
    public void ChooseSplit_MittelwertRatio_VerteiltAufBeideSplits()
    {
        // Mit 0.5-Ratio sollen beide Splits auftreten (statistische Stichprobe)
        var splits = Enumerable.Range(0, 200)
            .Select(i => YoloDatasetNaming.ChooseSplit($"frame_{i:D6}.png", 0.5))
            .ToList();

        Assert.Contains("train", splits);
        Assert.Contains("val", splits);
    }

    [Fact]
    public void ChooseSplit_Grenzwerte_WerdenBeachtet()
    {
        // ratio negativ -> train; ratio > 1 -> val (defensiv)
        Assert.Equal("train", YoloDatasetNaming.ChooseSplit("x.png", -0.1));
        Assert.Equal("val",   YoloDatasetNaming.ChooseSplit("x.png",  1.5));
    }
}
