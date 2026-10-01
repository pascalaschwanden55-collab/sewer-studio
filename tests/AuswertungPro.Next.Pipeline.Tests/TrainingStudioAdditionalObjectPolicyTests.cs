using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Workbench;
using AuswertungPro.Next.Application.UseCases.TrainingStudioMultiObject;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class TrainingStudioAdditionalObjectPolicyTests
{
    [Fact]
    public void CreateManualObject_preserves_image_context_but_clears_existing_and_pdf_identity()
    {
        var source = new WorkbenchItem(
            FramePath: @"C:\pdf\photo-12.png",
            CaseId: "100-200",
            MeterStart: 4.2,
            MeterEnd: 8.4,
            HaltungName: "100-200",
            VideoPath: @"C:\video\100-200.mp4",
            PipeDiameterMm: 300,
            ExistingSampleId: "sample-1",
            ExistingCode: "BAIAB",
            ExistingBeschreibung: "Bestehender Dichtringbefund",
            SuggestedMainCode: "BAI",
            IsStreckenschaden: true,
            SourceSuggestion: new WorkbenchSourceSuggestion(
                "BAIAB",
                "Operateurbefund Dichtring",
                "haltung.pdf",
                new string('a', 64),
                7,
                "12",
                "photo_id"))
        {
            InspectionDate = new DateTime(2026, 7, 15),
            ExistingSourceType = "PdfPhoto",
            ExistingNotes = "pdf_sha256=...",
            ExistingBox = new BoundingBox(0.5, 0.5, 0.2, 0.2),
            ExistingSegmentation = new WorkbenchSegmentation(
                "0,1,7999",
                100,
                80,
                0.1,
                "Maske",
                Degraded: false,
                MaskAreaPixels: 1),
            ExistingClockPosition = 10,
            ExistingSeverity = 3,
            ExpectedImageSha256 = new string('b', 64),
            ExpectedConfirmedAtUtc = new DateTimeOffset(2026, 7, 16, 8, 30, 0, TimeSpan.Zero),
        };

        var result = TrainingStudioAdditionalObjectPolicy.CreateManualObject(source);

        Assert.Equal(source.FramePath, result.FramePath);
        Assert.Equal(source.CaseId, result.CaseId);
        Assert.Equal(source.MeterStart, result.MeterStart);
        Assert.Equal(source.MeterStart, result.MeterEnd);
        Assert.Equal(source.HaltungName, result.HaltungName);
        Assert.Equal(source.VideoPath, result.VideoPath);
        Assert.Equal(source.PipeDiameterMm, result.PipeDiameterMm);
        Assert.Equal(source.SuggestedMainCode, result.SuggestedMainCode);
        Assert.Equal(source.InspectionDate, result.InspectionDate);
        Assert.Equal(source.ExpectedImageSha256, result.ExpectedImageSha256);
        Assert.False(result.IsStreckenschaden);

        Assert.Null(result.ExistingSampleId);
        Assert.Null(result.ExistingCode);
        Assert.Null(result.ExistingBeschreibung);
        Assert.Null(result.ExistingSourceType);
        Assert.Null(result.ExistingNotes);
        Assert.Null(result.ExistingBox);
        Assert.Null(result.ExistingSegmentation);
        Assert.Null(result.ExistingClockPosition);
        Assert.Null(result.ExistingSeverity);
        Assert.Null(result.ExpectedConfirmedAtUtc);
        Assert.Null(result.SourceSuggestion);
    }

    [Fact]
    public void CreateManualObject_rejects_missing_source()
        => Assert.Throws<ArgumentNullException>(
            () => TrainingStudioAdditionalObjectPolicy.CreateManualObject(null!));
}
