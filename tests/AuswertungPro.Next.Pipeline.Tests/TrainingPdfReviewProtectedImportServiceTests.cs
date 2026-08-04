using AuswertungPro.Next.Application.UseCases.PdfTrainingReview;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class TrainingPdfReviewProtectedImportServiceTests
{
    [Fact]
    public async Task ImportAsync_bindet_aktuellen_Schutzstand_an_den_Einzelimport()
    {
        var protection = new TrainingPdfReviewProtectionSnapshot(
            [new string('a', 64)],
            ["100-200"]);
        TrainingPdfReviewImportRequest? received = null;
        var inner = new DelegatingImportService((request, _) =>
        {
            received = request;
            return Task.FromResult(EmptyResult());
        });
        var service = new TrainingPdfReviewProtectedImportService(
            inner,
            () => protection);

        await service.ImportAsync(
            new TrainingPdfReviewImportRequest("haltung.pdf", 300));

        Assert.NotNull(received);
        Assert.Same(protection, received.Protection);
    }

    [Fact]
    public async Task ImportAsync_startet_bei_Schutzfehler_keinen_Einzelimport()
    {
        var importCalls = 0;
        var inner = new DelegatingImportService((_, _) =>
        {
            importCalls++;
            return Task.FromResult(EmptyResult());
        });
        var expected = new InvalidDataException("Eval-Schutz unlesbar.");
        var service = new TrainingPdfReviewProtectedImportService(
            inner,
            () => throw expected);

        var actual = await Assert.ThrowsAsync<InvalidDataException>(
            () => service.ImportAsync(
                new TrainingPdfReviewImportRequest("haltung.pdf", null)));

        Assert.Same(expected, actual);
        Assert.Equal(0, importCalls);
    }

    [Fact]
    public async Task ImportAsync_startet_nach_Abbruch_beim_Schutzladen_keinen_Einzelimport()
    {
        using var cancellation = new CancellationTokenSource();
        var importCalls = 0;
        var inner = new DelegatingImportService((_, _) =>
        {
            importCalls++;
            return Task.FromResult(EmptyResult());
        });
        var service = new TrainingPdfReviewProtectedImportService(
            inner,
            () =>
            {
                cancellation.Cancel();
                return TrainingPdfReviewProtectionSnapshot.Empty;
            });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ImportAsync(
                new TrainingPdfReviewImportRequest("haltung.pdf", null),
                cancellation.Token));

        Assert.Equal(0, importCalls);
    }

    private static TrainingPdfReviewImportResult EmptyResult()
        => new(
            "haltung.pdf",
            new string('a', 64),
            "100-200",
            1,
            0,
            0,
            [],
            []);

    private sealed class DelegatingImportService(
        Func<TrainingPdfReviewImportRequest, CancellationToken,
            Task<TrainingPdfReviewImportResult>> import)
        : ITrainingPdfReviewImportService
    {
        public Task<TrainingPdfReviewImportResult> ImportAsync(
            TrainingPdfReviewImportRequest request,
            CancellationToken cancellationToken = default)
            => import(request, cancellationToken);
    }
}
