using System.Globalization;
using AuswertungPro.Next.Application.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Charakterisierungstests fuer StageALabelFormatting.
/// </summary>
public sealed class StageALabelFormattingTests
{
    // --- Clamp01 ---

    [Theory]
    [InlineData(0.0,  0.0)]
    [InlineData(1.0,  1.0)]
    [InlineData(0.5,  0.5)]
    [InlineData(-1.0, 0.0)]
    [InlineData(1.5,  1.0)]
    [InlineData(double.NegativeInfinity, 0.0)]
    [InlineData(double.PositiveInfinity, 1.0)]
    public void Clamp01_KlemmtAufNullBisEins(double input, double expected)
        => Assert.Equal(expected, StageALabelFormatting.Clamp01(input));

    // --- SanitizeFileName ---

    [Fact]
    public void SanitizeFileName_ErsetztUngueltigeZeichen()
    {
        var result = StageALabelFormatting.SanitizeFileName("sample:name|test");
        Assert.Equal("sample_name_test", result);
    }

    [Fact]
    public void SanitizeFileName_BehaeltGueltigeZeichen()
    {
        var result = StageALabelFormatting.SanitizeFileName("abc-123_test");
        Assert.Equal("abc-123_test", result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void SanitizeFileName_LeererEingabe_LiefertGuid(string? input)
    {
        var result = StageALabelFormatting.SanitizeFileName(input!);
        // GUID-N-Format: 32 Hex-Zeichen ohne Trennzeichen
        Assert.Equal(32, result.Length);
        Assert.All(result, c => Assert.True(char.IsAsciiHexDigit(c)));
    }

    // --- BuildYoloLabelLine ---

    [Fact]
    public void BuildYoloLabelLine_MitBbox_LiefertKorrektesFormat()
    {
        var sample = new TrainingSample
        {
            BboxXCenter = 0.5,
            BboxYCenter = 0.25,
            BboxWidth   = 0.4,
            BboxHeight  = 0.6,
        };

        var line = StageALabelFormatting.BuildYoloLabelLine(3, sample);

        // Format: "classId xc yc w h" mit 6 Dezimalstellen, InvariantCulture
        Assert.Equal("3 0.500000 0.250000 0.400000 0.600000", line);
    }

    [Fact]
    public void BuildYoloLabelLine_OhneBbox_LiefertDefaultBox()
    {
        var sample = new TrainingSample(); // keine BBox-Felder gesetzt

        var line = StageALabelFormatting.BuildYoloLabelLine(0, sample);

        Assert.Equal("0 0.500000 0.500000 0.800000 0.800000", line);
    }

    [Fact]
    public void BuildYoloLabelLine_KlemtBboxWerteAufNull1()
    {
        var sample = new TrainingSample
        {
            BboxXCenter = -0.1, // zu klein
            BboxYCenter =  1.2, // zu gross
            BboxWidth   =  0.4,
            BboxHeight  =  0.6,
        };

        var line = StageALabelFormatting.BuildYoloLabelLine(1, sample);

        // Clamp: -0.1 -> 0.0; 1.2 -> 1.0
        Assert.Equal(
            string.Format(CultureInfo.InvariantCulture, "1 {0:F6} {1:F6} {2:F6} {3:F6}", 0.0, 1.0, 0.4, 0.6),
            line);
    }
}
