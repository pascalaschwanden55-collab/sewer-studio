using System.Security.Cryptography;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.UseCases.PdfTrainingReview;
using AuswertungPro.Next.Infrastructure.Ai.Training.PdfReview;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Training.PdfReview;

public sealed class TrainingPdfReviewEvalProtectionTests : IDisposable
{
    private static readonly byte[] TestPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

    private readonly string _tempRoot =
        Path.Combine(
            Path.GetTempPath(),
            $"sewerstudio_pdf_review_eval_{Guid.NewGuid():N}");

    [Theory]
    [InlineData("100-200", "100-200")]
    [InlineData("200-100", "100-200")]
    public async Task ImportAsync_GeschuetzteHaltungAuchInGegenrichtung_WirdVorStagingAusgelassen(
        string pdfHaltung,
        string protectedHaltung)
    {
        var fixture = CreateFixture(pdfHaltung);
        var protectedHaltungen = new HashSet<string>(
            [protectedHaltung],
            StringComparer.OrdinalIgnoreCase);
        var protection = new TrainingPdfReviewProtectionSnapshot(
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            protectedHaltungen);
        protectedHaltungen.Clear();

        var result = await fixture.Service.ImportAsync(
            new TrainingPdfReviewImportRequest(fixture.PdfPath, null)
            {
                Protection = protection,
            });

        Assert.Empty(result.Items);
        Assert.Equal(1, result.DetectedPhotoCount);
        Assert.Equal(0, result.MatchedPhotoCount);
        Assert.Equal(1, result.ProtectedPhotoCount);
        var issue = Assert.Single(
            result.Issues,
            candidate => candidate.ReasonCode == "eval_haltung");
        Assert.Equal(1, issue.PageNumber);
        Assert.False(Directory.Exists(fixture.StageRoot));
    }

    [Fact]
    public async Task ImportAsync_GeschuetzterBildhash_WirdVorStagingAusgelassen()
    {
        var fixture = CreateFixture("300-400");
        var photoHash = Convert.ToHexStringLower(SHA256.HashData(TestPng));
        var protectedHashes = new HashSet<string>(
            [photoHash],
            StringComparer.OrdinalIgnoreCase);
        var protection = new TrainingPdfReviewProtectionSnapshot(
            protectedHashes,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        protectedHashes.Clear();

        var result = await fixture.Service.ImportAsync(
            new TrainingPdfReviewImportRequest(fixture.PdfPath, null)
            {
                Protection = protection,
            });

        Assert.Empty(result.Items);
        Assert.Equal(1, result.DetectedPhotoCount);
        Assert.Equal(0, result.MatchedPhotoCount);
        Assert.Equal(1, result.ProtectedPhotoCount);
        var issue = Assert.Single(
            result.Issues,
            candidate => candidate.ReasonCode == "eval_image_hash");
        Assert.Equal(1, issue.PageNumber);
        Assert.False(Directory.Exists(fixture.StageRoot));
    }

    [Fact]
    public async Task ImportAsync_RequestOhneSchutz_BleibtKompatibelUndSchreibtPrueffoto()
    {
        var fixture = CreateFixture("500-600");

        var result = await fixture.Service.ImportAsync(
            new TrainingPdfReviewImportRequest(fixture.PdfPath, 300));

        var item = Assert.Single(result.Items);
        Assert.Equal("500-600", item.CaseId);
        Assert.Equal(1, result.MatchedPhotoCount);
        Assert.Equal(0, result.ProtectedPhotoCount);
        Assert.DoesNotContain(
            result.Issues,
            candidate => candidate.ReasonCode is "eval_haltung" or "eval_image_hash");
        Assert.True(File.Exists(item.FramePath));
        Assert.StartsWith(
            fixture.StageRoot,
            item.FramePath,
            StringComparison.OrdinalIgnoreCase);
        Assert.Single(
            Directory.EnumerateFiles(
                fixture.StageRoot,
                "*.*",
                SearchOption.TopDirectoryOnly));
    }

    private Fixture CreateFixture(string haltung)
    {
        var sourceFolder = Path.Combine(_tempRoot, haltung);
        var knowledgeRoot = Path.Combine(_tempRoot, "brain");
        Directory.CreateDirectory(sourceFolder);
        var pdfPath = Path.Combine(sourceFolder, $"20260730_{haltung}.pdf");
        File.WriteAllBytes(pdfPath, "%PDF-synthetischer-Test"u8.ToArray());

        var document = new TrainingPdfReviewDocument(
            1,
            $"""
             Leitung       {haltung}
             Insp.datum    30.07.2026
             """,
            [
                new TrainingPdfEmbeddedPhoto(
                    1,
                    1,
                    TestPng,
                    ".png",
                    """
                    Foto: 11
                    Zustand: BCE
                    Rohrende
                    """),
            ],
            []);
        IReadOnlyList<GroundTruthEntry> entries =
        [
            new()
            {
                VsaCode = "BCE",
                Text = "Rohrende",
                MeterStart = 12.3,
                MeterEnd = 12.3,
            },
        ];
        var service = new TrainingPdfReviewImportService(
            knowledgeRoot,
            new FixedDocumentReader(document),
            (_, _) => Task.FromResult(entries));
        var sourceSha = Convert.ToHexStringLower(
            SHA256.HashData(File.ReadAllBytes(pdfPath)));
        var stageRoot = Path.Combine(
            knowledgeRoot,
            "training",
            "pdf_review_imports",
            sourceSha);

        return new Fixture(service, pdfPath, stageRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    private sealed record Fixture(
        TrainingPdfReviewImportService Service,
        string PdfPath,
        string StageRoot);

    private sealed class FixedDocumentReader(TrainingPdfReviewDocument document)
        : ITrainingPdfReviewDocumentReader
    {
        public TrainingPdfReviewDocument Read(
            string pdfPath,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return document;
        }
    }
}
