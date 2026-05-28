using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class TechniqueFrameAnalyzerTests
{
    [Fact]
    public void AssessFrame_DetectsGoodLightingAndOsdMatch()
    {
        const int width = 120;
        const int height = 120;
        var pixels = CreateSolidBgra(width, height, value: 120);

        var assessment = TechniqueFrameAnalyzer.AssessFrame(
            new BgraImageFrame(width, height, pixels),
            osdMeterReading: 10.2,
            protocolMeter: 10.0);

        Assert.True(assessment.OsdReadable);
        Assert.Equal("Gut", assessment.LightingQuality);
        Assert.InRange(assessment.MeanLuminance, 119, 121);
        Assert.Equal("Schlecht", assessment.SharpnessQuality);
        Assert.Equal("B", assessment.OverallGrade);
    }

    private static byte[] CreateSolidBgra(int width, int height, byte value)
    {
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < width * height; i++)
        {
            var offset = i * 4;
            pixels[offset] = value;
            pixels[offset + 1] = value;
            pixels[offset + 2] = value;
            pixels[offset + 3] = 255;
        }

        return pixels;
    }
}
