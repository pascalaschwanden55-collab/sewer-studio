using System.Globalization;
using System.IO;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingSnapshotTargetPolicyTests
{
    [Fact]
    public void Build_uses_video_directory_fotos_folder_and_entry_time()
    {
        var entry = Entry("BCA", 4.0, TimeSpan.FromMilliseconds(11_123));
        var videoPath = Path.Combine("C:\\video", "haltung.mp4");

        var target = CodingSnapshotTargetPolicy.Build(
            entry,
            videoPath,
            new DateTimeOffset(2026, 6, 22, 14, 5, 9, TimeSpan.Zero));

        var expectedMeter = entry.MeterStart!.Value.ToString("F2", CultureInfo.CurrentCulture);
        Assert.Equal(Path.Combine("C:\\video", "Fotos"), target.PhotoDirectory);
        Assert.Equal(
            Path.Combine("C:\\video", "Fotos", $"BCA_{expectedMeter}m_00-00-11-123.png"),
            target.FilePath);
    }

    [Fact]
    public void Build_uses_temp_fotos_folder_and_current_time_when_video_or_entry_time_is_missing()
    {
        var entry = Entry("BCA", 4.0, time: null);

        var target = CodingSnapshotTargetPolicy.Build(
            entry,
            videoPath: null,
            new DateTimeOffset(2026, 6, 22, 14, 5, 9, TimeSpan.Zero));

        var expectedMeter = entry.MeterStart!.Value.ToString("F2", CultureInfo.CurrentCulture);
        var expectedDirectory = Path.Combine(Path.GetTempPath(), "Fotos");

        Assert.Equal(expectedDirectory, target.PhotoDirectory);
        Assert.Equal(
            Path.Combine(expectedDirectory, $"BCA_{expectedMeter}m_140509.png"),
            target.FilePath);
    }

    private static ProtocolEntry Entry(string code, double meter, TimeSpan? time)
        => new()
        {
            Code = code,
            MeterStart = meter,
            Zeit = time
        };
}
