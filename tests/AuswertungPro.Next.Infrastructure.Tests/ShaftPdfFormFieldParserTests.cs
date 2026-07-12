using AuswertungPro.Next.Infrastructure.HoldingDistribution;
using AuswertungPro.Next.Infrastructure.Import.Pdf;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class ShaftPdfFormFieldParserTests
{
    [Fact]
    public void BuildSyntheticText_PreservesDistinctLabelsAndUnlabeledValues()
    {
        var entries = new[]
        {
            Entry("Schachtnummer", "SCHACHTNUMMER", "MapName", "74467"),
            Entry(null, null, null, "02.10.2025")
        };

        var text = ShaftPdfFormFieldParser.BuildSyntheticText(entries);

        Assert.Equal("Schachtnummer: 74467\nMapName: 74467\n02.10.2025", text);
    }

    [Fact]
    public void TryExtractDate_PrefersLabeledFieldOverEarlierGenericValue()
    {
        var entries = new[]
        {
            Entry("Notiz", null, null, "Erstellt 01.01.2024"),
            Entry("Inspektionsdatum", null, null, "02/10/2025")
        };

        var date = ShaftPdfFormFieldParser.TryExtractDate(entries);

        Assert.Equal(new DateTime(2025, 10, 2), date);
    }

    [Fact]
    public void TryExtractDate_UsesGenericFieldAsFallback()
    {
        var entries = new[] { Entry("Field42", null, null, "2025-05-12") };

        var date = ShaftPdfFormFieldParser.TryExtractDate(entries);

        Assert.Equal(new DateTime(2025, 5, 12), date);
    }

    [Fact]
    public void TryExtractShaftNumber_PrefersLabeledFieldOverEarlierGenericNumber()
    {
        var entries = new[]
        {
            Entry("Auftrag", null, null, "998877"),
            Entry(null, "Schacht Nr.", null, "74467")
        };

        var shaft = ShaftPdfFormFieldParser.TryExtractShaftNumber(entries);

        Assert.Equal("74467", shaft);
    }

    [Fact]
    public void TryExtractShaftNumber_PreservesExistingDirectNumericFallbackBehavior()
    {
        var entries = new[]
        {
            Entry("Field1", null, null, "2025"),
            Entry("Field2", null, null, "Schachtwert 123456")
        };

        var shaft = ShaftPdfFormFieldParser.TryExtractShaftNumber(entries);

        Assert.Equal("2025", shaft);
    }

    private static PdfFormFieldEntry Entry(
        string? partialName,
        string? alternateName,
        string? mappingName,
        string value)
        => new(1, partialName, alternateName, mappingName, value);
}
