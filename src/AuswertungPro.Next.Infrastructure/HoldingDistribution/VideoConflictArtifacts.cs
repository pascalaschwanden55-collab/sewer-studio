using System.Globalization;
using System.Text;

namespace AuswertungPro.Next.Infrastructure.HoldingDistribution;

internal static class VideoConflictArtifacts
{
    public static void CopyCandidates(
        string unmatchedFolder,
        string dateStamp,
        string holding,
        IReadOnlyList<string> candidates)
    {
        for (var i = 0; i < candidates.Count; i++)
        {
            var source = candidates[i];
            var extension = Path.GetExtension(source);
            var name = $"{dateStamp}_{holding}_CANDIDATE_{(i + 1).ToString("00", CultureInfo.InvariantCulture)}{extension}";
            var destination = DistributionFileTransfer.EnsureUniquePath(
                Path.Combine(unmatchedFolder, name),
                overwrite: false);
            File.Copy(source, destination, overwrite: false);
        }
    }

    public static string BuildMissingInfo(string sourcePath, string videoName, DateTime date, string holding)
    {
        var text = new StringBuilder();
        text.AppendLine("VIDEO MISSING");
        text.AppendLine($"PDF: {sourcePath}");
        text.AppendLine($"Film: {videoName}");
        text.AppendLine($"Datum: {date:dd.MM.yyyy}");
        text.AppendLine($"Haltung: {holding}");
        return text.ToString();
    }

    public static string BuildAmbiguousInfo(
        string sourcePath,
        string videoName,
        DateTime date,
        string holding,
        IReadOnlyList<string> candidates)
    {
        var text = new StringBuilder();
        text.AppendLine("VIDEO AMBIGUOUS");
        text.AppendLine($"PDF: {sourcePath}");
        text.AppendLine($"Film: {videoName}");
        text.AppendLine($"Datum: {date:dd.MM.yyyy}");
        text.AppendLine($"Haltung: {holding}");
        text.AppendLine("Candidates:");
        foreach (var candidate in candidates)
            text.AppendLine($"- {candidate}");
        return text.ToString();
    }
}
