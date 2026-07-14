using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Training;

public sealed class TrainingFfmpegFilePathResolverTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "TrainingFfmpegFilePathResolverTests_" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void Resolve_bewahrt_existierende_Datei_und_verwirft_fehlenden_Pfad()
    {
        Directory.CreateDirectory(_root);
        var existing = Path.Combine(_root, "ffmpeg.exe");
        var missing = Path.Combine(_root, "nicht-da.exe");
        File.WriteAllText(existing, "test");
        ITrainingFfmpegPathResolver resolver = new TrainingFfmpegFilePathResolver();

        Assert.Equal(existing, resolver.Resolve(existing));
        Assert.Equal("ffmpeg", resolver.Resolve(missing));
        Assert.Equal("ffmpeg", resolver.Resolve(null));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Test-Aufraeumen darf das eigentliche Ergebnis nicht verdecken.
        }
    }
}
