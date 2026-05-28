using System;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Infrastructure.Vsa.Classification;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class VsaClassificationRuleSetV2Tests
{
    private static string DataPath(string fileName)
    {
        var root = TestPaths.FindSolutionRoot();
        return Path.Combine(root, "src", "AuswertungPro.Next.UI", "Data", fileName);
    }

    [Fact]
    public void ChannelRules_LoadV2Json_AndContainManifestAlignedCoreRules()
    {
        var rules = VsaClassificationRuleSet.LoadFromFile(
            DataPath("vsa_zustandsklassifizierung_2023_channels.json"));

        Assert.Equal(2, rules.SchemaVersion);
        Assert.Equal("channel", rules.AssetKind);
        Assert.All(rules.Rules, rule => Assert.InRange(rule.Classification.Ez ?? 0, 0, 4));

        var baaRigid = rules.Rules.Single(rule =>
            rule.Code == "BAA"
            && rule.Requirement == "S"
            && rule.Scope.PipeFlexibility == "rigid");
        Assert.Equal("%", baaRigid.Unit);
        Assert.Equal("q1", baaRigid.Parameter);
        Assert.Equal("range", baaRigid.Classification.Mode);
        Assert.Contains(baaRigid.Classification.Ranges, range => range.Ez == 0 && range.MinInclusive == 7.0);

        var baaFlexible = rules.Rules.Single(rule =>
            rule.Code == "BAA"
            && rule.Requirement == "S"
            && rule.Scope.PipeFlexibility == "flexible");
        Assert.Contains(baaFlexible.Classification.Ranges, range => range.Ez == 0 && range.MinInclusive == 15.0);

        var babCrack = rules.Rules.Single(rule =>
            rule.Code == "BAB"
            && rule.Requirement == "S"
            && rule.Ch1.SequenceEqual(new[] { "B", "C" })
            && rule.Unit == "mm"
            && rule.Classification.Mode == "range");
        Assert.Contains(babCrack.Classification.Ranges, range => range.Ez == 0 && range.MinInclusive == 8.0);

        var bbaRoots = rules.Rules.Where(rule => rule.Code == "BBA").ToList();
        Assert.NotEmpty(bbaRoots);
        Assert.All(bbaRoots, rule => Assert.DoesNotContain("deformation", string.Join(' ', rule.Notes), StringComparison.OrdinalIgnoreCase));

        Assert.Contains(rules.Rules, rule => rule.Status == "missing-vsa-source"
                                             && rule.Classification.Mode == "missing");
    }

    [Fact]
    public void ManholeRules_LoadV2Json_AndPreserveMissingSourceRows()
    {
        var rules = VsaClassificationRuleSet.LoadFromFile(
            DataPath("vsa_zustandsklassifizierung_2023_manholes.json"));

        Assert.Equal(2, rules.SchemaVersion);
        Assert.Equal("manhole", rules.AssetKind);

        Assert.Contains(rules.Rules, rule => rule.Status == "needs-review"
                                             && rule.Classification.Mode == "missing");

        var validEzValues = rules.Rules
            .SelectMany(rule => rule.Classification.Ranges.Select(range => range.Ez)
                .Concat(rule.Classification.Ez is int ez ? new[] { ez } : Array.Empty<int>()));
        Assert.All(validEzValues, ez => Assert.InRange(ez, 0, 4));
    }
}
