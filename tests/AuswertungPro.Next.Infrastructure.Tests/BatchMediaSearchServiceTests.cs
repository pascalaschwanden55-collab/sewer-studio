using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Media;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class BatchMediaSearchServiceTests
{
    [Fact]
    public void Search_findet_eindeutig_benanntes_video_und_markiert_es_zum_anwenden()
    {
        using var directory = new TempDirectory();
        var expectedVideo = Path.Combine(directory.Path, "100-200.mp4");
        File.WriteAllText(expectedVideo, "video");

        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "100-200", FieldSource.Manual, userEdited: false);
        var service = new BatchMediaSearchService();

        var result = service.Search(
            [record],
            new BatchMediaSearchOptions
            {
                SearchFolder = directory.Path,
                Recursive = false,
                SearchPdfs = false,
                SearchPhotos = false
            });

        var match = Assert.Single(result);
        Assert.Equal(MediaMatchStatus.Found, match.VideoStatus);
        Assert.Equal(expectedVideo, match.VideoPath);
        Assert.True(match.Apply);
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "batch_media_search_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // Test-Aufräumen darf das Testergebnis nicht verdecken.
            }
        }
    }
}
