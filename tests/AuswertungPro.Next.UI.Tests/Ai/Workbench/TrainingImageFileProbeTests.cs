using System.IO;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingImageFileProbeTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "sewerstudio-image-probe-" + Guid.NewGuid().ToString("N"));

    public TrainingImageFileProbeTests()
        => Directory.CreateDirectory(_tempDirectory);

    [Fact]
    public void Probe_liest_gueltiges_Png_und_lehnt_beschaedigte_Datei_ab()
    {
        var validPath = Path.Combine(_tempDirectory, "valid.png");
        var corruptPath = Path.Combine(_tempDirectory, "corrupt.png");
        File.WriteAllBytes(
            validPath,
            Convert.FromBase64String(
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwC" +
                "AAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="));
        File.WriteAllBytes(corruptPath, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A]);

        var dimensions = TrainingImageFileProbe.ReadDimensions(validPath);

        Assert.Equal((1, 1), dimensions);
        Assert.True(TrainingImageFileProbe.CanDecode(validPath));
        Assert.False(TrainingImageFileProbe.CanDecode(corruptPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, recursive: true);
    }
}
