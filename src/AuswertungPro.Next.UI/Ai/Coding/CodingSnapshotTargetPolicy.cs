using System.IO;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed record CodingSnapshotTarget(string PhotoDirectory, string FilePath);

public static class CodingSnapshotTargetPolicy
{
    public static CodingSnapshotTarget Build(
        ProtocolEntry entry,
        string? videoPath,
        DateTimeOffset now)
    {
        var videoDir = !string.IsNullOrEmpty(videoPath)
            ? Path.GetDirectoryName(videoPath) ?? Path.GetTempPath()
            : Path.GetTempPath();

        var photoDirectory = Path.Combine(videoDir, "Fotos");
        var timestamp = entry.Zeit.HasValue
            ? entry.Zeit.Value.ToString(@"hh\-mm\-ss\-fff")
            : now.ToString("HHmmss");
        var fileName = $"{entry.Code}_{entry.MeterStart:F2}m_{timestamp}.png";

        return new CodingSnapshotTarget(
            photoDirectory,
            Path.Combine(photoDirectory, fileName));
    }
}
