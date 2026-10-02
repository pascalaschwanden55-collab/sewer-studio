using System.IO;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai.Coding;

/// <param name="LiegtImTemp">
/// Ohne Video zeigt das Ziel in den Temp-Ordner. Dorthin gehoeren nie Befundfotos;
/// die Aufnahme lehnt ein solches Ziel ab (Deepscan 02.10.2026, R3).
/// </param>
public sealed record CodingSnapshotTarget(string PhotoDirectory, string FilePath, bool LiegtImTemp = false);

public static class CodingSnapshotTargetPolicy
{
    public static CodingSnapshotTarget Build(
        ProtocolEntry entry,
        string? videoPath,
        DateTimeOffset now)
    {
        var videoOrdner = !string.IsNullOrEmpty(videoPath)
            ? Path.GetDirectoryName(videoPath)
            : null;
        var liegtImTemp = string.IsNullOrEmpty(videoOrdner);
        var videoDir = liegtImTemp ? Path.GetTempPath() : videoOrdner!;

        var photoDirectory = Path.Combine(videoDir, "Fotos");
        var timestamp = entry.Zeit.HasValue
            ? entry.Zeit.Value.ToString(@"hh\-mm\-ss\-fff")
            : now.ToString("HHmmss");
        var fileName = $"{entry.Code}_{entry.MeterStart:F2}m_{timestamp}.png";

        return new CodingSnapshotTarget(
            photoDirectory,
            Path.Combine(photoDirectory, fileName),
            liegtImTemp);
    }
}
