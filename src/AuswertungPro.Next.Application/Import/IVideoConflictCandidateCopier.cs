namespace AuswertungPro.Next.Application.Import;

/// <summary>
/// Sichert mehrdeutige Videokandidaten mit stabilen Namen im Konfliktordner.
/// </summary>
public interface IVideoConflictCandidateCopier
{
    void CopyCandidates(
        string unmatchedFolder,
        string dateStamp,
        string holding,
        IReadOnlyList<string> candidates);
}
