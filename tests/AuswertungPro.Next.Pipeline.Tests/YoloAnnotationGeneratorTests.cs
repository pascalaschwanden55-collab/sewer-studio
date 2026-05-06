using System.IO;
using AuswertungPro.Next.UI.Ai.Training.Models;
using AuswertungPro.Next.UI.Ai.Training.Services;
using AuswertungPro.Next.Application.Ai.Training.Models;
using AuswertungPro.Next.Application.Ai.Training.Services;

namespace AuswertungPro.Next.Pipeline.Tests;

public class YoloAnnotationGeneratorTests
{
    /// <summary>BAB.B.A (Querriss) → Klasse 0, Full-Frame-Bbox.</summary>
    [Fact]
    public void GenerateLabel_Crack_ReturnsFullFrameBbox()
    {
        var entry = MakeEntry("BAB.B.A");

        var result = YoloAnnotationGenerator.GenerateLabelLine(entry);

        Assert.Equal("0 0.5 0.5 1.0 1.0", result);
    }

    /// <summary>BCD ist ein Steuercode → null.</summary>
    [Fact]
    public void GenerateLabel_Steuercode_ReturnsNull()
    {
        var entry = MakeEntry("BCD");

        var result = YoloAnnotationGenerator.GenerateLabelLine(entry);

        Assert.Null(result);
    }

    /// <summary>Null VsaCode → null.</summary>
    [Fact]
    public void GenerateLabel_NullCode_ReturnsNull()
    {
        var entry = new GroundTruthEntry
        {
            MeterStart = 0,
            MeterEnd = 0,
            VsaCode = null!,
            Text = ""
        };

        var result = YoloAnnotationGenerator.GenerateLabelLine(entry);

        Assert.Null(result);
    }

    /// <summary>BBA (Wurzeln, VSA 2018) → Klasse 5 (root).</summary>
    [Fact]
    public void GenerateLabel_Root_ClassId5()
    {
        var entry = MakeEntry("BBA");  // VSA 2018: BBA = Wurzeln

        var result = YoloAnnotationGenerator.GenerateLabelLine(entry);

        Assert.NotNull(result);
        Assert.StartsWith("5 ", result);
    }

    /// <summary>BBB (Anhaftende Stoffe, VSA 2018) → Klasse 6 (deposit).</summary>
    [Fact]
    public void GenerateLabel_Deposit_ClassId6()
    {
        var entry = MakeEntry("BBB");  // VSA 2018: BBB = Anhaftende Stoffe

        var result = YoloAnnotationGenerator.GenerateLabelLine(entry);

        Assert.NotNull(result);
        Assert.StartsWith("6 ", result);
    }

    /// <summary>ExportDataset ueberspringt Steuercodes und nicht-existierende Frames.</summary>
    [Fact]
    public async Task ExportDataset_SkipsInvalidEntries()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"yolo_test_{Guid.NewGuid():N}");
        try
        {
            // Erstelle ein gueltiges Testbild
            var imgDir = Path.Combine(tempDir, "src");
            Directory.CreateDirectory(imgDir);
            var imgPath = Path.Combine(imgDir, "frame.jpg");
            await File.WriteAllBytesAsync(imgPath, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 });

            var mappings = new List<(GroundTruthEntry Entry, string FramePath)>
            {
                (MakeEntry("BAB.B.A"), imgPath),           // gueltig
                (MakeEntry("BCD"), imgPath),               // Steuercode → skip
                (MakeEntry("BAC"), "/not/existing.jpg"),   // Datei fehlt → skip
                (MakeEntry("BBB"), imgPath),               // gueltig
            };

            var outputDir = Path.Combine(tempDir, "dataset");
            var stats = await YoloAnnotationGenerator.ExportDatasetAsync(mappings, outputDir);

            Assert.Equal(4, stats.Total);
            Assert.Equal(2, stats.Skipped);
            Assert.Equal(2, stats.Train + stats.Val);
            Assert.True(File.Exists(Path.Combine(outputDir, "data.yaml")));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    private static GroundTruthEntry MakeEntry(string vsaCode) => new()
    {
        MeterStart = 1.0,
        MeterEnd = 1.0,
        VsaCode = vsaCode,
        Text = $"Test {vsaCode}"
    };
}
