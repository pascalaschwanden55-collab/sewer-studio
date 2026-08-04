using System.Text.Json;
using System.Text.Json.Serialization;
using AuswertungPro.Next.Application.UseCases.PdfTrainingReview;
using AuswertungPro.Next.Infrastructure.Ai.Training.PdfReview;

namespace DetectReleaseHoldoutPdfExtractor;

internal sealed class ExtractionRunner
{
    private const string ReceiptFileName = "_pdf_extraction.json";
    private static readonly JsonSerializerOptions InputJsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private static readonly JsonSerializerOptions OutputJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    public async Task<int> RunAsync(string inputPath, CancellationToken cancellationToken)
    {
        var fullInputPath = PathSafety.RequireExistingFile(inputPath, "Auftragsdatei");
        if (!string.Equals(Path.GetExtension(fullInputPath), ".json", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Die Auftragsdatei muss eine JSON-Datei sein.");

        var inputBytes = await SafeFiles.ReadAllBytesLimitedAsync(
                fullInputPath,
                16 * 1024 * 1024,
                cancellationToken)
            .ConfigureAwait(false);
        var inputSha256 = Hashing.Sha256(inputBytes);
        var request = JsonSerializer.Deserialize<ExtractionRequest>(inputBytes, InputJsonOptions)
                      ?? throw new InvalidDataException("Die Auftragsdatei ist leer oder ungültig.");

        var knowledgeRoot = PathSafety.RequireExistingDirectory(
            request.KnowledgeRoot,
            "Wissensordner");
        var outputRoot = PathSafety.RequireExistingDirectory(
            request.OutputRoot,
            "Ausgabeordner");
        if (Directory.EnumerateFileSystemEntries(outputRoot).Any())
            throw new IOException("Der Ausgabeordner muss leer sein.");
        if (request.Pdfs is null || request.Pdfs.Count == 0)
            throw new InvalidDataException("Der Auftrag enthält keine PDFs.");

        var workRoot = Path.Combine(outputRoot, $".extract_work_{Guid.NewGuid():N}");
        var workImages = Path.Combine(workRoot, "images");
        Directory.CreateDirectory(workImages);
        PathSafety.RequireInside(outputRoot, workRoot, "Arbeitsordner");

        var importer = new TrainingPdfReviewImportService(knowledgeRoot);
        var images = new Dictionary<string, OutputImageBuilder>(StringComparer.OrdinalIgnoreCase);
        var pdfResults = new List<PdfExtractionResult>();
        var hadErrors = false;

        try
        {
            foreach (var pdf in request.Pdfs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                PreparedPdf prepared;
                try
                {
                    prepared = await PreparePdfAsync(
                            pdf,
                            importer,
                            knowledgeRoot,
                            request.FfmpegPath,
                            request.FfprobePath,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    hadErrors = true;
                    AddFailedPdf(pdfResults, pdf, ex);
                    continue;
                }

                try
                {
                    CommitPreparedImages(prepared.Images, images, workImages);
                }
                catch (InvalidDataException ex)
                {
                    // Herkunftskonflikte werden wie andere PDF-Fehler pro Datei
                    // gemeldet. Zu diesem Zeitpunkt wurde noch kein Bild kopiert.
                    hadErrors = true;
                    AddFailedPdf(pdfResults, pdf, ex);
                    continue;
                }

                pdfResults.Add(prepared.Result);
                if (prepared.Result.Status is "completed_with_issues" or "no_supported_items")
                    hadErrors = true;
                Console.WriteLine(
                    $"PDF geprüft: {prepared.Result.PdfName} – " +
                    $"{prepared.Result.AcceptedImageCount} Foto(s), " +
                    $"{prepared.Result.BackgroundImageCount} Hintergrundbild(er)");
            }

            cancellationToken.ThrowIfCancellationRequested();
            var finalImages = Path.Combine(outputRoot, "images");
            if (Directory.Exists(finalImages) || File.Exists(finalImages))
                throw new IOException("Der Zielordner 'images' ist unerwartet bereits vorhanden.");
            Directory.Move(workImages, finalImages);
            Directory.Delete(workRoot, recursive: false);

            var orderedImages = images.Values
                .Select(builder => builder.Build())
                .OrderBy(image => image.HoldingKey, StringComparer.Ordinal)
                .ThenBy(image => image.ImageSha256, StringComparer.Ordinal)
                .ToArray();
            var receipt = new ExtractionReceipt(
                SchemaVersion: "1.0",
                Purpose: "detect_release_holdout_pdf_extraction",
                CreatedAtUtc: DateTimeOffset.UtcNow,
                InputSha256: inputSha256,
                ModelPredictionsUsedForSelection: false,
                TrainingAllowed: false,
                GoldAllowed: false,
                Status: hadErrors ? "completed_with_errors" : "completed",
                PdfCount: request.Pdfs.Count,
                SuccessfulPdfCount: pdfResults.Count(result => result.Status != "failed"),
                FailedPdfCount: pdfResults.Count(result => result.Status == "failed"),
                ImageCount: orderedImages.Length,
                Pdfs: pdfResults,
                Images: orderedImages);

            var receiptPath = Path.Combine(outputRoot, ReceiptFileName);
            await SafeFiles.WriteJsonAtomicallyAsync(
                    outputRoot,
                    receiptPath,
                    receipt,
                    OutputJsonOptions,
                    cancellationToken)
                .ConfigureAwait(false);
            Console.WriteLine($"Prüfbeleg: {receiptPath}");
            return hadErrors ? 2 : 0;
        }
        finally
        {
            // Nur den von diesem Lauf eindeutig erzeugten, noch unveröffentlichten
            // Arbeitsordner entfernen. Fertige Bilder oder Kundenquellen bleiben unberührt.
            SafeFiles.TryDeleteOwnedWorkDirectory(outputRoot, workRoot);
        }
    }

    private static void AddFailedPdf(
        ICollection<PdfExtractionResult> results,
        PdfInput pdf,
        Exception exception)
    {
        var safeName = SafeFiles.SafeLeafName(pdf.Path, "unbekanntes_pdf.pdf");
        var expectedHash = InputValidation.TryNormalizeSha256(pdf.ResolveExpectedPdfSha256());
        var expectedHolding = InputValidation.TryNormalizeHolding(pdf.ResolveExpectedHoldingKey());
        var message = SafeFiles.HidePath(exception.Message, pdf.Path);
        results.Add(PdfExtractionResult.Failed(
            safeName,
            expectedHash,
            expectedHolding,
            ErrorCodes.For(exception),
            message));
        Console.Error.WriteLine($"PDF ausgelassen: {safeName} – {message}");
    }

    private static async Task<PreparedPdf> PreparePdfAsync(
        PdfInput pdf,
        ITrainingPdfReviewImportService importer,
        string knowledgeRoot,
        string? configuredFfmpeg,
        string? configuredFfprobe,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pdf);
        var pdfPath = PathSafety.RequireExistingFile(pdf.Path, "PDF-Protokoll");
        if (!string.Equals(Path.GetExtension(pdfPath), ".pdf", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Die angegebene Quelldatei ist kein PDF-Protokoll.");
        var pdfName = Path.GetFileName(pdfPath);
        var expectedSha = InputValidation.RequireSha256(
            pdf.ResolveExpectedPdfSha256(),
            "pdf_sha256");
        var expectedHolding = InputValidation.RequireHolding(
            pdf.ResolveExpectedHoldingKey(),
            "haltung_key");

        var sourceShaBefore = await Hashing.ComputeFileSha256Async(pdfPath, cancellationToken)
            .ConfigureAwait(false);
        if (!string.Equals(sourceShaBefore, expectedSha, StringComparison.Ordinal))
            throw new InvalidDataException("Der PDF-Hash stimmt vor dem Import nicht mit dem Auftrag überein.");

        var imported = await importer.ImportAsync(
                new TrainingPdfReviewImportRequest(pdfPath, PipeDiameterMm: null)
                {
                    Protection = TrainingPdfReviewProtectionSnapshot.Empty,
                },
                cancellationToken)
            .ConfigureAwait(false);

        var sourceShaAfterImport = await Hashing.ComputeFileSha256Async(pdfPath, cancellationToken)
            .ConfigureAwait(false);
        if (!string.Equals(sourceShaAfterImport, expectedSha, StringComparison.Ordinal))
            throw new IOException("Das PDF wurde während des Imports verändert.");
        if (!string.Equals(imported.SourceDocumentSha256, expectedSha, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Der Importbeleg nennt nicht den erwarteten PDF-Hash.");
        if (!string.Equals(
                InputValidation.RequireHolding(imported.HaltungId, "importierte Haltung"),
                expectedHolding,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException("Die Haltung aus dem PDF stimmt nicht mit dem Auftrag überein.");
        }

        var issues = imported.Issues
            .Select(issue => new OutputIssue(
                issue.ReasonCode,
                issue.Message,
                issue.PageNumber,
                issue.PhotoId))
            .ToList();
        var supported = new List<SupportedWorkbenchItem>();
        foreach (var item in imported.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (item.SourceSuggestion is null)
            {
                issues.Add(new OutputIssue(
                    "missing_source_suggestion",
                    "Ein Arbeitsvorschlag ohne Operateurreferenz wurde ausgelassen."));
                continue;
            }

            if (!DetectClassMap.TryResolve(item.SourceSuggestion.VsaCode, out var detectClass))
            {
                issues.Add(new OutputIssue(
                    "unsupported_main_code",
                    $"Der Operateurcode '{item.SourceSuggestion.VsaCode}' gehört nicht zu den 15 Detect-Klassen.",
                    item.SourceSuggestion.PageNumber,
                    item.SourceSuggestion.PhotoId));
                continue;
            }

            var itemHolding = InputValidation.RequireHolding(item.CaseId, "Foto-Haltung");
            if (!string.Equals(itemHolding, expectedHolding, StringComparison.Ordinal))
                throw new InvalidDataException("Ein PDF-Foto gehört nicht zur erwarteten Haltung.");
            if (!string.Equals(
                    item.SourceSuggestion.SourceDocumentSha256,
                    expectedSha,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Eine Operateurreferenz ist nicht an das erwartete PDF gebunden.");
            }

            supported.Add(new SupportedWorkbenchItem(item, detectClass));
        }

        var preparedImages = new List<PreparedImage>();
        foreach (var group in supported.GroupBy(
                     entry => Path.GetFullPath(entry.Item.FramePath),
                     StringComparer.OrdinalIgnoreCase))
        {
            var snapshot = await ReadImportedFrameAsync(
                    group.Key,
                    knowledgeRoot,
                    expectedSha,
                    cancellationToken)
                .ConfigureAwait(false);
            var references = group
                .Select(entry => CreateReference(entry.Item, entry.DetectClass))
                .Distinct()
                .ToArray();
            if (references.Length == 0)
                continue;

            preparedImages.Add(new PreparedImage(
                snapshot.Bytes,
                snapshot.Extension,
                snapshot.Sha256,
                snapshot.Width,
                snapshot.Height,
                expectedHolding,
                HoldingKeys.Physical(expectedHolding),
                "operator_pdf_photo",
                pdfName,
                expectedSha,
                references,
                Video: null));
        }

        var pdfImageCount = preparedImages.Count;
        var backgroundCount = 0;
        if (!string.IsNullOrWhiteSpace(pdf.VideoPath) || pdf.BackgroundFraction is not null)
        {
            try
            {
                var videoImage = await DeterministicVideoFrameExtractor.ExtractAsync(
                        pdf.VideoPath,
                        pdf.BackgroundFraction,
                        expectedHolding,
                        configuredFfmpeg,
                        configuredFfprobe,
                        cancellationToken)
                    .ConfigureAwait(false);
                videoImage = videoImage with
                {
                    SourcePdfName = pdfName,
                    SourcePdfSha256 = expectedSha,
                };
                preparedImages.Add(videoImage);
                backgroundCount = 1;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                issues.Add(new OutputIssue(
                    "video_frame_error",
                    $"Der feste Video-Hintergrundframe wurde nicht übernommen: {SafeFiles.HidePath(ex.Message, pdf.VideoPath)}"));
            }
        }

        var sourceShaAfterPreparation = await Hashing.ComputeFileSha256Async(pdfPath, cancellationToken)
            .ConfigureAwait(false);
        if (!string.Equals(sourceShaAfterPreparation, expectedSha, StringComparison.Ordinal))
            throw new IOException("Das PDF wurde während der Bildvorbereitung verändert.");

        var status = preparedImages.Count == 0
            ? "no_supported_items"
            : issues.Count > 0
                ? "completed_with_issues"
                : "completed";
        return new PreparedPdf(
            preparedImages,
            new PdfExtractionResult(
                PdfName: pdfName,
                PdfSha256: expectedSha,
                HoldingKey: expectedHolding,
                Status: status,
                PageCount: imported.PageCount,
                DetectedPhotoCount: imported.DetectedPhotoCount,
                MatchedPhotoCount: imported.MatchedPhotoCount,
                AcceptedImageCount: pdfImageCount,
                BackgroundImageCount: backgroundCount,
                Issues: issues,
                ErrorCode: null,
                ErrorMessage: null));
    }

    private static async Task<ImageSnapshot> ReadImportedFrameAsync(
        string framePath,
        string knowledgeRoot,
        string expectedPdfSha,
        CancellationToken cancellationToken)
    {
        var expectedStageRoot = Path.Combine(
            knowledgeRoot,
            "training",
            "pdf_review_imports",
            expectedPdfSha);
        PathSafety.RequireInside(expectedStageRoot, framePath, "PDF-Prüffoto");
        var safePath = PathSafety.RequireExistingFile(framePath, "PDF-Prüffoto");
        var bytes = await SafeFiles.ReadAllBytesLimitedAsync(
                safePath,
                100 * 1024 * 1024,
                cancellationToken)
            .ConfigureAwait(false);
        var sha = Hashing.Sha256(bytes);
        var fileNameHash = Path.GetFileNameWithoutExtension(safePath);
        if (!string.Equals(fileNameHash, sha, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Ein PDF-Prüffoto ist nicht mehr inhaltsadressiert.");
        var image = ImageHeaders.Read(bytes);
        return new ImageSnapshot(bytes, image.Extension, sha, image.Width, image.Height);
    }

    private static OperatorReference CreateReference(
        AuswertungPro.Next.Application.Ai.Workbench.WorkbenchItem item,
        DetectClass detectClass)
    {
        var source = item.SourceSuggestion
                     ?? throw new InvalidDataException("Operateurreferenz fehlt.");
        return new OperatorReference(
            SourcePdfName: source.SourceDocumentName,
            SourcePdfSha256: source.SourceDocumentSha256.ToLowerInvariant(),
            PageNumber: source.PageNumber,
            PhotoId: source.PhotoId,
            VsaCode: source.VsaCode.Trim().ToUpperInvariant(),
            MainCode: detectClass.MainCode,
            DetectClassId: detectClass.Id,
            DetectClassName: detectClass.Name,
            FindingText: source.Beschreibung?.Trim() ?? string.Empty,
            MatchKind: source.MatchKind,
            MeterStart: item.MeterStart,
            MeterEnd: item.MeterEnd,
            IsStreckenschaden: item.IsStreckenschaden);
    }

    private static void CommitPreparedImages(
        IReadOnlyList<PreparedImage> prepared,
        IDictionary<string, OutputImageBuilder> global,
        string workImages)
    {
        var local = new Dictionary<string, OutputImageBuilder>(StringComparer.OrdinalIgnoreCase);
        foreach (var image in prepared)
        {
            if (local.TryGetValue(image.Sha256, out var duplicate))
            {
                duplicate.Merge(image);
                continue;
            }

            var builder = new OutputImageBuilder(image);
            if (global.TryGetValue(image.Sha256, out var existing))
                existing.EnsureCompatible(image);
            local.Add(image.Sha256, builder);
        }

        foreach (var pair in local)
        {
            if (global.TryGetValue(pair.Key, out var existing))
            {
                existing.Merge(pair.Value);
                continue;
            }

            var image = pair.Value;
            var relativePath = Path.Combine("images", image.Sha256 + image.Extension);
            var target = Path.Combine(workImages, image.Sha256 + image.Extension);
            PathSafety.RequireInside(workImages, target, "extrahiertes Bild");
            SafeFiles.WriteNewFileVerified(target, image.Bytes, image.Sha256);
            image.RelativePath = relativePath.Replace('\\', '/');
            global.Add(pair.Key, image);
        }
    }
}
