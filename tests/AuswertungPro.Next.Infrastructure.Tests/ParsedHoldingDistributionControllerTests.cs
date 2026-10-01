using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.HoldingDistribution;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class ParsedHoldingDistributionControllerTests
{
    [Fact]
    public void Distribute_ohneVideo_kopiertPdf_und_schreibt_Fehlhinweis()
    {
        using var temp = new TempDirectory();
        var sourcePdf = Path.Combine(temp.Path, "quelle.pdf");
        var videoFolder = Directory.CreateDirectory(Path.Combine(temp.Path, "videos")).FullName;
        var destination = Directory.CreateDirectory(Path.Combine(temp.Path, "ziel")).FullName;
        WritePdf(sourcePdf, "Haltungsinspektion - 12.07.2026 - 1000-2000");
        var parsed = new HoldingFolderDistributor.ParsedPdf(
            true,
            null,
            new DateTime(2026, 7, 12),
            "1000-2000",
            null);

        var result = ParsedHoldingDistributionController.Distribute(
            parsed,
            new HoldingPdfSource(sourcePdf, sourcePdf),
            new HoldingVideoSearchContext(videoFolder, Recursive: true),
            new HoldingDistributionTarget(destination, MoveInsteadOfCopy: false, Overwrite: false, "__UNMATCHED"));

        Assert.True(result.Success, result.Message);
        Assert.Equal(HoldingFolderDistributor.VideoMatchStatus.NotFound, result.VideoStatus);
        Assert.NotNull(result.DestPdfPath);
        Assert.True(File.Exists(result.DestPdfPath));
        Assert.NotNull(result.InfoPath);
        Assert.True(File.Exists(result.InfoPath));
        Assert.Contains("Video missing", result.Message);
    }

    [Fact]
    public void Distribute_mitVideo_setzt_portablen_RecordLink()
    {
        using var temp = new TempDirectory();
        var sourcePdf = Path.Combine(temp.Path, "quelle.pdf");
        var videoFolder = Directory.CreateDirectory(Path.Combine(temp.Path, "videos")).FullName;
        var destination = Directory.CreateDirectory(Path.Combine(temp.Path, "ziel")).FullName;
        var sourceVideo = Path.Combine(videoFolder, "aufnahme.mpg");
        File.WriteAllText(sourceVideo, "video");
        WritePdf(sourcePdf, "Haltungsinspektion - 12.07.2026 - 1000-2000");
        var project = new Project();
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "1000-2000", FieldSource.Manual, userEdited: false);
        project.AddRecord(record);
        var parsed = new HoldingFolderDistributor.ParsedPdf(
            true,
            null,
            new DateTime(2026, 7, 12),
            "1000-2000",
            "aufnahme.mpg");

        var result = ParsedHoldingDistributionController.Distribute(
            parsed,
            new HoldingPdfSource(sourcePdf, sourcePdf),
            new HoldingVideoSearchContext(videoFolder, Recursive: true),
            new HoldingDistributionTarget(destination, MoveInsteadOfCopy: false, Overwrite: false, "__UNMATCHED"),
            project);

        Assert.True(result.Success, result.Message);
        Assert.Equal(HoldingFolderDistributor.VideoMatchStatus.Matched, result.VideoStatus);
        Assert.NotNull(result.DestVideoPath);
        Assert.True(File.Exists(result.DestVideoPath));
        Assert.EndsWith("20260712_1000-2000.mpg", record.GetFieldValue("Link"));
        Assert.True(project.Dirty);
    }

    [Fact]
    public void DistributeFiles_faengt_defektePdf_ab_und_verarbeitet_naechste_Datei()
    {
        using var temp = new TempDirectory();
        var invalidPdf = Path.Combine(temp.Path, "01_defekt.pdf");
        var validPdf = Path.Combine(temp.Path, "02_gueltig.pdf");
        var videoFolder = Directory.CreateDirectory(Path.Combine(temp.Path, "videos")).FullName;
        var destination = Directory.CreateDirectory(Path.Combine(temp.Path, "ziel")).FullName;
        File.WriteAllText(invalidPdf, "kein PDF");
        WritePdf(validPdf, "Haltungsinspektion - 12.07.2026 - 1000-2000");

        var results = HoldingFolderDistributor.DistributeFiles(
            [invalidPdf, validPdf],
            videoFolder,
            destination);

        Assert.Contains(results, result => !result.Success && result.SourcePdfPath == invalidPdf);
        Assert.Contains(results, result => result.Success && result.SourcePdfPath == validPdf);
        Assert.True(File.Exists(Path.Combine(destination, "1000-2000", "20260712_1000-2000.pdf")));
    }

    // ── Suchkaskade fuer das Video (Reihenfolge und Quelle festgehalten) ─────────

    [Fact]
    public void Suche_findet_Video_ueber_Seitenwagen_Link_und_nennt_die_Quelle()
    {
        using var s = Szenario.Neu();
        s.Video("M150_0815.mpg");

        var result = s.Verteile(parsedVideo: null, sidecarLinks: Karte(("1000-2000", "M150_0815.mpg")));

        Assert.True(result.Success, result.Message);
        Assert.Equal(HoldingFolderDistributor.VideoMatchStatus.Matched, result.VideoStatus);
        Assert.EndsWith("M150_0815.mpg", result.SourceVideoPath);
        Assert.Contains("[Quelle: M150/MDB]", result.Message);
    }

    [Fact]
    public void Suche_findet_Video_ueber_importierten_Datensatz_Link()
    {
        using var s = Szenario.Neu();
        var video = s.Video("aufnahme_anders.mpg");
        var project = s.ProjektMit("1000-2000", link: video, FieldSource.Xtf);

        var result = s.Verteile(parsedVideo: null, project: project);

        Assert.Equal(HoldingFolderDistributor.VideoMatchStatus.Matched, result.VideoStatus);
        Assert.Equal(video, result.SourceVideoPath);
        Assert.Contains("[Quelle: Datensatz-Link]", result.Message);
    }

    [Fact]
    public void Suche_findet_Video_ueber_CdIndex_Fotohinweis_im_Protokoll()
    {
        using var s = Szenario.Neu(pdfZeilen: ["Haltungsinspektion - 12.07.2026 - 1000-2000", "Foto: 1_2_3_A.jpg"]);
        s.Video("cd_0042.mpg");

        var result = s.Verteile(parsedVideo: null, cdIndex: Karte(("1_2_3_A", "cd_0042.mpg")));

        Assert.Equal(HoldingFolderDistributor.VideoMatchStatus.Matched, result.VideoStatus);
        Assert.EndsWith("cd_0042.mpg", result.SourceVideoPath);
        Assert.Contains("[Quelle: CDIndex-Foto]", result.Message);
    }

    [Fact]
    public void Direkter_Treffer_hat_Vorrang_vor_Seitenwagen()
    {
        using var s = Szenario.Neu();
        s.Video("20260712_1000-2000.mpg");
        s.Video("M150_0815.mpg");

        var result = s.Verteile(parsedVideo: null, sidecarLinks: Karte(("1000-2000", "M150_0815.mpg")));

        Assert.EndsWith("20260712_1000-2000.mpg", result.SourceVideoPath);
        Assert.DoesNotContain("[Quelle:", result.Message);
    }

    [Fact]
    public void Suche_nutzt_unkorrigierte_ProtokollHaltung_nach_PdfKorrektur()
    {
        using var s = Szenario.Neu();
        var altesVideo = s.Video("1000-2000.mpg");
        var project = s.ProjektMit("3000-4000");
        Assert.True(PdfCorrectionMetadata.RegisterHoldingRename(project, "1000-2000", "3000-4000"));

        var result = s.Verteile(parsedVideo: null, project: project);

        Assert.True(result.Success, result.Message);
        Assert.Equal(HoldingFolderDistributor.VideoMatchStatus.Matched, result.VideoStatus);
        Assert.Equal(altesVideo, result.SourceVideoPath);
        Assert.Equal("3000-4000", Path.GetFileName(result.HoldingFolder));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Mehrdeutiger_Seitenwagen_ersetzt_nur_nicht_gefunden(bool direkterTrefferMehrdeutig)
    {
        using var s = Szenario.Neu();
        var direkteTreffer = direkterTrefferMehrdeutig
            ? new[] { s.Video("20260712_1000-2000.mpg"), s.Video("20260712_1000-2000.mp4") }
            : Array.Empty<string>();
        var seitenwagenTreffer = new[] { s.Video("M150_A.mpg"), s.Video("M150_B.mpg") };
        var seitenwagen = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["1000-2000"] = ["M150_A.mpg", "M150_B.mpg"]
        };

        var suche = HoldingVideoSearch.Find(
            new HoldingVideoSearchContext(s.VideoOrdner, Recursive: true,
                SidecarVideoLinksByHolding: seitenwagen),
            project: null,
            videoFileFromPdf: null,
            holdingRaw: "1000-2000",
            holding: "1000-2000",
            originalHolding: "1000-2000",
            dateStamp: "20260712",
            pdfToStorePath: s.PdfPfad);

        Assert.Equal(HoldingFolderDistributor.VideoMatchStatus.Ambiguous, suche.Video.Status);
        Assert.Equal(
            (direkterTrefferMehrdeutig ? direkteTreffer : seitenwagenTreffer)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase),
            suche.Video.Candidates.OrderBy(path => path, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void Seitenwagen_ordnet_das_gefundene_Video_einer_anderen_Haltung_zu()
    {
        using var s = Szenario.Neu();
        s.Video("M150_0815.mpg");

        var result = s.Verteile(
            parsedVideo: "M150_0815.mpg",
            sidecarLinks: Karte(("3000-4000", "M150_0815.mpg")),
            sidecarHoldings: Karte(("M150_0815.mpg", "3000-4000")));

        Assert.Contains("[Haltung korrigiert via M150/MDB]", result.Message);
        Assert.Equal("3000-4000", Path.GetFileName(result.HoldingFolder));
        Assert.EndsWith("20260712_3000-4000.pdf", result.DestPdfPath);
    }

    [Fact]
    public void Videoname_einer_bekannten_Haltung_korrigiert_die_Haltung()
    {
        using var s = Szenario.Neu();
        s.Video("3000-4000.mpg");
        var project = s.ProjektMit("3000-4000");

        var result = s.Verteile(parsedVideo: "3000-4000.mpg", project: project);

        Assert.Contains("[Haltung korrigiert via M150/MDB]", result.Message);
        Assert.Equal("3000-4000", Path.GetFileName(result.HoldingFolder));
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> Karte(params (string Key, string Value)[] eintraege)
        => eintraege.ToDictionary(
            e => e.Key,
            e => (IReadOnlyList<string>)[e.Value],
            StringComparer.OrdinalIgnoreCase);

    private sealed class Szenario : IDisposable
    {
        private readonly TempDirectory _temp = new();
        private string SourcePdf => Path.Combine(_temp.Path, "quelle.pdf");
        private string VideoFolder => Path.Combine(_temp.Path, "videos");
        private string Destination => Path.Combine(_temp.Path, "ziel");
        public string VideoOrdner => VideoFolder;
        public string PdfPfad => SourcePdf;

        public static Szenario Neu(string[]? pdfZeilen = null)
        {
            var s = new Szenario();
            Directory.CreateDirectory(s.VideoFolder);
            Directory.CreateDirectory(s.Destination);
            WritePdf(s.SourcePdf, pdfZeilen ?? ["Haltungsinspektion - 12.07.2026 - 1000-2000"]);
            return s;
        }

        public string Video(string name)
        {
            var path = Path.Combine(VideoFolder, name);
            File.WriteAllText(path, "video " + name);
            return path;
        }

        public Project ProjektMit(string haltung, string? link = null, FieldSource linkQuelle = FieldSource.Unknown)
        {
            var project = new Project();
            var record = new HaltungRecord();
            record.SetFieldValue("Haltungsname", haltung, FieldSource.Manual, userEdited: false);
            if (link is not null)
                record.SetFieldValue("Link", link, linkQuelle, userEdited: false);
            project.AddRecord(record);
            return project;
        }

        public HoldingFolderDistributor.DistributionResult Verteile(
            string? parsedVideo,
            Project? project = null,
            IReadOnlyDictionary<string, IReadOnlyList<string>>? sidecarLinks = null,
            IReadOnlyDictionary<string, IReadOnlyList<string>>? sidecarHoldings = null,
            IReadOnlyDictionary<string, IReadOnlyList<string>>? cdIndex = null)
            => ParsedHoldingDistributionController.Distribute(
                new HoldingFolderDistributor.ParsedPdf(true, null, new DateTime(2026, 7, 12), "1000-2000", parsedVideo),
                new HoldingPdfSource(SourcePdf, SourcePdf),
                new HoldingVideoSearchContext(
                    VideoFolder,
                    Recursive: true,
                    SidecarVideoLinksByHolding: sidecarLinks,
                    SidecarHoldingsByVideoLink: sidecarHoldings,
                    CdIndexVideoLinksByPhoto: cdIndex),
                new HoldingDistributionTarget(Destination, MoveInsteadOfCopy: false, Overwrite: false, "__UNMATCHED"),
                project);

        public void Dispose() => _temp.Dispose();
    }

    private static void WritePdf(string path, params string[] lines)
    {
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);
        var y = 780m;
        foreach (var line in lines)
        {
            page.AddText(line, 12, new PdfPoint(40, y), font);
            y -= 18;
        }
        File.WriteAllBytes(path, builder.Build());
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "sewerstudio-parsed-distribution-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
