using System.Security.Cryptography;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Workbench;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.UseCases.PdfTrainingReview;
using AuswertungPro.Next.Infrastructure.Ai.Training.Services;
using AuswertungPro.Next.Infrastructure.Import.Pdf;

namespace AuswertungPro.Next.Infrastructure.Ai.Training.PdfReview;

/// <summary>
/// Importiert nur eindeutig verknuepfte PDF-Foto-/Operateurbefunde als
/// Workbench-Vorschlaege. Gold, KB und Teacher bleiben unberuehrt.
/// </summary>
public sealed partial class TrainingPdfReviewImportService : ITrainingPdfReviewImportService
{
    private readonly string _knowledgeRoot;
    private readonly ITrainingPdfReviewDocumentReader _documentReader;
    private readonly Func<string, CancellationToken, Task<IReadOnlyList<GroundTruthEntry>>> _readProtocolEntries;

    [GeneratedRegex(@"\d[\d.]*[-/]\d[\d.]*")]
    private static partial Regex HaltungIdRegex();

    [GeneratedRegex(
        @"\b(?:Haltung|Leitung|Haltungsnummer|Haltungs[- ]?Nr\.?)\b[ \t:.\-]{0,80}(?<id>\d[\d.]*[-/]\d[\d.]*)",
        RegexOptions.IgnoreCase)]
    private static partial Regex LabeledHaltungIdRegex();

    public TrainingPdfReviewImportService(string knowledgeRoot)
        : this(
            knowledgeRoot,
            new TrainingPdfReviewDocumentReader(),
            ReadProtocolEntriesAsync)
    {
    }

    public TrainingPdfReviewImportService(
        string knowledgeRoot,
        ITrainingPdfJpegColorNormalizer jpegColorNormalizer)
        : this(
            knowledgeRoot,
            new TrainingPdfReviewDocumentReader(
                jpegColorNormalizer
                ?? throw new ArgumentNullException(nameof(jpegColorNormalizer))),
            ReadProtocolEntriesAsync)
    {
    }

    internal TrainingPdfReviewImportService(
        string knowledgeRoot,
        ITrainingPdfReviewDocumentReader documentReader,
        Func<string, CancellationToken, Task<IReadOnlyList<GroundTruthEntry>>> readProtocolEntries)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(knowledgeRoot);
        _knowledgeRoot = Path.GetFullPath(knowledgeRoot);
        _documentReader = documentReader
                          ?? throw new ArgumentNullException(nameof(documentReader));
        _readProtocolEntries = readProtocolEntries
                               ?? throw new ArgumentNullException(nameof(readProtocolEntries));
    }

    private static Task<IReadOnlyList<GroundTruthEntry>> ReadProtocolEntriesAsync(
        string path,
        CancellationToken cancellationToken)
        => new PdfProtocolExtractor().ExtractAsync(
            path,
            framesDir: null,
            cancellationToken);

    public Task<TrainingPdfReviewImportResult> ImportAsync(
        TrainingPdfReviewImportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Task.Run(
            () => ImportCoreAsync(request, cancellationToken),
            cancellationToken);
    }

    private async Task<TrainingPdfReviewImportResult> ImportCoreAsync(
        TrainingPdfReviewImportRequest request,
        CancellationToken cancellationToken)
    {
        var sourcePath = ValidateSourcePath(request.PdfPath);
        RunUserVisibleValidation(
            () => PdfImportSafetyPolicy.ThrowIfFileTooLarge(sourcePath));
        var sourceName = Path.GetFileName(sourcePath);
        var sourceSha = await ComputeSha256Async(sourcePath, cancellationToken)
            .ConfigureAwait(false);

        var document = RunUserVisibleValidation(
            () => _documentReader.Read(sourcePath, cancellationToken));
        var protocolEntries = await _readProtocolEntries(sourcePath, cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var pathHaltungId = RunUserVisibleValidation(
            () => ResolvePathHaltungId(sourcePath));
        var protocolMetadata = RunUserVisibleValidation(
            () => TrainingPdfProtocolMetadataParser.ParseForPhotoImport(
                document.DocumentText,
                pathHaltungId));

        var sourceShaAfterRead = await ComputeSha256Async(sourcePath, cancellationToken)
            .ConfigureAwait(false);
        if (!string.Equals(sourceSha, sourceShaAfterRead, StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException(
                "Das PDF wurde waehrend des Imports veraendert. Es wurde nichts in die Pruefliste uebernommen.");
        }

        var haltungId = RunUserVisibleValidation(
            () => ResolveHaltungId(
                sourcePath,
                document.DocumentText,
                protocolMetadata.HaltungId));
        var issues = document.Issues
            .Select(message => new TrainingPdfReviewImportIssue(
                "image_read_error",
                message))
            .ToList();
        var items = new List<WorkbenchItem>();
        var distinctPhotoHaltungen = CollectDistinctHaltungIds(
            document.Photos
                .Where(photo => !photo.HasAmbiguousSectionHaltung)
                .Select(photo => photo.SectionHaltungId));
        var isMultiHaltungDocument =
            protocolMetadata.IsMultiHaltungDocument
            || distinctPhotoHaltungen.Count > 1;
        var matchedPhotos = 0;
        var protectedPhotos = 0;
        var stageRoot = ResolveStageRoot(sourceSha);
        var protection = request.Protection
                         ?? TrainingPdfReviewProtectionSnapshot.Empty;

        foreach (var photo in document.Photos)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (photo.HasAmbiguousSectionHaltung)
            {
                issues.Add(new TrainingPdfReviewImportIssue(
                    "ambiguous_haltung",
                    "Foto wurde ausgelassen, weil der PDF-Abschnitt mehrere Haltungs-IDs enthaelt.",
                    photo.PageNumber));
                continue;
            }

            var sectionHaltungId = TrainingPdfHaltungId.NormalizeForStorage(
                photo.SectionHaltungId);
            if (isMultiHaltungDocument && sectionHaltungId is null)
            {
                issues.Add(new TrainingPdfReviewImportIssue(
                    "ambiguous_haltung",
                    "Foto wurde ausgelassen, weil es in der Sammel-PDF keinem Haltungsabschnitt eindeutig zugeordnet ist.",
                    photo.PageNumber));
                continue;
            }

            var preferredSectionHaltungId = sectionHaltungId is null
                ? null
                : distinctPhotoHaltungen.FirstOrDefault(candidate =>
                    TrainingPdfHaltungId.AreEquivalent(
                        candidate,
                        sectionHaltungId)) ?? sectionHaltungId;
            var itemHaltungId = preferredSectionHaltungId is null
                ? haltungId
                : TrainingPdfHaltungId.AreEquivalent(
                    preferredSectionHaltungId,
                    haltungId)
                    ? TrainingPdfHaltungId.PreferCleanAlias(
                        preferredSectionHaltungId,
                        haltungId)!
                    : preferredSectionHaltungId;
            var protectionVerdict = EvalContaminationGuard.ClassifyForExport(
                protection.ImageHashes,
                protection.HoldingKeys,
                photo.ImageBytes,
                itemHaltungId);
            if (protectionVerdict !=
                EvalContaminationGuard.ExportContaminationResult.Clean)
            {
                protectedPhotos++;
                var isImageHash = protectionVerdict ==
                                  EvalContaminationGuard.ExportContaminationResult
                                      .EvalImageHash;
                issues.Add(new TrainingPdfReviewImportIssue(
                    isImageHash ? "eval_image_hash" : "eval_haltung",
                    isImageHash
                        ? "Foto wurde ausgelassen, weil seine Bilddaten zum eingefrorenen Mess-Set gehoeren."
                        : $"Foto wurde ausgelassen, weil die Haltung {itemHaltungId} zum eingefrorenen Mess-Set gehoert.",
                    photo.PageNumber));
                continue;
            }

            IReadOnlyList<GroundTruthEntry> photoEntries = protocolEntries;
            var photoMetadata = protocolMetadata;
            var matchingDocumentText = document.DocumentText;
            if (isMultiHaltungDocument)
            {
                matchingDocumentText = string.IsNullOrWhiteSpace(photo.SectionText)
                    ? photo.ContextText
                    : photo.SectionText;
                try
                {
                    photoMetadata = TrainingPdfProtocolMetadataParser.Parse(
                        matchingDocumentText);
                    photoEntries = CreateSectionEntries(photoMetadata);
                }
                catch (InvalidDataException)
                {
                    issues.Add(new TrainingPdfReviewImportIssue(
                        "ambiguous_haltung",
                        "Foto wurde ausgelassen, weil die Befunde seines Haltungsabschnitts nicht eindeutig sind.",
                        photo.PageNumber));
                    continue;
                }
            }

            var match = TrainingPdfPhotoFindingMatcher.Match(
                photo,
                photoEntries,
                matchingDocumentText,
                photoMetadata);
            if (match.Findings.Count == 0)
            {
                issues.Add(new TrainingPdfReviewImportIssue(
                    match.IssueCode ?? "unmatched",
                    match.IssueMessage ?? "Foto wurde nicht eindeutig zugeordnet.",
                    photo.PageNumber,
                    match.PhotoId));
                continue;
            }

            var stagedPath = StoreContentAddressed(
                stageRoot,
                photo.ImageBytes,
                photo.Extension);
            matchedPhotos++;
            foreach (var finding in match.Findings)
            {
                var sourceSuggestion = new WorkbenchSourceSuggestion(
                    finding.VsaCode,
                    finding.Beschreibung,
                    sourceName,
                    sourceSha,
                    photo.PageNumber,
                    finding.PhotoId,
                    finding.MatchKind)
                {
                    InspectionDate = photoMetadata.InspectionDate,
                };
                items.Add(new WorkbenchItem(
                    stagedPath,
                    itemHaltungId,
                    finding.MeterStart,
                    finding.MeterEnd,
                    HaltungName: itemHaltungId,
                    VideoPath: null,
                    PipeDiameterMm: request.PipeDiameterMm,
                    ExistingSampleId: null,
                    ExistingCode: null,
                    ExistingBeschreibung: null,
                    SuggestedMainCode: null,
                    IsStreckenschaden: finding.IsStreckenschaden,
                    SourceSuggestion: sourceSuggestion)
                {
                    InspectionDate = photoMetadata.InspectionDate,
                });
            }
        }

        return new TrainingPdfReviewImportResult(
            sourceName,
            sourceSha,
            haltungId,
            document.PageCount,
            document.Photos.Count,
            matchedPhotos,
            items,
            issues)
        {
            InspectionDate = protocolMetadata.InspectionDate,
            ProtectedPhotoCount = protectedPhotos,
        };
    }

    private static IReadOnlyList<GroundTruthEntry> CreateSectionEntries(
        TrainingPdfProtocolMetadata metadata)
        => metadata.Findings
            .Select(finding => new GroundTruthEntry
            {
                VsaCode = finding.VsaCode,
                Text = finding.Description,
                MeterStart = finding.MeterStart,
                MeterEnd = finding.MeterEnd,
                IsStreckenschaden = finding.IsStreckenschaden,
                Zeit = finding.ObservationTime,
            })
            .ToArray();

    private static IReadOnlyList<string> CollectDistinctHaltungIds(
        IEnumerable<string?> values)
    {
        var result = new List<string>();
        foreach (var value in values)
        {
            var normalized = TrainingPdfHaltungId.NormalizeForStorage(value);
            if (normalized is null)
                continue;

            var index = result.FindIndex(existing =>
                TrainingPdfHaltungId.AreEquivalent(existing, normalized));
            if (index < 0)
            {
                result.Add(normalized);
                continue;
            }

            result[index] = TrainingPdfHaltungId.PreferCleanAlias(
                result[index],
                normalized)!;
        }

        return result;
    }

    private string ResolveStageRoot(string sourceSha)
    {
        var root = Path.Combine(
            _knowledgeRoot,
            "training",
            "pdf_review_imports",
            sourceSha);
        EnsureInsideKnowledgeRoot(root);
        return root;
    }

    private string StoreContentAddressed(
        string stageRoot,
        byte[] bytes,
        string extension)
    {
        if (bytes.Length == 0)
            throw new InvalidDataException("Ein extrahiertes PDF-Foto ist leer.");

        var normalizedExtension = extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => ".jpg",
            ".png" => ".png",
            _ => throw new InvalidDataException(
                $"Nicht unterstuetztes PDF-Bildformat '{extension}'.")
        };
        var imageSha = Convert.ToHexStringLower(SHA256.HashData(bytes));
        Directory.CreateDirectory(stageRoot);
        EnsureInsideKnowledgeRoot(stageRoot);
        var target = Path.Combine(stageRoot, imageSha + normalizedExtension);
        EnsureInsideKnowledgeRoot(target);
        if (File.Exists(target))
        {
            var existingSha = ComputeSha256(target);
            if (!string.Equals(existingSha, imageSha, StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException(
                    "Ein vorhandenes PDF-Prueffoto hat unerwartet andere Bildbytes.");
            }

            return target;
        }

        var temp = Path.Combine(
            stageRoot,
            $".{imageSha}.{Guid.NewGuid():N}.tmp");
        EnsureInsideKnowledgeRoot(temp);
        try
        {
            File.WriteAllBytes(temp, bytes);
            try
            {
                File.Move(temp, target);
            }
            catch (IOException) when (File.Exists(target))
            {
                // Paralleler identischer Import: Das inhaltsadressierte Ziel gewinnt.
            }

            var storedSha = ComputeSha256(target);
            if (!string.Equals(storedSha, imageSha, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Das abgelegte PDF-Prueffoto ist nicht bytegleich.");
            return target;
        }
        finally
        {
            if (File.Exists(temp))
                File.Delete(temp);
        }
    }

    private static string ValidateSourcePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Bitte ein PDF-Protokoll waehlen.", nameof(path));
        var fullPath = Path.GetFullPath(path);
        if (!string.Equals(
                Path.GetExtension(fullPath),
                ".pdf",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Die gewaehlte Datei ist kein PDF-Protokoll.");
        }

        if (!File.Exists(fullPath))
            throw new FileNotFoundException("Das PDF-Protokoll wurde nicht gefunden.", fullPath);
        return fullPath;
    }

    private static string ResolveHaltungId(
        string pdfPath,
        string documentText,
        string? protocolHaltungId)
    {
        var primary = ResolvePathHaltungId(pdfPath);
        var protocol = TrainingPdfHaltungId.NormalizeForStorage(protocolHaltungId);
        if (protocol is not null)
        {
            // Der ausdrueckliche Protokollkopf ist fachlich staerker als ein
            // Ablage-/Dateiname. Bei blossen ".0"-Aliasen bleibt jedoch die
            // kuerzere, fuer den Benutzer bekannte Datei-/Ordner-ID erhalten.
            return primary is not null
                   && TrainingPdfHaltungId.AreEquivalent(primary, protocol)
                ? TrainingPdfHaltungId.PreferCleanAlias(primary, protocol)!
                : protocol;
        }

        // Freie Nummernpaare im PDF koennen Plan-, Telefon- oder Auftragsnummern
        // sein. Nur ausdruecklich mit Haltung/Leitung beschriftete Werte duerfen
        // den starken Datei-/Ordnerschluessel bestaetigen oder widerlegen.
        var documentIds = LabeledHaltungIdRegex()
            .Matches(documentText ?? string.Empty)
            .Select(match => TrainingPdfHaltungId.NormalizeForStorage(
                match.Groups["id"].Value))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Aggregate(
                new List<string>(),
                (unique, value) =>
                {
                    if (!unique.Any(existing =>
                            TrainingPdfHaltungId.AreEquivalent(existing, value)))
                    {
                        unique.Add(value);
                    }

                    return unique;
                })
            .ToArray();
        if (primary is null)
        {
            if (documentIds.Length != 1)
            {
                throw new InvalidDataException(
                    "Die Haltung im PDF ist nicht eindeutig. Import abgebrochen, damit Trainings- und Eval-Haltungen getrennt bleiben.");
            }

            return documentIds[0];
        }

        if (documentIds.Length == 1
            && !TrainingPdfHaltungId.AreEquivalent(primary, documentIds[0]))
        {
            throw new InvalidDataException(
                $"Haltungs-ID im PDF ({documentIds[0]}) passt nicht zum Dateinamen ({primary}).");
        }

        return primary;
    }

    private static string? ResolvePathHaltungId(string pdfPath)
    {
        var folderId = ExtractHaltungId(
            Path.GetFileName(Path.GetDirectoryName(pdfPath)));
        var fileId = TrainingPdfHaltungId.ExtractFromFileName(
            Path.GetFileNameWithoutExtension(pdfPath),
            folderId);
        if (fileId is not null
            && folderId is not null
            && !TrainingPdfHaltungId.AreEquivalent(fileId, folderId))
        {
            throw new InvalidDataException(
                $"Haltungs-ID in Dateiname ({fileId}) und Ordner ({folderId}) widersprechen sich.");
        }

        return TrainingPdfHaltungId.PreferCleanAlias(fileId, folderId);
    }

    private static T RunUserVisibleValidation<T>(Func<T> validation)
    {
        try
        {
            return validation();
        }
        catch (InvalidDataException ex)
        {
            // Diese Validierungen enthalten ausschliesslich bewusst formulierte
            // PDF-/Haltungsregeln. Der Nutzer soll die konkrete Ursache sehen,
            // waehrend unerwartete technische Fehler weiterhin verborgen und
            // nur im Programmlog protokolliert werden.
            throw new UserFacingException(ex.Message);
        }
    }

    private static void RunUserVisibleValidation(Action validation)
        => RunUserVisibleValidation(
            () =>
            {
                validation();
                return true;
            });

    private static string? ExtractHaltungId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return TrainingPdfHaltungId.Extract(value);
    }

    private void EnsureInsideKnowledgeRoot(string path)
    {
        var fullRoot = _knowledgeRoot.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(path);
        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            throw new IOException("PDF-Pruefdatei liegt ausserhalb des Wissensordners.");

        var relative = Path.GetRelativePath(_knowledgeRoot, fullPath);
        var current = _knowledgeRoot;
        foreach (var segment in relative.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (!Directory.Exists(current) && !File.Exists(current))
                continue;

            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException(
                    "PDF-Pruefablage enthaelt eine Verknuepfung ausserhalb des Wissensordners.");
            }
        }
    }

    private static async Task<string> ComputeSha256Async(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1024 * 128,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken)
            .ConfigureAwait(false);
        return Convert.ToHexStringLower(hash);
    }

    private static string ComputeSha256(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1024 * 128,
            FileOptions.SequentialScan);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }
}
