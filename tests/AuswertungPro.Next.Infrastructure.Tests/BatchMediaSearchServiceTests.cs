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

    // Deepscan A4: Die Mediensuche liest Datum_Jahr nach der gemeinsamen Regel. Frueher verlor
    // sie bei "5.3.2024", "24.09.25" oder "2024-03-05" still den Datumshinweis.
    [Theory]
    [MemberData(nameof(Common.HaltungFeldwerteTests.Datumsbeispiele), MemberType = typeof(Common.HaltungFeldwerteTests))]
    public void Search_nutzt_Datum_Jahr_nach_der_gemeinsamen_Leseregel(string datum, string? erwartetIso, bool nurJahr)
    {
        _ = nurJahr;
        using var directory = new TempDirectory();
        var stempel = erwartetIso is null
            ? "20240305"
            : Common.HaltungFeldwerteTests.Iso(erwartetIso).ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        var passend = Path.Combine(directory.Path, $"{stempel}_H100.mp4");
        var anderes = Path.Combine(directory.Path, "19991231_H100.mp4");
        File.WriteAllText(passend, "video");
        File.WriteAllText(anderes, "video");

        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "H100", FieldSource.Manual, userEdited: false);
        record.SetFieldValue(FieldKeys.InspectionYear, datum, FieldSource.Manual, userEdited: false);

        var match = Assert.Single(new BatchMediaSearchService().Search(
            [record],
            new BatchMediaSearchOptions { SearchFolder = directory.Path, Recursive = false, SearchPdfs = false, SearchPhotos = false }));

        if (erwartetIso is null)
        {
            Assert.Equal(MediaMatchStatus.Ambiguous, match.VideoStatus);
            return;
        }

        Assert.Equal(MediaMatchStatus.Found, match.VideoStatus);
        Assert.Equal(passend, match.VideoPath);
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
