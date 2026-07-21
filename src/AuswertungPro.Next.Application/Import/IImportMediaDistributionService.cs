using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Import;

public sealed record ImportMediaDistributionProgress(
    int Processed,
    int Total,
    string? CurrentFile);

public sealed record ImportMediaDistributionResult(
    int FilesCopied,
    int FilesSkipped,
    int Errors,
    IReadOnlyList<string> Messages);

public sealed record ImportMediaDistributionRequest(
    string ProjectFolder,
    Project Project,
    IProgress<ImportMediaDistributionProgress>? Progress = null,
    CancellationToken CancellationToken = default,
    bool DryRun = false,
    object? CollectionLock = null,
    bool IncludeVideos = true,
    bool IncludePdfs = true,
    bool IncludeSchacht = true,
    IImportFileStagingSession? FileStaging = null);

/// <summary>
/// Verteilt importierte Medien, ohne dass die UI eine konkrete
/// Infrastructure-Klasse erzeugen muss.
/// </summary>
public interface IImportMediaDistributionService
{
    ImportMediaDistributionResult Distribute(ImportMediaDistributionRequest request);
}
