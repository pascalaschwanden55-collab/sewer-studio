using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using AuswertungPro.Next.Application.Dossiers;
using AuswertungPro.Next.Application.Reports;

using UglyToad.PdfPig;

namespace AuswertungPro.Next.Infrastructure.Dossiers;

/// <summary>
/// Baut die Vorschau über genau denselben Word-Export und PDF-Wandler wie die
/// wirkliche Ausgabe. Alle Arbeitsdateien liegen in einem eigenen Temp-Ordner.
/// </summary>
public sealed class DossierOutputPreviewService : IDossierOutputPreviewService
{
    private readonly IDossierWordExportService _wordExport;
    private readonly Func<string, string?, bool> _convertWordToPdf;
    private readonly Func<byte[], IReadOnlyList<string>, string, byte[]> _composePdfPackage;
    private readonly Func<string, IReadOnlyList<DossierOutputPreviewPage>> _readPages;
    private readonly Func<string> _createWorkRoot;
    private readonly Func<
        DossierExportRequest,
        string,
        CancellationToken,
        Task<DossierAttachmentResult>>? _collectPreviewAttachments;

    public DossierOutputPreviewService(
        IDossierWordExportService wordExport,
        IPdfMergeService pdfMerge)
        : this(
            wordExport,
            DossierWordPdfConverter.TryConvertToPdf,
            CreateDefaultPackageComposer(pdfMerge),
            ReadPages,
            CreateWorkRoot,
            collectPreviewAttachments: null)
    {
        ArgumentNullException.ThrowIfNull(pdfMerge);
    }

    internal DossierOutputPreviewService(
        IDossierWordExportService wordExport,
        DossierPdfPackageComposer packageComposer,
        IDossierPreviewAttachmentService previewAttachments)
        : this(
            wordExport,
            DossierWordPdfConverter.TryConvertToPdf,
            UsePackageComposer(packageComposer),
            ReadPages,
            CreateWorkRoot,
            CreatePreviewAttachmentCollector(previewAttachments))
    {
    }

    public DossierOutputPreviewService(
        IDossierWordExportService wordExport,
        IPdfMergeService pdfMerge,
        IDossierPreviewAttachmentService previewAttachments)
        : this(
            wordExport,
            DossierWordPdfConverter.TryConvertToPdf,
            CreateDefaultPackageComposer(pdfMerge),
            ReadPages,
            CreateWorkRoot,
            CreatePreviewAttachmentCollector(previewAttachments))
    {
        ArgumentNullException.ThrowIfNull(pdfMerge);
    }

    internal DossierOutputPreviewService(
        IDossierWordExportService wordExport,
        Func<string, string?, bool> convertWordToPdf,
        Func<string, IReadOnlyList<DossierOutputPreviewPage>> readPages,
        Func<string> createWorkRoot)
        : this(
            wordExport,
            convertWordToPdf,
            (generated, _, _) => generated,
            readPages,
            createWorkRoot,
            collectPreviewAttachments: null)
    {
    }

    internal DossierOutputPreviewService(
        IDossierWordExportService wordExport,
        Func<string, string?, bool> convertWordToPdf,
        Func<byte[], IReadOnlyList<string>, byte[]> mergePdfs,
        Func<string, IReadOnlyList<DossierOutputPreviewPage>> readPages,
        Func<string> createWorkRoot,
        Func<
            DossierExportRequest,
            string,
            CancellationToken,
            Task<DossierAttachmentResult>>? collectPreviewAttachments = null)
        : this(
            wordExport,
            convertWordToPdf,
            (generated, attachments, _) => mergePdfs(generated, attachments),
            readPages,
            createWorkRoot,
            collectPreviewAttachments)
    {
        ArgumentNullException.ThrowIfNull(mergePdfs);
    }

    internal DossierOutputPreviewService(
        IDossierWordExportService wordExport,
        Func<string, string?, bool> convertWordToPdf,
        Func<byte[], IReadOnlyList<string>, string, byte[]> composePdfPackage,
        Func<string, IReadOnlyList<DossierOutputPreviewPage>> readPages,
        Func<string> createWorkRoot,
        Func<
            DossierExportRequest,
            string,
            CancellationToken,
            Task<DossierAttachmentResult>>? collectPreviewAttachments)
    {
        _wordExport = wordExport ?? throw new ArgumentNullException(nameof(wordExport));
        _convertWordToPdf = convertWordToPdf
            ?? throw new ArgumentNullException(nameof(convertWordToPdf));
        _composePdfPackage = composePdfPackage
            ?? throw new ArgumentNullException(nameof(composePdfPackage));
        _readPages = readPages ?? throw new ArgumentNullException(nameof(readPages));
        _createWorkRoot = createWorkRoot ?? throw new ArgumentNullException(nameof(createWorkRoot));
        _collectPreviewAttachments = collectPreviewAttachments;
    }

    public Task<DossierOutputPreviewResult> CreateAsync(
        DossierExportRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();

        // Word-Automation braucht einen STA-Thread. Gleichzeitig bleibt das
        // Vorschaufenster während der Umwandlung bedienbar.
        return RunStaAsync(() => CreateCoreAsync(request, ct), ct);
    }

    private async Task<DossierOutputPreviewResult> CreateCoreAsync(
        DossierExportRequest request,
        CancellationToken ct)
    {
        var workRoot = Path.GetFullPath(_createWorkRoot());

        try
        {
            Directory.CreateDirectory(workRoot);
            var outputFolder = Path.Combine(workRoot, "Ausgabe");

            // Ein relativer Planpfad gehört zum echten Projekt. Würde nur der
            // Projektroot auf den Temp-Ordner zeigen, verschwände der Plan aus
            // der Vorschau. Darum wird nur die Arbeitskopie absolut gemacht.
            var dossier = DossierDeepCopy.Of(request.Dossier);
            dossier.OverviewPlanPath = DossierWordTemplateExportService.ResolvePlanPath(request)
                ?? string.Empty;

            var previewRequest = request with
            {
                ProjectRoot = workRoot,
                TargetFolder = outputFolder,
                Area = DossierDeepCopy.Of(request.Area),
                Dossier = dossier
            };

            var word = await _wordExport.ExportAsync(previewRequest, ct).ConfigureAwait(false);
            if (!word.Success || string.IsNullOrWhiteSpace(word.FilePath) || !File.Exists(word.FilePath))
            {
                return Failed("Die Ausgabevorschau konnte keine Word-Datei erzeugen. " + word.Message);
            }

            ct.ThrowIfCancellationRequested();
            var pdfPath = Path.Combine(workRoot, "Dossier-Vorschau.pdf");
            if (!_convertWordToPdf(word.FilePath, pdfPath) || !File.Exists(pdfPath))
            {
                return Failed(
                    "Die Ausgabevorschau konnte die Word-Datei nicht in ein PDF umwandeln. "
                    + "Dafür wird Microsoft Word oder LibreOffice benötigt.");
            }

            ct.ThrowIfCancellationRequested();

            // Im produktiven Weg wird der echte Beilagenstand zuerst in den
            // Temp-Ordner kopiert. Dort sammelt derselbe Fachdienst die aktuell
            // gewählten Haltungen und Schächte neu. So bleibt der Kundenordner
            // unverändert und die Vorschau zeigt trotzdem den aktuellen Stand.
            DossierAttachmentResult? collectedAttachments = null;
            IReadOnlyList<string> attachmentPaths;
            if (_collectPreviewAttachments is null)
            {
                // Kompatibilitätsweg für bestehende direkte Konstruktoraufrufe.
                attachmentPaths = DossierPdfAssemblyService
                    .CollectAttachmentPdfs(request.TargetFolder);
            }
            else
            {
                var temporaryDossierFolder = Path.Combine(workRoot, "Gesamt-PDF");
                collectedAttachments = await _collectPreviewAttachments(
                        request,
                        temporaryDossierFolder,
                        ct)
                    .ConfigureAwait(false);
                attachmentPaths = DossierPdfAssemblyService
                    .CollectAttachmentPdfs(temporaryDossierFolder);
            }
            var wordPages = _readPages(pdfPath);

            // Die benannten Feldziele stehen im KATALOG der Word-PDF, nicht in ihren
            // Seiten. Das Zusammenfuehren der Beilagen kopiert nur Seiten - danach
            // sind sie weg. Deshalb werden sie hier gelesen, VOR den Beilagen.
            // Die Seitenzahlen bleiben gueltig, weil die Word-Seiten im
            // Gesamtdokument vorne stehen; genau darauf stuetzt sich auch das
            // IsAttachment-Kennzeichen weiter unten.
            var wordPdfBytes = await File.ReadAllBytesAsync(pdfPath, ct).ConfigureAwait(false);
            var anchors = DossierPdfFieldAnchorReader.Read(wordPdfBytes);

            // Der feste Erklaeranhang steht immer vor den normalen Beilagen. Darum wird
            // auch ohne ein einziges Protokoll zusammengefuehrt.
            var mergedBytes = _composePdfPackage(wordPdfBytes, attachmentPaths, workRoot);
            var previewPdfPath = Path.Combine(workRoot, "Dossier-Vorschau-komplett.pdf");
            File.WriteAllBytes(previewPdfPath, mergedBytes);
            var firstExplanationPageNumber = wordPages.Count + 1;
            var lastExplanationPageNumber = wordPages.Count
                + DossierConditionClassDefinitions.PdfRequiredPageCount;
            IReadOnlyList<DossierOutputPreviewPage> pages = _readPages(previewPdfPath)
                .Select(page => page with
                {
                    IsAttachment = page.Number > wordPages.Count,
                    IsConditionClassExplanation = page.Number >= firstExplanationPageNumber
                        && page.Number <= lastExplanationPageNumber
                        && page.Text.Contains(
                            DossierConditionClassDefinitions.PdfRequiredPageMarker,
                            StringComparison.Ordinal)
                })
                .ToList();

            if (pages.Count == 0)
                return Failed("Die erzeugte Ausgabevorschau enthält keine Seite.");

            var bytes = await File.ReadAllBytesAsync(previewPdfPath, ct).ConfigureAwait(false);
            var attachmentNote = attachmentPaths.Count == 0
                ? " Einschliesslich Erkläranhang."
                : attachmentPaths.Count == 1
                    ? " Einschliesslich Erkläranhang und 1 Beilage."
                    : $" Einschliesslich Erkläranhang und {attachmentPaths.Count} Beilagen.";
            var missingCount = (collectedAttachments?.MissingCount ?? 0)
                + request.Snapshot.MissingHoldingIds.Count
                + request.Snapshot.MissingShaftNumbers.Count;
            var missingNote = missingCount == 0
                ? string.Empty
                : missingCount == 1
                    ? " Ein ausgewähltes Protokoll fehlt."
                    : $" {missingCount} ausgewählte Protokolle fehlen.";
            return new DossierOutputPreviewResult(
                true,
                bytes,
                pages,
                pages.Count == 1
                    ? "Ausgabevorschau aktualisiert: 1 Seite." + attachmentNote + missingNote
                    : $"Ausgabevorschau aktualisiert: {pages.Count} Seiten."
                      + attachmentNote
                      + missingNote,
                anchors);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Failed("Die Ausgabevorschau konnte nicht erstellt werden: " + ex.Message);
        }
        finally
        {
            TryDeleteWorkRoot(workRoot);
        }
    }

    private static IReadOnlyList<DossierOutputPreviewPage> ReadPages(string pdfPath)
    {
        using var document = PdfDocument.Open(pdfPath);
        var pages = new List<DossierOutputPreviewPage>(document.NumberOfPages);

        foreach (var page in document.GetPages())
        {
            var words = page.GetWords()
                .Select(word => new DossierOutputPreviewWord(
                    word.Text,
                    word.BoundingBox.Left,
                    word.BoundingBox.Bottom,
                    word.BoundingBox.Right,
                    word.BoundingBox.Top))
                .ToList();

            pages.Add(new DossierOutputPreviewPage(
                page.Number,
                page.Width,
                page.Height,
                page.Text,
                words));
        }

        return pages;
    }

    private static DossierOutputPreviewResult Failed(string message)
        => new(false, null, Array.Empty<DossierOutputPreviewPage>(), message);

    private static Func<
        DossierExportRequest,
        string,
        CancellationToken,
        Task<DossierAttachmentResult>> CreatePreviewAttachmentCollector(
            IDossierPreviewAttachmentService previewAttachments)
    {
        ArgumentNullException.ThrowIfNull(previewAttachments);
        return previewAttachments.CollectIntoTemporaryAsync;
    }

    private static Func<byte[], IReadOnlyList<string>, string, byte[]> CreateDefaultPackageComposer(
        IPdfMergeService pdfMerge)
    {
        ArgumentNullException.ThrowIfNull(pdfMerge);
        var composer = new DossierPdfPackageComposer(
            pdfMerge,
            DossierConditionClassPdfService.Shared);
        return composer.Compose;
    }

    private static Func<byte[], IReadOnlyList<string>, string, byte[]> UsePackageComposer(
        DossierPdfPackageComposer packageComposer)
    {
        ArgumentNullException.ThrowIfNull(packageComposer);
        return packageComposer.Compose;
    }

    private static string CreateWorkRoot()
        => Path.Combine(
            Path.GetTempPath(),
            "SewerStudio_DossierPreview_" + Guid.NewGuid().ToString("N"));

    private static void TryDeleteWorkRoot(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            var tempRoot = Path.GetFullPath(Path.GetTempPath());
            var tempPrefix = Path.EndsInDirectorySeparator(tempRoot)
                ? tempRoot
                : tempRoot + Path.DirectorySeparatorChar;

            if (!fullPath.StartsWith(tempPrefix, StringComparison.OrdinalIgnoreCase))
                return;

            if (Directory.Exists(fullPath))
                Directory.Delete(fullPath, recursive: true);
        }
        catch
        {
            // Ein eigener liegen gebliebener Temp-Ordner verändert kein Projekt.
        }
    }

    private static Task<T> RunStaAsync<T>(Func<Task<T>> action, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (!OperatingSystem.IsWindows())
            return Task.Run(action, ct);

        var completion = new TaskCompletionSource<T>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                completion.TrySetResult(action().GetAwaiter().GetResult());
            }
            catch (OperationCanceledException ex)
            {
                completion.TrySetCanceled(ex.CancellationToken);
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        })
        {
            IsBackground = true,
            Name = "Dossier-Ausgabevorschau"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }
}
