using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class ProtocolPdfExporterCatalogMetaTests
{
    [Fact]
    public void Explicit_options_catalog_has_precedence_over_constructor_default()
    {
        var defaultCatalog = CreateCatalog("Standardwert");
        var explicitCatalog = CreateCatalog("Expliziter Wert");
        var exporter = new ProtocolPdfExporter(
            new ProtocolPdfAssetFileResolver(),
            layoutSettings: null,
            defaultCatalog);

        var pdf = BuildPdf(exporter, new HaltungsprotokollPdfOptions
        {
            IncludePhotos = false,
            IncludeHaltungsgrafik = false,
            CodeCatalog = explicitCatalog
        });

        Assert.NotEmpty(pdf);
        Assert.True(explicitCatalog.TryGetCalls > 0);
        Assert.Equal(0, defaultCatalog.TryGetCalls);
    }

    [Fact]
    public void Constructor_catalog_is_used_when_options_have_no_catalog()
    {
        var defaultCatalog = CreateCatalog("Standardwert");
        var exporter = new ProtocolPdfExporter(
            new ProtocolPdfAssetFileResolver(),
            layoutSettings: null,
            defaultCatalog);

        var pdf = BuildPdf(exporter, new HaltungsprotokollPdfOptions
        {
            IncludePhotos = false,
            IncludeHaltungsgrafik = false
        });

        Assert.NotEmpty(pdf);
        Assert.True(defaultCatalog.TryGetCalls > 0);
    }

    [Theory]
    [InlineData("BAB", "crack")]
    [InlineData("BABAC", "crack")]
    [InlineData("BAC", "break")]
    [InlineData("BAA", "deformation")]
    [InlineData("BAF", "surface")]
    [InlineData("BAH", "offset")]
    [InlineData("BAJ", "offset")]
    [InlineData("BBA", "roots")]
    [InlineData("BBB", "incrustation")]
    [InlineData("BBC", "deposit")]
    [InlineData("ZZZ", "default")]
    public void Damage_symbol_category_uses_correct_vsa_kek_mapping(string code, string expected)
    {
        Assert.Equal(expected, ProtocolPdfExporter.ResolveDamageSymbolCategory(code));
    }

    [Fact]
    public void Photo_caption_keeps_original_code_and_hides_catalog_metadata()
    {
        var entry = new ProtocolEntry
        {
            Code = "BAGA",
            CodeMeta = new ProtocolEntryCodeMeta
            {
                Code = "BAGA",
                Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["catalog.source"] = "VSA-KEK-2020-ILI",
                    ["catalog.canonicalCode"] = "BAG",
                    ["catalog.standardAnnotation"] = "A",
                    ["vsa.q1"] = "12 mm"
                }
            }
        };

        var caption = ProtocolPdfObservationText.BuildPhotoCaptionLine2(entry);

        Assert.Equal("BAGA Q1=12 mm", caption);
    }

    [Fact]
    public void Photo_caption_with_catalog_contains_primary_title_and_operator_note()
    {
        var entry = new ProtocolEntry
        {
            Code = "BCE",
            Beschreibung = "Anschluss von 12 Uhr in Schmutzleitung"
        };

        var caption = ProtocolPdfObservationText.BuildPhotoCaptionLine2(
            entry,
            VsaResolverTestCatalog.CreateDefault());

        Assert.Equal(
            "BCE Rohrende, Anschluss von 12 Uhr in Schmutzleitung",
            caption);
    }

    private static byte[] BuildPdf(
        ProtocolPdfExporter exporter,
        HaltungsprotokollPdfOptions options)
    {
        var entry = new ProtocolEntry
        {
            Code = "TEST",
            Beschreibung = "Testbefund",
            MeterStart = 1.2,
            CodeMeta = new ProtocolEntryCodeMeta
            {
                Code = "TEST",
                Parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Quantifizierung1"] = "12"
                }
            }
        };
        var document = new ProtocolDocument
        {
            HaltungId = "100-200",
            Current = new ProtocolRevision { Entries = [entry] }
        };
        var record = new HaltungRecord { Protocol = document };
        record.SetFieldValue("Haltungsname", "100-200", FieldSource.Manual, userEdited: true);
        record.SetFieldValue("Haltungslaenge_m", "10", FieldSource.Manual, userEdited: true);

        return exporter.BuildHaltungsprotokollPdf(
            new Project { Name = "Katalogtest" },
            record,
            document,
            Path.GetTempPath(),
            options);
    }

    private static TrackingCatalog CreateCatalog(string parameterName)
        => new(new CodeDefinition
        {
            Code = "TEST",
            Title = "Testcode",
            Parameters =
            [
                new CodeParameter
                {
                    Name = parameterName,
                    DataKey = "Quantifizierung1",
                    Unit = "mm"
                }
            ]
        });

    private sealed class TrackingCatalog(params CodeDefinition[] definitions) : ICodeCatalogProvider
    {
        private readonly IReadOnlyList<CodeDefinition> _definitions = definitions;

        public int TryGetCalls { get; private set; }

        public IReadOnlyList<CodeDefinition> GetAll() => _definitions;

        public bool TryGet(string code, out CodeDefinition def)
        {
            TryGetCalls++;
            def = _definitions.FirstOrDefault(candidate =>
                string.Equals(candidate.Code, code, StringComparison.OrdinalIgnoreCase))!;
            return def is not null;
        }

        public void Save(IReadOnlyList<CodeDefinition> codes) => throw new NotSupportedException();
        public IReadOnlyList<string> AllowedCodes() => _definitions.Select(def => def.Code).ToList();
        public IReadOnlyList<string> Validate(IReadOnlyList<CodeDefinition>? codes = null) => [];
    }

}
