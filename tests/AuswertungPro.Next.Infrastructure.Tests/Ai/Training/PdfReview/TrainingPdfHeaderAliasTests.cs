using AuswertungPro.Next.Infrastructure.Ai.Training.PdfReview;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Training.PdfReview;

public sealed class TrainingPdfHeaderAliasTests : IDisposable
{
    private readonly string _tempRoot =
        Path.Combine(Path.GetTempPath(), $"sewerstudio_pdf_alias_{Guid.NewGuid():N}");

    [Fact]
    public void ParseForPhotoImport_FretzAlias_IstKeinSammelPdf()
    {
        const string documentText =
            """
            --- Seite 1 ---
            Haltungsinspektion - 04.08.2023 - 101761-101774
            Kamera Witterung Haltung Nr.
            04.08.2023 trocken 37.72-37.11 5
            --- Seite 2 ---
            Haltungsbilder - 04.08.2023 - 37.72-37.11
            Datum Haltung Nr.
            04.08.2023 37.72-37.11 5
            """;

        var metadata = TrainingPdfProtocolMetadataParser.ParseForPhotoImport(
            documentText,
            preferredPathHaltungId: null);

        Assert.Equal("101761-101774", metadata.HaltungId);
        Assert.False(metadata.IsMultiHaltungDocument);
    }

    [Fact]
    public void ParseForPhotoImport_DirektesFeldNachAlias_BleibtNeueHaltung()
    {
        const string documentText =
            """
            --- Seite 1 ---
            Haltungsinspektion - 29.07.2026 - 100-200
            Kamera Witterung Haltung Nr.
            29.07.2026 trocken 300-400 5
            --- Seite 2 ---
            Haltungsbildbericht
            Haltung 300-400
            """;

        var metadata = TrainingPdfProtocolMetadataParser.ParseForPhotoImport(
            documentText,
            "100-200");

        Assert.True(metadata.IsMultiHaltungDocument);
    }

    [Fact]
    public void Read_HaltungsbilderSeite_ErzeugtKeinenAlias()
    {
        var result = Read(
            (page, font) =>
            {
                page.AddText(
                    "Haltungsbilder - 29.07.2026 - 100-200",
                    11,
                    new PdfPoint(40, 780),
                    font);
                page.AddText(
                    "Leitung 300-400",
                    11,
                    new PdfPoint(40, 750),
                    font);
            },
            (page, font) =>
            {
                page.AddText(
                    "Haltungsbilder - 29.07.2026 - 300-400",
                    11,
                    new PdfPoint(40, 780),
                    font);
                AddPhoto(page, font);
            });

        Assert.Equal(
            "300-400",
            Assert.Single(result.Photos).SectionHaltungId);
    }

    [Fact]
    public void Read_NeuerInspektionstitel_WirdNieDurchAltenAliasUmgeschrieben()
    {
        var result = Read(
            (page, font) =>
            {
                page.AddText(
                    "Haltungsinspektion - 29.07.2026 - 100-200",
                    11,
                    new PdfPoint(40, 780),
                    font);
                AddTwoLineAlias(page, font, "300-400");
            },
            (page, font) =>
            {
                page.AddText(
                    "Haltungsinspektion - 30.07.2026 - 300-400",
                    11,
                    new PdfPoint(40, 780),
                    font);
            },
            (page, font) =>
            {
                page.AddText(
                    "Haltungsbilder - 30.07.2026 - 300-400",
                    11,
                    new PdfPoint(40, 780),
                    font);
                AddPhoto(page, font);
            });

        Assert.Equal(
            "300-400",
            Assert.Single(result.Photos).SectionHaltungId);
    }

    [Fact]
    public void Read_BekannterKanonischerTitel_WirdNieZumAliasEinerNeuenHaltung()
    {
        var result = Read(
            (page, font) =>
            {
                page.AddText(
                    "Haltungsinspektion - 29.07.2026 - 100-200",
                    11,
                    new PdfPoint(40, 780),
                    font);
            },
            (page, font) =>
            {
                page.AddText(
                    "Haltungsinspektion - 30.07.2026 - 300-400",
                    11,
                    new PdfPoint(40, 780),
                    font);
                AddTwoLineAlias(page, font, "100-200");
            },
            (page, font) =>
            {
                page.AddText(
                    "Haltungsbilder - 30.07.2026 - 100-200",
                    11,
                    new PdfPoint(40, 780),
                    font);
                AddPhoto(page, font);
            });

        Assert.Equal(
            "100-200",
            Assert.Single(result.Photos).SectionHaltungId);
    }

    [Fact]
    public void Read_Aliaskollision_MachtSpaeterenFotoabschnittUneindeutig()
    {
        var result = Read(
            (page, font) =>
            {
                page.AddText(
                    "Haltungsinspektion - 29.07.2026 - 100-200",
                    11,
                    new PdfPoint(40, 780),
                    font);
                AddTwoLineAlias(page, font, "37.72-37.11");
            },
            (page, font) =>
            {
                page.AddText(
                    "Haltungsinspektion - 30.07.2026 - 300-400",
                    11,
                    new PdfPoint(40, 780),
                    font);
                AddTwoLineAlias(page, font, "37.72-37.11");
            },
            (page, font) =>
            {
                page.AddText(
                    "Haltungsbilder - 30.07.2026 - 37.72-37.11",
                    11,
                    new PdfPoint(40, 780),
                    font);
                AddPhoto(page, font);
            });

        var photo = Assert.Single(result.Photos);
        Assert.Null(photo.SectionHaltungId);
        Assert.True(photo.HasAmbiguousSectionHaltung);
    }

    [Fact]
    public void Read_HaltungsbildberichtMitDirektemFeld_TrenntAbschnitte()
    {
        var result = Read(
            (page, font) =>
            {
                page.AddText(
                    "Haltungsbildbericht",
                    11,
                    new PdfPoint(40, 780),
                    font);
                page.AddText(
                    "Haltung 60603-60602",
                    11,
                    new PdfPoint(40, 750),
                    font);
                AddPhoto(page, font);
            },
            (page, font) =>
            {
                page.AddText(
                    "Haltungsbildbericht",
                    11,
                    new PdfPoint(40, 780),
                    font);
                page.AddText(
                    "Haltung 60602-58932",
                    11,
                    new PdfPoint(40, 750),
                    font);
                AddPhoto(page, font);
            });

        Assert.Equal(
            new[] { "60603-60602", "60602-58932" },
            result.Photos.Select(photo => photo.SectionHaltungId).ToArray());
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    private TrainingPdfReviewDocument Read(
        params Action<PdfPageBuilder, PdfDocumentBuilder.AddedFont>[] pages)
    {
        Directory.CreateDirectory(_tempRoot);
        var pdfPath = Path.Combine(_tempRoot, "header-alias.pdf");
        using (var builder = new PdfDocumentBuilder())
        {
            var font = builder.AddStandard14Font(Standard14Font.Helvetica);
            foreach (var configure in pages)
            {
                var page = builder.AddPage(PageSize.A4);
                configure(page, font);
            }

            File.WriteAllBytes(pdfPath, builder.Build());
        }

        return new TrainingPdfReviewDocumentReader()
            .Read(pdfPath, CancellationToken.None);
    }

    private static void AddTwoLineAlias(
        PdfPageBuilder page,
        PdfDocumentBuilder.AddedFont font,
        string alias)
    {
        page.AddText(
            "Kamera Witterung Haltung Nr.",
            11,
            new PdfPoint(40, 750),
            font);
        page.AddText(
            $"29.07.2026 trocken {alias} 5",
            11,
            new PdfPoint(40, 730),
            font);
    }

    private static void AddPhoto(
        PdfPageBuilder page,
        PdfDocumentBuilder.AddedFont font)
    {
        page.AddPng(TestPng, new PdfRectangle(40, 390, 340, 615));
        page.AddText("Foto: 1", 11, new PdfPoint(365, 580), font);
        page.AddText("Zustand: BCE", 11, new PdfPoint(365, 555), font);
    }

    private static readonly byte[] TestPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
}
