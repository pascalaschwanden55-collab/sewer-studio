using System.IO;
using static AuswertungPro.Next.Infrastructure.Tests.TestRepoPaths;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class LegacyOfferCodeRemovedTests
{
    [Fact]
    public void Legacy_offer_calculation_path_is_not_present()
    {
        var costCalculationService = File.ReadAllText(
            RepoFile("src", "AuswertungPro.Next.Infrastructure", "Costs", "CostCalculationService.cs"));
        var offerPdfFactory = File.ReadAllText(
            RepoFile("src", "AuswertungPro.Next.Infrastructure", "Output", "Offers", "OfferPdfModelFactory.cs"));

        Assert.DoesNotContain("CalculateOffer(", costCalculationService);
        Assert.DoesNotContain("CalculateCombinedOffer(", costCalculationService);
        Assert.DoesNotContain("LegacyOfferTotalsCalculator", costCalculationService);
        Assert.DoesNotContain("Create(CalculatedOffer", offerPdfFactory);
    }

    [Theory]
    [InlineData("src", "AuswertungPro.Next.Infrastructure", "Costs", "LegacyOfferTotalsCalculator.cs")]
    [InlineData("src", "AuswertungPro.Next.Domain", "Models", "Costs", "CalculatedOffer.cs")]
    [InlineData("src", "AuswertungPro.Next.Domain", "Models", "Costs", "MeasureInputs.cs")]
    [InlineData("src", "AuswertungPro.Next.Domain", "Models", "Costs", "OfferLine.cs")]
    [InlineData("src", "AuswertungPro.Next.Domain", "Models", "Costs", "OfferTotals.cs")]
    [InlineData("src", "AuswertungPro.Next.UI", "Templates", "offer.sbnhtml")]
    [InlineData("src", "AuswertungPro.Next.UI", "Templates", "offer_profi.sbnhtml")]
    public void Legacy_offer_files_are_removed(params string[] relativeParts)
    {
        var path = Path.Combine(new[] { RepoRoot() }.Concat(relativeParts).ToArray());

        Assert.False(File.Exists(path), "Toter Legacy-Offertenpfad soll nicht mehr im Produktcode liegen: " + path);
    }

    [Fact]
    public void Docs_do_not_reference_removed_legacy_devis_symbols()
    {
        var docsRoot = Path.Combine(RepoRoot(), "docs");
        var hits = Directory
            .EnumerateFiles(docsRoot, "*.md", SearchOption.AllDirectories)
            .SelectMany(path => File.ReadLines(path).Select((line, index) => new { path, line, index }))
            .Where(x => x.line.Contains("DevisGenerator", StringComparison.Ordinal)
                        || x.line.Contains("DevisExcelExporter", StringComparison.Ordinal))
            .Select(x => $"{Path.GetRelativePath(RepoRoot(), x.path)}:{x.index + 1}: {x.line}")
            .ToArray();

        Assert.Empty(hits);
    }
}
