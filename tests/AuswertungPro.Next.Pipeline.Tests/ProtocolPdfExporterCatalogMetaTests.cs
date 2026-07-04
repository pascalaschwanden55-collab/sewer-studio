using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class ProtocolPdfExporterCatalogMetaTests
{
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

}
