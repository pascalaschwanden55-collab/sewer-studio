using AuswertungPro.Next.Application.Ai.Workbench;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.UseCases.PdfTrainingReview;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class TrainingPdfReviewBatchImportUseCaseTests
{
    [Fact]
    public async Task ImportFoldersAsync_liest_PDFs_sequenziell_und_aggregiert_ihre_Ergebnisse()
    {
        var discovery = new RecordingDiscovery(
            "eins.pdf",
            "zwei.pdf",
            "drei.pdf");
        var importer = new RecordingImporter(async (request, _) =>
        {
            await Task.Delay(15);
            return request.PdfPath switch
            {
                "eins.pdf" => PdfResult(
                    request.PdfPath,
                    sha256: Sha('1'),
                    detectedPhotos: 2,
                    matchedPhotos: 1,
                    protectedPhotos: 0,
                    Item("eins-1")),
                "zwei.pdf" => PdfResult(
                    request.PdfPath,
                    sha256: Sha('2'),
                    detectedPhotos: 3,
                    matchedPhotos: 2,
                    protectedPhotos: 1,
                    Item("zwei-1"),
                    Item("zwei-2")),
                _ => PdfResult(
                    request.PdfPath,
                    sha256: Sha('3'),
                    detectedPhotos: 1,
                    matchedPhotos: 1,
                    protectedPhotos: 0,
                    Item("drei-1"))
            };
        });
        var useCase = CreateUseCase(discovery, importer);

        var result = await useCase.ImportFoldersAsync(
            new TrainingPdfReviewBatchImportRequest(
                [@"D:\Haltungen A", @"E:\Haltungen B"],
                PipeDiameterMm: 400));

        Assert.Equal(
            ["eins.pdf", "zwei.pdf", "drei.pdf"],
            importer.Requests.Select(request => request.PdfPath));
        Assert.Equal(1, importer.MaximumConcurrentCalls);
        Assert.Equal(3, result.DiscoveredPdfCount);
        Assert.Equal(3, result.ReadPdfCount);
        Assert.Equal(0, result.FailedPdfCount);
        Assert.Equal(0, result.DuplicatePdfCount);
        Assert.Equal(6, result.DetectedPhotoCount);
        Assert.Equal(4, result.MatchedPhotoCount);
        Assert.Equal(1, result.ProtectedPhotoCount);
        Assert.Equal(
            ["eins-1", "zwei-1", "zwei-2", "drei-1"],
            result.Items.Select(item => item.CaseId));
        Assert.All(importer.Requests, request => Assert.Equal(400, request.PipeDiameterMm));
    }

    [Fact]
    public async Task ImportFoldersAsync_uebernimmt_identischen_PDF_Inhalt_nur_einmal()
    {
        var discovery = new RecordingDiscovery("original.pdf", "kopie.pdf");
        var importer = new RecordingImporter((request, _) =>
        {
            var item = request.PdfPath == "original.pdf"
                ? Item("aus-original")
                : Item("aus-kopie");
            return Task.FromResult(PdfResult(
                request.PdfPath,
                sha256: request.PdfPath == "original.pdf"
                    ? Sha('a')
                    : Sha('A'),
                detectedPhotos: 2,
                matchedPhotos: 1,
                protectedPhotos: 0,
                item));
        });
        var useCase = CreateUseCase(discovery, importer);

        var result = await useCase.ImportFoldersAsync(
            new TrainingPdfReviewBatchImportRequest([@"D:\Haltungen"], null));

        Assert.Equal(2, result.DiscoveredPdfCount);
        Assert.Equal(2, result.ReadPdfCount);
        Assert.Equal(0, result.FailedPdfCount);
        Assert.Equal(1, result.DuplicatePdfCount);
        Assert.Equal(2, result.DetectedPhotoCount);
        Assert.Equal(1, result.MatchedPhotoCount);
        Assert.Single(result.Items);
        Assert.Equal("aus-original", result.Items[0].CaseId);
    }

    [Fact]
    public async Task ImportFoldersAsync_setzt_nach_einem_PDF_Fehler_mit_den_uebrigen_Dateien_fort()
    {
        var discovery = new RecordingDiscovery(
            "gut-1.pdf",
            "defekt.pdf",
            "gut-2.pdf");
        var importer = new RecordingImporter((request, _) =>
        {
            if (request.PdfPath == "defekt.pdf")
                throw new UserFacingException("Test-PDF ist unlesbar.");

            return Task.FromResult(PdfResult(
                request.PdfPath,
                request.PdfPath == "gut-1.pdf" ? Sha('1') : Sha('2'),
                detectedPhotos: 1,
                matchedPhotos: 1,
                protectedPhotos: 0,
                Item(request.PdfPath)));
        });
        var useCase = CreateUseCase(discovery, importer);

        var result = await useCase.ImportFoldersAsync(
            new TrainingPdfReviewBatchImportRequest([@"D:\Haltungen"], 300));

        Assert.Equal(
            ["gut-1.pdf", "defekt.pdf", "gut-2.pdf"],
            importer.Requests.Select(request => request.PdfPath));
        Assert.Equal(3, result.DiscoveredPdfCount);
        Assert.Equal(2, result.ReadPdfCount);
        Assert.Equal(1, result.FailedPdfCount);
        Assert.Equal(2, result.Items.Count);
        Assert.Contains(
            result.Issues,
            issue => string.Equals(
                         issue.SourceDocumentName,
                         "defekt.pdf",
                         StringComparison.OrdinalIgnoreCase)
                     && issue.Message.Contains(
                         "unlesbar",
                         StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ImportFoldersAsync_laedt_Eval_Schutz_genau_einmal_und_bindet_denselben_Snapshot_an_alle_PDFs()
    {
        var discovery = new RecordingDiscovery("eins.pdf", "zwei.pdf", "drei.pdf");
        var importer = new RecordingImporter((request, _) =>
            Task.FromResult(PdfResult(
                request.PdfPath,
                request.PdfPath switch
                {
                    "eins.pdf" => Sha('1'),
                    "zwei.pdf" => Sha('2'),
                    _ => Sha('3')
                },
                detectedPhotos: 0,
                matchedPhotos: 0,
                protectedPhotos: 0)));
        var protection = ProtectionSnapshot();
        var protectionLoadCount = 0;
        ITrainingPdfReviewBatchImportUseCase useCase =
            new TrainingPdfReviewBatchImportUseCase(
                discovery,
                importer,
                () =>
                {
                    protectionLoadCount++;
                    return protection;
                });

        await useCase.ImportFoldersAsync(
            new TrainingPdfReviewBatchImportRequest([@"D:\Haltungen"], 250));

        Assert.Equal(1, protectionLoadCount);
        Assert.Equal(3, importer.Requests.Count);
        Assert.All(
            importer.Requests,
            request => Assert.Same(protection, request.Protection));
    }

    [Fact]
    public async Task ImportFoldersAsync_startet_bei_Schutz_Ladefehler_weder_Discovery_noch_PDF_Import()
    {
        var discovery = new RecordingDiscovery("darf-nicht-gelesen-werden.pdf");
        var importer = new RecordingImporter((request, _) =>
            Task.FromResult(PdfResult(
                request.PdfPath,
                Sha('f'),
                detectedPhotos: 0,
                matchedPhotos: 0,
                protectedPhotos: 0)));
        var expected = new InvalidDataException("Eval-Schutz konnte nicht geladen werden.");
        ITrainingPdfReviewBatchImportUseCase useCase =
            new TrainingPdfReviewBatchImportUseCase(
                discovery,
                importer,
                () => throw expected);

        var actual = await Assert.ThrowsAsync<InvalidDataException>(
            () => useCase.ImportFoldersAsync(
                new TrainingPdfReviewBatchImportRequest([@"D:\Haltungen"], null)));

        Assert.Same(expected, actual);
        Assert.Equal(0, discovery.CallCount);
        Assert.Empty(importer.Requests);
    }

    [Fact]
    public async Task ImportFoldersAsync_zeigt_keine_technischen_Exception_Details_und_behaelt_den_Quellpfad()
    {
        const string pdfPath = @"D:\Haltungen A\gleich.pdf";
        var discovery = new RecordingDiscovery(pdfPath);
        var importer = new RecordingImporter((_, _) =>
            throw new InvalidOperationException(
                @"Interner Parserfehler mit C:\geheim\diagnose.txt"));
        var useCase = CreateUseCase(discovery, importer);

        var result = await useCase.ImportFoldersAsync(
            new TrainingPdfReviewBatchImportRequest([@"D:\Haltungen A"], null));

        var issue = Assert.Single(result.Issues);
        Assert.Equal(pdfPath, issue.SourcePath);
        Assert.Equal("gleich.pdf", issue.SourceDocumentName);
        Assert.DoesNotContain("geheim", issue.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Programmlog", issue.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ImportFoldersAsync_uebernimmt_nach_Abbruch_keinen_Erfolg_eines_Token_ignorierenden_Imports()
    {
        using var cancellation = new CancellationTokenSource();
        var discovery = new RecordingDiscovery("eins.pdf");
        var importer = new RecordingImporter((request, _) =>
        {
            cancellation.Cancel();
            return Task.FromResult(PdfResult(
                request.PdfPath,
                Sha('1'),
                detectedPhotos: 1,
                matchedPhotos: 1,
                protectedPhotos: 0,
                Item("darf-nicht-uebernommen-werden")));
        });
        var useCase = CreateUseCase(discovery, importer);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => useCase.ImportFoldersAsync(
                new TrainingPdfReviewBatchImportRequest([@"D:\Haltungen"], null),
                cancellationToken: cancellation.Token));
    }

    private static ITrainingPdfReviewBatchImportUseCase CreateUseCase(
        ITrainingPdfFolderDiscoveryService discovery,
        ITrainingPdfReviewImportService importer)
        => new TrainingPdfReviewBatchImportUseCase(
            discovery,
            importer,
            ProtectionSnapshot);

    private static TrainingPdfReviewProtectionSnapshot ProtectionSnapshot()
        => new(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));

    private static TrainingPdfReviewImportResult PdfResult(
        string path,
        string sha256,
        int detectedPhotos,
        int matchedPhotos,
        int protectedPhotos,
        params WorkbenchItem[] items)
        => new(
            Path.GetFileName(path),
            sha256,
            "100-200",
            PageCount: 1,
            DetectedPhotoCount: detectedPhotos,
            MatchedPhotoCount: matchedPhotos,
            Items: items,
            Issues: Array.Empty<TrainingPdfReviewImportIssue>())
        {
            ProtectedPhotoCount = protectedPhotos
        };

    private static WorkbenchItem Item(string caseId)
        => new(
            FramePath: $"{caseId}.png",
            CaseId: caseId,
            MeterStart: 1,
            MeterEnd: 1,
            HaltungName: caseId,
            VideoPath: null,
            PipeDiameterMm: 300);

    private static string Sha(char value)
        => new(char.ToLowerInvariant(value), 64);

    private sealed class RecordingDiscovery(params string[] paths)
        : ITrainingPdfFolderDiscoveryService
    {
        public int CallCount { get; private set; }

        public TrainingPdfFolderDiscoveryResult Discover(
            IReadOnlyList<string> roots,
            CancellationToken cancellationToken)
        {
            CallCount++;
            cancellationToken.ThrowIfCancellationRequested();
            return new TrainingPdfFolderDiscoveryResult(
                paths,
                Array.Empty<TrainingPdfFolderDiscoveryIssue>());
        }
    }

    private sealed class RecordingImporter(
        Func<TrainingPdfReviewImportRequest, CancellationToken, Task<TrainingPdfReviewImportResult>> import)
        : ITrainingPdfReviewImportService
    {
        private readonly List<TrainingPdfReviewImportRequest> _requests = [];
        private int _activeCalls;

        public IReadOnlyList<TrainingPdfReviewImportRequest> Requests
        {
            get
            {
                lock (_requests)
                    return _requests.ToArray();
            }
        }

        public int MaximumConcurrentCalls { get; private set; }

        public async Task<TrainingPdfReviewImportResult> ImportAsync(
            TrainingPdfReviewImportRequest request,
            CancellationToken cancellationToken = default)
        {
            lock (_requests)
                _requests.Add(request);

            var active = Interlocked.Increment(ref _activeCalls);
            MaximumConcurrentCalls = Math.Max(MaximumConcurrentCalls, active);
            try
            {
                return await import(request, cancellationToken);
            }
            finally
            {
                Interlocked.Decrement(ref _activeCalls);
            }
        }
    }
}
