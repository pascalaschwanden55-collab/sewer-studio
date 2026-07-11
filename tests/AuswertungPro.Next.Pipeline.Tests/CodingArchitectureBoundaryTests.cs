using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class CodingArchitectureBoundaryTests
{
    [Fact]
    public void Pure_coding_rules_live_in_application_layer()
    {
        var applicationAssembly = typeof(CodingDedupPolicy).Assembly;
        var ruleTypes = new[]
        {
            typeof(CodingMeterResolver),
            typeof(CodingFindingProximityPolicy),
            typeof(CodingFindingCoveragePolicy),
            typeof(CodingOneTimeCodeDuplicatePolicy),
            typeof(CodingKnownFindingPolicy),
            typeof(CodingLiveFindingAcceptancePolicy),
            typeof(CodingLiveFindingAddDecisionPolicy),
            typeof(CodingFindingFilterPolicy),
            typeof(CodingMultiModelFindingAddDecisionPolicy),
            typeof(CodingImportFallbackCodeResolver),
            typeof(CodingImportFallbackCodePolicy),
            typeof(CodingFindingDedupeKeyBuilder),
            typeof(ClockPositionNormalizer)
        };

        Assert.All(ruleTypes, type => Assert.Equal(applicationAssembly, type.Assembly));
        Assert.DoesNotContain(
            applicationAssembly.GetReferencedAssemblies(),
            reference => reference.Name == "AuswertungPro.Next.UI");
    }

    [Fact]
    public void Catalog_dependent_code_resolution_stays_in_infrastructure_layer()
    {
        Assert.Equal(typeof(VsaCodeResolver).Assembly, typeof(CodingFindingCodeResolver).Assembly);
    }
}
