using System.Text;
using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Infrastructure.HoldingDistribution;

/// <summary>
/// Reine Konflikthinweise und kompatible Fassade fuer die Kandidatenkopie.
/// </summary>
public static class VideoConflictArtifacts
{
    private static readonly IVideoConflictCandidateCopier Default = new VideoConflictCandidateCopyService();

    public static IVideoConflictCandidateCopier Current => Default;

    [Obsolete("Die Videokonflikt-Fassade ist unveraenderbar. Abhaengigkeit direkt uebergeben.")]
    public static void Use(IVideoConflictCandidateCopier copier)
    {
        ArgumentNullException.ThrowIfNull(copier);
        throw new NotSupportedException(
            "Die Videokonflikt-Fassade kann nicht mehr global ersetzt werden.");
    }

    public static void CopyCandidates(
        string unmatchedFolder,
        string dateStamp,
        string holding,
        IReadOnlyList<string> candidates)
        => Current.CopyCandidates(unmatchedFolder, dateStamp, holding, candidates);

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
