using AuswertungPro.Next.Application.UseCases.PdfTrainingReview;
using AuswertungPro.Next.UI.ViewModels;

namespace AuswertungPro.Next.UI.Tests.Ai.Workbench;

public sealed class TrainingStudioPdfImportPresentationTests
{
    [Fact]
    public void FormatSingle_benennt_geschuetzte_Fotos_nicht_als_unsicher()
    {
        var result = new TrainingPdfReviewImportResult(
            "haltung.pdf",
            new string('a', 64),
            "100-200",
            1,
            DetectedPhotoCount: 2,
            MatchedPhotoCount: 1,
            Items: [],
            Issues:
            [
                new TrainingPdfReviewImportIssue(
                    "eval_haltung",
                    "Foto gehoert zum Mess-Set.",
                    1),
            ])
        {
            ProtectedPhotoCount = 1,
        };

        var text = TrainingStudioPdfImportPresentation.FormatSingle(result);

        Assert.Contains("1 Prüfbestandsfoto geschützt übersprungen", text);
        Assert.DoesNotContain("unsicher", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FormatBatch_zeigt_PDF_Fehler_Dubletten_und_Schutz_geschlossen_an()
    {
        var result = new TrainingPdfReviewBatchImportResult(
            RequestedFolderCount: 2,
            DiscoveredPdfCount: 5,
            ReadPdfCount: 4,
            FailedPdfCount: 1,
            DuplicatePdfCount: 1,
            DetectedPhotoCount: 8,
            MatchedPhotoCount: 5,
            ProtectedPhotoCount: 2,
            Items: [],
            Issues:
            [
                new TrainingPdfReviewBatchIssue(
                    "pdf_import_failed",
                    "defekt.pdf: unlesbar"),
            ]);

        var text = TrainingStudioPdfImportPresentation.FormatBatch(result);

        Assert.Contains("4 von 5 PDFs gelesen", text);
        Assert.Contains("1 PDF fehlerhaft", text);
        Assert.Contains("1 doppeltes PDF ausgelassen", text);
        Assert.Contains("2 Prüfbestandsfotos geschützt übersprungen", text);
        Assert.Contains("1 Foto unsicher", text);
        Assert.Contains("defekt.pdf: unlesbar", text);
    }

    [Fact]
    public void FormatSingle_verwendet_beim_einzelnen_Fall_echten_Singular()
    {
        var result = new TrainingPdfReviewImportResult(
            "haltung.pdf",
            new string('a', 64),
            "100-200",
            1,
            DetectedPhotoCount: 1,
            MatchedPhotoCount: 1,
            Items:
            [
                new(
                    FramePath: "foto.png",
                    CaseId: "100-200",
                    MeterStart: 1,
                    MeterEnd: 1,
                    HaltungName: "100-200",
                    VideoPath: null,
                    PipeDiameterMm: 300),
            ],
            Issues: []);

        var text = TrainingStudioPdfImportPresentation.FormatSingle(result);

        Assert.Contains(
            "1 Prüffall aus einem eindeutig zugeordneten Foto geladen",
            text);
        Assert.DoesNotContain("1 Prüffälle", text);
    }

    [Fact]
    public void FormatBatch_verwendet_Singular_fuer_Ordner_PDF_Foto_und_Prueffall()
    {
        var result = new TrainingPdfReviewBatchImportResult(
            RequestedFolderCount: 1,
            DiscoveredPdfCount: 1,
            ReadPdfCount: 1,
            FailedPdfCount: 0,
            DuplicatePdfCount: 0,
            DetectedPhotoCount: 1,
            MatchedPhotoCount: 1,
            ProtectedPhotoCount: 0,
            Items:
            [
                new(
                    FramePath: "foto.png",
                    CaseId: "100-200",
                    MeterStart: 1,
                    MeterEnd: 1,
                    HaltungName: "100-200",
                    VideoPath: null,
                    PipeDiameterMm: 300),
            ],
            Issues: []);

        var text = TrainingStudioPdfImportPresentation.FormatBatch(result);

        Assert.Contains(
            "1 Prüffall aus einem eindeutig zugeordneten Foto geladen",
            text);
        Assert.Contains("1 von 1 PDF gelesen", text);
        Assert.DoesNotContain("1 Prüffälle", text);
        Assert.DoesNotContain("1 PDFs", text);
    }

    [Fact]
    public void FormatBatch_benennt_einen_leeren_einzelnen_Ordner_lesbar()
    {
        var result = new TrainingPdfReviewBatchImportResult(
            RequestedFolderCount: 1,
            DiscoveredPdfCount: 0,
            ReadPdfCount: 0,
            FailedPdfCount: 0,
            DuplicatePdfCount: 0,
            DetectedPhotoCount: 0,
            MatchedPhotoCount: 0,
            ProtectedPhotoCount: 0,
            Items: [],
            Issues: []);

        var text = TrainingStudioPdfImportPresentation.FormatBatch(result);

        Assert.Contains("Keine PDFs im gewählten Ordner gefunden", text);
        Assert.DoesNotContain("1 gewählten Ordnern", text);
    }
}
