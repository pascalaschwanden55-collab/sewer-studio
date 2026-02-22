using System;
using System.Globalization;
using System.IO;
using AuswertungPro.Next.Infrastructure;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class HoldingTxtDistributionTests
{
    [Fact]
    public void DistributeTxtFiles_CreatesTxtAndVideoPerHolding()
    {
        using var temp = new TempDir();
        var src = Path.Combine(temp.Path, "src");
        var videos = Path.Combine(temp.Path, "videos");
        var dest = Path.Combine(temp.Path, "dest");
        Directory.CreateDirectory(src);
        Directory.CreateDirectory(videos);
        Directory.CreateDirectory(dest);

        var txtPath = Path.Combine(src, "kiDVDaten.txt");
        var infoPath = Path.Combine(src, "kiDVinfo.txt");
        var videoPath = Path.Combine(videos, "ABC001.MPG");

        File.WriteAllText(infoPath, "Aufnahmen: 04.12.14 - 05.12.14");
        File.WriteAllText(videoPath, "video");
        File.WriteAllText(txtPath, string.Join(Environment.NewLine, new[]
        {
            "Schmutzwasser 100 -> 200 UV 300 @Datei=ABC001.MPG",
            "  0.0m Rohranfang  @Pos=0:00:00",
            "  1.0m Rohrende  @Pos=0:00:05"
        }));

        var results = HoldingFolderDistributor.DistributeTxtFiles(
            txtFiles: new[] { txtPath },
            videoSourceFolder: videos,
            destGemeindeFolder: dest,
            moveInsteadOfCopy: false,
            overwrite: false,
            recursiveVideoSearch: true,
            unmatchedFolderName: "__UNMATCHED",
            project: null,
            progress: null);

        var item = Assert.Single(results);
        Assert.True(item.Success, item.Message);
        Assert.Equal(HoldingFolderDistributor.VideoMatchStatus.Matched, item.VideoStatus);
        Assert.False(string.IsNullOrWhiteSpace(item.DestPdfPath));
        Assert.False(string.IsNullOrWhiteSpace(item.DestVideoPath));
        Assert.True(File.Exists(item.DestPdfPath!));
        Assert.True(File.Exists(item.DestVideoPath!));

        var dateStamp = new DateTime(2014, 12, 4).ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        Assert.Contains($"{dateStamp}_100-200", Path.GetFileName(item.DestPdfPath!), StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"{dateStamp}_100-200", Path.GetFileName(item.DestVideoPath!), StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; }

        public TempDir()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "txt_dist_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // ignore cleanup failures
            }
        }
    }
}
