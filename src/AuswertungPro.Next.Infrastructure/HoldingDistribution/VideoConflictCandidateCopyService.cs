using System.Globalization;
using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Infrastructure.HoldingDistribution;

/// <summary>
/// Benennt mehrdeutige Videokandidaten nachvollziehbar und kopiert sie
/// ueber die gemeinsame, kollisionsgeschuetzte Dateiuebertragung.
/// </summary>
public sealed class VideoConflictCandidateCopyService : IVideoConflictCandidateCopier
{
    private readonly IDistributionFileTransfer _fileTransfer;

    public VideoConflictCandidateCopyService()
        : this(DistributionFileTransfer.Current)
    {
    }

    public VideoConflictCandidateCopyService(IDistributionFileTransfer fileTransfer)
    {
        _fileTransfer = fileTransfer ?? throw new ArgumentNullException(nameof(fileTransfer));
    }

    public void CopyCandidates(
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
            var destination = _fileTransfer.EnsureUniquePath(
                Path.Combine(unmatchedFolder, name),
                overwrite: false);
            _fileTransfer.MoveOrCopy(
                source,
                destination,
                move: false,
                overwrite: false);
        }
    }
}
