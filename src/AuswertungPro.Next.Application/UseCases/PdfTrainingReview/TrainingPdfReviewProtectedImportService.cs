namespace AuswertungPro.Next.Application.UseCases.PdfTrainingReview;

/// <summary>
/// Bindet den aktuellen Eval-Schutz fail-closed an den bestehenden Einzelimport.
/// Der eigentliche PDF-Leser und seine oeffentliche Fassade bleiben unveraendert.
/// </summary>
public sealed class TrainingPdfReviewProtectedImportService
    : ITrainingPdfReviewImportService
{
    private readonly ITrainingPdfReviewImportService _inner;
    private readonly Func<TrainingPdfReviewProtectionSnapshot> _loadProtection;

    public TrainingPdfReviewProtectedImportService(
        ITrainingPdfReviewImportService inner,
        Func<TrainingPdfReviewProtectionSnapshot> loadProtection)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _loadProtection = loadProtection
                          ?? throw new ArgumentNullException(nameof(loadProtection));
    }

    public async Task<TrainingPdfReviewImportResult> ImportAsync(
        TrainingPdfReviewImportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var protection = await Task.Run(_loadProtection, cancellationToken)
            .ConfigureAwait(false)
                         ?? throw new InvalidDataException(
                             "Der Eval-Schutz lieferte keinen gueltigen Stand.");
        cancellationToken.ThrowIfCancellationRequested();
        var result = await _inner.ImportAsync(
                request with
                {
                    Protection = protection,
                },
                cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }
}
