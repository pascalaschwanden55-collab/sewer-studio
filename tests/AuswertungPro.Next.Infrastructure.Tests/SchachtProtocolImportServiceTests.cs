using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Import.Protocols;
using AuswertungPro.Next.Infrastructure.Tests.Backup;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class SchachtProtocolImportServiceTests
{
    [Fact]
    public void ParseFromText_MitSchachtprotokoll_LiefertFelder()
    {
        var text = string.Join("\n", new[]
        {
            "Schachtprotokoll   Nr. 74467",
            "Schachttyp Kontrollschacht",
            "Schachtform Oval",
            "Dimension 1000 x 800 mm",
            "Schachttiefe 2,35 m",
            "Datum 02/10/2025"
        });

        var result = SchachtProtocolImportService.ParseFromText(text);

        Assert.True(result.IstSchachtprotokoll);
        Assert.Equal("74467", result.Schachtnummer);
        Assert.Equal("Kontrollschacht", result.Funktion);
        Assert.Equal("Oval", result.Schachtform);
        Assert.Equal("1000 x 800 mm", result.Dimension);
        Assert.Equal("2.35", result.Schachttiefe);
    }

    [Fact]
    public void ParseFromText_OhneSchachtprotokoll_IstFalse()
    {
        var result = SchachtProtocolImportService.ParseFromText("Irgendein Haltungsprotokoll Text");

        Assert.False(result.IstSchachtprotokoll);
        Assert.Null(result.Schachtnummer);
        Assert.Empty(result.Schaeden);
    }

    [Fact]
    public void ParseFromText_ZustandsaufnahmeSchachtMitSteuerzeichen_LiefertFelder()
    {
        var text = string.Join("\n", new[]
        {
            "GEP Bürglen Zone 5.01",
            "Zustandsaufnahme Schacht Nr\u0011 80454",
            "Schachttyp Kontrollschacht",
            "Dimension 1100/900",
            "7LHIH\u0003 $EVWLFK \u0003P 2.12",
            "Schachtdeckel defekt klemmt",
            "Deckelrahmen lose ausgebrochen",
            "Schachthals",
            "■",
            "gerissen schlecht verputzt ausgebrochen",
            "Bankett",
            "■",
            "ausgebrochen Ablagerungen",
            "20/09/2024",
            "Datum Joel Gerber"
        });

        var result = SchachtProtocolImportService.ParseFromText(text);

        Assert.True(result.IstSchachtprotokoll);
        Assert.Equal("80454", result.Schachtnummer);
        Assert.Equal("Kontrollschacht", result.Funktion);
        Assert.Equal("Rechteckig", result.Schachtform);
        Assert.Equal("1100 x 900 mm", result.Dimension);
        Assert.Equal("2.12", result.Schachttiefe);
        Assert.Equal("20.09.2024", result.Datum);
        Assert.Contains(result.Schaeden, damage => damage == ("Schachthals", "gerissen"));
        Assert.Contains(result.Schaeden, damage => damage == ("Bankett", "ausgebrochen"));
    }

    [Fact]
    public void ParseFromText_HaltungsprotokollMitSchachtwoertern_BleibtFalse()
    {
        var result = SchachtProtocolImportService.ParseFromText(
            "Haltungsinspektion 80454-80455\nOberer Schacht 80454\nUnterer Schacht 80455");

        Assert.False(result.IstSchachtprotokoll);
    }

    [Fact]
    public void ParseWithOcrFallback_BildScan_LiestSchachtprotokoll()
    {
        var ocrCalls = 0;

        var result = SchachtProtocolImportService.ParseWithOcrFallback(
            "",
            () =>
            {
                ocrCalls++;
                return new SchachtProtocolOcrReadResult(
                    true,
                    "Zustandsaufnahme Schacht Nr. 80454\nSchachttyp Kontrollschacht\nDimension 1100/900\nTiefe 2.12\n20/09/2024",
                    3,
                    3,
                    null);
            });

        Assert.True(result.IstSchachtprotokoll);
        Assert.Equal("80454", result.Schachtnummer);
        Assert.Equal("1100 x 900 mm", result.Dimension);
        Assert.Equal("2.12", result.Schachttiefe);
        Assert.Equal(1, ocrCalls);
        Assert.Contains("OCR", result.Lesehinweis, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseWithOcrFallback_LesbarerDirekttext_StartetKeineOcr()
    {
        var result = SchachtProtocolImportService.ParseWithOcrFallback(
            "Schachtprotokoll Nr. 74467\nSchachttyp Kontrollschacht\nDimension 1000 mm\nSchachttiefe 2.35 m",
            () => throw new InvalidOperationException("OCR darf nicht aufgerufen werden."));

        Assert.True(result.IstSchachtprotokoll);
        Assert.Null(result.Lesehinweis);
    }

    [Fact]
    public void ParseWithOcrFallback_FehlendesWerkzeug_LiefertVerstaendlichenHinweis()
    {
        var result = SchachtProtocolImportService.ParseWithOcrFallback(
            "",
            () => new SchachtProtocolOcrReadResult(
                false,
                "",
                3,
                0,
                "pdftoppm.exe wurde nicht gefunden."));

        Assert.False(result.IstSchachtprotokoll);
        Assert.Contains("Bild-Scan", result.Lesehinweis, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("pdftoppm.exe", result.Lesehinweis, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseWithOcrFallback_OcrTextOhneSchachtprotokoll_UnterscheidetErkennungsfehler()
    {
        var result = SchachtProtocolImportService.ParseWithOcrFallback(
            "",
            () => new SchachtProtocolOcrReadResult(
                true,
                "Gesamtauszug Gemeinde ohne passende Protokollfelder",
                1,
                1,
                null));

        Assert.False(result.IstSchachtprotokoll);
        Assert.Contains("Texterkennung wurde ausgefuehrt", result.Lesehinweis, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_NutztEingespritzteTextausleseUndSchachtOcr()
    {
        var textExtractor = new EmptyPdfTextExtractor();
        var ocrReader = new SuccessfulSchachtOcrReader();
        var service = new SchachtProtocolImportService(textExtractor, ocrReader);

        var result = service.Parse("scan.pdf");

        Assert.True(result.IstSchachtprotokoll);
        Assert.Equal("80454", result.Schachtnummer);
        Assert.Equal(1, textExtractor.Calls);
        Assert.Equal(1, ocrReader.Calls);
    }

    [Fact]
    public void FindSchacht_FindetPerSchachtnummer()
    {
        var project = new Project();
        var schacht = new SchachtRecord();
        schacht.SetFieldValue("Schachtnummer", "74467");
        project.SchaechteData.Add(schacht);
        var svc = new SchachtProtocolImportService();

        var found = svc.FindSchacht(project, "74467");

        Assert.Same(schacht, found);
    }

    [Fact]
    public void FindSchacht_NullWennNichtVorhanden()
    {
        var svc = new SchachtProtocolImportService();

        Assert.Null(svc.FindSchacht(new Project(), "99999"));
    }

    [Fact]
    public void Apply_BautRecordNeuAuf()
    {
        var ergebnis = new SchachtProtocolParseResult(
            true, "74467", "02.10.2025", "Kontrollschacht",
            "Rund", "1000 mm", "2.35", null, null, "offen", null,
            new[] { ("Schachtdeckel", "gerissen") });
        var schacht = new SchachtRecord();
        var svc = new SchachtProtocolImportService();

        svc.Apply(schacht, ergebnis, "Schaechte_Verteilt/74467/quelle.pdf");

        Assert.Equal("74467", schacht.GetFieldValue("Schachtnummer"));
        Assert.Equal("Kontrollschacht", schacht.GetFieldValue("Funktion"));
        Assert.Equal("Rund", schacht.GetFieldValue("Schachtform"));
        Assert.Equal("1000", schacht.GetFieldValue(FieldKeys.ShaftDimension1Mm));
        Assert.Equal("1000", schacht.GetFieldValue(FieldKeys.ShaftDimension2Mm));
        Assert.Equal("2.35", schacht.GetFieldValue("Schachttiefe"));
        Assert.Equal("Schaechte_Verteilt/74467/quelle.pdf", schacht.GetFieldValue("PDF_Path"));
        Assert.NotNull(schacht.Protocol);
        Assert.Single(schacht.Protocol!.Original.Entries);
        Assert.Equal("Schachtdeckel", schacht.Protocol!.Original.Entries[0].Code);
    }

    [Fact]
    public void DistributePdf_KopiertUndGibtRelativenPfad()
    {
        var root = Path.Combine(Path.GetTempPath(), "sst_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var src = Path.Combine(root, "quelle.pdf");
            File.WriteAllText(src, "%PDF-1.4 dummy");
            var svc = new SchachtProtocolImportService();

            var result = svc.DistributePdfWithResult(root, "74467", src);
            string legacyPath = svc.DistributePdf(root, "74467", src);
            var existingResult = svc.DistributePdfWithResult(root, "74467", src);

            var expected = Path.Combine(ProjectStructure.SchachtVerteiltDir(root, "74467"), "quelle.pdf");
            Assert.True(File.Exists(expected));
            Assert.Equal(ProjectPathResolver.MakeRelative(expected, root), result.RelativePath);
            Assert.Contains("74467", result.RelativePath);
            Assert.Contains("quelle.pdf", result.RelativePath);
            Assert.True(result.FileCreated);
            Assert.False(existingResult.FileCreated);
            Assert.Equal(result.RelativePath, existingResult.RelativePath);
            Assert.Equal(result.RelativePath, legacyPath);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* Best effort. */ }
        }
    }

    [Fact]
    public void DistributePdf_legacy_facade_copies_a_fresh_file()
    {
        var root = Path.Combine(Path.GetTempPath(), "sst_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "legacy.pdf");
            File.WriteAllText(source, "%PDF-1.4 legacy");
            ISchachtProtocolImportService service = new SchachtProtocolImportService();

            string relativePath = service.DistributePdf(root, "L-1", source);

            var expected = Path.Combine(ProjectStructure.SchachtVerteiltDir(root, "L-1"), "legacy.pdf");
            Assert.True(File.Exists(expected));
            Assert.Equal(ProjectPathResolver.MakeRelative(expected, root), relativePath);
            Assert.Equal("%PDF-1.4 legacy", File.ReadAllText(expected));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* Best effort. */ }
        }
    }

    [Fact]
    public void DistributePdf_GleichnamigesZielMitGleicherGroesseAberAnderemInhalt_BekommtEindeutigenPfad()
    {
        var root = Path.Combine(Path.GetTempPath(), "sst_" + Guid.NewGuid().ToString("N"));
        var sourceFolder = Path.Combine(root, "Quelle");
        Directory.CreateDirectory(sourceFolder);
        try
        {
            var source = Path.Combine(sourceFolder, "protokoll.pdf");
            File.WriteAllText(source, "NEU-1234");
            var destinationFolder = ProjectStructure.SchachtVerteiltDir(root, "74467");
            Directory.CreateDirectory(destinationFolder);
            var existing = Path.Combine(destinationFolder, "protokoll.pdf");
            File.WriteAllText(existing, "ALT-1234");
            var service = new SchachtProtocolImportService();

            var result = service.DistributePdfWithResult(root, "74467", source);

            var distributed = ProjectPathResolver.ResolveFilePathFromProjectFolder(
                result.RelativePath,
                root);
            Assert.True(result.FileCreated);
            Assert.NotNull(distributed);
            Assert.NotEqual(existing, distributed);
            Assert.Equal("ALT-1234", File.ReadAllText(existing));
            Assert.Equal("NEU-1234", File.ReadAllText(source));
            Assert.Equal("NEU-1234", File.ReadAllText(distributed!));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* Best effort. */ }
        }
    }

    [JunctionFact]
    public void DistributePdf_VerknuepfteSchachtZielwurzel_SchreibtNichtInFremdenOrdner()
    {
        var root = Path.Combine(Path.GetTempPath(), "sst_" + Guid.NewGuid().ToString("N"));
        var projectFolder = Path.Combine(root, "Projekt");
        var sourceFolder = Path.Combine(root, "Quelle");
        var foreignFolder = Path.Combine(root, "Fremd");
        var targetLink = Path.Combine(projectFolder, ProjectStructure.SchaechteVerteilt);
        Directory.CreateDirectory(projectFolder);
        Directory.CreateDirectory(sourceFolder);
        Directory.CreateDirectory(foreignFolder);
        var source = Path.Combine(sourceFolder, "protokoll.pdf");
        File.WriteAllText(source, "KUNDENORIGINAL");
        JunctionTestSupport.CreateDirectoryLink(targetLink, foreignFolder);
        try
        {
            var service = new SchachtProtocolImportService();

            var error = Assert.Throws<IOException>(() =>
                service.DistributePdfWithResult(projectFolder, "74467", source));

            Assert.Contains("Verknuepfung", error.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(Directory.EnumerateFileSystemEntries(foreignFolder));
            Assert.Equal("KUNDENORIGINAL", File.ReadAllText(source));
        }
        finally
        {
            try
            {
                if (Directory.Exists(targetLink))
                    Directory.Delete(targetLink);
            }
            catch
            {
                // Test-Aufraeumen ist Best-Effort.
            }

            try { Directory.Delete(root, recursive: true); } catch { /* Best effort. */ }
        }
    }

    private sealed class EmptyPdfTextExtractor : IPdfTextExtractor
    {
        public int Calls { get; private set; }

        public string FindPdfToTextPath(string? explicitPath = null)
            => string.Empty;

        public PdfTextExtractionResult ExtractPages(string pdfPath, string? explicitPdfToTextPath = null)
        {
            Calls++;
            return new PdfTextExtractionResult(Array.Empty<string>(), string.Empty);
        }

        public void ThrowIfPageBudgetExceeded(string pdfPath, int? maxPages = null)
        {
        }
    }

    private sealed class SuccessfulSchachtOcrReader : ISchachtProtocolOcrReader
    {
        public int Calls { get; private set; }

        public SchachtProtocolOcrReadResult TryRead(string pdfPath, int maxPages = 40)
        {
            Calls++;
            return new SchachtProtocolOcrReadResult(
                true,
                "Schachtprotokoll Nr. 80454\nSchachttyp Kontrollschacht",
                1,
                1,
                null);
        }
    }

}
