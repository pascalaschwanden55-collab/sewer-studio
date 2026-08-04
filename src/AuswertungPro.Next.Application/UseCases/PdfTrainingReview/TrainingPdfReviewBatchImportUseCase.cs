using AuswertungPro.Next.Application.Ai.Workbench;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Application.UseCases.PdfTrainingReview;

public sealed record TrainingPdfReviewBatchImportRequest(
    IReadOnlyList<string> FolderPaths,
    int? PipeDiameterMm);

public sealed record TrainingPdfReviewBatchProgress(
    int CurrentPdfNumber,
    int DiscoveredPdfCount,
    string SourceDocumentName);

public sealed record TrainingPdfReviewBatchIssue(
    string ReasonCode,
    string Message,
    string? SourceDocumentName = null,
    int? PageNumber = null,
    string? PhotoId = null)
{
    public string? SourcePath { get; init; }
}

public sealed record TrainingPdfReviewBatchImportResult(
    int RequestedFolderCount,
    int DiscoveredPdfCount,
    int ReadPdfCount,
    int FailedPdfCount,
    int DuplicatePdfCount,
    int DetectedPhotoCount,
    int MatchedPhotoCount,
    int ProtectedPhotoCount,
    IReadOnlyList<WorkbenchItem> Items,
    IReadOnlyList<TrainingPdfReviewBatchIssue> Issues);

public interface ITrainingPdfReviewBatchImportUseCase
{
    Task<TrainingPdfReviewBatchImportResult> ImportFoldersAsync(
        TrainingPdfReviewBatchImportRequest request,
        IProgress<TrainingPdfReviewBatchProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Sucht PDFs in mehreren Ordnern und fuehrt den bestehenden sicheren
/// Einzelimport nacheinander aus. Ein defektes PDF stoppt die anderen nicht.
/// </summary>
public sealed class TrainingPdfReviewBatchImportUseCase
    : ITrainingPdfReviewBatchImportUseCase
{
    private readonly ITrainingPdfFolderDiscoveryService _discovery;
    private readonly ITrainingPdfReviewImportService _pdfImport;
    private readonly Func<TrainingPdfReviewProtectionSnapshot> _loadProtection;

    public TrainingPdfReviewBatchImportUseCase(
        ITrainingPdfFolderDiscoveryService discovery,
        ITrainingPdfReviewImportService pdfImport,
        Func<TrainingPdfReviewProtectionSnapshot> loadProtection)
    {
        _discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
        _pdfImport = pdfImport ?? throw new ArgumentNullException(nameof(pdfImport));
        _loadProtection = loadProtection
                          ?? throw new ArgumentNullException(nameof(loadProtection));
    }

    public async Task<TrainingPdfReviewBatchImportResult> ImportFoldersAsync(
        TrainingPdfReviewBatchImportRequest request,
        IProgress<TrainingPdfReviewBatchProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.FolderPaths);

        cancellationToken.ThrowIfCancellationRequested();
        var protection = await Task.Run(_loadProtection, cancellationToken)
            .ConfigureAwait(false)
                         ?? throw new InvalidDataException(
                             "Der Eval-Schutz lieferte keinen gueltigen Stand.");
        cancellationToken.ThrowIfCancellationRequested();

        var discovery = await Task.Run(
                () => _discovery.Discover(request.FolderPaths, cancellationToken),
                cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var issues = discovery.Issues
            .Select(issue => new TrainingPdfReviewBatchIssue(
                issue.ReasonCode,
                issue.Message,
                Path.GetFileName(issue.Path))
            {
                SourcePath = issue.Path,
            })
            .ToList();
        var items = new List<WorkbenchItem>();
        var documentHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var readPdfCount = 0;
        var failedPdfCount = 0;
        var duplicatePdfCount = 0;
        var detectedPhotoCount = 0;
        var matchedPhotoCount = 0;
        var protectedPhotoCount = 0;

        for (var index = 0; index < discovery.PdfPaths.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pdfPath = discovery.PdfPaths[index];
            var documentName = Path.GetFileName(pdfPath);
            progress?.Report(new TrainingPdfReviewBatchProgress(
                index + 1,
                discovery.PdfPaths.Count,
                documentName));

            try
            {
                var result = await _pdfImport.ImportAsync(
                        new TrainingPdfReviewImportRequest(
                            pdfPath,
                            request.PipeDiameterMm)
                        {
                            Protection = protection,
                        },
                        cancellationToken)
                    .ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(result.SourceDocumentSha256))
                    throw new InvalidDataException(
                        "Der PDF-Import lieferte keine Dokument-Pruefsumme.");
                readPdfCount++;
                if (!documentHashes.Add(result.SourceDocumentSha256))
                {
                    duplicatePdfCount++;
                    issues.Add(new TrainingPdfReviewBatchIssue(
                        "duplicate_pdf",
                        "Doppeltes PDF ausgelassen.",
                        documentName)
                    {
                        SourcePath = pdfPath,
                    });
                    continue;
                }

                detectedPhotoCount += result.DetectedPhotoCount;
                matchedPhotoCount += result.MatchedPhotoCount;
                protectedPhotoCount += result.ProtectedPhotoCount;
                items.AddRange(result.Items);
                issues.AddRange(result.Issues.Select(issue =>
                {
                    var batchIssue = new TrainingPdfReviewBatchIssue(
                        issue.ReasonCode,
                        issue.Message,
                        result.SourceDocumentName,
                        issue.PageNumber,
                        issue.PhotoId)
                    {
                        SourcePath = pdfPath,
                    };
                    return batchIssue;
                }));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (OutOfMemoryException)
            {
                throw;
            }
            catch (Exception ex)
            {
                failedPdfCount++;
                var userMessage = UserError.DescribeAndReport(
                    ex,
                    $"PDF-Ordnerimport ({pdfPath})");
                issues.Add(new TrainingPdfReviewBatchIssue(
                    "pdf_import_failed",
                    userMessage,
                    documentName)
                {
                    SourcePath = pdfPath,
                });
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        return new TrainingPdfReviewBatchImportResult(
            request.FolderPaths.Count,
            discovery.PdfPaths.Count,
            readPdfCount,
            failedPdfCount,
            duplicatePdfCount,
            detectedPhotoCount,
            matchedPhotoCount,
            protectedPhotoCount,
            items,
            issues);
    }
}
