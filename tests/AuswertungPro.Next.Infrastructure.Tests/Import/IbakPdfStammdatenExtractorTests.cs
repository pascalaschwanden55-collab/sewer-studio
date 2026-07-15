using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Import.Ibak;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

public sealed class IbakPdfStammdatenExtractorTests
{
    [Fact]
    public void ExtractFromText_LiestDieBekanntenKiasStammdaten()
    {
        const string text = """
            Haltung 100-200
            Haltungslange 12,50 m
            Material Beton
            Profilhohe 300 mm
            Profilbreite 280 mm
            Geometrie Kreisprofil
            Nutzungsart Mischabwasser
            """;

        var data = IbakPdfStammdatenExtractor.ExtractFromText(text);

        Assert.NotNull(data);
        Assert.Equal("100-200", data.Haltungsname);
        Assert.Equal("Beton", data.Material);
        Assert.Equal(12.5, data.Laenge_m);
        Assert.Equal(300, data.DN_mm);
        Assert.Equal(280, data.Profilbreite_mm);
        Assert.Equal("Kreisprofil", data.Geometrie);
        Assert.Equal("Mischabwasser", data.Nutzungsart);
    }

    [Fact]
    public void Extract_LiestBewusstNurDieErstenZweiSeiten()
    {
        using var temp = new TempDirectory();
        var pdfPath = Path.Combine(temp.Path, "H_100-200.pdf");
        WritePdf(
            pdfPath,
            ["Haltung 100-200"],
            ["Material Steinzeug"],
            ["Profilhohe 999 mm"]);

        var data = IbakPdfStammdatenExtractor.Extract(pdfPath);

        Assert.NotNull(data);
        Assert.Equal("100-200", data.Haltungsname);
        Assert.Equal("Steinzeug", data.Material);
        Assert.Null(data.DN_mm);
    }

    [Fact]
    public void Extract_FehlendeOderKaputtePdf_LiefertKeinErgebnis()
    {
        using var temp = new TempDirectory();
        var brokenPath = Path.Combine(temp.Path, "kaputt.pdf");
        File.WriteAllText(brokenPath, "kein PDF");

        Assert.Null(IbakPdfStammdatenExtractor.Extract(Path.Combine(temp.Path, "fehlt.pdf")));
        Assert.Null(IbakPdfStammdatenExtractor.Extract(brokenPath));
    }

    [Fact]
    public void BuildIndex_BevorzugtDenReportOrdner()
    {
        using var temp = new TempDirectory();
        WritePdf(
            Path.Combine(temp.Path, "H_900-901.pdf"),
            ["Haltung 900-901", "Material Beton"]);
        var reportPath = Path.Combine(temp.Path, "Report");
        Directory.CreateDirectory(reportPath);
        WritePdf(
            Path.Combine(reportPath, "H_100-200.pdf"),
            ["Haltung 100-200", "Material Steinzeug"]);

        var index = IbakPdfStammdatenExtractor.BuildIndex(temp.Path);

        var entry = Assert.Single(index);
        Assert.Equal("100-200", entry.Key);
        Assert.Equal("Steinzeug", entry.Value.Material);
    }

    [Fact]
    public void BuildIndex_ErgaenztFehlendeFelderAusWeiterenPdfDateien()
    {
        using var temp = new TempDirectory();
        var reportPath = Path.Combine(temp.Path, "Report");
        Directory.CreateDirectory(reportPath);
        WritePdf(
            Path.Combine(reportPath, "H_100-200.pdf"),
            ["Haltung 100-200", "Material Beton"]);
        WritePdf(
            Path.Combine(reportPath, "H_100-200~G.pdf"),
            ["Haltung 100-200", "Haltungslange 8.75 m"]);

        var index = IbakPdfStammdatenExtractor.BuildIndex(temp.Path);

        var data = Assert.Single(index).Value;
        Assert.Equal("Beton", data.Material);
        Assert.Equal(8.75, data.Laenge_m);
    }

    [Fact]
    public void BuildIndex_VerwendetDieEingespritztePdfQuelle()
    {
        var sourceReader = new RecordingSourceReader(
            "Haltung 700-800\nMaterial Polyethylen\nProfilhohe 250 mm");

        var index = IbakPdfStammdatenExtractor.BuildIndex(
            "virtueller-export",
            sourceReader);

        var data = Assert.Single(index).Value;
        Assert.Equal("Polyethylen", data.Material);
        Assert.Equal(250, data.DN_mm);
        Assert.Equal("virtueller-export", sourceReader.EnumeratedRoot);
        Assert.Equal("virtuelle.pdf", sourceReader.ReadPath);
        Assert.Equal(2, sourceReader.ReadPageCount);
    }

    [Fact]
    public void SourceReader_VerwendetDenEingespritztenPdfTextleser()
    {
        using var temp = new TempDirectory();
        var pdfPath = Path.Combine(temp.Path, "vorhanden.pdf");
        File.WriteAllText(pdfPath, "Platzhalter");
        var pdfTextExtractor = new RecordingPdfTextExtractor(
            ["Seite eins", "Seite zwei", "Seite drei"]);
        var sourceReader = new IbakPdfStammdatenSourceReader(pdfTextExtractor);

        var text = sourceReader.TryReadFirstPagesText(pdfPath, maxPages: 2);

        Assert.Equal("Seite eins\nSeite zwei", text);
        Assert.Equal(pdfPath, pdfTextExtractor.ExtractedPath);
    }

    private static void WritePdf(string path, params string[][] pages)
    {
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        foreach (var lines in pages)
        {
            var page = builder.AddPage(PageSize.A4);
            var y = 780d;
            foreach (var line in lines)
            {
                page.AddText(line, 12, new PdfPoint(40, y), font);
                y -= 20;
            }
        }

        File.WriteAllBytes(path, builder.Build());
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"sewerstudio-ibak-pdf-stammdaten-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // Test-Aufraeumfehler duerfen das Ergebnis nicht verdecken.
            }
        }
    }

    private sealed class RecordingSourceReader(string text)
        : IIbakPdfStammdatenSourceReader
    {
        public string? EnumeratedRoot { get; private set; }

        public string? ReadPath { get; private set; }

        public int ReadPageCount { get; private set; }

        public IReadOnlyList<string> EnumeratePdfFiles(string exportRoot)
        {
            EnumeratedRoot = exportRoot;
            return ["virtuelle.pdf"];
        }

        public string? TryReadFirstPagesText(string pdfPath, int maxPages)
        {
            ReadPath = pdfPath;
            ReadPageCount = maxPages;
            return text;
        }
    }

    private sealed class RecordingPdfTextExtractor(IReadOnlyList<string> pages)
        : IPdfTextExtractor
    {
        public string? ExtractedPath { get; private set; }

        public string FindPdfToTextPath(string? explicitPath = null) => "pdftotext.exe";

        public PdfTextExtractionResult ExtractPages(
            string pdfPath,
            string? explicitPdfToTextPath = null)
        {
            ExtractedPath = pdfPath;
            return new PdfTextExtractionResult(pages, string.Join("\f", pages));
        }

        public void ThrowIfPageBudgetExceeded(string pdfPath, int? maxPages = null)
        {
        }
    }
}
