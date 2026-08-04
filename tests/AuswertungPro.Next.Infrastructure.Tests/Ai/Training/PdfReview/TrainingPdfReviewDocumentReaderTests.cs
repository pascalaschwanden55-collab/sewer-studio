using System.Security.Cryptography;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.UseCases.PdfTrainingReview;
using AuswertungPro.Next.Infrastructure.Ai.Training.PdfReview;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Training.PdfReview;

public sealed class TrainingPdfReviewDocumentReaderTests : IDisposable
{
    private readonly string _tempRoot =
        Path.Combine(Path.GetTempPath(), $"sewerstudio_pdf_review_{Guid.NewGuid():N}");

    [Fact]
    public void Read_GrossesFotoUndKleinesLogo_LiefertNurFotoMitLokalemText()
    {
        Directory.CreateDirectory(_tempRoot);
        var pdfPath = Path.Combine(_tempRoot, "20260729_100-200.pdf");
        WritePdf(
            pdfPath,
            (page, font) =>
            {
                page.AddPng(TestPng, new PdfRectangle(40, 755, 120, 780));
                page.AddText("Firmenlogo", 9, new PdfPoint(130, 760), font);

                page.AddPng(TestPng, new PdfRectangle(40, 390, 340, 615));
                page.AddText("Foto: IMG_0042.JPG", 11, new PdfPoint(365, 580), font);
                page.AddText("Zustand: BCCAY", 11, new PdfPoint(365, 555), font);
                page.AddText("Entf. 0.71 m", 11, new PdfPoint(365, 530), font);
                page.AddText("Bogen nach links", 11, new PdfPoint(365, 505), font);
            });

        var result = new TrainingPdfReviewDocumentReader()
            .Read(pdfPath, CancellationToken.None);

        var photo = Assert.Single(result.Photos);
        Assert.Equal(1, photo.PageNumber);
        Assert.Contains("IMG_0042.JPG", photo.ContextText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("BCCAY", photo.ContextText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Firmenlogo", photo.ContextText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Read_ZweiFotosNebeneinanderMitGleichenTextzeilen_HaeltBefundeGetrennt()
    {
        Directory.CreateDirectory(_tempRoot);
        var pdfPath = Path.Combine(_tempRoot, "20260729_100-200.pdf");
        WritePdf(
            pdfPath,
            (page, font) =>
            {
                page.AddPng(TestPng, new PdfRectangle(30, 420, 250, 600));
                page.AddPng(TestPng, new PdfRectangle(315, 420, 535, 600));

                page.AddText("Foto: 11", 9, new PdfPoint(40, 405), font);
                page.AddText("Foto: 12", 9, new PdfPoint(325, 405), font);
                page.AddText("Zustand: BCCAY", 9, new PdfPoint(40, 385), font);
                page.AddText("Zustand: BCAAA", 9, new PdfPoint(325, 385), font);
                page.AddText("Bogen nach links", 9, new PdfPoint(40, 365), font);
                page.AddText("Anschluss mit Formstueck", 9, new PdfPoint(325, 365), font);
            });

        var result = new TrainingPdfReviewDocumentReader()
            .Read(pdfPath, CancellationToken.None);

        Assert.Equal(2, result.Photos.Count);
        var left = result.Photos[0];
        var right = result.Photos[1];
        Assert.Contains("Foto: 11", left.ContextText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("BCCAY", left.ContextText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("BCAAA", left.ContextText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Foto: 12", right.ContextText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("BCAAA", right.ContextText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("BCCAY", right.ContextText, StringComparison.OrdinalIgnoreCase);

        var leftMatch = TrainingPdfPhotoFindingMatcher.Match(left, [], result.DocumentText);
        var rightMatch = TrainingPdfPhotoFindingMatcher.Match(right, [], result.DocumentText);
        Assert.Equal("BCCAY", Assert.Single(leftMatch.Findings).VsaCode);
        Assert.Equal("BCAAA", Assert.Single(rightMatch.Findings).VsaCode);
    }

    [Fact]
    public void Read_SammelPdf_BindetFotosAnDenJeweiligenHaltungsabschnitt()
    {
        Directory.CreateDirectory(_tempRoot);
        var pdfPath = Path.Combine(_tempRoot, "20260729_100-200.pdf");
        using (var builder = new PdfDocumentBuilder())
        {
            var font = builder.AddStandard14Font(Standard14Font.Helvetica);
            var first = builder.AddPage(PageSize.A4);
            first.AddText(
                "Haltungsbilder - 29.07.2026 - 100-200",
                11,
                new PdfPoint(40, 780),
                font);
            first.AddPng(TestPng, new PdfRectangle(40, 390, 340, 615));
            first.AddText("Foto: 1", 11, new PdfPoint(365, 580), font);
            first.AddText("Zustand: BCE", 11, new PdfPoint(365, 555), font);

            var second = builder.AddPage(PageSize.A4);
            second.AddText(
                "Haltungsbilder - 29.07.2026 - 300-400",
                11,
                new PdfPoint(40, 780),
                font);
            second.AddPng(TestPng, new PdfRectangle(40, 390, 340, 615));
            second.AddText("Foto: 2", 11, new PdfPoint(365, 580), font);
            second.AddText("Zustand: BCCAY", 11, new PdfPoint(365, 555), font);
            File.WriteAllBytes(pdfPath, builder.Build());
        }

        var result = new TrainingPdfReviewDocumentReader()
            .Read(pdfPath, CancellationToken.None);

        Assert.Equal(2, result.Photos.Count);
        Assert.Equal("100-200", result.Photos[0].SectionHaltungId);
        Assert.Equal("300-400", result.Photos[1].SectionHaltungId);
    }

    [Fact]
    public void Read_InternerAliasAufTitelseite_BleibtAufFolgeseitenKanonischeHaltung()
    {
        Directory.CreateDirectory(_tempRoot);
        var pdfPath = Path.Combine(_tempRoot, "20260729_101761-101774.pdf");
        using (var builder = new PdfDocumentBuilder())
        {
            var font = builder.AddStandard14Font(Standard14Font.Helvetica);
            var title = builder.AddPage(PageSize.A4);
            title.AddText(
                "Haltungsinspektion - 29.07.2026 - 101761-101774",
                11,
                new PdfPoint(40, 780),
                font);
            title.AddText(
                "Kamera Witterung Haltung Nr.",
                11,
                new PdfPoint(40, 750),
                font);
            title.AddText(
                "04.08.2023 trocken 37.72-37.11.0 5",
                11,
                new PdfPoint(40, 730),
                font);

            var photoPage = builder.AddPage(PageSize.A4);
            photoPage.AddText(
                "Haltungsbilder - 29.07.2026 - 37.72-37.11",
                11,
                new PdfPoint(40, 780),
                font);
            photoPage.AddPng(TestPng, new PdfRectangle(40, 390, 340, 615));
            photoPage.AddText("Foto: 1", 11, new PdfPoint(365, 580), font);
            photoPage.AddText("Zustand: BCE", 11, new PdfPoint(365, 555), font);
            File.WriteAllBytes(pdfPath, builder.Build());
        }

        var result = new TrainingPdfReviewDocumentReader()
            .Read(pdfPath, CancellationToken.None);

        Assert.Equal(
            "101761-101774",
            Assert.Single(result.Photos).SectionHaltungId);
    }

    [Fact]
    public void Read_ZweitesFotoUeberschreitetKumulativesBytebudget_BrichtKomplettAb()
    {
        Directory.CreateDirectory(_tempRoot);
        var pdfPath = Path.Combine(_tempRoot, "bytebudget.pdf");
        WritePdf(
            pdfPath,
            (page, _) =>
            {
                page.AddPng(TestPng, new PdfRectangle(30, 420, 250, 600));
                page.AddPng(TestPng, new PdfRectangle(315, 420, 535, 600));
            });
        var baseline = new TrainingPdfReviewDocumentReader()
            .Read(pdfPath, CancellationToken.None);
        var firstPhotoBytes = baseline.Photos[0].ImageBytes.LongLength;

        var error = Assert.Throws<InvalidDataException>(
            () => new TrainingPdfReviewDocumentReader(
                    maximumTotalPhotoBytes: firstPhotoBytes,
                    maximumTotalPhotoPixels: long.MaxValue)
                .Read(pdfPath, CancellationToken.None));

        Assert.Contains("Byte-Gesamtlimit", error.Message, StringComparison.Ordinal);
        Assert.Contains("Seite 1, Foto 2", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Read_ZweitesFotoUeberschreitetKumulativesPixelbudget_BrichtKomplettAb()
    {
        Directory.CreateDirectory(_tempRoot);
        var pdfPath = Path.Combine(_tempRoot, "pixelbudget.pdf");
        WritePdf(
            pdfPath,
            (page, _) =>
            {
                page.AddPng(TestPng, new PdfRectangle(30, 420, 250, 600));
                page.AddPng(TestPng, new PdfRectangle(315, 420, 535, 600));
            });

        var error = Assert.Throws<InvalidDataException>(
            () => new TrainingPdfReviewDocumentReader(
                    maximumTotalPhotoBytes: long.MaxValue,
                    maximumTotalPhotoPixels: 1)
                .Read(pdfPath, CancellationToken.None));

        Assert.Contains("Pixel-Gesamtlimit", error.Message, StringComparison.Ordinal);
        Assert.Contains("Seite 1, Foto 2", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportAsync_ReaderSicherheitsfehler_BleibtAlsKlartextSichtbar()
    {
        Directory.CreateDirectory(_tempRoot);
        var pdfPath = Path.Combine(_tempRoot, "100-200.pdf");
        await File.WriteAllBytesAsync(pdfPath, "%PDF-synthetischer-Test"u8.ToArray());
        var service = new TrainingPdfReviewImportService(
            Path.Combine(_tempRoot, "brain"),
            new ThrowingDocumentReader(
                new InvalidDataException(
                    "Seite 4, Foto 2: Pixel-Gesamtlimit ueberschritten.")),
            (_, _) => Task.FromResult<IReadOnlyList<GroundTruthEntry>>([]));

        var error = await Assert.ThrowsAsync<UserFacingException>(
            () => service.ImportAsync(
                new TrainingPdfReviewImportRequest(pdfPath, null)));

        Assert.Contains("Pixel-Gesamtlimit", error.Message);
        Assert.Contains("Seite 4", error.Message);
    }

    [Fact]
    public void Match_DirekterBlockMitZweiCodes_ErhaeltBeideCodesAmSelbenFoto()
    {
        var photo = new TrainingPdfEmbeddedPhoto(
            2,
            1,
            TestPng,
            ".png",
            """
            Foto: 17
            Video 00:00:21
            Entf. 1.60 m
            Zustand BCAEA
            Zustand BAHC
            Anschluss und schadhafte Verbindung
            """);
        IReadOnlyList<GroundTruthEntry> entries =
        [
            new()
            {
                VsaCode = "BCAEA",
                Text = "Seitlicher Anschluss",
                MeterStart = 1.6,
                MeterEnd = 1.6,
                Zeit = TimeSpan.FromSeconds(21)
            },
            new()
            {
                VsaCode = "BAHC",
                Text = "Schadhafter Anschluss",
                MeterStart = 1.6,
                MeterEnd = 1.6,
                Zeit = TimeSpan.FromSeconds(21)
            }
        ];

        var result = TrainingPdfPhotoFindingMatcher.Match(photo, entries, "");

        Assert.Null(result.IssueCode);
        Assert.Equal(
            new[] { "BAHC", "BCAEA" },
            result.Findings.Select(finding => finding.VsaCode).Order().ToArray());
        Assert.All(result.Findings, finding => Assert.Equal("same_block", finding.MatchKind));
    }

    [Fact]
    public void Match_OhneEindeutigenSchluessel_LiefertIssueUndKeinenVorschlag()
    {
        var photo = new TrainingPdfEmbeddedPhoto(
            1,
            1,
            TestPng,
            ".png",
            "Unbeschriftetes Kanalbild");
        IReadOnlyList<GroundTruthEntry> entries =
        [
            new()
            {
                VsaCode = "BABCA",
                Text = "Riss radial",
                MeterStart = 2.4,
                MeterEnd = 2.4
            }
        ];

        var result = TrainingPdfPhotoFindingMatcher.Match(photo, entries, "");

        Assert.Empty(result.Findings);
        Assert.Equal("unmatched", result.IssueCode);
    }

    [Fact]
    public void Match_ExakterFotodateiname_VerbindetGetrennteFotoseiteUndOperateurzeile()
    {
        var photo = new TrainingPdfEmbeddedPhoto(
            2,
            1,
            TestPng,
            ".png",
            """
            Foto: IMG_0042.JPG
            Video 00:00:44
            Entf. 0.71 m
            Bogen nach links
            """);
        IReadOnlyList<GroundTruthEntry> entries =
        [
            new()
            {
                VsaCode = "BCCAY",
                Text = "Bogen nach links",
                MeterStart = 0.71,
                MeterEnd = 0.71,
                Zeit = TimeSpan.FromSeconds(44)
            }
        ];
        const string documentText =
            "IMG_0042.JPG  00:00:44  0.71 m  BCCAY  Bogen nach links";

        var result = TrainingPdfPhotoFindingMatcher.Match(
            photo,
            entries,
            documentText);

        var finding = Assert.Single(result.Findings);
        Assert.Null(result.IssueCode);
        Assert.Equal("BCCAY", finding.VsaCode);
        Assert.Equal("photo_id", finding.MatchKind);
    }

    [Fact]
    public void Match_KitFototoken_VerbindetExaktMitCodezeile()
    {
        var photo = new TrainingPdfEmbeddedPhoto(
            2,
            1,
            TestPng,
            ".png",
            """
            60-1-6A
            Riss radial
            """);
        IReadOnlyList<GroundTruthEntry> entries =
        [
            new()
            {
                VsaCode = "BABCA",
                Text = "Riss radial",
                MeterStart = 2.4,
                MeterEnd = 2.4
            }
        ];
        const string documentText = "60-1-6A  BABCA  Riss radial";

        var result = TrainingPdfPhotoFindingMatcher.Match(
            photo,
            entries,
            documentText);

        var finding = Assert.Single(result.Findings);
        Assert.Null(result.IssueCode);
        Assert.Equal("BABCA", finding.VsaCode);
        Assert.Equal("60-1-6A", finding.PhotoId);
        Assert.Equal("photo_id", finding.MatchKind);
    }

    [Fact]
    public void Match_DoppeltesFototokenMitVerschiedenenCodes_IstMehrdeutig()
    {
        var photo = new TrainingPdfEmbeddedPhoto(
            2,
            1,
            TestPng,
            ".png",
            "Foto: IMG_0042.JPG");
        IReadOnlyList<GroundTruthEntry> entries =
        [
            new()
            {
                VsaCode = "BABCA",
                Text = "Riss radial",
                MeterStart = 1.2,
                MeterEnd = 1.2,
            },
            new()
            {
                VsaCode = "BCCAY",
                Text = "Bogen nach links",
                MeterStart = 4.8,
                MeterEnd = 4.8,
            },
        ];
        const string documentText =
            """
            IMG_0042.JPG  BABCA  Riss radial
            IMG_0042.JPG  BCCAY  Bogen nach links
            """;

        var result = TrainingPdfPhotoFindingMatcher.Match(
            photo,
            entries,
            documentText);

        Assert.Empty(result.Findings);
        Assert.Equal("ambiguous", result.IssueCode);
    }

    [Fact]
    public void Match_GleicherCodeMitGleichStarkenMeterankern_IstMehrdeutig()
    {
        var photo = new TrainingPdfEmbeddedPhoto(
            2,
            1,
            TestPng,
            ".png",
            """
            Foto: IMG_0042.JPG
            Entf. 1.20 m
            """);
        IReadOnlyList<GroundTruthEntry> entries =
        [
            new()
            {
                VsaCode = "BCCAY",
                Text = "Bogen nach links",
                MeterStart = 1.2,
                MeterEnd = 1.2,
            },
            new()
            {
                VsaCode = "BCCAY",
                Text = "Bogen nach oben",
                MeterStart = 1.2,
                MeterEnd = 1.2,
            },
        ];
        const string documentText = "IMG_0042.JPG  BCCAY";

        var result = TrainingPdfPhotoFindingMatcher.Match(
            photo,
            entries,
            documentText);

        Assert.Empty(result.Findings);
        Assert.Equal("ambiguous", result.IssueCode);
    }

    [Fact]
    public void Match_GleicherCodeMitExaktemLokaltext_WirdEindeutig()
    {
        var photo = new TrainingPdfEmbeddedPhoto(
            2,
            1,
            TestPng,
            ".png",
            """
            Foto: IMG_0042.JPG
            Entf. 1.20 m
            Bogen nach links
            """);
        IReadOnlyList<GroundTruthEntry> entries =
        [
            new()
            {
                VsaCode = "BCCAY",
                Text = "Bogen nach links",
                MeterStart = 1.2,
                MeterEnd = 1.2,
            },
            new()
            {
                VsaCode = "BCCAY",
                Text = "Bogen nach oben",
                MeterStart = 1.2,
                MeterEnd = 1.2,
            },
        ];
        const string documentText = "IMG_0042.JPG  BCCAY";

        var result = TrainingPdfPhotoFindingMatcher.Match(
            photo,
            entries,
            documentText);

        var finding = Assert.Single(result.Findings);
        Assert.Null(result.IssueCode);
        Assert.Equal("Bogen nach links", finding.Beschreibung);
    }

    [Fact]
    public void Match_DirektcodeMitMehrerenNichtZuordenbarenEintraegen_IstMehrdeutig()
    {
        var photo = new TrainingPdfEmbeddedPhoto(
            2,
            1,
            TestPng,
            ".png",
            """
            Foto: 17
            Zustand: BCCAY
            """);
        IReadOnlyList<GroundTruthEntry> entries =
        [
            new()
            {
                VsaCode = "BCCAY",
                Text = "Bogen nach links",
                MeterStart = 1.2,
                MeterEnd = 1.2,
            },
            new()
            {
                VsaCode = "BCCAY",
                Text = "Bogen nach oben",
                MeterStart = 4.8,
                MeterEnd = 4.8,
            },
        ];

        var result = TrainingPdfPhotoFindingMatcher.Match(photo, entries, "");

        Assert.Empty(result.Findings);
        Assert.Equal("ambiguous", result.IssueCode);
    }

    [Fact]
    public void Match_ExakteAnkerMitMehrerenGleichenCodes_NimmtNichtStillDenErsten()
    {
        var photo = new TrainingPdfEmbeddedPhoto(
            2,
            1,
            TestPng,
            ".png",
            """
            Video 00:00:44
            Entf. 2.40 m
            Riss radial
            """);
        IReadOnlyList<GroundTruthEntry> entries =
        [
            new()
            {
                VsaCode = "BABCA",
                Text = "Riss radial",
                MeterStart = 2.4,
                MeterEnd = 2.4,
                Zeit = TimeSpan.FromSeconds(44),
            },
            new()
            {
                VsaCode = "BABCA",
                Text = "Riss radial",
                MeterStart = 2.4,
                MeterEnd = 3.4,
                Zeit = TimeSpan.FromSeconds(44),
                IsStreckenschaden = true,
            },
        ];

        var result = TrainingPdfPhotoFindingMatcher.Match(photo, entries, "");

        Assert.Empty(result.Findings);
        Assert.Equal("ambiguous", result.IssueCode);
    }

    [Fact]
    public void Match_ZeitMeterCaptionOhneEinheit_VerbindetNurBeiExaktemBefund()
    {
        IReadOnlyList<GroundTruthEntry> entries =
        [
            new()
            {
                VsaCode = "BABCA",
                Text = "Riss radial",
                MeterStart = 1.43,
                MeterEnd = 1.43,
                Zeit = TimeSpan.FromSeconds(52)
            },
            new()
            {
                VsaCode = "BCCAY",
                Text = "Bogen nach rechts",
                MeterStart = 1.43,
                MeterEnd = 1.43,
                Zeit = TimeSpan.FromSeconds(52)
            }
        ];
        var exactPhoto = new TrainingPdfEmbeddedPhoto(
            1,
            1,
            TestPng,
            ".png",
            """
            00:00:52, 1.43
            Riss radial
            """);
        var inexactPhoto = exactPhoto with
        {
            ContextText =
            """
            00:00:52, 1.43
            Riss radial oben
            """
        };

        var exactResult = TrainingPdfPhotoFindingMatcher.Match(exactPhoto, entries, "");
        var inexactResult = TrainingPdfPhotoFindingMatcher.Match(inexactPhoto, entries, "");

        var finding = Assert.Single(exactResult.Findings);
        Assert.Equal("BABCA", finding.VsaCode);
        Assert.Equal("time_meter_text", finding.MatchKind);
        Assert.Empty(inexactResult.Findings);
        Assert.Contains(inexactResult.IssueCode, new[] { "ambiguous", "unmatched" });
    }

    [Fact]
    public void Match_WortFotobeispiel_ErzeugtKeineFotoId()
    {
        var photo = new TrainingPdfEmbeddedPhoto(
            1,
            1,
            TestPng,
            ".png",
            """
            Fotobeispiel Anschluss
            Zustand: BCAAA
            Anschluss mit Formstueck
            """);

        var result = TrainingPdfPhotoFindingMatcher.Match(photo, [], "");

        var finding = Assert.Single(result.Findings);
        Assert.Null(result.IssueCode);
        Assert.Equal("BCAAA", finding.VsaCode);
        Assert.Null(finding.PhotoId);
    }

    [Fact]
    public void Match_ZeitUndMeterOhneExaktGleichenBefund_WaehltNichtNachTextaehnlichkeit()
    {
        var photo = new TrainingPdfEmbeddedPhoto(
            2,
            1,
            TestPng,
            ".png",
            """
            Video 00:00:44
            Entf. 2.40 m
            Riss radial an Rohrwand oben
            """);
        IReadOnlyList<GroundTruthEntry> entries =
        [
            new()
            {
                VsaCode = "BABCA",
                Text = "Riss radial an Rohrwand",
                MeterStart = 2.4,
                MeterEnd = 2.4,
                Zeit = TimeSpan.FromSeconds(44)
            },
            new()
            {
                VsaCode = "BCCAY",
                Text = "Bogen nach rechts",
                MeterStart = 2.4,
                MeterEnd = 2.4,
                Zeit = TimeSpan.FromSeconds(44)
            }
        ];

        var result = TrainingPdfPhotoFindingMatcher.Match(photo, entries, "");

        Assert.Empty(result.Findings);
        Assert.Contains(result.IssueCode, new[] { "ambiguous", "unmatched" });
    }

    [Fact]
    public void Parse_BefundfotoUndUmgebrocheneBefundzeilen_LiefertFachdatenUndStrecke()
    {
        const string documentText =
            """
            Deckblatt-Referenz 06.887943-90327
            Inspektionsdatum: 23.11.2023
            1.60 A04 BABBC Riss, komplexe Rissbildung, Scherbenbildung von 10 Uhr bis 2 00:00:54 999001-90327_12345678-1234-1234-1234-123456789abc.jpg
            Uhr, Start                                        327_f10bb
            4.60 B04 BABBC Riss, komplexe Rissbildung, Scherbenbildung von 10 Uhr bis 2 00:02:49
            Uhr, Ende
            10.10 BAFBE Abplatzung, Ursache nicht eindeutig feststellbar, an einer 00:03:44
            Rohrverbindung von 5 Uhr bis 7 Uhr                 327_939b
            999001-90327_12345678-1234-1234-1234-123456789abc.jpg
            Dieser Text gehoert zu einem spaeteren Fotoblock
            """;

        var metadata = TrainingPdfProtocolMetadataParser.Parse(documentText);

        Assert.Equal("999001-90327", metadata.HaltungId);
        Assert.Equal(new DateTime(2023, 11, 23), metadata.InspectionDate);
        var start = Assert.Single(metadata.Findings.Where(finding =>
            finding.VsaCode == "BABBC"
            && finding.ObservationMeter == 1.6));
        Assert.Equal(
            "Riss, komplexe Rissbildung, Scherbenbildung von 10 Uhr bis 2 Uhr, Start",
            start.Description);
        Assert.True(start.IsStreckenschaden);
        Assert.Equal(1.6, start.MeterStart);
        Assert.Equal(4.6, start.MeterEnd);
        var point = Assert.Single(metadata.Findings.Where(finding =>
            finding.VsaCode == "BAFBE"));
        Assert.Equal(
            "Abplatzung, Ursache nicht eindeutig feststellbar, an einer Rohrverbindung von 5 Uhr bis 7 Uhr",
            point.Description);
        Assert.False(point.IsStreckenschaden);
    }

    [Fact]
    public void Parse_WiderspruechlicheProtokollkoepfe_BrichtTrotzZweiZuEinsMehrheitAb()
    {
        const string documentText =
            """
            Haltungsinspektion - 23.11.2023 - 100-200
            Haltungsbilder - 23.11.2023 - 100-200
            Haltungsbilder - 23.11.2023 - 300-400
            """;

        var exception = Assert.Throws<InvalidDataException>(
            () => TrainingPdfProtocolMetadataParser.Parse(documentText));

        Assert.Contains("widerspruechliche", exception.Message);
    }

    [Fact]
    public void ParseForPhotoImport_SammelPdfMitVerschiedenenDaten_LaesstDatumOffen()
    {
        const string documentText =
            """
            Haltungsbilder - 23.11.2023 - 100-200
            Haltungsbilder - 24.11.2023 - 300-400
            """;

        var metadata = TrainingPdfProtocolMetadataParser.ParseForPhotoImport(
            documentText,
            "100-200");

        Assert.True(metadata.IsMultiHaltungDocument);
        Assert.Null(metadata.InspectionDate);
    }

    [Fact]
    public void Parse_EindeutigerProtokollkopf_UeberstimmtAbweichendenInternenHaltungsalias()
    {
        const string documentText =
            """
            Haltungsinspektion - 04.08.2023 - 101761-101774
            Kamera Witterung Haltung Nr.
            04.08.2023 trocken 37.72-37.11 5
            """;

        var metadata = TrainingPdfProtocolMetadataParser.Parse(documentText);

        Assert.Equal("101761-101774", metadata.HaltungId);
    }

    [Fact]
    public void Parse_EindeutigerProtokollkopf_BleibtTrotzVieleSchwacherFototrefferMassgebend()
    {
        const string documentText =
            """
            Haltungsinspektion - 23.11.2023 - 100-200
            900-901_12345678-1234-1234-1234-123456789abc.jpg
            900-901_22345678-1234-1234-1234-123456789abc.jpg
            900-901_32345678-1234-1234-1234-123456789abc.jpg
            900-901_42345678-1234-1234-1234-123456789abc.jpg
            900-901_52345678-1234-1234-1234-123456789abc.jpg
            900-901_62345678-1234-1234-1234-123456789abc.jpg
            Freitext 900-901
            Freitext 900-901
            """;

        var metadata = TrainingPdfProtocolMetadataParser.Parse(documentText);

        Assert.Equal("100-200", metadata.HaltungId);
    }

    [Fact]
    public void Parse_AusdruecklichePunktNullAliase_GeltenAlsDieselbeHaltung()
    {
        const string documentText =
            """
            Haltungsinspektion - 10.11.2025 - 9906-9906.0
            Leitung                         9906-9906
            Haltungsnummer                  9906-9906.0
            """;

        var metadata = TrainingPdfProtocolMetadataParser.Parse(documentText);

        Assert.Equal("9906-9906", metadata.HaltungId);
    }

    [Fact]
    public void Parse_FreiePunktNullAliase_GeltenAlsDieselbeHaltung()
    {
        const string documentText =
            """
            Foto 9906-9906_ABCDEF0123456789
            Foto 9906-9906.0_1234ABCDEF567890
            """;

        var metadata = TrainingPdfProtocolMetadataParser.Parse(documentText);

        Assert.Equal("9906-9906", metadata.HaltungId);
    }

    [Fact]
    public void ExtractFromFileName_KompaktesDatumUndPunktNullAlias_SindSymmetrisch()
    {
        var result = TrainingPdfHaltungId.ExtractFromFileName(
            "202511109906-9906",
            "9906-9906.0");

        Assert.Equal("9906-9906", result);
    }

    [Fact]
    public async Task ImportAsync_LeitungsAliasMitPunktNull_UndWeitesDatumsfeld_BleibenFachlichKorrekt()
    {
        var sourceFolder = Path.Combine(_tempRoot, "9906-9906");
        var knowledgeRoot = Path.Combine(_tempRoot, "brain");
        Directory.CreateDirectory(sourceFolder);
        var pdfPath = Path.Combine(sourceFolder, "202511109906-9906.0.pdf");
        await File.WriteAllBytesAsync(pdfPath, "%PDF-synthetischer-Test"u8.ToArray());
        const string documentText =
            """
            Leitung                         9906-9906.0
            Insp.datum                      10.11.2025
            """;
        var photo = new TrainingPdfEmbeddedPhoto(
            2,
            1,
            TestPng,
            ".png",
            """
            Foto: 11
            Zustand: BCE
            Rohrende
            """);
        var document = new TrainingPdfReviewDocument(
            2,
            documentText,
            [photo],
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

        var result = await service.ImportAsync(
            new TrainingPdfReviewImportRequest(pdfPath, null));

        Assert.Equal("9906-9906", result.HaltungId);
        Assert.Equal(new DateTime(2025, 11, 10), result.InspectionDate);
        Assert.Equal("9906-9906", Assert.Single(result.Items).CaseId);
    }

    [Fact]
    public async Task ImportAsync_GleichwertigeFotoabschnitte_BleibenEineSaubereHaltung()
    {
        var sourceFolder = Path.Combine(_tempRoot, "9906-9906");
        var knowledgeRoot = Path.Combine(_tempRoot, "brain");
        Directory.CreateDirectory(sourceFolder);
        var pdfPath = Path.Combine(sourceFolder, "202511109906-9906.0.pdf");
        await File.WriteAllBytesAsync(pdfPath, "%PDF-synthetischer-Test"u8.ToArray());
        var first = new TrainingPdfEmbeddedPhoto(
            1,
            1,
            TestPng,
            ".png",
            "Zustand BCE")
        {
            SectionHaltungId = "9906-9906.0",
        };
        var second = new TrainingPdfEmbeddedPhoto(
            2,
            1,
            TestPng,
            ".png",
            "Zustand BCE")
        {
            SectionHaltungId = "9906-9906",
        };
        var document = new TrainingPdfReviewDocument(
            2,
            "Haltungsbilder - 10.11.2025 - 9906-9906",
            [first, second],
            []);
        var service = new TrainingPdfReviewImportService(
            knowledgeRoot,
            new FixedDocumentReader(document),
            (_, _) => Task.FromResult<IReadOnlyList<GroundTruthEntry>>(
            [
                new()
                {
                    VsaCode = "BCE",
                    Text = "Rohrende",
                    MeterStart = 7,
                    MeterEnd = 7,
                },
            ]));

        var result = await service.ImportAsync(
            new TrainingPdfReviewImportRequest(pdfPath, null));

        Assert.Equal(2, result.Items.Count);
        Assert.All(result.Items, item =>
        {
            Assert.Equal("9906-9906", item.CaseId);
            Assert.Equal(7, item.MeterStart);
        });
    }

    [Fact]
    public async Task ImportAsync_DatumsblockDirektVorHaltungsId_NutztUebereinstimmendenOrdner()
    {
        var sourceFolder = Path.Combine(_tempRoot, "61721-61720");
        var knowledgeRoot = Path.Combine(_tempRoot, "brain");
        Directory.CreateDirectory(sourceFolder);
        var pdfPath = Path.Combine(sourceFolder, "20240461721-61720.pdf");
        await File.WriteAllBytesAsync(pdfPath, "%PDF-synthetischer-Test"u8.ToArray());
        var photo = new TrainingPdfEmbeddedPhoto(
            2,
            1,
            TestPng,
            ".png",
            """
            Foto: 11
            Zustand: BCE
            Rohrende
            """);
        var document = new TrainingPdfReviewDocument(
            2,
            "Haltungsinspektion",
            [photo],
            []);
        IReadOnlyList<GroundTruthEntry> entries =
        [
            new()
            {
                VsaCode = "BCE",
                Text = "Rohrende",
                MeterStart = 0,
                MeterEnd = 0,
            },
        ];
        var service = new TrainingPdfReviewImportService(
            knowledgeRoot,
            new FixedDocumentReader(document),
            (_, _) => Task.FromResult(entries));

        var result = await service.ImportAsync(
            new TrainingPdfReviewImportRequest(pdfPath, null));

        Assert.Equal("61721-61720", result.HaltungId);
        Assert.Equal("61721-61720", Assert.Single(result.Items).CaseId);
    }

    [Fact]
    public async Task ImportAsync_SammelPdf_HaeltHaltungenProFotoGetrennt()
    {
        var sourceFolder = Path.Combine(_tempRoot, "100-200");
        var knowledgeRoot = Path.Combine(_tempRoot, "brain");
        Directory.CreateDirectory(sourceFolder);
        var pdfPath = Path.Combine(sourceFolder, "20260729_100-200.pdf");
        await File.WriteAllBytesAsync(pdfPath, "%PDF-synthetischer-Test"u8.ToArray());
        var withoutSection = new TrainingPdfEmbeddedPhoto(
            1,
            1,
            TestPng,
            ".png",
            """
            Foto: 0
            Zustand: BCE
            Rohrende
            """);
        var first = new TrainingPdfEmbeddedPhoto(
            2,
            1,
            TestPng,
            ".png",
            """
            Foto: 1
            Zustand: BCE
            Rohrende
            """)
        {
            SectionHaltungId = "100-200",
        };
        var second = new TrainingPdfEmbeddedPhoto(
            4,
            1,
            TestPng,
            ".png",
            """
            Foto: 2
            Zustand: BCCAY
            Bogen nach links
            """)
        {
            SectionHaltungId = "300-400",
        };
        var document = new TrainingPdfReviewDocument(
            4,
            """
            Haltungsbilder - 29.07.2026 - 100-200
            Folgeseite enthaelt einen neuen expliziten Haltungsabschnitt.
            """,
            [withoutSection, first, second],
            []);
        var service = new TrainingPdfReviewImportService(
            knowledgeRoot,
            new FixedDocumentReader(document),
            (_, _) => Task.FromResult<IReadOnlyList<GroundTruthEntry>>(
            [
                new()
                {
                    VsaCode = "BCE",
                    Text = "Fremder Eintrag einer anderen Haltung",
                    MeterStart = 99,
                    MeterEnd = 99,
                },
            ]));

        var result = await service.ImportAsync(
            new TrainingPdfReviewImportRequest(pdfPath, null));

        Assert.Equal(2, result.MatchedPhotoCount);
        Assert.Equal(
            new[] { "100-200", "300-400" },
            result.Items.Select(item => item.CaseId).ToArray());
        Assert.Equal(0, result.Items[0].MeterStart);
        Assert.Contains(
            result.Issues,
            issue => issue.ReasonCode == "ambiguous_haltung"
                     && issue.PageNumber == 1);
    }

    [Fact]
    public async Task ImportAsync_SammelPdf_MitNurEinemFotoabschnitt_NutztKeineGlobalenBefunde()
    {
        var sourceFolder = Path.Combine(_tempRoot, "100-200");
        var knowledgeRoot = Path.Combine(_tempRoot, "brain");
        Directory.CreateDirectory(sourceFolder);
        var pdfPath = Path.Combine(sourceFolder, "20260729_100-200.pdf");
        await File.WriteAllBytesAsync(pdfPath, "%PDF-synthetischer-Test"u8.ToArray());
        var photo = new TrainingPdfEmbeddedPhoto(
            1,
            1,
            TestPng,
            ".png",
            "Foto: 007")
        {
            SectionHaltungId = "100-200",
        };
        var document = new TrainingPdfReviewDocument(
            2,
            """
            Haltungsbilder - 29.07.2026 - 100-200
            Haltungsbilder - 29.07.2026 - 300-400
            Foto: 007 BCE Fremder Befund
            """,
            [photo],
            []);
        var service = new TrainingPdfReviewImportService(
            knowledgeRoot,
            new FixedDocumentReader(document),
            (_, _) => Task.FromResult<IReadOnlyList<GroundTruthEntry>>(
            [
                new()
                {
                    VsaCode = "BCE",
                    Text = "Fremder Befund",
                    MeterStart = 9,
                    MeterEnd = 9,
                },
            ]));

        var result = await service.ImportAsync(
            new TrainingPdfReviewImportRequest(pdfPath, null));

        Assert.Equal(0, result.MatchedPhotoCount);
        Assert.Empty(result.Items);
        Assert.Contains(
            result.Issues,
            issue => issue.ReasonCode == "unmatched"
                     && issue.PageNumber == 1);
    }

    [Fact]
    public async Task ImportAsync_SammelPdf_BewahrtLokalenStreckenschaden()
    {
        var sourceFolder = Path.Combine(_tempRoot, "100-200");
        var knowledgeRoot = Path.Combine(_tempRoot, "brain");
        Directory.CreateDirectory(sourceFolder);
        var pdfPath = Path.Combine(sourceFolder, "20260729_100-200.pdf");
        await File.WriteAllBytesAsync(pdfPath, "%PDF-synthetischer-Test"u8.ToArray());
        var stretchPhoto = new TrainingPdfEmbeddedPhoto(
            1,
            1,
            TestPng,
            ".png",
            """
            Foto: 001
            Video 00:00:10
            Entf. 1.00 m
            Zustand BABBC
            Riss radial, Start
            """)
        {
            SectionHaltungId = "100-200",
            SectionText =
                """
                Haltungsbilder - 29.07.2026 - 100-200
                1.00 A04 BABBC Riss radial, Start 00:00:10
                4.00 B04 BABBC Riss radial, Ende 00:00:20
                """,
        };
        var secondPhoto = new TrainingPdfEmbeddedPhoto(
            2,
            1,
            TestPng,
            ".png",
            """
            Foto: 002
            Zustand BCCAY
            Bogen nach links
            """)
        {
            SectionHaltungId = "300-400",
            SectionText =
                """
                Haltungsbilder - 29.07.2026 - 300-400
                """,
        };
        var document = new TrainingPdfReviewDocument(
            2,
            """
            Haltungsbilder - 29.07.2026 - 100-200
            Haltungsbilder - 29.07.2026 - 300-400
            """,
            [stretchPhoto, secondPhoto],
            []);
        var service = new TrainingPdfReviewImportService(
            knowledgeRoot,
            new FixedDocumentReader(document),
            (_, _) => Task.FromResult<IReadOnlyList<GroundTruthEntry>>([]));

        var result = await service.ImportAsync(
            new TrainingPdfReviewImportRequest(pdfPath, null));

        var stretchItem = Assert.Single(result.Items.Where(item =>
            item.SourceSuggestion?.VsaCode == "BABBC"));
        Assert.True(stretchItem.IsStreckenschaden);
        Assert.Equal(1, stretchItem.MeterStart);
        Assert.Equal(4, stretchItem.MeterEnd);
    }

    [Fact]
    public void Parse_MehrdeutigeStartEndeFolge_WirdNichtAlsStreckeGeraten()
    {
        const string documentText =
            """
            1.00 BABBC Riss, Start 00:00:10
            2.00 BABBC Riss, Start 00:00:20
            3.00 BABBC Riss, Ende 00:00:30
            """;

        var metadata = TrainingPdfProtocolMetadataParser.Parse(documentText);

        Assert.Equal(3, metadata.Findings.Count);
        Assert.All(
            metadata.Findings,
            finding =>
            {
                Assert.False(finding.IsStreckenschaden);
                Assert.Equal(finding.ObservationMeter, finding.MeterStart);
                Assert.Equal(finding.ObservationMeter, finding.MeterEnd);
            });
    }

    [Fact]
    public async Task ImportAsync_PdfFachdatenSindStaerkerAlsDeckblattname_UndWerdenVollstaendigTransportiert()
    {
        var sourceFolder = Path.Combine(_tempRoot, "06.887943-90327");
        var knowledgeRoot = Path.Combine(_tempRoot, "brain");
        Directory.CreateDirectory(sourceFolder);
        var pdfPath = Path.Combine(sourceFolder, "20231123_06.887943-90327.pdf");
        var originalBytes = "%PDF-synthetischer-Test"u8.ToArray();
        await File.WriteAllBytesAsync(pdfPath, originalBytes);
        const string documentText =
            """
            Haltungsinspektion - 23.11.2023 - 999001-90327
            1.60 A04 BABBC Riss, komplexe Rissbildung, Scherbenbildung von 10 Uhr bis 2 00:00:54
            Uhr, Start
            4.60 B04 BABBC Riss, komplexe Rissbildung, Scherbenbildung von 10 Uhr bis 2 00:02:49
            Uhr, Ende
            """;
        var photo = new TrainingPdfEmbeddedPhoto(
            3,
            1,
            TestPng,
            ".png",
            """
            Foto: 17
            Zustand: BABBC
            Video 00:00:54
            Entf. 1.60 m
            Riss, komplexe Rissbildung, Scherbenbildung von 10 Uhr bis 2
            """);
        var document = new TrainingPdfReviewDocument(
            3,
            documentText,
            [photo],
            []);
        IReadOnlyList<GroundTruthEntry> entries =
        [
            new()
            {
                VsaCode = "BABBC",
                Text = "Riss, komplexe Rissbildung, Scherbenbildung von 10 Uhr bis 2",
                MeterStart = 1.6,
                MeterEnd = 1.6,
                Zeit = TimeSpan.FromSeconds(54),
            },
        ];
        var service = new TrainingPdfReviewImportService(
            knowledgeRoot,
            new FixedDocumentReader(document),
            (_, _) => Task.FromResult(entries));

        var result = await service.ImportAsync(
            new TrainingPdfReviewImportRequest(pdfPath, 700));

        var item = Assert.Single(result.Items);
        var source = Assert.IsType<AuswertungPro.Next.Application.Ai.Workbench.WorkbenchSourceSuggestion>(
            item.SourceSuggestion);
        Assert.Equal("999001-90327", result.HaltungId);
        Assert.Equal("999001-90327", item.CaseId);
        Assert.Equal(new DateTime(2023, 11, 23), result.InspectionDate);
        Assert.Equal(result.InspectionDate, item.InspectionDate);
        Assert.Equal(result.InspectionDate, source.InspectionDate);
        Assert.Equal(
            "Riss, komplexe Rissbildung, Scherbenbildung von 10 Uhr bis 2 Uhr, Start",
            source.Beschreibung);
        Assert.True(item.IsStreckenschaden);
        Assert.Equal(1.6, item.MeterStart);
        Assert.Equal(4.6, item.MeterEnd);
        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(pdfPath));
    }

    [Fact]
    public async Task ImportAsync_DirekterCodeMitLogo_SchreibtNurPrueffotoUndBindetPdfVorgabe()
    {
        var sourceFolder = Path.Combine(_tempRoot, "100-200");
        var knowledgeRoot = Path.Combine(_tempRoot, "brain");
        Directory.CreateDirectory(sourceFolder);
        var pdfPath = Path.Combine(sourceFolder, "20260729_100-200.pdf");
        WritePdf(
            pdfPath,
            (page, font) =>
            {
                page.AddPng(TestPng, new PdfRectangle(40, 755, 120, 780));
                page.AddText("Firmenlogo", 9, new PdfPoint(130, 760), font);
                page.AddPng(TestPng, new PdfRectangle(40, 390, 340, 615));
                page.AddText("Foto: IMG_0042.JPG", 11, new PdfPoint(365, 580), font);
                page.AddText("Zustand: BCCAY", 11, new PdfPoint(365, 555), font);
                page.AddText("Video 00:00:44", 11, new PdfPoint(365, 530), font);
                page.AddText("Entf. 0.71 m", 11, new PdfPoint(365, 505), font);
                page.AddText("Bogen nach links", 11, new PdfPoint(365, 480), font);
            });
        var originalHash = ComputeSha256(pdfPath);

        var service = new TrainingPdfReviewImportService(knowledgeRoot);
        var result = await service.ImportAsync(
            new TrainingPdfReviewImportRequest(pdfPath, 300));

        var item = Assert.Single(result.Items);
        Assert.Equal("100-200", result.HaltungId);
        Assert.Equal(1, result.DetectedPhotoCount);
        Assert.Equal(1, result.MatchedPhotoCount);
        Assert.True(File.Exists(item.FramePath));
        Assert.StartsWith(
            Path.Combine(knowledgeRoot, "training", "pdf_review_imports"),
            item.FramePath,
            StringComparison.OrdinalIgnoreCase);
        Assert.Null(item.ExistingSampleId);
        Assert.Null(item.ExistingCode);
        Assert.Null(item.ExistingBeschreibung);
        var source = Assert.IsType<AuswertungPro.Next.Application.Ai.Workbench.WorkbenchSourceSuggestion>(
            item.SourceSuggestion);
        Assert.Equal("BCCAY", source.VsaCode);
        Assert.Equal("same_block", source.MatchKind);
        Assert.Equal(result.SourceDocumentSha256, source.SourceDocumentSha256);
        Assert.Equal(
            result.SourceDocumentSha256,
            Path.GetFileName(Path.GetDirectoryName(item.FramePath)));
        Assert.Equal(originalHash, ComputeSha256(pdfPath));
    }

    [Fact]
    public async Task ImportAsync_UnbeschriftetesFremdesNummernpaar_WidersprichtNichtDateiHaltung()
    {
        var sourceFolder = Path.Combine(_tempRoot, "700-800");
        var knowledgeRoot = Path.Combine(_tempRoot, "brain");
        Directory.CreateDirectory(sourceFolder);
        var pdfPath = Path.Combine(sourceFolder, "20260729_700-800.pdf");
        WritePdf(
            pdfPath,
            (page, font) =>
            {
                page.AddText("999-888", 10, new PdfPoint(40, 780), font);
                page.AddPng(TestPng, new PdfRectangle(40, 390, 340, 615));
                page.AddText("Foto: 21", 11, new PdfPoint(365, 580), font);
                page.AddText("Zustand: BCCAY", 11, new PdfPoint(365, 555), font);
                page.AddText("Bogen nach links", 11, new PdfPoint(365, 530), font);
            });

        var result = await new TrainingPdfReviewImportService(knowledgeRoot)
            .ImportAsync(new TrainingPdfReviewImportRequest(pdfPath, null));

        Assert.Equal("700-800", result.HaltungId);
        Assert.Equal("BCCAY", Assert.Single(result.Items).SourceSuggestion!.VsaCode);
    }

    [Fact]
    public async Task ImportAsync_GleichesPdfZweimal_IstIdempotentUndLaesstOriginalUnveraendert()
    {
        var sourceFolder = Path.Combine(_tempRoot, "300-400");
        var knowledgeRoot = Path.Combine(_tempRoot, "brain");
        Directory.CreateDirectory(sourceFolder);
        var pdfPath = Path.Combine(sourceFolder, "20260729_300-400.pdf");
        WritePdf(
            pdfPath,
            (page, font) =>
            {
                page.AddPng(TestPng, new PdfRectangle(40, 390, 340, 615));
                page.AddText("Foto: 17", 11, new PdfPoint(365, 580), font);
                page.AddText("Zustand: BCAEA", 11, new PdfPoint(365, 555), font);
                page.AddText("Zustand: BAHC", 11, new PdfPoint(365, 530), font);
                page.AddText("Entf. 1.60 m", 11, new PdfPoint(365, 505), font);
                page.AddText("Anschluss und schadhafte Verbindung", 11, new PdfPoint(365, 480), font);
            });
        var originalBytes = await File.ReadAllBytesAsync(pdfPath);

        var service = new TrainingPdfReviewImportService(knowledgeRoot);
        var first = await service.ImportAsync(new TrainingPdfReviewImportRequest(pdfPath, null));
        var second = await service.ImportAsync(new TrainingPdfReviewImportRequest(pdfPath, null));

        Assert.Equal(2, first.Items.Count);
        Assert.Equal(
            new[] { "BAHC", "BCAEA" },
            first.Items
                .Select(item => item.SourceSuggestion!.VsaCode)
                .Order()
                .ToArray());
        Assert.Equal(
            first.Items.Select(item => item.FramePath).Distinct().Single(),
            second.Items.Select(item => item.FramePath).Distinct().Single());
        var stageFolder = Path.GetDirectoryName(first.Items[0].FramePath)!;
        Assert.Single(Directory.EnumerateFiles(stageFolder, "*.*", SearchOption.TopDirectoryOnly));
        Assert.Equal(originalBytes, await File.ReadAllBytesAsync(pdfPath));
    }

    [Fact]
    public async Task ImportAsync_UnklareFotozuordnung_MeldetIssueUndSchreibtKeinPrueffoto()
    {
        var sourceFolder = Path.Combine(_tempRoot, "500-600");
        var knowledgeRoot = Path.Combine(_tempRoot, "brain");
        Directory.CreateDirectory(sourceFolder);
        var pdfPath = Path.Combine(sourceFolder, "20260729_500-600.pdf");
        WritePdf(
            pdfPath,
            (page, font) =>
            {
                page.AddPng(TestPng, new PdfRectangle(40, 390, 340, 615));
                page.AddText("Unbeschriftetes Kanalbild", 11, new PdfPoint(365, 555), font);
            });
        var originalHash = ComputeSha256(pdfPath);

        var result = await new TrainingPdfReviewImportService(knowledgeRoot)
            .ImportAsync(new TrainingPdfReviewImportRequest(pdfPath, null));

        Assert.Empty(result.Items);
        Assert.Contains(result.Issues, issue => issue.ReasonCode == "unmatched");
        Assert.False(Directory.Exists(
            Path.Combine(knowledgeRoot, "training", "pdf_review_imports")));
        Assert.Equal(originalHash, ComputeSha256(pdfPath));
    }

    public void Dispose()
    {
        if (!Directory.Exists(_tempRoot))
            return;

        Directory.Delete(_tempRoot, recursive: true);
    }

    private static void WritePdf(
        string path,
        Action<PdfPageBuilder, PdfDocumentBuilder.AddedFont> configure)
    {
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);
        configure(page, font);
        File.WriteAllBytes(path, builder.Build());
    }

    private static readonly byte[] TestPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

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

    private sealed class ThrowingDocumentReader(Exception exception)
        : ITrainingPdfReviewDocumentReader
    {
        public TrainingPdfReviewDocument Read(
            string pdfPath,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw exception;
        }
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }
}
