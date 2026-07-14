using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Protocols;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class SchachtStammdatenErgaenzungsServiceTests
{
    [Fact]
    public void Ermitteln_UsesRelativeLink_WhenPdfPathIsStale()
    {
        using var temp = new TempProject();
        var pdf = temp.CreatePdf("Schächte_Verteilt", "80638", "20250924_80638.pdf");
        var parser = new FakeProtocolImportService(pdf, ParseResult("80638", "Rund", "1000 mm", "2.35"));
        var sut = new SchachtStammdatenErgaenzungsService(parser);
        var recordId = Guid.NewGuid();

        var result = sut.Ermitteln(temp.Root, new[]
        {
            new SchachtStammdatenQuelle(
                recordId,
                "80638",
                Path.Combine(temp.Root, "fehlt", "80638_01.pdf"),
                "Schächte_Verteilt/80638/20250924_80638.pdf",
                "",
                "",
                "")
        });

        var update = Assert.Single(result.Ergaenzungen);
        Assert.Equal(recordId, update.RecordId);
        Assert.Equal(Path.GetFullPath(pdf), update.PdfPath);
        Assert.Equal("Rund", update.Schachtform);
        Assert.Equal("1000 mm", update.Dimension);
        Assert.Equal("2.35", update.Schachttiefe);
        Assert.Equal(1, result.PdfGefunden);
        Assert.Equal(0, result.PdfNichtGefunden);
    }

    [Fact]
    public void Ermitteln_ReturnsOnlyFieldsThatWereMissing()
    {
        using var temp = new TempProject();
        var pdf = temp.CreatePdf("Schächte_Verteilt", "74466", "74466.pdf");
        var parser = new FakeProtocolImportService(pdf, ParseResult("74466", "Rund", "1200 x 800 mm", "1.9"));
        var sut = new SchachtStammdatenErgaenzungsService(parser);

        var result = sut.Ermitteln(temp.Root, new[]
        {
            new SchachtStammdatenQuelle(
                Guid.NewGuid(), "74466", "", "", "Oval", "", "")
        });

        var update = Assert.Single(result.Ergaenzungen);
        Assert.Null(update.Schachtform);
        Assert.Equal("1200 x 800 mm", update.Dimension);
        Assert.Equal("1.9", update.Schachttiefe);
    }

    [Fact]
    public void Ermitteln_FindsPdfInKnownNumberFolder_AndContinuesAfterReadError()
    {
        using var temp = new TempProject();
        var badPdf = temp.CreatePdf("Schächte_1.15", "80547", "80547.pdf");
        var goodPdf = temp.CreatePdf("Schächte_Verteilt", "80551", "80551.pdf");
        var parser = new FakeProtocolImportService(
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            {
                [badPdf] = new InvalidDataException("Testfehler"),
                [goodPdf] = ParseResult("80551", "Quadratisch", "900 mm", "1.75")
            });
        var sut = new SchachtStammdatenErgaenzungsService(parser);

        var result = sut.Ermitteln(temp.Root, new[]
        {
            MissingSource("80547"),
            MissingSource("80551")
        });

        Assert.Single(result.Ergaenzungen);
        Assert.Equal("Quadratisch", result.Ergaenzungen[0].Schachtform);
        Assert.Equal(2, result.PdfGefunden);
        Assert.Equal(1, result.NichtLesbar);
        Assert.Contains(result.Meldungen, message => message.Contains("80547", StringComparison.Ordinal));
    }

    [Fact]
    public void Ermitteln_SkipsCompleteRecord_WithoutReadingPdf()
    {
        using var temp = new TempProject();
        var parser = new FakeProtocolImportService(new Dictionary<string, object>());
        var sut = new SchachtStammdatenErgaenzungsService(parser);

        var result = sut.Ermitteln(temp.Root, new[]
        {
            new SchachtStammdatenQuelle(
                Guid.NewGuid(), "80635", "", "", "Rund", "1000 mm", "2.1")
        });

        Assert.Empty(result.Ergaenzungen);
        Assert.Equal(1, result.BereitsVollstaendig);
        Assert.Equal(0, parser.ParseCalls);
    }

    [Fact]
    public void Ermitteln_UebernimmtOcrFehlerInDieMeldung()
    {
        using var temp = new TempProject();
        var pdf = temp.CreatePdf("Schaechte_Verteilt", "80454", "80454.pdf");
        var parseResult = new SchachtProtocolParseResult(
            false,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            Array.Empty<(string, string)>(),
            "Bild-Scan: pdftoppm.exe wurde nicht gefunden.");
        var parser = new FakeProtocolImportService(pdf, parseResult);
        var sut = new SchachtStammdatenErgaenzungsService(parser);

        var result = sut.Ermitteln(temp.Root, new[] { MissingSource("80454") });

        Assert.Equal(1, result.NichtLesbar);
        Assert.Contains(result.Meldungen, message =>
            message.Contains("pdftoppm.exe", StringComparison.OrdinalIgnoreCase));
    }

    private static SchachtStammdatenQuelle MissingSource(string number)
        => new(Guid.NewGuid(), number, "", "", "", "", "");

    private static SchachtProtocolParseResult ParseResult(
        string number,
        string form,
        string dimension,
        string depth)
        => new(
            true,
            number,
            null,
            null,
            form,
            dimension,
            depth,
            null,
            null,
            null,
            null,
            Array.Empty<(string, string)>());

    private sealed class FakeProtocolImportService : ISchachtProtocolImportService
    {
        private readonly IReadOnlyDictionary<string, object> _results;

        public FakeProtocolImportService(string pdfPath, SchachtProtocolParseResult result)
            : this(new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { [pdfPath] = result })
        {
        }

        public FakeProtocolImportService(IReadOnlyDictionary<string, object> results)
        {
            _results = results;
        }

        public int ParseCalls { get; private set; }

        public SchachtProtocolParseResult Parse(string pdfPfad)
        {
            ParseCalls++;
            var value = _results[pdfPfad];
            if (value is Exception exception)
                throw exception;
            return (SchachtProtocolParseResult)value;
        }

        public SchachtRecord? FindSchacht(Project project, string? schachtnummer) => throw new NotSupportedException();

        public void Apply(SchachtRecord ziel, SchachtProtocolParseResult ergebnis, string pdfPfadFuerFeld)
            => throw new NotSupportedException();

        public string DistributePdf(string projektOrdner, string schachtnummer, string pdfQuelle)
            => throw new NotSupportedException();
    }

    private sealed class TempProject : IDisposable
    {
        public TempProject()
        {
            Root = Path.Combine(Path.GetTempPath(), "SchachtStammdatenTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public string CreatePdf(string baseFolder, string number, string fileName)
        {
            var directory = Path.Combine(Root, baseFolder, number);
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, fileName);
            File.WriteAllBytes(path, "%PDF-test"u8.ToArray());
            return path;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch
            {
                // Testaufraeumen darf das Testergebnis nicht verdecken.
            }
        }
    }
}
